using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_App.Diagnostics;
using MVC_App.Exceptions;
using MVC_App.Models;
using MVC_App.Models.Api;

namespace MVC_App.Controllers.Api;

/// <summary>
/// CRUD over Department as JSON. Every action documents, and actually produces, the status codes
/// the ELK dashboard charts: 200, 201, 204, 400, 401, 404, 409, 500 and 503.
/// </summary>
[Route("api/departments")]
public sealed class DepartmentsApiController : ApiControllerBase
{
    private const string ResourceName = "Department";

    private readonly CompanyContext _context;
    private readonly ILogger<DepartmentsApiController> _logger;

    public DepartmentsApiController(CompanyContext context, ILogger<DepartmentsApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/departments
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll([FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);

        var departments = await _context.Departments
            .OrderBy(d => d.DeptNo)
            .ToListAsync();

        _logger.LogInformation("Returned {Count} departments [{ErrorCategory}]",
            departments.Count, ErrorConditions.None);

        return Ok(departments.Select(DepartmentDto.FromEntity).ToList());
    }

    // GET: api/departments/5
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DepartmentDto>> GetById(int id, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);

        var department = await _context.Departments.FirstOrDefaultAsync(d => d.DeptNo == id);
        if (department is null)
        {
            // 404: the not-found condition, logged with error.category "not_found".
            throw new PrimaryKeyNotFoundException(ResourceName, id);
        }

        return Ok(DepartmentDto.FromEntity(department));
    }

    // GET: api/departments/by-name/Sales
    [HttpGet("by-name/{deptName}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> GetByName(string deptName, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);

        var department = await _context.Departments.FirstOrDefaultAsync(d => d.DeptName == deptName);
        if (department is null)
        {
            throw new DeptNameNotFoundException(deptName);
        }

        return Ok(DepartmentDto.FromEntity(department));
    }

    // POST: api/departments
    [HttpPost]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DepartmentDto>> Create(
        [FromBody] DepartmentDto department, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);
        ThrowIfInvalid(ResourceName);

        if (await _context.Departments.AnyAsync(d => d.DeptNo == department.DeptNo))
        {
            throw new DuplicatePrimaryKeyException(ResourceName, department.DeptNo);
        }

        if (await _context.Departments.AnyAsync(d => d.DeptName == department.DeptName))
        {
            throw new DuplicateDeptNameException(department.DeptName);
        }

        _context.Departments.Add(department.ToEntity());
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created department {DeptNo} ({DeptName})",
            department.DeptNo, department.DeptName);

        // 201 with a Location header pointing at GetById.
        return CreatedAtAction(nameof(GetById), new { id = department.DeptNo }, department);
    }

    // PUT: api/departments/5
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DepartmentDto>> Update(
        int id, [FromBody] DepartmentDto department, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);
        ThrowIfInvalid(ResourceName);

        if (id != department.DeptNo)
        {
            throw new ValidationFailedException(
                $"Route id '{id}' does not match DeptNo '{department.DeptNo}' in the request body.");
        }

        var existing = await _context.Departments.FirstOrDefaultAsync(d => d.DeptNo == id);
        if (existing is null)
        {
            throw new PrimaryKeyNotFoundException(ResourceName, id);
        }

        if (await _context.Departments.AnyAsync(d => d.DeptName == department.DeptName && d.DeptNo != id))
        {
            throw new DuplicateDeptNameException(department.DeptName);
        }

        existing.DeptName = department.DeptName;
        existing.Location = department.Location;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Reported as 500 rather than letting a raw EF exception escape, so the response
            // shape and the error.category on the log entry stay consistent.
            throw new ServerSideException($"Failed to update {ResourceName} '{id}'.", ex);
        }

        return Ok(DepartmentDto.FromEntity(existing));
    }

    // DELETE: api/departments/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? simulate = null)
    {
        ApplySimulatedFault(simulate, ResourceName);

        var department = await _context.Departments.FirstOrDefaultAsync(d => d.DeptNo == id);
        if (department is null)
        {
            throw new PrimaryKeyNotFoundException(ResourceName, id);
        }

        // Employee.DeptName references Department.DeptName by convention only; there is no foreign
        // key in the database, so without this check the delete succeeds and silently leaves every
        // employee in the department pointing at a name that no longer exists.
        var employeeCount = await _context.Employees.CountAsync(e => e.DeptName == department.DeptName);
        if (employeeCount > 0)
        {
            throw new DepartmentHasEmployeesException(department.DeptName, employeeCount);
        }

        _context.Departments.Remove(department);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new ServerSideException($"Failed to delete {ResourceName} '{id}'.", ex);
        }

        _logger.LogInformation("Deleted department {DeptNo}", id);
        return NoContent();
    }
}
