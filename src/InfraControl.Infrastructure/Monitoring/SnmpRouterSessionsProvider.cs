using System.Diagnostics;
using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T007b: check SnmpRouterSessions — tabela de sessões TCP via
/// <c>tcpConnTable</c> (TCP-MIB RFC 4022, universal). As linhas são
/// agrupadas por instância composta (localAddr.localPort.remAddr.remPort);
/// conta-se o total, established(5) e listen(2); os remotos com mais
/// conexões vão em <c>topRemoteIps</c> (JSON, top 10 — listen wildcard
/// 0.0.0.0 excluído). Informativo por design — sem regra no AlertEvaluator
/// (limiar de sessões é específico de modelo/capacidade). Opt-in — não é
/// default de Router/Firewall.
/// </summary>
public sealed class SnmpRouterSessionsProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>tcpConnTable (colunas state/localAddr/localPort/remAddr/remPort).</summary>
    public const string ConnTableOid = "1.3.6.1.2.1.6.13.1";

    // colunas da tcpConnTable (RFC 4022)
    private const string ColState = "1";
    private const string ColRemAddress = "4";

    // tcpConnState (RFC 4022): established(5), listen(2).
    private const string StateEstablished = "5";
    private const string StateListen = "2";

    private const string WildcardAddress = "0.0.0.0";

    private const int TopRemotes = 10;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpRouterSessions.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, Name, StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do SNMP é obrigatório.");

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<SnmpVariable> rows;
        try
        {
            rows = await snmpClient.WalkAsync(
                target.Trim(), ConnTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar SNMP: {ex.Message}");
        }

        var sessions = GroupByInstance(rows)
            .Select(pair => ToSession(pair.Value))
            .Where(session => session is not null)
            .Select(session => session!)
            .ToArray();

        stopwatch.Stop();

        if (sessions.Length == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Nenhuma sessão TCP válida encontrada no router/firewall.");

        var establishedCount = sessions.Count(s => s.State == StateEstablished);
        var listenCount = sessions.Count(s => s.State == StateListen);
        var topRemotes = sessions
            .Where(s => !string.IsNullOrEmpty(s.RemAddress)
                && s.RemAddress != WildcardAddress)
            .GroupBy(s => s.RemAddress, StringComparer.Ordinal)
            .Select(group => new { ip = group.Key, connections = group.Count() })
            .OrderByDescending(entry => entry.connections)
            .ThenBy(entry => entry.ip, StringComparer.Ordinal)
            .Take(TopRemotes)
            .ToArray();

        var values = new Dictionary<string, string>
        {
            ["sessionCount"] = sessions.Length.ToString(),
            ["establishedCount"] = establishedCount.ToString(),
            ["listenCount"] = listenCount.ToString(),
            ["topRemoteIps"] = JsonSerializer.Serialize(topRemotes)
        };

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    private sealed record SessionInfo(string State, string? RemAddress);

    private static Dictionary<string, SessionEntry> GroupByInstance(
        IReadOnlyList<SnmpVariable> rows)
    {
        // O INDEX da tcpConnTable é composto (localAddr.localPort.remAddr.
        // remPort = 10 sub-identificadores); a coluna fica em posição FIXA
        // logo após o base OID e a instância é o resto do OID.
        var baseLength = ConnTableOid.Split('.').Length;
        var byInstance = new Dictionary<string, SessionEntry>(StringComparer.Ordinal);
        foreach (var variable in rows)
        {
            var parts = variable.Oid.TrimEnd('.').Split('.');
            if (parts.Length < baseLength + 2)
                continue;

            var column = parts[baseLength];
            var instance = string.Join('.', parts.AsSpan(baseLength + 1));
            if (!byInstance.TryGetValue(instance, out var entry))
                entry = new SessionEntry(null, null);

            entry = column switch
            {
                ColState => entry with { State = variable.Value },
                ColRemAddress => entry with { RemAddress = variable.Value },
                _ => entry
            };
            byInstance[instance] = entry;
        }

        return byInstance;
    }

    private static SessionInfo? ToSession(SessionEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.State))
            return null;

        return new SessionInfo(entry.State, entry.RemAddress);
    }

    private sealed record SessionEntry(string? State, string? RemAddress);
}
