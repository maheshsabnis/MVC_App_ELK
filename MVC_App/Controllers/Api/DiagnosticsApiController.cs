using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_App.Diagnostics;
using MVC_App.Exceptions;

namespace MVC_App.Controllers.Api;

/// <summary>
/// Endpoints whose only job is to produce one specific error condition on demand, so the ELK
/// dashboard can be built and demonstrated without having to provoke a real failure in the
/// Department/Employee data. Each one throws the same exception type the real endpoints throw,
/// so the resulting log documents are indistinguishable apart from <c>url.path</c>.
/// </summary>
[Route("api/diagnostics")]
public sealed class DiagnosticsApiController : ApiControllerBase
{
    private const string ResourceName = "Diagnostics";

    private readonly ILogger<DiagnosticsApiController> _logger;

    public DiagnosticsApiController(ILogger<DiagnosticsApiController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Anonymous liveness check, the one endpoint that does not require an API key. Useful as the
    /// "is the app up at all" baseline next to the error panels on a dashboard.
    /// </summary>
    // GET: api/diagnostics/ping
    [HttpGet("ping")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        _logger.LogInformation("Diagnostics ping [{ErrorCategory}]", ErrorConditions.None);
        return Ok(new
        {
            status = "ok",
            service = "MVC_App",
            utc = DateTimeOffset.UtcNow
        });
    }

    /// <summary>Requires a valid API key; confirms the key works before running the error cases.</summary>
    // GET: api/diagnostics/whoami
    [HttpGet("whoami")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult WhoAmI() => Ok(new
    {
        name = User.Identity?.Name,
        authenticated = User.Identity?.IsAuthenticated ?? false,
        scheme = User.Identity?.AuthenticationType
    });

    // GET: api/diagnostics/unauthorized  ->  401, error.category "unauthorized"
    [HttpGet("unauthorized")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult RaiseUnauthorized()
        => throw new UnauthorizedRequestException(
            "the caller's credentials were rejected by the diagnostics endpoint.");

    // GET: api/diagnostics/forbidden  ->  403, error.category "forbidden"
    [HttpGet("forbidden")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult RaiseForbidden()
        => throw new ForbiddenRequestException(
            "the caller is authenticated but lacks permission for this operation.");

    // GET: api/diagnostics/not-found  ->  404, error.category "not_found"
    [HttpGet("not-found")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult RaiseNotFound()
        => throw new ResourceNotFoundException("api/diagnostics/not-found");

    // GET: api/diagnostics/bad-request  ->  400, error.category "validation_error"
    [HttpGet("bad-request")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult RaiseBadRequest()
        => throw new ValidationFailedException(
            "The diagnostics endpoint was asked to produce a validation failure.");

    // GET: api/diagnostics/conflict  ->  409, error.category "conflict"
    [HttpGet("conflict")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult RaiseConflict()
        => throw new DuplicatePrimaryKeyException(ResourceName, "diagnostics-conflict");

    /// <summary>
    /// 500 via a deliberately raised <see cref="ServerSideException"/>, i.e. a failure the app
    /// knew about and classified.
    /// </summary>
    // GET: api/diagnostics/server-error
    [HttpGet("server-error")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult RaiseServerError()
        => throw new ServerSideException(
            "The diagnostics endpoint was asked to produce a server-side failure.",
            new InvalidOperationException("Simulated downstream fault."));

    /// <summary>
    /// 500 via an exception the app never anticipated. Produces the same status code as
    /// <see cref="RaiseServerError"/>, but with <c>error.type : "DivideByZeroException"</c>,
    /// which is what makes the two distinguishable in Kibana.
    /// </summary>
    // GET: api/diagnostics/unhandled
    [HttpGet("unhandled")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult RaiseUnhandled()
    {
        var divisor = 0;
        // Intentional: an exception type the app has no specific handling for, so the
        // "unexpected server error" branch of ExceptionHandlingMiddleware is exercised.
        return Ok(10 / divisor);
    }

    // GET: api/diagnostics/unavailable  ->  503, error.category "service_unavailable"
    [HttpGet("unavailable")]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult RaiseUnavailable()
        => throw new DependencyUnavailableException("Company-database");
}
