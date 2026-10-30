using System.ComponentModel.DataAnnotations;

namespace MVC_App.BlazorClient.Api;

/// <summary>
/// Client-side copy of <c>MVC_App/Models/Api/ApiDtos.cs</c>. The validation attributes are
/// repeated verbatim so an <c>EditForm</c> rejects a bad value in the browser with the same
/// message the API would return as a 400, which keeps the two in agreement without a round trip.
///
/// Deliberately a copy rather than a project reference: referencing MVC_App would pull EF Core and
/// Identity into this client, and these DTOs are the published API contract, which is exactly the
/// thing a client is supposed to hold its own copy of.
/// </summary>
public sealed class DepartmentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "DeptNo must be a positive number.")]
    public int DeptNo { get; set; }

    [Required(ErrorMessage = "DeptName is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "DeptName must be between 2 and 200 characters.")]
    public string DeptName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 40 characters.")]
    public string Location { get; set; } = string.Empty;

    public DepartmentDto Clone() => new()
    {
        DeptNo = DeptNo,
        DeptName = DeptName,
        Location = Location
    };
}

/// <summary>Client-side copy of the <c>/api/employees</c> contract.</summary>
public sealed class EmployeeDto
{
    [Range(1, int.MaxValue, ErrorMessage = "EmpNo must be a positive number.")]
    public int EmpNo { get; set; }

    [Required(ErrorMessage = "EmpName is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "EmpName must be between 2 and 100 characters.")]
    public string EmpName { get; set; } = string.Empty;

    [Range(1, 10_000_000, ErrorMessage = "Salary must be greater than zero.")]
    public int Salary { get; set; }

    [Required(ErrorMessage = "DeptName is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "DeptName must be between 2 and 100 characters.")]
    public string DeptName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Designation is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Designation must be between 2 and 100 characters.")]
    public string Designation { get; set; } = string.Empty;

    public EmployeeDto Clone() => new()
    {
        EmpNo = EmpNo,
        EmpName = EmpName,
        Salary = Salary,
        DeptName = DeptName,
        Designation = Designation
    };
}

/// <summary>
/// Client-side copy of <c>MVC_App/Diagnostics/ApiProblemDetails.cs</c>, the single JSON error
/// shape every <c>/api/...</c> endpoint returns. <c>TraceId</c> is the useful part: it matches the
/// <c>trace_id</c> on the server's log documents, so a failure shown in this UI can be pasted
/// straight into Kibana.
/// </summary>
public sealed class ApiProblemDetails
{
    public int Status { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public string? Instance { get; set; }

    public string? ErrorCategory { get; set; }

    public string? ErrorType { get; set; }

    public string? TraceId { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
}
