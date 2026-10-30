using System.Net;

namespace MVC_App.ApiClient;

/// <summary>Console output for a bulk load run: the plan up front, the results at the end.</summary>
public static class LoadReport
{
    public static void WritePlan(ClientOptions options, IReadOnlyList<LoadUnit> units, int idBase)
    {
        var planned = LoadPlan.CountByMethod(units);
        var total = planned.Values.Sum();

        Console.WriteLine();
        Console.WriteLine(new string('=', 92));
        Console.WriteLine("Load run");
        Console.WriteLine(new string('=', 92));
        Console.WriteLine();
        Console.WriteLine($"  Target          {options.BaseAddress}");
        Console.WriteLine($"  Concurrency     {options.Concurrency} workers");
        Console.WriteLine($"  Entity ids      {idBase}..{idBase + units.Count} (created and deleted by this run)");
        Console.WriteLine();
        Console.WriteLine($"  Planned requests");

        foreach (var method in new[] { "GET", "POST", "PUT", "DELETE" })
        {
            var requested = method switch
            {
                "GET" => options.GetCount,
                "POST" => options.PostCount,
                "PUT" => options.PutCount,
                _ => options.DeleteCount
            };

            var actual = planned.GetValueOrDefault(method);
            var note = actual == requested ? "" : $"   (requested {requested})";
            Console.WriteLine($"    {method,-8}{actual,8:N0}{note}");
        }

        Console.WriteLine($"    {"TOTAL",-8}{total,8:N0}");
        Console.WriteLine();

        if (options.ProgressEvery > 0)
        {
            Console.WriteLine($"  Progress every {options.ProgressEvery:N0} requests");
            Console.WriteLine();
        }
    }

    public static void WriteResult(LoadStats stats, LoadRunner runner, ClientOptions options, int idBase)
    {
        var original = Console.ForegroundColor;
        var seconds = runner.Elapsed.TotalSeconds;
        var sorted = stats.LatenciesMs.ToArray();
        Array.Sort(sorted);

        Console.WriteLine();
        Console.WriteLine(new string('=', 92));
        Console.WriteLine("Load summary");
        Console.WriteLine(new string('=', 92));
        Console.WriteLine();
        Console.WriteLine($"  Requests sent   {stats.Total,8:N0}");
        Console.WriteLine($"  Elapsed         {seconds,8:F1} s");
        Console.WriteLine($"  Throughput      {(seconds > 0 ? stats.Total / seconds : 0),8:F0} req/s");

        Console.ForegroundColor = stats.Unexpected == 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"  Unexpected      {stats.Unexpected,8:N0} (status other than the one planned)");
        Console.ForegroundColor = original;

        if (stats.TransportErrors > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Unreachable     {stats.TransportErrors,8:N0} (the app did not answer)");
            Console.ForegroundColor = original;
        }

        Console.WriteLine();
        Console.WriteLine("  Requests by method");
        foreach (var method in new[] { "GET", "POST", "PUT", "DELETE" })
        {
            if (stats.ByMethod.TryGetValue(method, out var count))
            {
                Console.WriteLine($"    {method,-8}{count,8:N0}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Responses by status code");
        foreach (var (status, count) in stats.ByStatus.OrderBy(s => s.Key))
        {
            Console.Write("    ");
            Console.ForegroundColor = ColourForStatus(status);
            Console.Write($"{status,-5}");
            Console.ForegroundColor = original;
            Console.WriteLine($"{count,8:N0}  {(HttpStatusCode)status}");
        }

        if (stats.ByErrorCategory.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Error conditions (error.category in Kibana)");
            foreach (var (category, count) in stats.ByErrorCategory.OrderByDescending(c => c.Value))
            {
                Console.WriteLine($"    {category,-22}{count,8:N0}");
            }
        }

        if (sorted.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Latency, milliseconds");
            Console.WriteLine($"    min      {sorted[0],8:F1}");
            Console.WriteLine($"    p50      {LoadStats.Percentile(sorted, 50),8:F1}");
            Console.WriteLine($"    p95      {LoadStats.Percentile(sorted, 95),8:F1}");
            Console.WriteLine($"    p99      {LoadStats.Percentile(sorted, 99),8:F1}");
            Console.WriteLine($"    max      {sorted[^1],8:F1}");
            Console.WriteLine($"    mean     {sorted.Average(),8:F1}");
        }

        if (runner.UnitsAbandoned > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  {runner.UnitsAbandoned} lifecycle(s) did not reach their DELETE, so some rows were left behind.");
            Console.WriteLine($"  They are in the id range {idBase}..{idBase + 200000}, named 'LOAD-Dept-*'.");
            Console.ForegroundColor = original;
        }
        else if (options.PostCount <= options.DeleteCount)
        {
            Console.WriteLine();
            Console.WriteLine("  Every row this run created was deleted again; the database is as it was.");
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  {options.PostCount - options.DeleteCount} surplus POST(s) had no matching DELETE, so those rows remain.");
            Console.ForegroundColor = original;
        }

        Console.WriteLine();
        Console.WriteLine("  In Kibana, this run is:");
        Console.WriteLine("    log_attributes.http.request.is_api : true and log_attributes.url.path : \"/api/*\"");
        Console.WriteLine("    log_attributes.http.request.method : (\"POST\" or \"PUT\" or \"DELETE\")");
        Console.WriteLine();
    }

    private static ConsoleColor ColourForStatus(int statusCode) => statusCode switch
    {
        >= 500 => ConsoleColor.Red,
        401 or 403 => ConsoleColor.Magenta,
        >= 400 => ConsoleColor.Yellow,
        >= 200 and < 300 => ConsoleColor.Green,
        _ => ConsoleColor.Gray
    };
}
