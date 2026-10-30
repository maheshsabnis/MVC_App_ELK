using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MVC_App.Data;

/// <summary>
/// Logs every database command EF Core executes, success or failure, as a structured entry
/// under the <c>MVC_App.Data.DbCommandLoggingInterceptor</c> category. Registered on both
/// CompanyContext and IdentityContext in Program.cs. Runs inside the ambient Activity created
/// by the SqlClient trace instrumentation (see Telemetry/TelemetryExtensions.cs), so each log
/// entry's trace_id/span_id line up with the matching database span.
/// </summary>
public sealed class DbCommandLoggingInterceptor : DbCommandInterceptor
{
    private const int MaxCommandTextLength = 1000;

    private readonly ILogger<DbCommandLoggingInterceptor> _logger;

    public DbCommandLoggingInterceptor(ILogger<DbCommandLoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogSuccess(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        LogSuccess(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogSuccess(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        LogSuccess(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        LogSuccess(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        LogSuccess(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        LogFailure(command, eventData);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        LogFailure(command, eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void LogSuccess(DbCommand command, CommandExecutedEventData eventData)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["db.system"] = "mssql",
            ["db.name"] = command.Connection?.Database,
            ["db.operation"] = eventData.ExecuteMethod.ToString(),
            ["db.duration_ms"] = eventData.Duration.TotalMilliseconds
        });

        _logger.LogInformation(
            "DB call succeeded: {Operation} against {Database} in {DurationMs}ms: {CommandText}",
            eventData.ExecuteMethod, command.Connection?.Database, eventData.Duration.TotalMilliseconds,
            Truncate(command.CommandText));
    }

    private void LogFailure(DbCommand command, CommandErrorEventData eventData)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["db.system"] = "mssql",
            ["db.name"] = command.Connection?.Database,
            ["db.operation"] = eventData.ExecuteMethod.ToString(),
            ["db.duration_ms"] = eventData.Duration.TotalMilliseconds
        });

        _logger.LogError(
            eventData.Exception,
            "DB call failed: {Operation} against {Database} after {DurationMs}ms: {CommandText}",
            eventData.ExecuteMethod, command.Connection?.Database, eventData.Duration.TotalMilliseconds,
            Truncate(command.CommandText));
    }

    private static string Truncate(string? commandText)
    {
        if (string.IsNullOrEmpty(commandText))
        {
            return string.Empty;
        }

        return commandText.Length <= MaxCommandTextLength
            ? commandText
            : commandText[..MaxCommandTextLength] + "...";
    }
}
