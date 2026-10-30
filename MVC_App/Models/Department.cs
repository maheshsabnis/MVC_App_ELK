using System;
using System.Collections.Generic;

namespace MVC_App.Models;

public partial class Department
{
    public int DeptNo { get; set; }

    public string DeptName { get; set; } = null!;

    public string Location { get; set; } = null!;
}
