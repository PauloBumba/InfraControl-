using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Check SnmpRadio (F30-T004 S2, docs/52 §S2): fallback SNMP do Access
/// Point pela IEEE 802.11 MIB (ficha 45 §C/D/E), no molde do
/// SnmpInterfacesMonitoringProvider — só a seam <c>ISnmpClient</c>, sem
/// vendor (a MIB é universal) e sem repositório.
///
/// Uma única walk na raiz <c>dot11Smt 1.3.6.1.2.1.80.1.1.3</c>, instância
/// <c>&lt;coluna&gt;.&lt;ifIndex&gt;</c> (ficha):
/// 1 = estações móveis, 2 = SSID desejado, 4 = canal atual,
/// 5 = potência de TX, 6 = RSSI, 7 = privacidade.
///
/// REGRA da casa: só afirma o que o device respondeu — coluna ausente ou
/// valor em branco = chave AUSENTE (nunca 0); a chave leva o ifIndex para
/// não fundir rádios (2.4/5 GHz têm canal e SSID distintos). Sem resposta
/// nenhuma = falha honesta em pt-BR com <c>Values = null</c>.
/// Opt-in por device (DeviceProfileDefaults intocado); guard AccessPoint-
/// only vive no RunCheckCommandHandler.
/// </summary>
public sealed class SnmpRadioProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>Raiz dot11Smt — walkada de uma vez só.</summary>
    public const string Dot11RootOid = "1.3.6.1.2.1.80.1.1.3";

    /// <summary>Colunas da ficha 45: nome da chave → prefixo do OID da coluna.</summary>
    private static readonly (string Key, string Oid)[] Columns =
    [
        ("mobileStations", "1.3.6.1.2.1.80.1.1.3.1"),
        ("ssid", "1.3.6.1.2.1.80.1.1.3.2"),
        ("channel", "1.3.6.1.2.1.80.1.1.3.4"),
        ("txPowerLevel", "1.3.6.1.2.1.80.1.1.3.5"),
        ("rssi", "1.3.6.1.2.1.80.1.1.3.6"),
        ("privacyImplemented", "1.3.6.1.2.1.80.1.1.3.7")
    ];

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpRadio.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(
            providerKey, CheckKind.SnmpRadio.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do SNMP é obrigatório.");

        var stopwatch = Stopwatch.StartNew();

        IReadOnlyList<SnmpVariable> sample;
        try
        {
            sample = await snmpClient.WalkAsync(
                target.Trim(), Dot11RootOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                $"Falha ao consultar IEEE 802.11 MIB (dot11): {exception.Message}");
        }

        var metrics = new Dictionary<string, string>();
        var radios = new HashSet<int>();

        foreach (var variable in sample)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var oid = variable.Oid.TrimStart('.');
            foreach (var (key, columnOid) in Columns)
            {
                if (!oid.StartsWith(columnOid + ".", StringComparison.Ordinal))
                    continue;

                // Instância tem que ser SÓ o ifIndex ("5" ok; "abc",
                // "5.9" e colunas desconhecidas caem fora).
                var suffix = oid[(columnOid.Length + 1)..];
                if (!int.TryParse(suffix, out var ifIndex) || ifIndex < 0)
                    continue;

                metrics[$"{key}.{ifIndex}"] = variable.Value.Trim();
                radios.Add(ifIndex);
                break;
            }
        }

        stopwatch.Stop();

        if (radios.Count == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                $"Rádio 802.11 indisponível: nenhuma coluna dot11 "
                + $"(estações/SSID/canal/potência/RSSI) respondeu em {Dot11RootOid}.");

        // radioCount primeiro (contrato de exibição), depois o que respondeu.
        var values = new Dictionary<string, string>
        {
            ["radioCount"] = radios.Count.ToString()
        };
        foreach (var (key, value) in metrics)
            values[key] = value;

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }
}
