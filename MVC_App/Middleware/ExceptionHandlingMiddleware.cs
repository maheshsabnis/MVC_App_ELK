using MVC_App.Diagnostics;
using MVC_App.Exceptions;

namespace MVC_App.Middleware;

/// <summary>
/// Catches domain validation exceptions (<see cref="AppException"/>) close to the endpoint,
/// logs them at a severity ELK can filter/dashboard on, and renders them in the shape the caller
/// expects: a redirect to the shared error page for browser requests, an
/// <see cref="ApiProblemDetails"/> JSON document for <c>/api/...</c> requests.
///
/// Any other exception is logged as an unexpected server error. For API requests it is converted
/// into a 500 problem document so the caller always gets JSON back; for browser requests it is
/// rethrown so the environment's own handler (developer exception page, or app.UseExceptionHandler)
/// still runs.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException appException)
        {
            RequestLogging.LogByStatusCode(_logger, context, appException.StatusCode, appException);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            if (RequestLogging.IsApiRequest(context))
            {
                await ApiProblemDetails.WriteAsync(
                    context,
                    appException.StatusCode,
                    title: TitleFor(appException.StatusCode),
                    detail: appException.Message,
                    errorType: appException.GetType().Name);
                return;
            }

            context.Response.StatusCode = appException.StatusCode;
            context.Response.Redirect(
                $"/Error?message={Uri.EscapeDataString(appException.Message)}&statusCode={appException.StatusCode}");
        }
        catch (Exception ex)
        {
            RequestLogging.LogByStatusCode(_logger, context, StatusCodes.Status500InternalServerError, ex);

            if (!RequestLogging.IsApiRequest(context) || context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            await ApiProblemDetails.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "The request could not be completed because of an unexpected server-side failure.",
                errorType: ex.GetType().Name);
        }
    }

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        >= 500 => "Internal Server Error",
        _ => "Request Failed"
    };
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseAppExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
