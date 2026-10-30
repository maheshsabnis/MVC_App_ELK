using System.ComponentModel.DataAnnotations;

namespace MVC_App.Models.Api;

/// <summary>
/// Request/response shape for <c>/api/departments</c>. Kept separate from the EF entity so the
/// API contract (and its validation rules, which drive the 400 condition) can change without
/// touching the database model.
/// </summary>
public sealed class DepartmentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "DeptNo must be a positive number.")]
    public int DeptNo { get; set; }

    [Required(ErrorMessage = "DeptName is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "DeptName must be between 2 and 200 characters.")]
    public string DeptName { get; set; } = null!;

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 40 characters.")]
    public string Location { get; set; } = null!;

    public static DepartmentDto FromEntity(Department department) => new()
    {
        DeptNo = department.DeptNo,
        DeptName = department.DeptName,
        Location = department.Location
    };

    public Department ToEntity() => new()
    {
        DeptNo = DeptNo,
        DeptName = DeptName,
        Location = Location
    };
}

/// <summary>Request/response shape for <c>/api/employees</c>.</summary>
public sealed class EmployeeDto
{
    [Range(1, int.MaxValue, ErrorMessage = "EmpNo must be a positive number.")]
    public int EmpNo { get; set; }

    [Required(ErrorMessage = "EmpName is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "EmpName must be between 2 and 100 characters.")]
    public string EmpName { get; set; } = null!;

    [Range(1, 10_000_000, ErrorMessage = "Salary must be greater than zero.")]
    public int Salary { get; set; }

    [Required(ErrorMessage = "DeptName is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "DeptName must be between 2 and 100 characters.")]
    public string DeptName { get; set; } = null!;

    [Required(ErrorMessage = "Designation is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Designation must be between 2 and 100 characters.")]
    public string Designation { get; set; } = null!;

    public static EmployeeDto FromEntity(Employee employee) => new()
    {
        EmpNo = employee.EmpNo,
        EmpName = employee.EmpName,
        Salary = employee.Salary,
        DeptName = employee.DeptName,
        Designation = employee.Designation
    };

    public Employee ToEntity() => new()
    {
        EmpNo = EmpNo,
        EmpName = EmpName,
        Salary = Salary,
        DeptName = DeptName,
        Designation = Designation
    };
}
