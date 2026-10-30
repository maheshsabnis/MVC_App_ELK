using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_App.Exceptions;
using MVC_App.Models;

namespace MVC_App.Controllers;

[Authorize]
public class DepartmentController : Controller
{
    private readonly CompanyContext _context;

    public DepartmentController(CompanyContext context)
    {
        _context = context;
    }

    // GET: Department
    public async Task<IActionResult> Index(int? pageNumber)
    {
        var departments = _context.Departments.OrderBy(d => d.DeptNo);
        int pageSize = 5;
        return View(await PaginatedList<Department>.CreateAsync(departments, pageNumber ?? 1, pageSize));
    }

    // GET: Department/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var department = await _context.Departments
            .FirstOrDefaultAsync(m => m.DeptNo == id);
        if (department == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Department), id);
        }

        // Checked on the way in as well as on the POST, so the confirmation page is never shown
        // for a department that cannot actually be deleted.
        await ThrowIfHasEmployeesAsync(department.DeptName);

        return View(department);
    }

    // GET: Department/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Department/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("DeptNo,DeptName,Location")] Department department)
    {
        if (ModelState.IsValid)
        {
            if (DepartmentExists(department.DeptNo))
            {
                throw new DuplicatePrimaryKeyException(nameof(Department), department.DeptNo);
            }

            if (await DepartmentNameExistsAsync(department.DeptName))
            {
                throw new DuplicateDeptNameException(department.DeptName);
            }

            _context.Add(department);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(department);
    }

    // GET: Department/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var department = await _context.Departments.FindAsync(id);
        if (department == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Department), id);
        }
        return View(department);
    }

    // POST: Department/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("DeptNo,DeptName,Location")] Department department)
    {
        if (id != department.DeptNo)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            if (await DepartmentNameExistsAsync(department.DeptName, excludeDeptNo: department.DeptNo))
            {
                throw new DuplicateDeptNameException(department.DeptName);
            }

            try
            {
                _context.Update(department);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DepartmentExists(department.DeptNo))
                {
                    throw new PrimaryKeyNotFoundException(nameof(Department), department.DeptNo);
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(department);
    }

    // GET: Department/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var department = await _context.Departments
            .FirstOrDefaultAsync(m => m.DeptNo == id);
        if (department == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Department), id);
        }

        // Checked on the way in as well as on the POST, so the confirmation page is never shown
        // for a department that cannot actually be deleted.
        await ThrowIfHasEmployeesAsync(department.DeptName);

        return View(department);
    }

    // POST: Department/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Department), id);
        }

        await ThrowIfHasEmployeesAsync(department.DeptName);

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Blocks deleting a department that still has employees. Employee.DeptName references
    /// Department.DeptName by convention only, with no foreign key in the database, so nothing
    /// below this layer stops the delete from orphaning them.
    ///
    /// This controller writes through the DbContext directly rather than calling the API, so the
    /// identical check in DepartmentsApiController.Delete does not cover it and both need it.
    /// </summary>
    private async Task ThrowIfHasEmployeesAsync(string deptName)
    {
        var employeeCount = await _context.Employees.CountAsync(e => e.DeptName == deptName);
        if (employeeCount > 0)
        {
            throw new DepartmentHasEmployeesException(deptName, employeeCount);
        }
    }

    private bool DepartmentExists(int id)
    {
        return _context.Departments.Any(e => e.DeptNo == id);
    }

    private Task<bool> DepartmentNameExistsAsync(string deptName, int? excludeDeptNo = null)
    {
        return _context.Departments
            .Where(d => d.DeptName == deptName && d.DeptNo != (excludeDeptNo ?? -1))
            .AnyAsync();
    }
}
