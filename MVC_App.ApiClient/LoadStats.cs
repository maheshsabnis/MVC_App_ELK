namespace MVC_App.ApiClient;

/// <summary>
/// Per-worker tallies, merged once at the end. Keeping a separate instance per worker avoids
/// locking on the hot path; the only shared state during the run is the completed counter in
/// <see cref="LoadRunner"/>.
/// </summary>
public sealed class LoadStats
{
    private readonly List<double> _latenciesMs = [];

    public Dictionary<string, int> ByMethod { get; } = new(StringComparer.Ordinal);

    public Dictionary<int, int> ByStatus { get; } = [];

    public Dictionary<string, int> ByErrorCategory { get; } = new(StringComparer.Ordinal);

    public int Total { get; private set; }

    public int Unexpected { get; private set; }

    public int TransportErrors { get; private set; }

    public IReadOnlyList<double> LatenciesMs => _latenciesMs;

    public void Record(ApiCallResult result)
    {
        Total++;
        Increment(ByMethod, result.Method.Method);

        if (result.TransportError is not null)
        {
            TransportErrors++;
            Unexpected++;
            return;
        }

        _latenciesMs.Add(result.Elapsed.TotalMilliseconds);
        ByStatus[result.ActualStatus] = ByStatus.GetValueOrDefault(result.ActualStatus) + 1;

        if (result.ErrorCategory is { Length: > 0 } category && category != "none")
        {
            Increment(ByErrorCategory, category);
        }

        if (!result.Matched)
        {
            Unexpected++;
        }
    }

    public static LoadStats Merge(IEnumerable<LoadStats> parts)
    {
        var merged = new LoadStats();

        foreach (var part in parts)
        {
            merged.Total += part.Total;
            merged.Unexpected += part.Unexpected;
            merged.TransportErrors += part.TransportErrors;
            merged._latenciesMs.AddRange(part._latenciesMs);

            foreach (var (key, value) in part.ByMethod)
            {
                merged.ByMethod[key] = merged.ByMethod.GetValueOrDefault(key) + value;
            }

            foreach (var (key, value) in part.ByStatus)
            {
                merged.ByStatus[key] = merged.ByStatus.GetValueOrDefault(key) + value;
            }

            foreach (var (key, value) in part.ByErrorCategory)
            {
                merged.ByErrorCategory[key] = merged.ByErrorCategory.GetValueOrDefault(key) + value;
            }
        }

        return merged;
    }

    /// <summary>
    /// Nearest-rank percentile over the recorded latencies. The caller sorts once and passes the
    /// sorted array in, so a report asking for p50/p95/p99 doesn't sort three times.
    /// </summary>
    public static double Percentile(double[] sortedLatencies, double percentile)
    {
        if (sortedLatencies.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile / 100d * sortedLatencies.Length);
        return sortedLatencies[Math.Clamp(rank - 1, 0, sortedLatencies.Length - 1)];
    }

    private static void Increment(Dictionary<string, int> counter, string key)
        => counter[key] = counter.GetValueOrDefault(key) + 1;
}
