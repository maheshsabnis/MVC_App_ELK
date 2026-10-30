namespace MVC_App.ApiClient;

/// <summary>Console formatting: one line per call, then a summary grouped by status code.</summary>
public static class ConsoleReport
{
    public static void WriteCallLine(ApiCallResult result, bool verbose)
    {
        var original = Console.ForegroundColor;

        Console.ForegroundColor = result.Matched ? ConsoleColor.Green : ConsoleColor.Red;
        Console.Write(result.Matched ? "  ok   " : "  FAIL ");
        Console.ForegroundColor = original;

        Console.Write($"{result.Method.Method,-6} {Truncate(result.Path, 46),-46} ");

        Console.ForegroundColor = ColourForStatus(result.ActualStatus);
        Console.Write($"{(result.TransportError is null ? result.ActualStatus.ToString() : "---"),-4}");
        Console.ForegroundColor = original;

        Console.Write($" {Truncate(result.Scenario, 42),-42} {result.Elapsed.TotalMilliseconds,6:F0} ms");

        if (result.ErrorCategory is { Length: > 0 } category && category != "none")
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  {category}");
            Console.ForegroundColor = original;
        }

        Console.WriteLine();

        if (result.TransportError is not null)
        {
            WriteDetail($"transport: {result.TransportError}", ConsoleColor.Red);
            return;
        }

        if (!result.Matched)
        {
            WriteDetail($"expected {result.ExpectedStatus}, got {result.ActualStatus}", ConsoleColor.Red);
        }

        if (result.TraceId is { Length: > 0 } traceId)
        {
            WriteDetail($"trace_id : \"{traceId}\"", ConsoleColor.DarkGray);
        }

        if (verbose && result.Body.Length > 0)
        {
            WriteDetail(Truncate(result.Body.ReplaceLineEndings(" "), 160), ConsoleColor.DarkGray);
        }
    }

    public static void WriteSummary(IReadOnlyList<ApiCallResult> results, ClientOptions options)
    {
        var original = Console.ForegroundColor;
        var matched = results.Count(r => r.Matched);
        var transportFailures = results.Count(r => r.TransportError is not null);

        Console.WriteLine();
        Console.WriteLine(new string('=', 92));
        Console.WriteLine("Summary");
        Console.WriteLine(new string('=', 92));

        Console.WriteLine();
        Console.WriteLine($"  Target          {options.BaseAddress}");
        Console.WriteLine($"  Calls           {results.Count}");

        Console.ForegroundColor = matched == results.Count ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"  Matched         {matched} of {results.Count}");
        Console.ForegroundColor = original;

        if (transportFailures > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Unreachable     {transportFailures} (the app did not answer)");
            Console.ForegroundColor = original;
        }

        Console.WriteLine();
        Console.WriteLine("  Responses by status code");
        foreach (var group in results
                     .Where(r => r.TransportError is null)
                     .GroupBy(r => r.ActualStatus)
                     .OrderBy(g => g.Key))
        {
            Console.Write("    ");
            Console.ForegroundColor = ColourForStatus(group.Key);
            Console.Write($"{group.Key,-5}");
            Console.ForegroundColor = original;
            Console.WriteLine($"{group.Count(),4} call(s)");
        }

        var categories = results
            .Where(r => r.ErrorCategory is { Length: > 0 } && r.ErrorCategory != "none")
            .GroupBy(r => r.ErrorCategory!)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (categories.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Error conditions reported by the app (error.category in Kibana)");
            foreach (var group in categories)
            {
                Console.WriteLine($"    {group.Key,-22}{group.Count(),4} call(s)");
            }
        }

        var mismatches = results.Where(r => !r.Matched).ToList();
        if (mismatches.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  Calls that did not return the expected status");
            Console.ForegroundColor = original;
            foreach (var mismatch in mismatches)
            {
                Console.WriteLine(
                    $"    {mismatch.Method.Method,-6} {Truncate(mismatch.Path, 44),-44} " +
                    $"expected {mismatch.ExpectedStatus}, got {(mismatch.TransportError is null ? mismatch.ActualStatus.ToString() : "no response")}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Next: open Kibana and run one of the queries from");
        Console.WriteLine("  docs/elk-dashboard-tutorial.html, e.g.");
        Console.WriteLine("    log_attributes.error.category : \"server_error\"");
        Console.WriteLine("    log_attributes.http.request.is_api : true and log_attributes.error.is_not_found : true");
        Console.WriteLine();
    }

    private static void WriteDetail(string text, ConsoleColor colour)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = colour;
        Console.WriteLine($"         {text}");
        Console.ForegroundColor = original;
    }

    private static ConsoleColor ColourForStatus(int statusCode) => statusCode switch
    {
        >= 500 => ConsoleColor.Red,
        401 or 403 => ConsoleColor.Magenta,
        404 => ConsoleColor.Yellow,
        >= 400 => ConsoleColor.Yellow,
        >= 200 and < 300 => ConsoleColor.Green,
        _ => ConsoleColor.Gray
    };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
