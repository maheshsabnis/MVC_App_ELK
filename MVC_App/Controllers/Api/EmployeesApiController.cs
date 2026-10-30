using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_App.Diagnostics;
using MVC_App.Exceptions;
using MVC_App.Models;
using MVC_App.Models.Api;

namespace MVC_App.Controllers.Api;

/// <summary>
/// CRUD over Employee as JSON, mirroring <see cref="DepartmentsApiController"/> so both resources
/// produce the same set of status codes and the same error-condition fields in ELK.
/// </summary>
[Route("api/employees")]
public sealed class EmployeesApiController : ApiControllerBase
{
    private const string ResourceName = "Employee";

    private readonly CompanyContext _context;
    private readonly ILogger<EmployeesApiController> _logger;

    public EmployeesApiController(CompanyContext context, ILogger<EmployeesApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/employees?deptName=Sales
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetAll(
        [FromQuery] string? deptName = null, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);

        var query = _context.Employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(deptName))
        {
            // Filtering by a department that doesn't exist is a not-found condition, not an
            // empty 200, so the dashboard can tell "no such department" from "no employees".
            if (!await _context.Departments.AnyAsync(d => d.DeptName == deptName))
            {
                throw new DeptNameNotFoundException(deptName);
            }

            query = query.Where(e => e.DeptName == deptName);
        }

        var employees = await query.OrderBy(e => e.EmpNo).ToListAsync();

        _logger.LogInformation("Returned {Count} employees for department filter {DeptName} [{ErrorCategory}]",
            employees.Count, deptName ?? "(none)", ErrorConditions.None);

        return Ok(employees.Select(EmployeeDto.FromEntity).ToList());
    }

    // GET: api/employees/5
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmployeeDto>> GetById(int id, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmpNo == id);
        if (employee is null)
        {
            throw new PrimaryKeyNotFoundException(ResourceName, id);
        }

        return Ok(EmployeeDto.FromEntity(employee));
    }

    // POST: api/employees
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] EmployeeDto employee, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);
        ThrowIfInvalid(ResourceName);

        if (await _context.Employees.AnyAsync(e => e.EmpNo == employee.EmpNo))
        {
            throw new DuplicatePrimaryKeyException(ResourceName, employee.EmpNo);
        }

        if (!await _context.Departments.AnyAsync(d => d.DeptName == employee.DeptName))
        {
            throw new DeptNameNotFoundException(employee.DeptName);
        }

        _context.Employees.Add(employee.ToEntity());
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created employee {EmpNo} ({EmpName})", employee.EmpNo, employee.EmpName);

        return CreatedAtAction(nameof(GetById), new { id = employee.EmpNo }, employee);
    }

    // PUT: api/employees/5
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmployeeDto>> Update(
        int id, [FromBody] EmployeeDto employee, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);
        ThrowIfInvalid(ResourceName);

        if (id != employee.EmpNo)
        {
            throw new ValidationFailedException(
                $"Route id '{id}' does not match EmpNo '{employee.EmpNo}' in the request body.");
        }

        var existing = await _context.Employees.FirstOrDefaultAsync(e => e.EmpNo == id);
        if (existing is null)
        {
            throw new PrimaryKeyNotFoundException(ResourceName, id);
        }

        if (!await _context.Departments.AnyAsync(d => d.DeptName == employee.DeptName))
        {
            throw new DeptNameNotFoundException(employee.DeptName);
        }

        existing.EmpName = employee.EmpName;
        existing.Salary = employee.Salary;
        existing.DeptName = employee.DeptName;
        existing.Designation = employee.Designation;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new ServerSideException($"Failed to update {ResourceName} '{id}'.", ex);
        }

        return Ok(EmployeeDto.FromEntity(existing));
    }

    // DELETE: api/employees/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmpNo == id);
        if (employee is null)
        {
            throw new PrimaryKeyNotFoundException(ResourceName, id);
        }

        _context.Employees.Remove(employee);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new ServerSideException($"Failed to delete {ResourceName} '{id}'.", ex);
        }

        _logger.LogInformation("Deleted employee {EmpNo}", id);
        return NoContent();
    }
}
