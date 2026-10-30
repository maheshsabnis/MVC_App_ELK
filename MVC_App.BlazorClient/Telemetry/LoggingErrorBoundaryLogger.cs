using Microsoft.AspNetCore.Components.Web;
using MVC_App.BlazorClient.Api;
using MVC_App.BlazorClient.Diagnostics;

namespace MVC_App.BlazorClient.Telemetry;

/// <summary>
/// Logs exceptions caught by an <see cref="ErrorBoundary"/> in the component tree.
///
/// This is the third of the three ways something can go wrong here, and the one a plain
/// middleware cannot see. A Blazor Server component renders over the SignalR circuit, long after
/// the HTTP request that opened the page has completed, so an exception thrown in
/// <c>OnInitializedAsync</c> or in a button handler never passes through
/// <see cref="RequestLoggingMiddleware"/>. The framework's default implementation writes it to the
/// local logger only; replacing the service routes it through the same field set as everything
/// else and therefore into <c>mvcapp-logs-*</c>.
///
/// An <see cref="ApiException"/> keeps the status code the API actually returned, so a 409 from
/// the server is logged as a conflict here too rather than being flattened into a 500.
/// </summary>
public sealed class LoggingErrorBoundaryLogger : IErrorBoundaryLogger
{
    private readonly ILogger<LoggingErrorBoundaryLogger> _logger;

    public LoggingErrorBoundaryLogger(ILogger<LoggingErrorBoundaryLogger> logger)
    {
        _logger = logger;
    }

    public ValueTask LogErrorAsync(Exception exception)
    {
        var statusCode = exception is ApiException apiException
            ? apiException.StatusCode
            : StatusCodes.Status500InternalServerError;

        var condition = ErrorConditions.ForStatusCode(statusCode);

        var fields = AppLogging.Fields(statusCode, exception, new Dictionary<string, object?>
        {
            // Names where in the app the failure surfaced, since there is no URL path to key on.
            ["event.module"] = "blazor-component",
            ["error.boundary"] = true
        });

        using var scope = _logger.BeginScope(fields);

        _logger.Log(AppLogging.LevelForStatusCode(statusCode), exception,
            "{ExceptionType} while rendering a component -> {StatusCode} [{ErrorCategory}]: {Message}",
            exception.GetType().Name, statusCode, condition, exception.Message);

        return ValueTask.CompletedTask;
    }
}
