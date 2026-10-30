using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MVC_App.Authentication;
using MVC_App.Data;
using MVC_App.Diagnostics;
using MVC_App.Middleware;
using MVC_App.Models;
using MVC_App.Models.Identity;
using MVC_App.Telemetry;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationTelemetry();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// The JSON API controllers validate their own models (ApiControllerBase.ThrowIfInvalid) so that a
// 400 flows through ExceptionHandlingMiddleware and picks up the same error.category fields as
// every other failure. ApiController's automatic 400 would short-circuit before that runs.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// Logs every DB command (success and failure) on both DbContexts; see Data/DbCommandLoggingInterceptor.cs.
builder.Services.AddSingleton<DbCommandLoggingInterceptor>();

builder.Services.AddDbContext<CompanyContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CompanyContext"))
        .AddInterceptors(sp.GetRequiredService<DbCommandLoggingInterceptor>()));

// Microsoft Identity is stored in its own code-first database on the local SQL Server
// instance, kept separate from the existing (database-first) CompanyContext.
builder.Services.AddDbContext<IdentityContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection"))
        .AddInterceptors(sp.GetRequiredService<DbCommandLoggingInterceptor>()));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<IdentityContext>()
    .AddDefaultTokenProviders();

// Identity's cookie stays the default scheme for the MVC views. The API key scheme is additive:
// /api controllers opt into it explicitly, which is what lets an unauthenticated API call return
// a 401 instead of a 302 redirect to the login page.
builder.Services.AddAuthentication()
    .AddApiKeyAuthentication(builder.Configuration);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Logs the final status code of every request (2xx/3xx as Information, 401/402/403/4xx as
// Warning, 5xx as Error) along with its error condition (error.category, error.is_unauthorized,
// error.is_not_found, error.is_server_error) so the ELK dashboard can chart the full
// status-code distribution and filter on a stable condition slug.
app.UseAppRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Catches domain validation exceptions (UnauthorizedRequestException, ResourceNotFoundException,
// ServerSideException, PrimaryKeyNotFoundException, DuplicateDeptNameException, ...), logs them
// with their error condition, and either redirects to /Error (browser) or writes an
// ApiProblemDetails document (/api).
//
// Registered ahead of UseStatusCodePages so it also wraps that callback. Everything downstream of
// this line -- every API and MVC controller, every Razor Page, routing, authentication and the
// status-code pages -- has its exceptions logged here. Only the environment's own handler above
// sits outside it, and RequestLoggingMiddleware catches anything that escapes that far.
app.UseAppExceptionHandling();

// Gives /api responses that the framework produced without a body (an unmatched route's 404, a
// 405 from the wrong verb, a 401 challenge) the same JSON problem shape as the handled errors,
// so an API caller never has to special-case an empty body.
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (!RequestLogging.IsApiRequest(context.HttpContext) ||
        response.ContentLength.HasValue ||
        !string.IsNullOrEmpty(response.ContentType))
    {
        return;
    }

    await ApiProblemDetails.WriteAsync(
        context.HttpContext,
        response.StatusCode,
        title: ReasonPhrases.For(response.StatusCode),
        detail: $"The request to '{context.HttpContext.Request.Path}' ended with status {response.StatusCode}.");
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

/// <summary>Minimal status-code-to-title map for the problem documents written above.</summary>
internal static class ReasonPhrases
{
    public static string For(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status405MethodNotAllowed => "Method Not Allowed",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status415UnsupportedMediaType => "Unsupported Media Type",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        >= 500 => "Internal Server Error",
        _ => "Request Failed"
    };
}
