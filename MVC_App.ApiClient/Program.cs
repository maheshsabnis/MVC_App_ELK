using MVC_App.ApiClient;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (!ClientOptions.TryParse(args, out var options, out var error))
{
    if (!string.IsNullOrEmpty(error))
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
    }

    Console.WriteLine(ClientOptions.Usage);
    return string.IsNullOrEmpty(error) ? 0 : 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

Console.WriteLine(new string('=', 92));
Console.WriteLine("MVC_App API client");
Console.WriteLine(new string('=', 92));
Console.WriteLine($"  Base address   {options.BaseAddress}");
Console.WriteLine($"  API key        {Mask(options.ApiKey)} (sent as the X-Api-Key header)");

using var client = new MvcAppApiClient(options);

return options.Load
    ? await RunLoadAsync(client, options, cancellation.Token)
    : await RunScenariosAsync(client, options, cancellation.Token);

// ---------------------------------------------------------------------------

static async Task<int> RunScenariosAsync(
    MvcAppApiClient client, ClientOptions options, CancellationToken cancellationToken)
{
    Console.WriteLine($"  Iterations     {options.Iterations}");
    if (options.Delay > TimeSpan.Zero)
    {
        Console.WriteLine($"  Delay          {options.Delay.TotalMilliseconds:F0} ms between calls");
    }
    Console.WriteLine();
    Console.WriteLine("  Every call below is logged by the app with an error.category field, so the");
    Console.WriteLine("  same run is visible in Kibana under the mvcapp-logs-* data view.");

    var runner = new ScenarioRunner(client, options);

    try
    {
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            await runner.RunAsync(iteration, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine();
        Console.WriteLine("Cancelled. Reporting on the calls completed so far.");
    }

    ConsoleReport.WriteSummary(runner.Results, options);

    // 0 when every call returned the status it was supposed to, 1 otherwise, so this is usable as
    // a smoke test in a build pipeline and not only as a telemetry generator.
    return runner.Results.Count > 0 && runner.Results.All(r => r.Matched) ? 0 : 1;
}

static async Task<int> RunLoadAsync(
    MvcAppApiClient client, ClientOptions options, CancellationToken cancellationToken)
{
    var units = LoadPlan.Build(options, out var idBase);

    if (units.Count == 0)
    {
        Console.Error.WriteLine("Nothing to do: every request count is zero.");
        return 2;
    }

    LoadReport.WritePlan(options, units, idBase);

    var runner = new LoadRunner(client, options, units);
    var stats = await runner.RunAsync(cancellationToken);

    if (cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine();
        Console.WriteLine("Cancelled. Reporting on the requests completed so far.");
    }

    LoadReport.WriteResult(stats, runner, options, idBase);

    return stats.Unexpected == 0 && !cancellationToken.IsCancellationRequested ? 0 : 1;
}

static string Mask(string key)
    => key.Length <= 6 ? new string('*', key.Length) : $"{key[..3]}{new string('*', key.Length - 6)}{key[^3..]}";
