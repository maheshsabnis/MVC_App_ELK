using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MVC_App.Exceptions;
using MVC_App.Models;

namespace MVC_App.Controllers;

[Authorize]
public class EmployeeController : Controller
{
    private readonly CompanyContext _context;

    public EmployeeController(CompanyContext context)
    {
        _context = context;
    }

    // GET: Employee
    public async Task<IActionResult> Index(int? pageNumber)
    {
        var employees = _context.Employees.OrderBy(e => e.EmpNo);
        int pageSize = 5;
        return View(await PaginatedList<Employee>.CreateAsync(employees, pageNumber ?? 1, pageSize));
    }

    // GET: Employee/ListByDepartment?deptName=Sales
    public async Task<IActionResult> ListByDepartment(string deptName)
    {
        var departments = await _context.Departments
            .Select(d => d.DeptName)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        if (!string.IsNullOrEmpty(deptName) && !departments.Contains(deptName))
        {
            throw new DeptNameNotFoundException(deptName);
        }

        ViewBag.Departments = new SelectList(departments, deptName);
        ViewBag.SelectedDeptName = deptName;

        var employees = _context.Employees.AsQueryable();
        if (!string.IsNullOrEmpty(deptName))
        {
            employees = employees.Where(e => e.DeptName == deptName);
        }

        return View(await employees.OrderBy(e => e.EmpName).ToListAsync());
    }

    // GET: Employee/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(m => m.EmpNo == id);
        if (employee == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Employee), id);
        }

        return View(employee);
    }

    // GET: Employee/Create
    public async Task<IActionResult> Create()
    {
        await PopulateDepartmentsDropDownList();
        return View();
    }

    // POST: Employee/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmpNo,EmpName,Salary,DeptName,Designation")] Employee employee)
    {
        if (ModelState.IsValid)
        {
            if (EmployeeExists(employee.EmpNo))
            {
                throw new DuplicatePrimaryKeyException(nameof(Employee), employee.EmpNo);
            }

            _context.Add(employee);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateDepartmentsDropDownList(employee.DeptName);
        return View(employee);
    }

    private async Task PopulateDepartmentsDropDownList(object? selectedDept = null)
    {
        var departments = await _context.Departments
            .Select(d => d.DeptName)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
        ViewBag.Departments = new SelectList(departments, selectedDept);
    }

    // GET: Employee/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Employee), id);
        }
        return View(employee);
    }

    // POST: Employee/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("EmpNo,EmpName,Salary,DeptName,Designation")] Employee employee)
    {
        if (id != employee.EmpNo)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(employee);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(employee.EmpNo))
                {
                    throw new PrimaryKeyNotFoundException(nameof(Employee), employee.EmpNo);
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(employee);
    }

    // GET: Employee/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(m => m.EmpNo == id);
        if (employee == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Employee), id);
        }

        return View(employee);
    }

    // POST: Employee/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
        {
            throw new PrimaryKeyNotFoundException(nameof(Employee), id);
        }

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private bool EmployeeExists(int id)
    {
        return _context.Employees.Any(e => e.EmpNo == id);
    }
}
