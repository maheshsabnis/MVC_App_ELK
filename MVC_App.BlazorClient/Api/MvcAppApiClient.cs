using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MVC_App.BlazorClient.Api;

/// <summary>
/// Typed client for the MVC_App JSON API. Every method either returns the deserialized result or
/// throws <see cref="ApiException"/> carrying the server's own problem document, so the pages can
/// render a real error message and the error boundary can log it under the right category.
///
/// The API key is attached by the handler registered in <c>Program.cs</c>, never here, so it stays
/// a configuration concern. Because this is Blazor Server the key lives in server-side
/// configuration and is never sent to the browser.
/// </summary>
public sealed class MvcAppApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<MvcAppApiClient> _logger;

    public MvcAppApiClient(HttpClient http, ILogger<MvcAppApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Departments
    // -----------------------------------------------------------------------

    public Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(string? simulate = null, CancellationToken ct = default)
        => GetListAsync<DepartmentDto>(WithSimulate("api/departments", simulate), ct);

    public Task<DepartmentDto> GetDepartmentAsync(int deptNo, CancellationToken ct = default)
        => GetAsync<DepartmentDto>($"api/departments/{deptNo}", ct);

    public Task<DepartmentDto> CreateDepartmentAsync(DepartmentDto department, CancellationToken ct = default)
        => SendAsync<DepartmentDto>(HttpMethod.Post, "api/departments", department, ct);

    public Task<DepartmentDto> UpdateDepartmentAsync(DepartmentDto department, CancellationToken ct = default)
        => SendAsync<DepartmentDto>(HttpMethod.Put, $"api/departments/{department.DeptNo}", department, ct);

    public Task DeleteDepartmentAsync(int deptNo, CancellationToken ct = default)
        => SendAsync($"api/departments/{deptNo}", ct);

    // -----------------------------------------------------------------------
    // Employees
    // -----------------------------------------------------------------------

    public Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(
        string? deptName = null, string? simulate = null, CancellationToken ct = default)
    {
        var uri = "api/employees";
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(deptName))
        {
            query.Add($"deptName={Uri.EscapeDataString(deptName)}");
        }

        if (!string.IsNullOrWhiteSpace(simulate))
        {
            query.Add($"simulate={Uri.EscapeDataString(simulate)}");
        }

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return GetListAsync<EmployeeDto>(uri, ct);
    }

    public Task<EmployeeDto> GetEmployeeAsync(int empNo, CancellationToken ct = default)
        => GetAsync<EmployeeDto>($"api/employees/{empNo}", ct);

    public Task<EmployeeDto> CreateEmployeeAsync(EmployeeDto employee, CancellationToken ct = default)
        => SendAsync<EmployeeDto>(HttpMethod.Post, "api/employees", employee, ct);

    public Task<EmployeeDto> UpdateEmployeeAsync(EmployeeDto employee, CancellationToken ct = default)
        => SendAsync<EmployeeDto>(HttpMethod.Put, $"api/employees/{employee.EmpNo}", employee, ct);

    public Task DeleteEmployeeAsync(int empNo, CancellationToken ct = default)
        => SendAsync($"api/employees/{empNo}", ct);

    // -----------------------------------------------------------------------
    // Plumbing
    // -----------------------------------------------------------------------

    private static string WithSimulate(string uri, string? simulate)
        => string.IsNullOrWhiteSpace(simulate) ? uri : $"{uri}?simulate={Uri.EscapeDataString(simulate)}";

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct);
        await ThrowIfFailedAsync(response, uri, ct);

        return await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, ct) ?? [];
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct);
        await ThrowIfFailedAsync(response, uri, ct);

        return await ReadRequiredAsync<T>(response, uri, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        using var response = await _http.SendAsync(request, ct);
        await ThrowIfFailedAsync(response, uri, ct);

        return await ReadRequiredAsync<T>(response, uri, ct);
    }

    /// <summary>DELETE returns 204 with no body, so there is nothing to deserialize.</summary>
    private async Task SendAsync(string uri, CancellationToken ct)
    {
        using var response = await _http.DeleteAsync(uri, ct);
        await ThrowIfFailedAsync(response, uri, ct);
    }

    private async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, string uri, CancellationToken ct)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);

        if (value is null)
        {
            // A 2xx with an empty or unparseable body is still a broken contract, and silently
            // returning null here would surface later as a NullReferenceException with no context.
            throw new ApiException(
                (int)response.StatusCode,
                $"The API returned {(int)response.StatusCode} for {uri} but the body could not be read as {typeof(T).Name}.",
                problem: null,
                requestUri: uri);
        }

        return value;
    }

    /// <summary>
    /// Turns a failure status into an <see cref="ApiException"/>, reading the server's problem
    /// document for the real reason. The document is written by the MVC app's
    /// ExceptionHandlingMiddleware and carries the same error.category and trace id that its log
    /// entry does.
    /// </summary>
    private async Task ThrowIfFailedAsync(HttpResponseMessage response, string uri, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        ApiProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(JsonOptions, ct);
        }
        catch (Exception readFailure) when (readFailure is JsonException or NotSupportedException)
        {
            // Not every failure carries the JSON shape, e.g. a proxy error page or an HTML 502.
            // The status code alone is still enough to report and categorise the failure.
            _logger.LogDebug(readFailure,
                "Response body for the failed call to {Uri} was not an ApiProblemDetails document.", uri);
        }

        var message = problem?.Detail
            ?? problem?.Title
            ?? $"{response.RequestMessage?.Method.Method} {uri} failed with {statusCode} {response.ReasonPhrase}.";

        throw new ApiException(statusCode, message, problem, uri);
    }
}
