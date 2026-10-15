using System.Diagnostics;
using System.Globalization;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Check SnmpHealth (F30-T001, docs/42 §C): CPU, memória e temperatura de um
/// switch, escolhendo o mapa de OIDs pelo <c>Device.Vendor</c>.
///
/// Segue a MESMA seam dos demais providers (<see cref="IMonitoringProvider"/> +
/// <see cref="ISnmpClient"/>): a ficha 42 propõe um <c>ISnmpHealthProvider</c>
/// próprio, mas é a única das 8 fichas da F30 que desenha uma seam nova — as
/// outras 7 já usam o padrão <c>SnmpXxxProvider</c> (decisão registrada no
/// plano docs/51 §2). O vendor chega por <c>IDeviceRepository.GetByIpAsync</c>,
/// porta que já existe: <c>CheckAsync(target)</c> não muda.
///
/// REGRA: só afirma o que o device respondeu. Métrica ausente = chave ausente
/// (nunca 0); vendor desconhecido ou device não cadastrado = falha honesta com
/// <c>Values = null</c>. Onde o OID reporta porcentagem direta, o valor é
/// passado como o device o reportou; só UniFi calcula (total − livre)/total,
/// porque os dois OIDs dela são de bytes.
/// </summary>
public sealed class SnmpHealthMonitoringProvider(
    ISnmpClient snmpClient,
    IDeviceRepository deviceRepository) : IMonitoringProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Listagem usada na recusa de vendor desconhecido (docs/42 §C).</summary>
    private const string SupportedVendors = "UniFi/Ubiquiti, Intelbras, HP, Aruba, TP-Link";

    /// <summary>
    /// OIDs docs/42 §C (L170-177), agrupados por fabricante e por métrica.
    /// Cada lista é ordenada por prioridade: o primeiro OID que responder
    /// vence (Intelbras tem 2 linhas — OLT e Série 3000 — em cada métrica).
    /// </summary>
    private sealed record VendorHealthOids(
        IReadOnlyList<string> CpuOids,
        IReadOnlyList<string> MemoryPercentOids,
        IReadOnlyList<string> MemoryTotalOids,
        IReadOnlyList<string> MemoryFreeOids,
        IReadOnlyList<string> TemperatureOids);

    private static readonly VendorHealthOids UnifiOids = new(
        CpuOids: ["1.3.6.1.4.1.10002.1.1.1.4.2.1.3.1"],                       // 1m avg
        MemoryPercentOids: [],
        MemoryTotalOids: ["1.3.6.1.4.1.10002.1.1.1.1.1.0"],
        MemoryFreeOids: ["1.3.6.1.4.1.10002.1.1.1.1.2.0"],
        TemperatureOids: ["1.3.6.1.4.1.4413.1.1.43.1.8.1.5.1.0"]);

    private static readonly VendorHealthOids IntelbrasOids = new(
        CpuOids:
        [
            "1.3.6.1.4.1.13464.1.14.2.1.3.1.0",        // OLT
            "1.3.6.1.4.1.26138.3.600.9.1.1.3.33.1"     // Série 3000
        ],
        MemoryPercentOids:
        [
            "1.3.6.1.4.1.13464.1.14.2.1.3.2.0",        // OLT
            "1.3.6.1.4.1.26138.3.600.10.1.1.10.33"     // Série 3000
        ],
        MemoryTotalOids: [],
        MemoryFreeOids: [],
        TemperatureOids:
        [
            "1.3.6.1.4.1.13464.1.14.2.1.3.3.0",        // OLT (chipset)
            "1.3.6.1.4.1.26138.3.600.2.1.1.20.33"      // Série 3000
        ]);

    private static readonly VendorHealthOids HpOids = new(
        CpuOids: ["1.3.6.1.4.1.11.2.14.11.5.1.9.6.1.0"],
        MemoryPercentOids: ["1.3.6.1.4.1.11.2.14.11.5.1.1.2.1.1.1.6.1"],
        MemoryTotalOids: [],
        MemoryFreeOids: [],
        TemperatureOids: ["1.3.6.1.4.1.11.2.14.11.1.2.8.1.1.9.0"]);

    private static readonly VendorHealthOids ArubaOids = new(
        CpuOids:
        [
            "1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.3",    // instant
            "1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.10",   // 1m avg
            "1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.11"    // 5m avg
        ],
        MemoryPercentOids: ["1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.4"],
        MemoryTotalOids: [],
        MemoryFreeOids: [],
        TemperatureOids: []);    // docs/42 L174: "via MIBs de hardware" — sem OID na tabela

    private static readonly VendorHealthOids TpLinkOids = new(
        CpuOids:
        [
            "1.3.6.1.4.1.11863.6.4.1.1.1.1.2",    // 5s
            "1.3.6.1.4.1.11863.6.4.1.1.1.1.3",    // 1m
            "1.3.6.1.4.1.11863.6.4.1.1.1.1.4"     // 5m
        ],
        MemoryPercentOids: ["1.3.6.1.4.1.11863.6.4.1.2.1.1.2"],
        MemoryTotalOids: [],
        MemoryFreeOids: [],
        TemperatureOids: ["1.3.6.1.4.1.11863.6.4.1.3"]);    // OctetString — ver TryParseDecimal

    public string Name => CheckKind.SnmpHealth.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, CheckKind.SnmpHealth.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do SNMP é obrigatório.");

        var stopwatch = Stopwatch.StartNew();
        var destination = target.Trim();

        // O vendor decide quais OIDs consultar — resolvido ANTES de qualquer
        // walk (sem vendor conhecido não se consulta nada).
        var (device, lookupError) = await ResolveDeviceAsync(destination, cancellationToken);
        if (device is null)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                lookupError ?? $"Device não cadastrado para o destino '{destination}'.");

        if (string.IsNullOrWhiteSpace(device.Vendor))
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Device sem fabricante cadastrado — sem vendor não há como escolher os OIDs de saúde.");

        var oids = ResolveOids(device.Vendor);
        if (oids is null)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                $"Fabricante não suportado para saúde SNMP: '{device.Vendor}'. " +
                $"Suportados: {SupportedVendors}.");

        var values = new Dictionary<string, string>();
        Exception? firstFailure = null;

        // Local assíncrono: acumula a primeira falha de walk sem `ref`
        // (proibido em método assíncrono).
        async Task<string?> ReadAsync(IReadOnlyList<string> candidates)
        {
            var (value, failure) = await ReadFirstAsync(
                destination, candidates, cancellationToken);
            firstFailure ??= failure;
            return value;
        }

        var cpu = await ReadAsync(oids.CpuOids);
        if (cpu is not null && TryFormatPercent(cpu, out var cpuPercent))
            values["cpuPercent"] = cpuPercent;

        if (oids.MemoryPercentOids.Count > 0)
        {
            // Fabricante que reporta porcentagem direta — passado como reportado.
            var memory = await ReadAsync(oids.MemoryPercentOids);
            if (memory is not null && TryFormatPercent(memory, out var memoryPercent))
                values["memoryPercent"] = memoryPercent;
        }
        else if (oids.MemoryTotalOids.Count > 0 && oids.MemoryFreeOids.Count > 0)
        {
            // UniFi: bytes total e livre => % em uso. Sem o par completo,
            // a chave fica ausente (nunca 0).
            var total = await ReadAsync(oids.MemoryTotalOids);
            var free = await ReadAsync(oids.MemoryFreeOids);
            if (TryParseDecimal(total, out var totalBytes)
                && TryParseDecimal(free, out var freeBytes)
                && totalBytes > 0)
            {
                var usedPercent = (totalBytes - freeBytes) / totalBytes * 100m;
                if (usedPercent >= 0 && usedPercent <= 100)
                    values["memoryPercent"] = usedPercent.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        var temperature = await ReadAsync(oids.TemperatureOids);
        if (temperature is not null && TryParseDecimal(temperature, out var degrees))
            values["temperatureCelsius"] = degrees.ToString("0.##", CultureInfo.InvariantCulture);

        stopwatch.Stop();

        if (values.Count == 0)
        {
            var detail = firstFailure is null
                ? "nenhuma métrica (CPU/memória/temperatura) respondeu"
                : $"falha ao consultar SNMP: {firstFailure.Message}";

            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                $"Saúde SNMP indisponível: {detail} para '{device.Vendor}'.");
        }

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    /// <summary>
    /// Resolve o device pelo IP do target (porta já existente do produto) só
    /// para saber o fabricante. Devolve <c>LookupError</c> quando a consulta
    /// em si falhou — assim o operador não lê "não cadastrado" quando o
    /// problema foi o banco. Falha vira resultado, nunca exceção vazada.
    /// </summary>
    private async Task<(Device? Device, string? LookupError)> ResolveDeviceAsync(
        string destination,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await deviceRepository.GetByIpAsync(destination, cancellationToken);
            return (device, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (null, $"Falha ao consultar o device por IP '{destination}': {exception.Message}");
        }
    }

    /// <summary>
    /// Percorre os candidatos em ordem e pára no primeiro que responder.
    /// Devolve também a primeira exceção de walk, para a mensagem de falha.
    /// </summary>
    private async Task<(string? Value, Exception? Failure)> ReadFirstAsync(
        string destination,
        IReadOnlyList<string> oids,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;

        foreach (var oid in oids)
        {
            IReadOnlyList<SnmpVariable> sample;
            try
            {
                sample = await snmpClient.WalkAsync(
                    destination, oid, DefaultTimeout, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failure ??= exception;
                continue;
            }

            foreach (var variable in sample)
            {
                if (!string.Equals(variable.Oid.TrimStart('.'), oid, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrWhiteSpace(variable.Value))
                    break;

                return (variable.Value.Trim(), failure);
            }
        }

        return (null, failure);
    }

    private static VendorHealthOids? ResolveOids(string vendor)
    {
        // O cadastro é texto livre ("Ubiquiti", "Hewlett-Packard", "ProCurve
        // 2920-48G", "TP-Link"...) — casa por conteúdo e por token, sem
        // distinção de caixa. Nada de `Contains("hp")` solto: cairia em
        // qualquer vendor cujo nome contenha a letra/par.
        var text = vendor.Trim();

        if (text.Contains("unifi", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ubiquiti", StringComparison.OrdinalIgnoreCase))
            return UnifiOids;

        if (text.Contains("intelbras", StringComparison.OrdinalIgnoreCase))
            return IntelbrasOids;

        if (text.Contains("aruba", StringComparison.OrdinalIgnoreCase))
            return ArubaOids;

        if (text.Contains("tplink", StringComparison.OrdinalIgnoreCase)
            || (HasToken(text, "tp") && HasToken(text, "link")))
            return TpLinkOids;

        if (text.Contains("hewlett", StringComparison.OrdinalIgnoreCase)
            || text.Contains("procurve", StringComparison.OrdinalIgnoreCase)
            || HasToken(text, "hp")
            || HasToken(text, "hpe"))
            return HpOids;

        return null;
    }

    /// <summary>Token inteiro ("HP", "HPE", "TP"), não subcadeia.</summary>
    private static bool HasToken(string text, string token)
        => text.Split(
                [' ', '-', '_', '.', '/', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(part, token, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // Pode vir como "41.5", "41,5" ou com unidade — tenta os dois separadores.
        var text = raw.Trim();
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("pt-BR"), out value);
    }

    private static bool TryFormatPercent(string raw, out string formatted)
    {
        formatted = string.Empty;
        if (!TryParseDecimal(raw, out var value))
            return false;

        // Porcentagem fora de 0..100 não é crível (ex.: bytes no lugar de %)
        // — descarta em vez de reportar número enganoso.
        if (value < 0 || value > 100)
            return false;

        formatted = value.ToString("0.##", CultureInfo.InvariantCulture);
        return true;
    }
}
