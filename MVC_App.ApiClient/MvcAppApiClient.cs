using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MVC_App.ApiClient;

/// <summary>
/// Thin wrapper over <see cref="HttpClient"/> that never throws on a non-success status code,
/// because here the status code <em>is</em> the result. Every call returns an
/// <see cref="ApiCallResult"/> carrying the expected and the actual status side by side.
/// </summary>
public sealed class MvcAppApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ClientOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MvcAppApiClient(ClientOptions options)
    {
        _options = options;

        var handler = new HttpClientHandler
        {
            // The app calls UseHttpsRedirection, so an http base address answers 307 before the
            // request ever reaches a controller. Following redirects keeps `--url http://...`
            // usable; the API endpoints themselves never redirect, they answer 401 directly.
            AllowAutoRedirect = true,

            // The default per-server connection limit would serialise a concurrent load run
            // behind a handful of sockets and make the measured latencies meaningless.
            MaxConnectionsPerServer = Math.Max(options.Concurrency * 2, 16)
        };

        if (options.Insecure)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        _http = new HttpClient(handler)
        {
            BaseAddress = options.BaseAddress,
            Timeout = TimeSpan.FromSeconds(30)
        };

        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MVC_App.ApiClient/1.0");
    }

    /// <summary>
    /// Sends one request. <paramref name="apiKey"/> defaults to the configured key; pass an empty
    /// string to send no key at all (the 401 case) or a wrong value to test key rejection.
    /// </summary>
    public async Task<ApiCallResult> SendAsync(
        string scenario,
        HttpMethod method,
        string path,
        int expectedStatus,
        object? body = null,
        string? apiKey = null,
        bool retainBody = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(method, path);

        var key = apiKey ?? _options.ApiKey;
        if (!string.IsNullOrEmpty(key))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", key);
        }

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);

            // Always read the body to completion, even when it is being discarded: leaving it
            // unread would hold the connection open and skew the timings of every later call.
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var (category, type, traceId) = ApiCallResult.ReadProblemFields(responseBody);

            return new ApiCallResult(
                scenario,
                method,
                path,
                expectedStatus,
                (int)response.StatusCode,
                category,
                type,
                traceId,
                // A load run holds thousands of results; keeping every body would retain
                // megabytes of JSON nobody reads.
                retainBody ? responseBody : string.Empty,
                stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            return ApiCallResult.Transport(
                scenario, method, path, expectedStatus, Describe(ex), stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Turns the usual "can't reach the app" failures into a message that says what to do, rather
    /// than a nested exception chain.
    /// </summary>
    private string Describe(Exception ex)
    {
        var root = ex;
        while (root.InnerException is not null)
        {
            root = root.InnerException;
        }

        var message = $"{ex.GetType().Name}: {root.Message}";

        if (root.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
        {
            message += "  ->  run 'dotnet dev-certs https --trust', or re-run this client with --insecure.";
        }
        else if (root.Message.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
                 root.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase))
        {
            message += $"  ->  is MVC_App running on {_options.BaseAddress}?";
        }

        return message;
    }

    public void Dispose() => _http.Dispose();
}
