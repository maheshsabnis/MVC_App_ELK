using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MVC_App.BlazorClient.Api;
using MVC_App.BlazorClient.Components;
using MVC_App.BlazorClient.Identity;
using MVC_App.BlazorClient.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Exports logs, traces and metrics over OTLP to the same Collector as MVC_App, so this app's
// entries land in the same mvcapp-logs-* indices. See Telemetry/TelemetryExtensions.cs.
builder.AddApplicationTelemetry();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// -----------------------------------------------------------------------------
// Authentication
//
// Points at the same MVC_AppIdentity database as the MVC app, so one account works in both.
// Nothing here creates or migrates the schema: MVC_App owns the migrations, this app only reads
// and writes the existing tables.
// -----------------------------------------------------------------------------
builder.Services.AddDbContext<IdentityContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Matched to the MVC app's rules. If the two disagreed, a password acceptable to one app
        // would be rejected by the other against the same user row.
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<IdentityContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";

    // Named distinctly from the MVC app's cookie. They are separate sessions on the same host, and
    // sharing a name would have each app overwrite the other's cookie on the localhost origin.
    options.Cookie.Name = "MVC_App.BlazorClient.Auth";
});

// Supplies the AuthenticationState that <AuthorizeView> and [Authorize] read, and flows it into
// the SignalR circuit when an interactive page takes over from the server-rendered one.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddScoped<AuthEventLogger>();

// Replaces the framework's default, which writes a component exception to the local logger only.
// Ours stamps the ELK field set on it first, so a failure inside a component is queryable in
// Kibana the same way an API failure is. Registered before AddRazorComponents' own default wins.
builder.Services.AddScoped<IErrorBoundaryLogger, LoggingErrorBoundaryLogger>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<ApiLoggingHandler>();

// Base address and key default to the MVC app's https launch profile and its Api:Keys entry, so
// running both projects needs no configuration. Override in appsettings.Development.json.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7119";
var apiKey = builder.Configuration["Api:Key"] ?? "mvcapp-local-dev-key";
var apiHeaderName = builder.Configuration["Api:HeaderName"] ?? "X-Api-Key";

builder.Services.AddHttpClient<MvcAppApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add(apiHeaderName, apiKey);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    // Logs every outbound call into mvcapp-logs-*. The OTel HttpClient instrumentation traces
    // these calls too, but a trace is not a log and never reaches the log index.
    .AddHttpMessageHandler<ApiLoggingHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // The MVC app runs on the ASP.NET Core development certificate, which this process does
        // not trust by default. Scoped to Development only; in any other environment a bad
        // certificate stays a hard failure.
        ServerCertificateCustomValidationCallback = builder.Environment.IsDevelopment()
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null
    });

var app = builder.Build();

// Outermost, so it sees the status code that was ultimately written and is the last place an
// escaping exception can still be recorded. Mirrors MVC_App's own pipeline order.
app.UseAppRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Before UseAntiforgery and the endpoints, so an authenticated identity exists by the time a
// component is authorized or a sign-in form is handled.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
