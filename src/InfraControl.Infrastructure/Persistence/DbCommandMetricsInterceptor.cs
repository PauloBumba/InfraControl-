using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfraControl.Infrastructure.Persistence;

/// <summary>
/// Métricas de cada comando SQL executado pelo EF Core — duração em
/// histograma e contagem por tipo de operação (SELECT/INSERT/UPDATE/
/// DELETE/OTHER). Tags de baixa cardinalidade apenas: o texto do comando
/// NUNCA vira label (vazaria dados e explodiria o Prometheus).
/// Os dados fluem pelo pipeline OTel → collector → Prometheus/Grafana.
/// </summary>
public sealed class DbCommandMetricsInterceptor : DbCommandInterceptor
{
    private const int MaxStatementLength = 500;

    private static readonly Meter Meter = new("InfraControl.Infrastructure");

    // Mesmo nome de meter → AddSource("InfraControl.*") já escuta; os spans
    // aparecem como filhos do request MediatR no Tempo/Grafana.
    private static readonly ActivitySource ActivitySource = new("InfraControl.Infrastructure");

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "infracontrol.db.command.duration",
        unit: "ms",
        description: "Duração dos comandos SQL executados pelo EF Core.");

    private static readonly Counter<long> Commands = Meter.CreateCounter<long>(
        "infracontrol.db.commands",
        unit: "{command}",
        description: "Comandos SQL executados, por tipo de operação.");

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Record(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData,
        object? result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Classifica o comando pelo primeiro token do texto — uma única tag
    /// de baixa cardinalidade (db.operation) para agregar no Grafana.
    /// </summary>
    private static void Record(DbCommand command, CommandExecutedEventData eventData)
    {
        var sql = command.CommandText.AsSpan().TrimStart();
        var tag = sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ? "select"
            : sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ? "insert"
            : sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ? "update"
            : sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ? "delete"
            : "other";
        var tags = new TagList { { "db.operation", tag } };
        Commands.Add(1, tags);
        Duration.Record(eventData.Duration.TotalMilliseconds, tags);

        // Span de trace por comando: criado DEPOIS da execução com
        // SetStartTime retroativo (os métodos *Executed só rodam no fim).
        // db.statement vai truncado — CommandText não carrega valores de
        // parâmetros, então não vaza dado; traces não são métricas, a
        // cardinalidade aqui não explode o Prometheus.
        var activity = ActivitySource.StartActivity($"db {tag}", ActivityKind.Client);
        if (activity is null)
            return;
        activity.SetStartTime(DateTime.UtcNow - eventData.Duration);
        activity.SetTag("db.system", "postgresql");
        activity.SetTag("db.operation", tag);
        activity.SetTag("db.duration_ms", eventData.Duration.TotalMilliseconds);
        var statement = command.CommandText;
        activity.SetTag("db.statement",
            statement.Length <= MaxStatementLength ? statement : statement[..MaxStatementLength]);
        activity.Dispose();
    }
}
