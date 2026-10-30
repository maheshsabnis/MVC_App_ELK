using MVC_App.Diagnostics;

namespace MVC_App.Middleware;

/// <summary>
/// Maps an HTTP status code to the log level ELK should surface it at, and writes a single
/// structured log entry per occurrence. 401/402/403 and 5xx are always Warning/Error so they are
/// never filtered out; other 4xx are Warning; 2xx/3xx are Information.
///
/// Every entry also carries an explicit error condition (<c>error.category</c>, plus the three
/// boolean flags <c>error.is_unauthorized</c> / <c>error.is_not_found</c> / <c>error.is_server_error</c>)
/// so Kibana can filter on a stable value rather than re-deriving meaning from the status code.
/// </summary>
internal static class RequestLogging
{
    /// <summary>
    /// <see cref="HttpContext.Items"/> key holding the exception that <see cref="LogByStatusCode"/>
    /// has already written an entry for. It lets the outermost middleware tell "this was logged
    /// downstream, only the final status code is still missing" apart from "nothing logged this at
    /// all, and I am the last chance to", without logging the same failure twice.
    /// </summary>
    private const string LoggedExceptionKey = "MVC_App.LoggedException";

    /// <summary>
    /// True when <paramref name="exception"/> has already been written out for this request by an
    /// inner middleware. Compared by reference, so a different exception thrown later in the same
    /// request (e.g. while rendering the error response) is still treated as unlogged.
    /// </summary>
    public static bool WasAlreadyLogged(HttpContext context, Exception exception)
        => context.Items.TryGetValue(LoggedExceptionKey, out var logged) && ReferenceEquals(logged, exception);

    public static LogLevel LevelForStatusCode(int statusCode) => statusCode switch
    {
        >= 500 => LogLevel.Error,
        401 or 402 or 403 => LogLevel.Warning,
        >= 400 => LogLevel.Warning,
        _ => LogLevel.Information
    };

    public static void LogByStatusCode(ILogger logger, HttpContext context, int statusCode, Exception? exception)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;
        var logLevel = LevelForStatusCode(statusCode);
        var condition = ErrorConditions.ForStatusCode(statusCode);
        var isFailure = ErrorConditions.IsFailure(statusCode);

        // Null when the URL matched no endpoint at all, which is how a "no such route" 404 is
        // told apart from a "route was fine, the row wasn't there" 404.
        var routePattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;

        var fields = new Dictionary<string, object?>
        {
            ["http.request.method"] = method,
            ["http.response.status_code"] = statusCode,
            ["url.path"] = path,

            // Logged separately from url.path, which excludes it. Needed to tell a deliberately
            // faulted call (?simulate=500) apart from a real failure on the same route.
            ["url.query"] = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
            ["http.route"] = routePattern,

            // Deliberately NOT named "http.route.matched": Elasticsearch would have to make
            // `http.route` an object to hold a child field, which conflicts with the string
            // above, and every document carrying both would be rejected at index time.
            ["http.route_matched"] = routePattern is not null,

            // Error-condition fields. Present on *every* log entry, including successful ones
            // (error.category = "none"), so a Kibana filter never silently drops documents just
            // because a field is missing on the healthy path.
            ["error.category"] = condition,
            ["error.is_unauthorized"] = condition == ErrorConditions.Unauthorized,
            ["error.is_not_found"] = condition == ErrorConditions.NotFound,
            ["error.is_server_error"] = condition == ErrorConditions.ServerError,
            ["error.type"] = exception?.GetType().Name,
            ["event.outcome"] = isFailure ? "failure" : "success",
            ["event.kind"] = isFailure ? "error" : "event",
            ["http.request.is_api"] = IsApiRequest(context)
        };

        // Drop the nulls rather than exporting them. Elasticsearch treats a null the same as a
        // missing field for `exists`, so a query like `error.type : *` behaves identically either
        // way, but an absent field keeps the documents readable in Discover.
        foreach (var key in fields.Where(f => f.Value is null).Select(f => f.Key).ToList())
        {
            fields.Remove(key);
        }

        using var scope = logger.BeginScope(fields);

        if (exception is not null)
        {
            logger.Log(logLevel, exception,
                "{ExceptionType} while handling {Method} {Path} -> {StatusCode} [{ErrorCategory}]: {Message}",
                exception.GetType().Name, method, path, statusCode, condition, exception.Message);

            // Record that this exception is now on the wire, so the outermost middleware does not
            // emit a second entry for it (see WasAlreadyLogged).
            context.Items[LoggedExceptionKey] = exception;
        }
        else
        {
            logger.Log(logLevel,
                "{Method} {Path} responded {StatusCode} [{ErrorCategory}]",
                method, path, statusCode, condition);
        }
    }

    /// <summary>
    /// True when the request targets the JSON API surface (<c>/api/...</c>) rather than a
    /// server-rendered MVC view or Razor Page. Used both to pick the error response format and
    /// to let ELK split API traffic from browser traffic.
    /// </summary>
    public static bool IsApiRequest(HttpContext context)
        => context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
