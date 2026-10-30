namespace MVC_App.BlazorClient.Diagnostics;

/// <summary>
/// The canonical error-condition slugs stamped onto every log entry as <c>error.category</c>.
///
/// This is a deliberate copy of <c>MVC_App/Diagnostics/ErrorConditions.cs</c> rather than a project
/// reference. Referencing the MVC project would drag EF Core, Identity and the whole data layer
/// into this client for the sake of nine string constants. The values must stay identical to the
/// MVC app's, because both write into the same <c>mvcapp-logs-*</c> index and the saved Kibana
/// queries filter on these exact slugs.
/// </summary>
public static class ErrorConditions
{
    /// <summary>2xx/3xx: the request completed as intended.</summary>
    public const string None = "none";

    /// <summary>400: the request itself was malformed or failed model validation.</summary>
    public const string Validation = "validation_error";

    /// <summary>401: the caller is unauthenticated (missing/invalid credentials or API key).</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>403: the caller is authenticated but not allowed to perform this operation.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>404: the requested route, or the entity behind it, does not exist.</summary>
    public const string NotFound = "not_found";

    /// <summary>409: the write conflicts with data that already exists.</summary>
    public const string Conflict = "conflict";

    /// <summary>Any other 4xx that isn't one of the named conditions above.</summary>
    public const string ClientError = "client_error";

    /// <summary>500: an unhandled or deliberately raised failure inside the app.</summary>
    public const string ServerError = "server_error";

    /// <summary>502/503/504: a dependency the app relies on was unavailable.</summary>
    public const string Unavailable = "service_unavailable";

    /// <summary>Maps an HTTP status code onto the condition slug it should be reported as.</summary>
    public static string ForStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => Validation,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound or StatusCodes.Status410Gone => NotFound,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status502BadGateway
            or StatusCodes.Status503ServiceUnavailable
            or StatusCodes.Status504GatewayTimeout => Unavailable,
        >= 500 => ServerError,
        >= 400 => ClientError,
        _ => None
    };

    /// <summary>
    /// True for the status codes that represent a failed request. 3xx redirects and 2xx
    /// successes are not failures, so <c>event.outcome</c> stays "success" for them.
    /// </summary>
    public static bool IsFailure(int statusCode) => statusCode >= 400;
}
