using System;
using System.Collections.Generic;

namespace MVC_App.Models;

public partial class Employee
{
    public int EmpNo { get; set; }

    public string EmpName { get; set; } = null!;

    public int Salary { get; set; }

    public string DeptName { get; set; } = null!;

    public string Designation { get; set; } = null!;
}
