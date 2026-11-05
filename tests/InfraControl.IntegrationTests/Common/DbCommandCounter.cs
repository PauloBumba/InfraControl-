using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// F46-T003 — interceptor passivo que conta comandos SQL executados pelo EF
/// (caça a N+1/queries por request). Só é registrado no host de teste via
/// ConfigureTestServices. As classes E2E rodam em PARALELO com hosts
/// distintos (cada fixture tem seu Postgres Testcontainers), então os totais
/// são POR HOST: cada E2EHost registra sua chave ("Port=xxxxx", única por
/// container) e os testes leem/resetam só a própria chave.
/// </summary>
public sealed class DbCommandCounter : DbCommandInterceptor
{
    public static readonly DbCommandCounter Instance = new();

    private readonly ConcurrentDictionary<string, long> _totals = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _recent = new();

    public void Register(string hostKey)
    {
        _totals.TryAdd(hostKey, 0);
        _recent.TryAdd(hostKey, new ConcurrentQueue<string>());
    }

    public void Unregister(string hostKey)
    {
        _totals.TryRemove(hostKey, out _);
        _recent.TryRemove(hostKey, out _);
    }

    public long Total(string hostKey)
        => _totals.TryGetValue(hostKey, out var value)
            ? Interlocked.Read(ref value)
            : 0;

    public void Reset(string hostKey)
    {
        _totals[hostKey] = 0;
        if (_recent.TryGetValue(hostKey, out var queue))
        {
            while (queue.TryDequeue(out _))
            {
            }
        }
    }

    /// <summary>Últimos comandos (texto truncado) — diagnóstico de orçamento.</summary>
    public IReadOnlyList<string> Recent(string hostKey)
        => _recent.TryGetValue(hostKey, out var queue)
            ? queue.ToArray()
            : [];

    private void Count(DbCommand command)
    {
        var connectionString = command.Connection?.ConnectionString;
        if (string.IsNullOrEmpty(connectionString))
            return;

        foreach (var key in _totals.Keys)
        {
            if (connectionString.Contains(key, StringComparison.Ordinal))
            {
                _totals.AddOrUpdate(key, 1, static (_, current) => current + 1);
                if (_recent.TryGetValue(key, out var queue))
                {
                    var text = command.CommandText;
                    if (text.Length > 240)
                        text = text[..240] + "…";
                    queue.Enqueue(text);
                    while (queue.Count > 80 && queue.TryDequeue(out _))
                    {
                    }
                }
                return;
            }
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Count(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Count(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Count(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
