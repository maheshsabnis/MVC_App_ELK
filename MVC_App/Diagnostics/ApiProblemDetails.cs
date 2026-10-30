using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MVC_App.Diagnostics;

/// <summary>
/// The single JSON error shape every <c>/api/...</c> endpoint returns, modelled on RFC 7807
/// with two extra members the ELK dashboard relies on: <c>errorCategory</c> (the same slug that
/// goes into <c>log_attributes.error.category</c>) and <c>traceId</c>, which matches the
/// <c>trace_id</c> on the corresponding log and trace documents. A caller that gets a failure can
/// paste its <c>traceId</c> straight into Kibana and find the server-side record of it.
/// </summary>
public sealed class ApiProblemDetails
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>The ELK filter value, e.g. "unauthorized", "not_found", "server_error".</summary>
    [JsonPropertyName("errorCategory")]
    public string ErrorCategory { get; init; } = ErrorConditions.None;

    /// <summary>The exception's CLR type name, when the failure came from a thrown exception.</summary>
    [JsonPropertyName("errorType")]
    public string? ErrorType { get; init; }

    /// <summary>Correlates this response with the log and trace documents in Elasticsearch.</summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ApiProblemDetails Create(
        HttpContext context, int statusCode, string title, string? detail, string? errorType = null)
        => new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path.Value,
            ErrorCategory = ErrorConditions.ForStatusCode(statusCode),
            ErrorType = errorType,
            TraceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
        };

    /// <summary>
    /// Writes the problem document to the response. No-ops when the response has already
    /// started, since the status code and headers can no longer be changed at that point.
    /// </summary>
    public static async Task WriteAsync(
        HttpContext context, int statusCode, string title, string? detail, string? errorType = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = Create(context, statusCode, title, detail, errorType);
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions), context.RequestAborted);
    }
}
