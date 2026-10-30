using MVC_App.BlazorClient.Diagnostics;
using MVC_App.BlazorClient.Telemetry;

namespace MVC_App.BlazorClient.Identity;

/// <summary>
/// Writes sign-in and sign-out events into <c>mvcapp-logs-*</c> with the same field set as every
/// other entry, so authentication activity is queryable next to API calls and exceptions rather
/// than sitting in a separate audit trail.
///
/// A failed sign-in is logged at Warning with <c>error.category : "unauthorized"</c>, which is the
/// same slug the MVC API uses for a rejected API key. One Kibana filter therefore covers "someone
/// was refused access" across both apps and both mechanisms.
///
/// The user name is recorded because it is the subject of the event and the reason to look at it
/// at all. The password is never read, logged, or passed anywhere except to SignInManager.
/// </summary>
public sealed class AuthEventLogger
{
    private const string EventCategory = "authentication";

    private readonly ILogger<AuthEventLogger> _logger;

    public AuthEventLogger(ILogger<AuthEventLogger> logger)
    {
        _logger = logger;
    }

    public void SignInSucceeded(string userName)
    {
        using var scope = _logger.BeginScope(Fields(StatusCodes.Status200OK, userName, "sign_in"));

        _logger.LogInformation(
            "Sign-in succeeded for {UserName} [{ErrorCategory}]", userName, ErrorConditions.None);
    }

    /// <param name="reason">
    /// The Identity outcome, e.g. "invalid_credentials" or "locked_out". Deliberately coarse: it
    /// says enough to investigate without telling an attacker reading the logs whether the user
    /// name existed.
    /// </param>
    public void SignInFailed(string userName, string reason)
    {
        var fields = Fields(StatusCodes.Status401Unauthorized, userName, "sign_in");
        fields["auth.failure_reason"] = reason;

        using var scope = _logger.BeginScope(fields);

        _logger.LogWarning(
            "Sign-in failed for {UserName}: {Reason} [{ErrorCategory}]",
            userName, reason, ErrorConditions.Unauthorized);
    }

    public void SignedOut(string userName)
    {
        using var scope = _logger.BeginScope(Fields(StatusCodes.Status200OK, userName, "sign_out"));

        _logger.LogInformation(
            "Sign-out for {UserName} [{ErrorCategory}]", userName, ErrorConditions.None);
    }

    private static Dictionary<string, object?> Fields(int statusCode, string userName, string action)
    {
        var fields = AppLogging.Fields(statusCode, exception: null, new Dictionary<string, object?>
        {
            ["event.category"] = EventCategory,
            ["event.action"] = action,
            ["user.name"] = userName
        });

        return fields;
    }
}
