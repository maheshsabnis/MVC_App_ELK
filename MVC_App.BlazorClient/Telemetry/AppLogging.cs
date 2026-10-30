using MVC_App.BlazorClient.Diagnostics;

namespace MVC_App.BlazorClient.Telemetry;

/// <summary>
/// Builds the structured field set that every log entry from this app carries, so a document
/// written here is shaped exactly like one written by MVC_App and the same Kibana queries find
/// both (see docs/ELK-KQL-Examples.txt).
///
/// Three callers use it, covering the three ways something can happen in a Blazor Server app:
/// <list type="bullet">
/// <item>inbound page and SignalR requests, via <see cref="RequestLoggingMiddleware"/>;</item>
/// <item>outbound API calls, via <see cref="ApiLoggingHandler"/>;</item>
/// <item>exceptions raised while a component renders, via <see cref="LoggingErrorBoundaryLogger"/>.</item>
/// </list>
/// </summary>
internal static class AppLogging
{
    /// <summary>
    /// Maps an HTTP status code to the level ELK should surface it at. Identical to the MVC app's
    /// rule: 5xx is Error, every 4xx is Warning, everything else is Information. Note that this
    /// puts a 404 or a 409 at Warning, so a <c>severity_text : "Error"</c> filter in Kibana will
    /// not show them. Filter on <c>error.type : *</c> instead.
    /// </summary>
    public static LogLevel LevelForStatusCode(int statusCode) => statusCode switch
    {
        >= 500 => LogLevel.Error,
        401 or 402 or 403 => LogLevel.Warning,
        >= 400 => LogLevel.Warning,
        _ => LogLevel.Information
    };

    /// <summary>
    /// The common field set. <paramref name="extra"/> adds call-site specific fields, e.g. the
    /// target URL on an outbound call. Nulls are stripped, because an absent field and a null
    /// behave identically for an <c>exists</c> query but an absent one keeps Discover readable.
    /// </summary>
    public static Dictionary<string, object?> Fields(
        int statusCode,
        Exception? exception,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var condition = ErrorConditions.ForStatusCode(statusCode);
        var isFailure = ErrorConditions.IsFailure(statusCode);

        var fields = new Dictionary<string, object?>
        {
            ["http.response.status_code"] = statusCode,

            // Error-condition fields, present on every entry including the successful ones
            // (error.category = "none"), so a Kibana filter never silently drops documents
            // just because a field is missing on the healthy path.
            ["error.category"] = condition,
            ["error.is_unauthorized"] = condition == ErrorConditions.Unauthorized,
            ["error.is_not_found"] = condition == ErrorConditions.NotFound,
            ["error.is_server_error"] = condition == ErrorConditions.ServerError,
            ["error.type"] = exception?.GetType().Name,
            ["event.outcome"] = isFailure ? "failure" : "success",
            ["event.kind"] = isFailure ? "error" : "event",

            // Distinguishes this app's documents from MVC_App's inside the shared index. The MVC
            // app stamps http.request.is_api; this is the Blazor equivalent dimension.
            ["app.component"] = "blazor-client"
        };

        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                fields[key] = value;
            }
        }

        foreach (var key in fields.Where(f => f.Value is null).Select(f => f.Key).ToList())
        {
            fields.Remove(key);
        }

        return fields;
    }
}
