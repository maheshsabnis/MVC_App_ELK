using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MVC_App.Diagnostics;
using MVC_App.Middleware;

namespace MVC_App.Authentication;

public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>The header the key is read from. Overridable mostly so tests can vary it.</summary>
    public string HeaderName { get; set; } = ApiKeyAuthenticationDefaults.HeaderName;

    /// <summary>
    /// The accepted keys, loaded from configuration (<c>Api:Keys</c>). An empty collection means
    /// no caller can authenticate, which surfaces as a 401 rather than as an open door.
    /// </summary>
    public IList<string> ValidKeys { get; } = new List<string>();
}

/// <summary>
/// Minimal header-based scheme for the JSON API, so a non-interactive caller (the
/// MVC_App.ApiClient console app) can authenticate without the cookie/login round trip that the
/// MVC views use. A missing or unrecognised key fails authentication, which the challenge below
/// turns into a 401 with a JSON body, logged with <c>error.category : "unauthorized"</c>.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var suppliedKey) ||
            string.IsNullOrWhiteSpace(suppliedKey))
        {
            // NoResult rather than Fail: the cookie-authenticated browser also hits endpoints
            // that allow anonymous access, and a hard failure here would mask those.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var key = suppliedKey.ToString();
        if (!Options.ValidKeys.Contains(key, StringComparer.Ordinal))
        {
            Logger.LogWarning(
                "API key authentication failed for {Method} {Path}: supplied key was not recognised [{ErrorCategory}]",
                Request.Method, Request.Path.Value, ErrorConditions.Unauthorized);
            return Task.FromResult(AuthenticateResult.Fail("The supplied API key was not recognised."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "api-client"),
            new Claim(ClaimTypes.NameIdentifier, "api-client"),
            new Claim("auth_method", ApiKeyAuthenticationDefaults.Scheme)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Writes a 401 as JSON instead of redirecting to the login page. The status code is what
    /// <see cref="RequestLoggingMiddleware"/> records, so this is the single place the API's
    /// "unauthorized" condition originates.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"{Scheme.Name} header=\"{Options.HeaderName}\"";

        await ApiProblemDetails.WriteAsync(
            Context,
            StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: $"Supply a valid API key in the '{Options.HeaderName}' request header.");
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;

        await ApiProblemDetails.WriteAsync(
            Context,
            StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: "The supplied API key is valid but is not permitted to perform this operation.");
    }
}

public static class ApiKeyAuthenticationExtensions
{
    /// <summary>
    /// Registers the API key scheme alongside Identity's cookie scheme. Identity stays the
    /// default for the MVC views; API controllers opt in explicitly with
    /// <c>[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]</c>.
    /// </summary>
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder, IConfiguration configuration)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.Scheme,
            options =>
            {
                options.HeaderName = configuration["Api:HeaderName"] ?? ApiKeyAuthenticationDefaults.HeaderName;

                foreach (var key in configuration.GetSection("Api:Keys").Get<string[]>() ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        options.ValidKeys.Add(key);
                    }
                }
            });
    }
}
