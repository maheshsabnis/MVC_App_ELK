using System.Diagnostics;
using MVC_App.BlazorClient.Diagnostics;

namespace MVC_App.BlazorClient.Telemetry;

/// <summary>
/// Writes one structured log entry for every HTTP call this app makes to the MVC API, including
/// the ones that fail before a response exists (connection refused, TLS failure, timeout).
///
/// This is deliberately separate from the OpenTelemetry HttpClient instrumentation registered in
/// <see cref="TelemetryExtensions"/>. That instrumentation produces <em>traces</em>, which go
/// straight to Elasticsearch as <c>traces-generic.otel-default</c> and never reach
/// <c>mvcapp-logs-*</c>. Outbound calls therefore have to be logged explicitly to be queryable
/// alongside everything else in the log index.
///
/// Each entry carries the same <c>trace_id</c> as the MVC app's server-side entry for the same
/// call, so one trace id in Kibana shows the click, the outbound request, the API's own request
/// log, its SQL, and any exception it threw.
/// </summary>
public sealed class ApiLoggingHandler : DelegatingHandler
{
    private readonly ILogger<ApiLoggingHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public ApiLoggingHandler(ILogger<ApiLoggingHandler> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = _timeProvider.GetTimestamp();
        var method = request.Method.Method;
        var url = request.RequestUri?.ToString();
        var path = request.RequestUri?.AbsolutePath;

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            var elapsedMs = _timeProvider.GetElapsedTime(started).TotalMilliseconds;
            var statusCode = (int)response.StatusCode;
            var condition = ErrorConditions.ForStatusCode(statusCode);

            using var scope = _logger.BeginScope(AppLogging.Fields(statusCode, exception: null, Extra(
                method, url, path, elapsedMs)));

            _logger.Log(AppLogging.LevelForStatusCode(statusCode),
                "API call {Method} {Path} responded {StatusCode} [{ErrorCategory}] in {DurationMs:F1}ms",
                method, path, statusCode, condition, elapsedMs);

            return response;
        }
        catch (Exception exception)
        {
            // No response came back at all. Reported as 503 rather than 500: the failure is that a
            // dependency (the MVC API) could not be reached, which is the same condition the MVC
            // app reports with DependencyUnavailableException, so both show up under the one
            // error.category : "service_unavailable" filter.
            var elapsedMs = _timeProvider.GetElapsedTime(started).TotalMilliseconds;
            const int statusCode = StatusCodes.Status503ServiceUnavailable;

            using var scope = _logger.BeginScope(AppLogging.Fields(statusCode, exception, Extra(
                method, url, path, elapsedMs)));

            _logger.LogError(exception,
                "API call {Method} {Path} failed after {DurationMs:F1}ms without a response [{ErrorCategory}]: {Message}",
                method, path, elapsedMs, ErrorConditions.Unavailable, exception.Message);

            throw;
        }
    }

    private static Dictionary<string, object?> Extra(
        string method, string? url, string? path, double elapsedMs) => new()
    {
        ["http.request.method"] = method,
        ["url.full"] = url,
        ["url.path"] = path,
        ["http.client.duration_ms"] = elapsedMs,

        // Marks this as a call leaving the app, as opposed to one arriving at it. Both shapes
        // carry http.request.method and url.path, so without this they are hard to tell apart.
        ["http.request.direction"] = "outbound",
        ["trace.id"] = Activity.Current?.TraceId.ToString()
    };
}
