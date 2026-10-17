using System.Diagnostics;
using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T007b: check SnmpRouterSessions — sessões TCP via <c>tcpConnTable</c>
/// (TCP-MIB RFC 4022, universal). As linhas são agrupadas por instância
/// (localAddr.localPort.remoteAddr.remotePort); o estado é classificado:
/// listen(2) = escutando, established(5) = estabelecida, demais = other.
/// Reporta contagem total, estabelecidas, escutando e top-10 IPs remotos.
/// Informativo por design — sem regra no AlertEvaluator (limiar de sessão
/// é específico de modelo; DDoS fica para prevenção futura).
/// Opt-in — não é default de Router/Firewall.
/// </summary>
public sealed class SnmpRouterSessionsProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>tcpConnTable.1 — estado da conexão TCP.</summary>
    public const string ConnStateOid = "1.3.6.1.2.1.6.13.1.1";

    // tcpConnState (RFC 4022)
    private const string StateListen = "2";
    private const string StateEstablished = "5";

    private const int TopIps = 10;

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
                target.Trim(), ConnStateOid, DefaultTimeout, cancellationToken);
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

        var sessions = rows
            .Select(variable => ExtractRemoteIp(variable.Oid))
            .Where(ip => ip is not null)
            .Select(ip => ip!)
            .ToArray();

        stopwatch.Stop();

        if (sessions.Length == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Nenhuma sessão TCP encontrada no router/firewall.");

        var establishedCount = rows.Count(variable => variable.Value == StateEstablished);
        var listenCount = rows.Count(variable => variable.Value == StateListen);

        var topIps = sessions
            .GroupBy(ip => ip)
            .Select(group => new { Ip = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .Take(TopIps)
            .Select(x => new { ip = x.Ip, count = x.Count })
            .ToArray();

        var values = new Dictionary<string, string>
        {
            ["sessionCount"] = sessions.Length.ToString(),
            ["establishedCount"] = establishedCount.ToString(),
            ["listenCount"] = listenCount.ToString(),
            ["topIps"] = JsonSerializer.Serialize(topIps)
        };

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    /// <summary>
    /// Extrai o IP remoto do OID da tcpConnTable. O INDEX é composto
    /// (localAddr.localPort.remoteAddr.remotePort = 4 sub-identificadores),
    /// todos em formato decimal do endereço IP (ex: 192.168.1.1 =
    /// 192.168.1.1.5). O IP remoto é o 3º componente do INDEX.
    /// </summary>
    private static string? ExtractRemoteIp(string oid)
    {
        // OID base: 1.3.6.1.2.1.6.13.1.1 (10 sub-ids)
        // INDEX: localAddr.localPort.remoteAddr.remotePort
        var parts = oid.TrimEnd('.').Split('.');
        if (parts.Length < 14) // 10 + 4 do INDEX mínimo
            return null;

        // localAddr = parts[10], localPort = parts[11], remoteAddr = parts[12], remotePort = parts[13]
        // Mas localAddr e remoteAddr podem ter 4 sub-ids (IPv4) ou 16 (IPv6)
        // Vamos assumir IPv4 por enquanto (simples) - o INDEX real é mais complexo
        // Para simplificar, vamos extrair baseado na posição conhecida
        var baseLength = ConnStateOid.Split('.').Length;
        if (parts.Length < baseLength + 3)
            return null;

        // O INDEX de tcpConnTable é: localAddr (hex ou decimal) + localPort + remoteAddr + remotePort
        // Para IPv4 decimal, o IP são 4 octetos. Vamos tentar extrair o remoteAddr.
        // Como o formato é complexo (pode ser hex), vamos simplificar: extrair o último IP válido
        // que aparece antes da porta remota.

        // Abordagem simplificada: pega os últimos 4 octetos antes do último número (porta)
        var lastPortIndex = parts.Length - 1;
        if (lastPortIndex < 4)
            return null;

        // Tenta extrair como IPv4 (4 octetos antes da porta)
        var ip1 = parts[lastPortIndex - 4];
        var ip2 = parts[lastPortIndex - 3];
        var ip3 = parts[lastPortIndex - 2];
        var ip4 = parts[lastPortIndex - 1];

        // Valida se são octetos válidos (0-255)
        if (IsValidOctet(ip1) && IsValidOctet(ip2) && IsValidOctet(ip3) && IsValidOctet(ip4))
            return $"{ip1}.{ip2}.{ip3}.{ip4}";

        return null;
    }

    private static bool IsValidOctet(string value)
        => int.TryParse(value, out var octet) && octet is >= 0 and <= 255;
}
