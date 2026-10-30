using MVC_App.Diagnostics;

namespace MVC_App.Exceptions;

/// <summary>
/// Base type for domain/validation exceptions that carry the HTTP status code
/// they should be reported as, so a single middleware can log and render them consistently.
/// <see cref="ErrorCategory"/> is the slug ELK filters on (see <see cref="ErrorConditions"/>).
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public string ErrorCategory => ErrorConditions.ForStatusCode(StatusCode);
}

// ---------------------------------------------------------------------------
// 404 - not found
// ---------------------------------------------------------------------------

public sealed class PrimaryKeyNotFoundException : AppException
{
    public PrimaryKeyNotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.", StatusCodes.Status404NotFound)
    {
    }
}

public sealed class DeptNameNotFoundException : AppException
{
    public DeptNameNotFoundException(string deptName)
        : base($"Department name '{deptName}' was not found.", StatusCodes.Status404NotFound)
    {
    }
}

/// <summary>
/// A not-found condition that isn't tied to a primary key, e.g. an unknown sub-resource or a
/// route that resolved but has nothing behind it.
/// </summary>
public sealed class ResourceNotFoundException : AppException
{
    public ResourceNotFoundException(string resource)
        : base($"The requested resource '{resource}' was not found.", StatusCodes.Status404NotFound)
    {
    }
}

// ---------------------------------------------------------------------------
// 409 - conflict
// ---------------------------------------------------------------------------

public sealed class DuplicatePrimaryKeyException : AppException
{
    public DuplicatePrimaryKeyException(string entityName, object key)
        : base($"{entityName} with key '{key}' already exists.", StatusCodes.Status409Conflict)
    {
    }
}

public sealed class DuplicateDeptNameException : AppException
{
    public DuplicateDeptNameException(string deptName)
        : base($"Department name '{deptName}' already exists. Department names must be unique.", StatusCodes.Status409Conflict)
    {
    }
}

/// <summary>
/// A department still has employees assigned to it, so deleting it would leave them pointing at a
/// department that no longer exists.
///
/// The database has no foreign key between Employee.DeptName and Department.DeptName, so nothing
/// below this layer prevents the orphaning. The rule is enforced here, and reported as 409 rather
/// than 400 because the request is well formed; it conflicts with data that already exists.
/// </summary>
public sealed class DepartmentHasEmployeesException : AppException
{
    public DepartmentHasEmployeesException(string deptName, int employeeCount)
        : base($"Department '{deptName}' still has {employeeCount} employee(s) assigned to it and cannot be deleted. " +
               "Reassign or remove them first.", StatusCodes.Status409Conflict)
    {
        DeptName = deptName;
        EmployeeCount = employeeCount;
    }

    public string DeptName { get; }

    public int EmployeeCount { get; }
}

// ---------------------------------------------------------------------------
// 400 - validation
// ---------------------------------------------------------------------------

/// <summary>Model binding or business-rule validation failed on an otherwise well-formed request.</summary>
public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(string message)
        : base(message, StatusCodes.Status400BadRequest)
    {
    }

    public ValidationFailedException(string entityName, IEnumerable<string> errors)
        : base($"{entityName} failed validation: {string.Join("; ", errors)}", StatusCodes.Status400BadRequest)
    {
    }
}

// ---------------------------------------------------------------------------
// 401 / 403 - authentication and authorization
// ---------------------------------------------------------------------------

/// <summary>
/// The caller presented no credentials, or credentials that could not be verified. Reported as
/// 401 and logged with <c>error.category : "unauthorized"</c>.
/// </summary>
public sealed class UnauthorizedRequestException : AppException
{
    public UnauthorizedRequestException(string reason)
        : base($"Unauthorized: {reason}", StatusCodes.Status401Unauthorized)
    {
    }

    public static UnauthorizedRequestException MissingApiKey(string headerName)
        => new($"the '{headerName}' header was not supplied.");

    public static UnauthorizedRequestException InvalidApiKey(string headerName)
        => new($"the '{headerName}' header did not match a known API key.");
}

/// <summary>
/// The caller is authenticated but is not permitted to perform this operation. Reported as 403
/// and logged with <c>error.category : "forbidden"</c>.
/// </summary>
public sealed class ForbiddenRequestException : AppException
{
    public ForbiddenRequestException(string reason)
        : base($"Forbidden: {reason}", StatusCodes.Status403Forbidden)
    {
    }
}

// ---------------------------------------------------------------------------
// 5xx - server-side failures
// ---------------------------------------------------------------------------

/// <summary>
/// An operation failed for a reason that is the server's fault, not the caller's. Reported as
/// 500 and logged with <c>error.category : "server_error"</c>. Prefer this over a bare
/// <see cref="Exception"/> so the failure carries its intended status code through the pipeline.
/// </summary>
public sealed class ServerSideException : AppException
{
    public ServerSideException(string message, Exception? innerException = null)
        : base(message, StatusCodes.Status500InternalServerError, innerException)
    {
    }
}

/// <summary>
/// A dependency the app needs (database, downstream service) could not be reached. Reported as
/// 503 and logged with <c>error.category : "service_unavailable"</c>.
/// </summary>
public sealed class DependencyUnavailableException : AppException
{
    public DependencyUnavailableException(string dependencyName, Exception? innerException = null)
        : base($"The dependency '{dependencyName}' is currently unavailable.",
            StatusCodes.Status503ServiceUnavailable, innerException)
    {
    }
}
