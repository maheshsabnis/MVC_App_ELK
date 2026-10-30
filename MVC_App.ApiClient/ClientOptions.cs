namespace MVC_App.ApiClient;

/// <summary>
/// Command-line and environment configuration for the console client. Every value has a default
/// that matches the <c>https</c> launch profile in <c>MVC_App/Properties/launchSettings.json</c>
/// and the <c>Api:Keys</c> entry in <c>appsettings.Development.json</c>, so the common case is
/// just <c>dotnet run</c> with no arguments.
/// </summary>
public sealed class ClientOptions
{
    public Uri BaseAddress { get; private set; } = new("https://localhost:7119");

    public string ApiKey { get; private set; } = "mvcapp-local-dev-key";

    /// <summary>Accept the local development certificate without validating it.</summary>
    public bool Insecure { get; private set; }

    /// <summary>How many times to run the whole scenario suite. Useful for filling a dashboard.</summary>
    public int Iterations { get; private set; } = 1;

    /// <summary>Pause between individual calls, so a time-series panel has visible spread.</summary>
    public TimeSpan Delay { get; private set; } = TimeSpan.Zero;

    /// <summary>Print the response body of every call, not just the failures.</summary>
    public bool Verbose { get; private set; }

    // -----------------------------------------------------------------------
    // Load mode: a high-volume run with an exact request count per HTTP verb,
    // instead of the 44-call correctness suite.
    // -----------------------------------------------------------------------

    /// <summary>Run the bulk load instead of the scenario suite.</summary>
    public bool Load { get; private set; }

    public int GetCount { get; private set; } = 3000;

    public int PostCount { get; private set; } = 1000;

    public int PutCount { get; private set; } = 1000;

    public int DeleteCount { get; private set; } = 1000;

    /// <summary>Requests in flight at once. Each worker owns a disjoint range of entity ids.</summary>
    public int Concurrency { get; private set; } = 8;

    /// <summary>Print a progress line every N completed requests. 0 disables progress output.</summary>
    public int ProgressEvery { get; private set; } = 250;

    public int TotalLoadRequests => GetCount + PostCount + PutCount + DeleteCount;

    public static bool TryParse(string[] args, out ClientOptions options, out string? error)
    {
        options = new ClientOptions();
        error = null;

        if (Environment.GetEnvironmentVariable("MVCAPP_API_BASEURL") is { Length: > 0 } envUrl &&
            Uri.TryCreate(envUrl, UriKind.Absolute, out var parsedEnvUrl))
        {
            options.BaseAddress = parsedEnvUrl;
        }

        if (Environment.GetEnvironmentVariable("MVCAPP_API_KEY") is { Length: > 0 } envKey)
        {
            options.ApiKey = envKey;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--url" or "-u":
                    if (!TryTakeValue(args, ref i, out var url) ||
                        !Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
                    {
                        error = $"'{arg}' needs an absolute URL, e.g. --url https://localhost:7119";
                        return false;
                    }
                    options.BaseAddress = parsedUrl;
                    break;

                case "--key" or "-k":
                    if (!TryTakeValue(args, ref i, out var key))
                    {
                        error = $"'{arg}' needs a value, e.g. --key mvcapp-local-dev-key";
                        return false;
                    }
                    options.ApiKey = key;
                    break;

                case "--iterations" or "-n":
                    if (!TryTakeValue(args, ref i, out var rawIterations) ||
                        !int.TryParse(rawIterations, out var iterations) || iterations < 1)
                    {
                        error = $"'{arg}' needs a positive whole number, e.g. --iterations 5";
                        return false;
                    }
                    options.Iterations = iterations;
                    break;

                case "--delay" or "-d":
                    if (!TryTakeValue(args, ref i, out var rawDelay) ||
                        !int.TryParse(rawDelay, out var delayMs) || delayMs < 0)
                    {
                        error = $"'{arg}' needs a millisecond count, e.g. --delay 250";
                        return false;
                    }
                    options.Delay = TimeSpan.FromMilliseconds(delayMs);
                    break;

                case "--load" or "-l":
                    options.Load = true;
                    break;

                case "--gets":
                case "--posts":
                case "--puts":
                case "--deletes":
                case "--concurrency" or "-c":
                case "--progress":
                {
                    if (!TryTakeValue(args, ref i, out var rawCount) ||
                        !int.TryParse(rawCount, out var count) || count < 0)
                    {
                        error = $"'{arg}' needs a whole number of 0 or more, e.g. {arg} 1000";
                        return false;
                    }

                    switch (arg)
                    {
                        case "--gets": options.GetCount = count; break;
                        case "--posts": options.PostCount = count; break;
                        case "--puts": options.PutCount = count; break;
                        case "--deletes": options.DeleteCount = count; break;
                        case "--progress": options.ProgressEvery = count; break;
                        default:
                            if (count < 1)
                            {
                                error = "'--concurrency' needs at least 1.";
                                return false;
                            }
                            options.Concurrency = count;
                            break;
                    }

                    // Any of these implies the bulk run; nobody passes --gets 3000 for a 44-call suite.
                    if (arg is not ("--concurrency" or "-c" or "--progress"))
                    {
                        options.Load = true;
                    }

                    break;
                }

                case "--insecure":
                    options.Insecure = true;
                    break;

                case "--verbose" or "-v":
                    options.Verbose = true;
                    break;

                case "--help" or "-h" or "-?":
                    error = string.Empty;
                    return false;

                default:
                    error = $"Unrecognised argument '{arg}'.";
                    return false;
            }
        }

        return true;
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    public const string Usage = """
        MVC_App.ApiClient - exercises the MVC_App JSON API and prints the status code each call returned.

        Two modes. By default it runs the 44-call correctness suite, which produces every status
        code once. With --load it runs a bulk load with an exact request count per HTTP verb.

        Usage:
          dotnet run --project MVC_App.ApiClient [options]

        Options:
          -u, --url <url>          Base address of the running app.   Default: https://localhost:7119
                                   Also read from MVCAPP_API_BASEURL.
          -k, --key <key>          API key sent as the X-Api-Key header.
                                   Default: mvcapp-local-dev-key. Also read from MVCAPP_API_KEY.
          -n, --iterations <n>     Run the whole suite n times.       Default: 1
          -d, --delay <ms>         Pause between calls, in ms.        Default: 0
              --insecure           Skip TLS validation (local dev certificate only).
          -v, --verbose            Print every response body, not just the unexpected ones.
          -h, --help               Show this help.

        Load mode:
          -l, --load               Run the bulk load instead of the scenario suite.
              --gets <n>           GET requests to issue.             Default: 3000
              --posts <n>          POST requests to issue.            Default: 1000
              --puts <n>           PUT requests to issue.             Default: 1000
              --deletes <n>        DELETE requests to issue.          Default: 1000
          -c, --concurrency <n>    Requests in flight at once.        Default: 8
              --progress <n>       Progress line every n requests.    Default: 250 (0 = off)

          Passing any of --gets/--posts/--puts/--deletes turns on load mode by itself.

        Examples:
          dotnet run --project MVC_App.ApiClient
          dotnet run --project MVC_App.ApiClient -- --url http://localhost:5121
          dotnet run --project MVC_App.ApiClient -- --iterations 20 --delay 300

          # 6000 requests: 3000 GET, 1000 POST, 1000 PUT, 1000 DELETE
          dotnet run --project MVC_App.ApiClient -- --url http://localhost:5121 --load

          # 5000 requests: 2500 GET, 1000 POST, 750 PUT, 750 DELETE
          dotnet run --project MVC_App.ApiClient -- --url http://localhost:5121 \
            --gets 2500 --posts 1000 --puts 750 --deletes 750 --concurrency 16
        """;
}
