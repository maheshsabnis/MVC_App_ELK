using MVC_App.Exceptions;

namespace MVC_App.Middleware;

/// <summary>
/// Outermost middleware: logs the final outcome of every request (status code + route) at a
/// level based on that status code, so the ELK dashboard can chart 2xx/3xx traffic alongside
/// 401/402/5xx errors. Registered before any exception-handling middleware so it always sees
/// the status code that was ultimately written to the response.
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
            RequestLogging.LogByStatusCode(_logger, context, context.Response.StatusCode, exception: null);
        }
        catch (Exception exception)
        {
            // Last line of defence: nothing outside this middleware can log, so an exception that
            // reaches here unlogged would be lost entirely.
            //
            // ExceptionHandlingMiddleware covers everything registered inside it, which is every
            // API and MVC controller, Razor Page, and the auth/routing middleware. For those this
            // only needs to add the final status code, and passing the exception again would
            // duplicate the entry. But an exception raised *outside* that middleware (the
            // UseStatusCodePages callback, or the environment's own handler while it renders the
            // error response) never passed through it, so here it is still unrecorded and this is
            // the only place left that can write it out with its stack trace.
            var alreadyLogged = RequestLogging.WasAlreadyLogged(context, exception);

            // An AppException rethrown because the response had already started keeps its own
            // status code; anything else genuinely is a 500.
            var statusCode = exception is AppException appException
                ? appException.StatusCode
                : StatusCodes.Status500InternalServerError;

            RequestLogging.LogByStatusCode(_logger, context, statusCode, alreadyLogged ? null : exception);
            throw;
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAppRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
