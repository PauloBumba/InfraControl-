using System.Globalization;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Provider ICMP de discovery (F4-T003): sonda o IP do device com a mesma
/// seam de ICMP do PingMonitoringProvider (sem rede real nos testes).
/// Achado negativo (host não responde) é um resultado válido — o run é
/// finalizado pelo comando, não aqui.
/// </summary>
public sealed class IcmpDiscoveryProvider(IIcmpClient icmpClient) : IDiscoveryProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public string Name => "ICMP";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, "Icmp", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DiscoveryProviderResult>> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.Host,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "Destino do ping é obrigatório.",
                "Destino do ping é obrigatório.")];

        target = target.Trim();
        var reply = await icmpClient.SendAsync(target, DefaultTimeout, cancellationToken);

        return reply.Success
            ? [new DiscoveryProviderResult(
                DiscoveryResultKind.Host,
                true,
                new Dictionary<string, string>
                {
                    ["reachable"] = "true",
                    ["roundtrip_ms"] = reply.RoundtripMs.ToString(CultureInfo.InvariantCulture)
                },
                null,
                $"Host {target} acessível em {reply.RoundtripMs} ms.")]
            : [new DiscoveryProviderResult(
                DiscoveryResultKind.Host,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                reply.Error ?? "Host inacessível.",
                $"Host {target} inacessível.")];
    }
}
