namespace MVC_App.ApiClient;

/// <summary>
/// Walks the whole API surface once, deliberately producing every status code the ELK dashboard
/// charts: 200, 201, 204, 400, 401, 403, 404, 405, 409, 500 and 503.
///
/// The happy-path calls use a department and an employee the run creates for itself (ids in the
/// 900000+ range, well clear of real data) and deletes again at the end, so the suite can be run
/// repeatedly against the same database.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly MvcAppApiClient _client;
    private readonly ClientOptions _options;
    private readonly List<ApiCallResult> _results = [];

    public ScenarioRunner(MvcAppApiClient client, ClientOptions options)
    {
        _client = client;
        _options = options;
    }

    public IReadOnlyList<ApiCallResult> Results => _results;

    public async Task RunAsync(int iteration, CancellationToken cancellationToken)
    {
        // Unique per run so a repeat pass doesn't collide with leftovers from a previous one.
        var deptNo = 900_000 + Random.Shared.Next(1, 90_000);
        var empNo = 900_000 + Random.Shared.Next(1, 90_000);
        var deptName = $"QA-Dept-{deptNo}";

        Console.WriteLine();
        Console.WriteLine($"--- Iteration {iteration}: department {deptNo} '{deptName}', employee {empNo} ---");

        await ConnectivityAndAuthAsync(cancellationToken);
        await DepartmentCrudAsync(deptNo, deptName, cancellationToken);
        await EmployeeCrudAsync(empNo, deptName, cancellationToken);
        await NotFoundConditionsAsync(cancellationToken);
        await ServerErrorConditionsAsync(cancellationToken);
        await SimulatedFaultsAsync(cancellationToken);
        await CleanupAsync(empNo, deptNo, cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Connectivity, 200 and the 401 condition
    // -----------------------------------------------------------------------

    private async Task ConnectivityAndAuthAsync(CancellationToken cancellationToken)
    {
        await RecordAsync("Liveness ping (anonymous)",
            HttpMethod.Get, "/api/diagnostics/ping", 200, cancellationToken: cancellationToken);

        await RecordAsync("401: no API key supplied",
            HttpMethod.Get, "/api/departments", 401, apiKey: string.Empty, cancellationToken: cancellationToken);

        await RecordAsync("401: wrong API key supplied",
            HttpMethod.Get, "/api/departments", 401, apiKey: "not-a-real-key", cancellationToken: cancellationToken);

        await RecordAsync("401: raised inside the endpoint",
            HttpMethod.Get, "/api/diagnostics/unauthorized", 401, cancellationToken: cancellationToken);

        await RecordAsync("403: authenticated but not permitted",
            HttpMethod.Get, "/api/diagnostics/forbidden", 403, cancellationToken: cancellationToken);

        await RecordAsync("200: API key accepted",
            HttpMethod.Get, "/api/diagnostics/whoami", 200, cancellationToken: cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Department: GET / POST / PUT / DELETE
    // -----------------------------------------------------------------------

    private async Task DepartmentCrudAsync(int deptNo, string deptName, CancellationToken cancellationToken)
    {
        var department = new { DeptNo = deptNo, DeptName = deptName, Location = "Pune" };

        await RecordAsync("200: list all departments",
            HttpMethod.Get, "/api/departments", 200, cancellationToken: cancellationToken);

        await RecordAsync("201: create a department",
            HttpMethod.Post, "/api/departments", 201, body: department, cancellationToken: cancellationToken);

        await RecordAsync("200: read the department back",
            HttpMethod.Get, $"/api/departments/{deptNo}", 200, cancellationToken: cancellationToken);

        await RecordAsync("200: read the department by name",
            HttpMethod.Get, $"/api/departments/by-name/{Uri.EscapeDataString(deptName)}", 200,
            cancellationToken: cancellationToken);

        await RecordAsync("409: create the same department twice",
            HttpMethod.Post, "/api/departments", 409, body: department, cancellationToken: cancellationToken);

        await RecordAsync("400: create with an empty DeptName",
            HttpMethod.Post, "/api/departments", 400,
            body: new { DeptNo = deptNo + 1, DeptName = "", Location = "Pune" },
            cancellationToken: cancellationToken);

        await RecordAsync("200: update the department",
            HttpMethod.Put, $"/api/departments/{deptNo}", 200,
            body: new { DeptNo = deptNo, DeptName = deptName, Location = "Mumbai" },
            cancellationToken: cancellationToken);

        await RecordAsync("400: route id does not match the body",
            HttpMethod.Put, $"/api/departments/{deptNo}", 400,
            body: new { DeptNo = deptNo + 7, DeptName = deptName, Location = "Mumbai" },
            cancellationToken: cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Employee: GET / POST / PUT / DELETE
    // -----------------------------------------------------------------------

    private async Task EmployeeCrudAsync(int empNo, string deptName, CancellationToken cancellationToken)
    {
        var employee = new
        {
            EmpNo = empNo,
            EmpName = $"QA Employee {empNo}",
            Salary = 55_000,
            DeptName = deptName,
            Designation = "Engineer"
        };

        await RecordAsync("200: list all employees",
            HttpMethod.Get, "/api/employees", 200, cancellationToken: cancellationToken);

        await RecordAsync("201: create an employee",
            HttpMethod.Post, "/api/employees", 201, body: employee, cancellationToken: cancellationToken);

        await RecordAsync("200: read the employee back",
            HttpMethod.Get, $"/api/employees/{empNo}", 200, cancellationToken: cancellationToken);

        await RecordAsync("200: list employees in that department",
            HttpMethod.Get, $"/api/employees?deptName={Uri.EscapeDataString(deptName)}", 200,
            cancellationToken: cancellationToken);

        await RecordAsync("409: create the same employee twice",
            HttpMethod.Post, "/api/employees", 409, body: employee, cancellationToken: cancellationToken);

        await RecordAsync("400: create with a zero salary",
            HttpMethod.Post, "/api/employees", 400,
            body: new
            {
                EmpNo = empNo + 1,
                EmpName = "QA",
                Salary = 0,
                DeptName = deptName,
                Designation = "Engineer"
            },
            cancellationToken: cancellationToken);

        await RecordAsync("200: update the employee",
            HttpMethod.Put, $"/api/employees/{empNo}", 200,
            body: employee,
            cancellationToken: cancellationToken);
    }

    // -----------------------------------------------------------------------
    // The not-found condition, from five different directions
    // -----------------------------------------------------------------------

    private async Task NotFoundConditionsAsync(CancellationToken cancellationToken)
    {
        await RecordAsync("404: department id that does not exist",
            HttpMethod.Get, "/api/departments/987654", 404, cancellationToken: cancellationToken);

        await RecordAsync("404: department name that does not exist",
            HttpMethod.Get, "/api/departments/by-name/NoSuchDepartment", 404, cancellationToken: cancellationToken);

        await RecordAsync("404: employee id that does not exist",
            HttpMethod.Get, "/api/employees/987654", 404, cancellationToken: cancellationToken);

        await RecordAsync("404: filter by a department that does not exist",
            HttpMethod.Get, "/api/employees?deptName=NoSuchDepartment", 404, cancellationToken: cancellationToken);

        await RecordAsync("404: update a department that does not exist",
            HttpMethod.Put, "/api/departments/987654", 404,
            body: new { DeptNo = 987654, DeptName = "Ghost", Location = "Nowhere" },
            cancellationToken: cancellationToken);

        await RecordAsync("404: delete an employee that does not exist",
            HttpMethod.Delete, "/api/employees/987654", 404, cancellationToken: cancellationToken);

        await RecordAsync("404: raised inside the endpoint",
            HttpMethod.Get, "/api/diagnostics/not-found", 404, cancellationToken: cancellationToken);

        await RecordAsync("404: route that matches no endpoint at all",
            HttpMethod.Get, "/api/no-such-resource", 404, cancellationToken: cancellationToken);

        await RecordAsync("405: correct route, wrong verb",
            HttpMethod.Delete, "/api/departments", 405, cancellationToken: cancellationToken);
    }

    // -----------------------------------------------------------------------
    // The 500 condition, both classified and unanticipated
    // -----------------------------------------------------------------------

    private async Task ServerErrorConditionsAsync(CancellationToken cancellationToken)
    {
        await RecordAsync("500: classified server-side failure",
            HttpMethod.Get, "/api/diagnostics/server-error", 500, cancellationToken: cancellationToken);

        await RecordAsync("500: unanticipated exception (DivideByZero)",
            HttpMethod.Get, "/api/diagnostics/unhandled", 500, cancellationToken: cancellationToken);

        await RecordAsync("503: dependency unavailable",
            HttpMethod.Get, "/api/diagnostics/unavailable", 503, cancellationToken: cancellationToken);

        await RecordAsync("400: validation failure raised directly",
            HttpMethod.Get, "/api/diagnostics/bad-request", 400, cancellationToken: cancellationToken);

        await RecordAsync("409: conflict raised directly",
            HttpMethod.Get, "/api/diagnostics/conflict", 409, cancellationToken: cancellationToken);
    }

    // -----------------------------------------------------------------------
    // The same conditions, raised on the real resource routes via ?simulate=
    // -----------------------------------------------------------------------

    private async Task SimulatedFaultsAsync(CancellationToken cancellationToken)
    {
        await RecordAsync("401 on a real route (?simulate=401)",
            HttpMethod.Get, "/api/departments?simulate=401", 401, cancellationToken: cancellationToken);

        await RecordAsync("404 on a real route (?simulate=404)",
            HttpMethod.Get, "/api/employees?simulate=404", 404, cancellationToken: cancellationToken);

        await RecordAsync("500 on a real route (?simulate=500)",
            HttpMethod.Get, "/api/departments?simulate=500", 500, cancellationToken: cancellationToken);

        await RecordAsync("500 on a write route (?simulate=500)",
            HttpMethod.Post, "/api/employees?simulate=500", 500,
            body: new { EmpNo = 1, EmpName = "Ignored", Salary = 1, DeptName = "Ignored", Designation = "Ignored" },
            cancellationToken: cancellationToken);

        await RecordAsync("404 on a delete route (?simulate=404)",
            HttpMethod.Delete, "/api/departments/1?simulate=404", 404, cancellationToken: cancellationToken);
    }

    // -----------------------------------------------------------------------
    // 204, and leaving the database as we found it
    // -----------------------------------------------------------------------

    private async Task CleanupAsync(int empNo, int deptNo, CancellationToken cancellationToken)
    {
        await RecordAsync("204: delete the employee",
            HttpMethod.Delete, $"/api/employees/{empNo}", 204, cancellationToken: cancellationToken);

        await RecordAsync("404: delete the same employee again",
            HttpMethod.Delete, $"/api/employees/{empNo}", 404, cancellationToken: cancellationToken);

        await RecordAsync("204: delete the department",
            HttpMethod.Delete, $"/api/departments/{deptNo}", 204, cancellationToken: cancellationToken);

        await RecordAsync("404: delete the same department again",
            HttpMethod.Delete, $"/api/departments/{deptNo}", 404, cancellationToken: cancellationToken);
    }

    // -----------------------------------------------------------------------

    private async Task RecordAsync(
        string scenario,
        HttpMethod method,
        string path,
        int expectedStatus,
        object? body = null,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.SendAsync(
            scenario, method, path, expectedStatus, body, apiKey, cancellationToken: cancellationToken);
        _results.Add(result);
        ConsoleReport.WriteCallLine(result, _options.Verbose);

        if (_options.Delay > TimeSpan.Zero)
        {
            await Task.Delay(_options.Delay, cancellationToken);
        }
    }
}
