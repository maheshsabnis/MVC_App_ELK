namespace MVC_App.ApiClient;

/// <summary>One planned request. The plan is built in full before anything is sent.</summary>
public sealed record LoadOperation(
    HttpMethod Method,
    string Path,
    object? Body,
    int ExpectedStatus,
    string Label);

/// <summary>
/// A sequence of operations that must run in order against the same entities, and that no other
/// unit touches. Units are handed to workers whole, so the whole plan can run concurrently
/// without two workers ever racing on the same row.
/// </summary>
public sealed record LoadUnit(int Index, IReadOnlyList<LoadOperation> Operations);

/// <summary>
/// Turns the requested per-verb counts into a concrete list of requests.
///
/// The building block is a <em>pair</em>: one department and one employee taken through a full
/// lifecycle. A pair is 2 POST, 6 GET, 2 PUT and 2 DELETE, which is exactly the 1 : 3 : 1 : 1
/// ratio of the default 1000 / 3000 / 1000 / 1000 run, so 500 pairs produce those counts with
/// nothing left over. Because every pair creates what it reads and deletes what it created, a
/// completed run leaves the database exactly as it found it.
///
/// Counts that don't divide into whole pairs still come out exact: whatever is left over is
/// emitted as standalone operations after the pairs (see <see cref="AddLeftovers"/>).
/// </summary>
public static class LoadPlan
{
    private const int GetsPerPair = 6;
    private const int PostsPerPair = 2;
    private const int PutsPerPair = 2;
    private const int DeletesPerPair = 2;

    /// <summary>An id range of its own, clear of the 900000+ ids the scenario suite uses.</summary>
    private const int IdRangeStart = 500_000;

    public static IReadOnlyList<LoadUnit> Build(ClientOptions options, out int idBase)
    {
        // Offset each run inside the range so two runs started close together are unlikely to
        // collide on the unique DeptName index.
        idBase = IdRangeStart + Random.Shared.Next(0, 150) * 1_000;

        var gets = options.GetCount;
        var posts = options.PostCount;
        var puts = options.PutCount;
        var deletes = options.DeleteCount;

        // As many complete pairs as every budget can cover.
        var pairs = Math.Min(
            Math.Min(gets / GetsPerPair, posts / PostsPerPair),
            Math.Min(puts / PutsPerPair, deletes / DeletesPerPair));

        var units = new List<LoadUnit>(pairs + 16);

        for (var i = 0; i < pairs; i++)
        {
            units.Add(new LoadUnit(units.Count, BuildPair(idBase + i)));
        }

        gets -= pairs * GetsPerPair;
        posts -= pairs * PostsPerPair;
        puts -= pairs * PutsPerPair;
        deletes -= pairs * DeletesPerPair;

        AddLeftovers(units, idBase + pairs, gets, posts, puts, deletes);

        return units;
    }

    /// <summary>
    /// One department and one employee, created, read, updated, read again and removed. The
    /// employee is created after its department and deleted before it, so the department a
    /// request references always exists while it is referenced.
    /// </summary>
    private static List<LoadOperation> BuildPair(int id)
    {
        var deptName = $"LOAD-Dept-{id}";
        var encodedDeptName = Uri.EscapeDataString(deptName);

        var department = new { DeptNo = id, DeptName = deptName, Location = "Pune" };
        var departmentUpdated = new { DeptNo = id, DeptName = deptName, Location = "Mumbai" };

        var employee = new
        {
            EmpNo = id,
            EmpName = $"LOAD Employee {id}",
            Salary = 50_000,
            DeptName = deptName,
            Designation = "Engineer"
        };

        var employeeUpdated = new
        {
            EmpNo = id,
            EmpName = $"LOAD Employee {id}",
            Salary = 60_000,
            DeptName = deptName,
            Designation = "Senior Engineer"
        };

        return
        [
            // 2 POST
            new(HttpMethod.Post, "/api/departments", department, 201, "create department"),
            new(HttpMethod.Post, "/api/employees", employee, 201, "create employee"),

            // 3 GET
            new(HttpMethod.Get, $"/api/departments/{id}", null, 200, "read department"),
            new(HttpMethod.Get, $"/api/employees/{id}", null, 200, "read employee"),
            new(HttpMethod.Get, $"/api/employees?deptName={encodedDeptName}", null, 200, "list department employees"),

            // 2 PUT
            new(HttpMethod.Put, $"/api/departments/{id}", departmentUpdated, 200, "update department"),
            new(HttpMethod.Put, $"/api/employees/{id}", employeeUpdated, 200, "update employee"),

            // 3 GET
            new(HttpMethod.Get, $"/api/departments/by-name/{encodedDeptName}", null, 200, "read department by name"),
            new(HttpMethod.Get, $"/api/departments/{id}", null, 200, "re-read department"),
            new(HttpMethod.Get, $"/api/employees/{id}", null, 200, "re-read employee"),

            // 2 DELETE, employee first so its department outlives it
            new(HttpMethod.Delete, $"/api/employees/{id}", null, 204, "delete employee"),
            new(HttpMethod.Delete, $"/api/departments/{id}", null, 204, "delete department")
        ];
    }

    /// <summary>
    /// Emits whatever the whole pairs couldn't absorb, one operation per unit so they interleave
    /// freely. Surplus reads hit the collection endpoints; surplus writes target ids that were
    /// never created, which answer 404 and leave no rows behind.
    /// </summary>
    private static void AddLeftovers(
        List<LoadUnit> units, int idBase, int gets, int posts, int puts, int deletes)
    {
        for (var i = 0; i < gets; i++)
        {
            // Alternate the two collection endpoints so neither dominates the route breakdown.
            var path = i % 2 == 0 ? "/api/departments" : "/api/employees";
            units.Add(new LoadUnit(units.Count, [new(HttpMethod.Get, path, null, 200, "list collection")]));
        }

        // A surplus POST has no matching DELETE, so its row survives the run. Reported at the end.
        for (var i = 0; i < posts; i++)
        {
            var id = idBase + i;
            var body = new { DeptNo = id, DeptName = $"LOAD-Dept-{id}", Location = "Pune" };
            units.Add(new LoadUnit(units.Count,
                [new(HttpMethod.Post, "/api/departments", body, 201, "surplus create department")]));
        }

        for (var i = 0; i < puts; i++)
        {
            var id = 990_000 + i;
            var body = new { DeptNo = id, DeptName = $"LOAD-Missing-{id}", Location = "Nowhere" };
            units.Add(new LoadUnit(units.Count,
                [new(HttpMethod.Put, $"/api/departments/{id}", body, 404, "surplus update, no such row")]));
        }

        for (var i = 0; i < deletes; i++)
        {
            units.Add(new LoadUnit(units.Count,
                [new(HttpMethod.Delete, $"/api/departments/{990_000 + i}", null, 404, "surplus delete, no such row")]));
        }
    }

    /// <summary>Counts the plan by verb, so the run can state up front what it is about to send.</summary>
    public static Dictionary<string, int> CountByMethod(IReadOnlyList<LoadUnit> units)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var operation in units.SelectMany(u => u.Operations))
        {
            counts[operation.Method.Method] = counts.GetValueOrDefault(operation.Method.Method) + 1;
        }

        return counts;
    }
}
