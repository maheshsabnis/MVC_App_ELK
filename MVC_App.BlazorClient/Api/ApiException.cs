using MVC_App.BlazorClient.Diagnostics;

namespace MVC_App.BlazorClient.Api;

/// <summary>
/// Thrown when the API answers with a failure status. It carries the status code and the server's
/// own <c>ApiProblemDetails</c> so the UI can show the real reason ("Department name 'HRD-20'
/// already exists") instead of a generic message, and so the exception can be logged under the
/// right <c>error.category</c> rather than being flattened to a 500.
///
/// <see cref="TraceId"/> is the server's trace id for the failed call. Showing it in the UI means
/// a user can report a failure by quoting an id that finds the server-side record in Kibana.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string message, ApiProblemDetails? problem, string? requestUri)
        : base(message)
    {
        StatusCode = statusCode;
        Problem = problem;
        RequestUri = requestUri;
    }

    public int StatusCode { get; }

    public ApiProblemDetails? Problem { get; }

    public string? RequestUri { get; }

    /// <summary>The slug this failure is filed under in ELK, e.g. "conflict", "not_found".</summary>
    public string ErrorCategory => Problem?.ErrorCategory ?? ErrorConditions.ForStatusCode(StatusCode);

    /// <summary>The server-side exception type, when the failure came from a thrown exception.</summary>
    public string? ServerErrorType => Problem?.ErrorType;

    /// <summary>Correlates with <c>trace_id</c> on the server's log and trace documents.</summary>
    public string? TraceId => Problem?.TraceId;

    /// <summary>
    /// The sentence to put in front of a user. Prefers the server's own detail, which is written
    /// for exactly this purpose, and falls back to the title and then the status code.
    /// </summary>
    public string DisplayMessage =>
        Problem?.Detail
        ?? (string.IsNullOrWhiteSpace(Problem?.Title) ? null : Problem!.Title)
        ?? $"The API returned {StatusCode}.";
}
