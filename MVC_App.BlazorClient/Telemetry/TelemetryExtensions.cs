using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MVC_App.BlazorClient.Telemetry;

/// <summary>
/// Wires this app into the same observability stack as MVC_App: OTLP over HTTP to the
/// OpenTelemetry Collector in compose.yaml, which hands logs to Logstash and traces/metrics
/// straight to Elasticsearch.
///
/// Logs therefore land in the same <c>mvcapp-logs-*</c> indices as the MVC app's, because the
/// Logstash pipeline indexes everything it reads from the Collector's log file under that name
/// regardless of which service produced it. <c>service.name</c> is the field that tells them
/// apart, so this app reports itself as MVC_App.BlazorClient rather than borrowing MVC_App's name.
/// </summary>
public static class TelemetryExtensions
{
    public static WebApplicationBuilder AddApplicationTelemetry(this WebApplicationBuilder builder)
    {
        var endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!builder.Configuration.GetValue("Telemetry:Enabled", true) || string.IsNullOrWhiteSpace(endpoint))
        {
            return builder;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("OTEL_EXPORTER_OTLP_ENDPOINT must be an absolute HTTP or HTTPS base URL.");
        }

        builder.Logging.AddOpenTelemetry(options =>
        {
            // IncludeScopes is what carries the AppLogging field set onto the exported record;
            // without it every error.category / error.type field would be dropped at the exporter.
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = builder.Environment.EnvironmentName
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                // Produces a client span for every call this app makes to the MVC API. The span
                // carries the same trace_id as the server-side spans and log entries the MVC app
                // writes for that call, so one trace_id in Kibana shows the whole hop.
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, endpointUri);

        return builder;
    }
}
