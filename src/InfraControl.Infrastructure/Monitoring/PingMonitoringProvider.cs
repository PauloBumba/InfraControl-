using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Provider de ping ICMP (F5-T003 / ADR-014: chave "Ping"). Up/down estilo
/// Zabbix; a execução contínua fica para o worker (F5-T007).
/// </summary>
public sealed class PingMonitoringProvider(IIcmpClient icmpClient) : IMonitoringProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public string Name => "Ping";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, CheckKind.Ping.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(false, TimeSpan.Zero, "Destino do ping é obrigatório.");

        var stopwatch = Stopwatch.StartNew();
        var reply = await icmpClient.SendAsync(target.Trim(), DefaultTimeout, cancellationToken);
        stopwatch.Stop();

        return new MonitoringProviderResult(
            reply.Success,
            stopwatch.Elapsed,
            reply.Success ? null : reply.Error ?? "Host inacessível.");
    }
}
