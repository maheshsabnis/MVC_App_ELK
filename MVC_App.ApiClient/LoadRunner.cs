using System.Diagnostics;

namespace MVC_App.ApiClient;

/// <summary>
/// Executes a <see cref="LoadPlan"/> across a fixed number of workers. Workers pull whole units
/// from a shared cursor, so each unit's operations stay in order and no two workers ever touch
/// the same entity. Nothing is retried: a load run is meant to report what the API did, not to
/// paper over it.
/// </summary>
public sealed class LoadRunner
{
    private readonly MvcAppApiClient _client;
    private readonly ClientOptions _options;
    private readonly IReadOnlyList<LoadUnit> _units;
    private readonly Lock _consoleLock = new();

    private int _cursor = -1;
    private int _completed;
    private int _unitsStarted;
    private int _unitsFinished;

    public LoadRunner(MvcAppApiClient client, ClientOptions options, IReadOnlyList<LoadUnit> units)
    {
        _client = client;
        _options = options;
        _units = units;
    }

    /// <summary>Units begun but not carried through to their DELETE, i.e. rows possibly left behind.</summary>
    public int UnitsAbandoned => _unitsStarted - _unitsFinished;

    public async Task<LoadStats> RunAsync(CancellationToken cancellationToken)
    {
        var total = _units.Sum(u => u.Operations.Count);
        var stopwatch = Stopwatch.StartNew();

        var workers = new Task<LoadStats>[Math.Min(_options.Concurrency, Math.Max(_units.Count, 1))];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(() => WorkerAsync(total, stopwatch, cancellationToken), cancellationToken);
        }

        LoadStats[] results;
        try
        {
            results = await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            // Collect whatever the workers that did finish managed to record.
            results = workers
                .Where(w => w.IsCompletedSuccessfully)
                .Select(w => w.Result)
                .ToArray();
        }

        stopwatch.Stop();
        Elapsed = stopwatch.Elapsed;

        return LoadStats.Merge(results);
    }

    public TimeSpan Elapsed { get; private set; }

    private async Task<LoadStats> WorkerAsync(int total, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var stats = new LoadStats();

        while (!cancellationToken.IsCancellationRequested)
        {
            var index = Interlocked.Increment(ref _cursor);
            if (index >= _units.Count)
            {
                break;
            }

            var unit = _units[index];
            Interlocked.Increment(ref _unitsStarted);

            foreach (var operation in unit.Operations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return stats;
                }

                var result = await _client.SendAsync(
                    operation.Label,
                    operation.Method,
                    operation.Path,
                    operation.ExpectedStatus,
                    operation.Body,
                    apiKey: null,
                    retainBody: false,
                    cancellationToken: cancellationToken);

                stats.Record(result);
                ReportProgress(total, stopwatch);

                if (_options.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(_options.Delay, cancellationToken);
                }
            }

            Interlocked.Increment(ref _unitsFinished);
        }

        return stats;
    }

    private void ReportProgress(int total, Stopwatch stopwatch)
    {
        var completed = Interlocked.Increment(ref _completed);

        if (_options.ProgressEvery <= 0 || (completed % _options.ProgressEvery != 0 && completed != total))
        {
            return;
        }

        var seconds = stopwatch.Elapsed.TotalSeconds;
        var rate = seconds > 0 ? completed / seconds : 0;
        var percent = total > 0 ? completed * 100d / total : 0;

        lock (_consoleLock)
        {
            Console.WriteLine(
                $"  {completed,7:N0} / {total,-7:N0} {percent,5:F1}%   {rate,7:F0} req/s   elapsed {seconds,6:F1}s");
        }
    }
}
