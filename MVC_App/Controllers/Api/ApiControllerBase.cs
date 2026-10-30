using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_App.Authentication;
using MVC_App.Exceptions;

namespace MVC_App.Controllers.Api;

/// <summary>
/// Shared base for the JSON API controllers. Fixes three things in one place: the API key
/// authentication scheme (so an unauthenticated call is a 401 and not a redirect to the login
/// page), the validation-failure path (so a bad body is a 400 with the same problem shape as
/// every other error), and the <c>?simulate=</c> switch used to generate each error condition
/// on demand for the ELK dashboard.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Throws the <see cref="AppException"/> matching a <c>?simulate=</c> query value, so each
    /// error condition can be produced deliberately against a real endpoint and real route.
    /// Returns without doing anything when the parameter is absent or unrecognised.
    ///
    /// Supported values: <c>401</c>/<c>unauthorized</c>, <c>403</c>/<c>forbidden</c>,
    /// <c>404</c>/<c>notfound</c>, <c>409</c>/<c>conflict</c>, <c>400</c>/<c>validation</c>,
    /// <c>500</c>/<c>servererror</c>, <c>503</c>/<c>unavailable</c>.
    /// </summary>
    protected void ApplySimulatedFault(string? simulate, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(simulate))
        {
            return;
        }

        switch (simulate.Trim().ToLowerInvariant())
        {
            case "400":
            case "validation":
                throw new ValidationFailedException(
                    $"Simulated validation failure on {resourceName}, requested via ?simulate={simulate}.");

            case "401":
            case "unauthorized":
                throw new UnauthorizedRequestException(
                    $"simulated authentication failure on {resourceName}, requested via ?simulate={simulate}.");

            case "403":
            case "forbidden":
                throw new ForbiddenRequestException(
                    $"simulated authorization failure on {resourceName}, requested via ?simulate={simulate}.");

            case "404":
            case "notfound":
            case "not_found":
                throw new ResourceNotFoundException($"{resourceName} (simulated via ?simulate={simulate})");

            case "409":
            case "conflict":
                throw new DuplicatePrimaryKeyException(resourceName, $"simulated-{simulate}");

            case "500":
            case "servererror":
            case "server_error":
                throw new ServerSideException(
                    $"Simulated server-side failure on {resourceName}, requested via ?simulate={simulate}.",
                    new InvalidOperationException("Simulated downstream fault."));

            case "503":
            case "unavailable":
                throw new DependencyUnavailableException($"{resourceName}-store");
        }
    }

    /// <summary>
    /// Converts a failed <see cref="ControllerBase.ModelState"/> into a
    /// <see cref="ValidationFailedException"/> so a 400 flows through the same middleware, log
    /// fields, and JSON shape as every other error condition. Without this, ApiController's
    /// automatic 400 would short-circuit before the app's own logging ran.
    /// </summary>
    protected void ThrowIfInvalid(string entityName)
    {
        if (ModelState.IsValid)
        {
            return;
        }

        var errors = ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
                $"{entry.Key}: {(string.IsNullOrEmpty(error.ErrorMessage) ? "invalid value" : error.ErrorMessage)}"))
            .ToList();

        throw new ValidationFailedException(entityName, errors);
    }
}
