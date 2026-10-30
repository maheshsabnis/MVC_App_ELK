using System.Data.Common;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MVC_App.Telemetry;

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
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });

        // Do not export EF Core's informational SQL command text as structured logs.
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
            "Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = builder.Environment.EnvironmentName
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // Traces every ADO.NET command EF Core issues (both CompanyContext and
                // IdentityContext use Microsoft.Data.SqlClient under the hood), success or
                // failure, as a child span of the request that triggered it. RecordException
                // marks failed commands with the exception on the span itself; EnrichWithSqlCommand
                // adds the parameterized SQL text (placeholders, not literal values, since
                // sensitive-data logging is never enabled) so failed/slow queries are identifiable.
                .AddSqlClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithSqlCommand = (activity, command) =>
                    {
                        if (command is DbCommand dbCommand)
                        {
                            activity.SetTag("db.statement", dbCommand.CommandText);
                        }
                    };
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel",
                    "Microsoft.AspNetCore.Routing", "Microsoft.AspNetCore.Diagnostics"))
            // Registers one exporter per signal, including logs. Paths are appended automatically.
            .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, endpointUri);

        return builder;
    }
}
