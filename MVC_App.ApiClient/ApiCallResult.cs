using System.Net;
using System.Text.Json;

namespace MVC_App.ApiClient;

/// <summary>
/// One call's outcome: what was expected, what came back, and the correlation id the server put
/// in the problem document. The <see cref="TraceId"/> is the value to paste into Kibana
/// (<c>trace_id : "..."</c>) to find the server-side log entry for this exact call.
/// </summary>
public sealed record ApiCallResult(
    string Scenario,
    HttpMethod Method,
    string Path,
    int ExpectedStatus,
    int ActualStatus,
    string? ErrorCategory,
    string? ErrorType,
    string? TraceId,
    string Body,
    TimeSpan Elapsed,
    string? TransportError = null)
{
    public bool Matched => TransportError is null && ActualStatus == ExpectedStatus;

    public static ApiCallResult Transport(
        string scenario, HttpMethod method, string path, int expectedStatus, string error, TimeSpan elapsed)
        => new(scenario, method, path, expectedStatus, 0, null, null, null, string.Empty, elapsed, error);

    /// <summary>
    /// Pulls <c>errorCategory</c>, <c>errorType</c> and <c>traceId</c> out of an
    /// <c>application/problem+json</c> body. Returns nulls for a success response or any body that
    /// isn't the expected shape; a malformed body is never a reason to fail the run.
    /// </summary>
    public static (string? Category, string? Type, string? TraceId) ReadProblemFields(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
        {
            return (null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return (
                root.TryGetProperty("errorCategory", out var category) ? category.GetString() : null,
                root.TryGetProperty("errorType", out var type) ? type.GetString() : null,
                root.TryGetProperty("traceId", out var traceId) ? traceId.GetString() : null);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    public string StatusText => TransportError is not null
        ? "ERR"
        : $"{ActualStatus} {(HttpStatusCode)ActualStatus}";
}
