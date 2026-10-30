using MVC_App.BlazorClient.Diagnostics;

namespace MVC_App.BlazorClient.Telemetry;

/// <summary>
/// Outermost middleware: logs the final outcome of every inbound request, and is the last place
/// an exception can be recorded before it leaves the process.
///
/// Mirrors <c>MVC_App/Middleware/RequestLoggingMiddleware.cs</c>, including its rule that an
/// exception reaching here unlogged must be written out with its stack trace rather than reduced
/// to a bare status code.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
            Log(context, context.Response.StatusCode, exception: null);
        }
        catch (Exception exception)
        {
            Log(context, StatusCodes.Status500InternalServerError, exception);
            throw;
        }
    }

    private void Log(HttpContext context, int statusCode, Exception? exception)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        // The Blazor SignalR circuit is a single long-lived request that would otherwise be logged
        // once per connection with no useful outcome. Component activity inside the circuit is
        // logged by LoggingErrorBoundaryLogger and ApiLoggingHandler instead.
        if (path is not null && path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var condition = ErrorConditions.ForStatusCode(statusCode);

        var fields = AppLogging.Fields(statusCode, exception, new Dictionary<string, object?>
        {
            ["http.request.method"] = method,
            ["url.path"] = path,
            ["url.query"] = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null
        });

        using var scope = _logger.BeginScope(fields);

        if (exception is not null)
        {
            _logger.Log(AppLogging.LevelForStatusCode(statusCode), exception,
                "{ExceptionType} while handling {Method} {Path} -> {StatusCode} [{ErrorCategory}]: {Message}",
                exception.GetType().Name, method, path, statusCode, condition, exception.Message);
        }
        else
        {
            _logger.Log(AppLogging.LevelForStatusCode(statusCode),
                "{Method} {Path} responded {StatusCode} [{ErrorCategory}]",
                method, path, statusCode, condition);
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAppRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
