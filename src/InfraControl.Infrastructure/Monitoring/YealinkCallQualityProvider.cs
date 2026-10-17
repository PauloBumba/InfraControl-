using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T006b: check YealinkCallQuality — qualidade de chamada
/// (MOS/jitter/latência/perda — docs/47 §D) + PoE (§E) via API Yealink.
/// Seam <see cref="IYealinkPhoneClient"/>; Values mos/jitterMs/latencyMs/
/// packetLossPercent (só métricas de chamada ativa; ausência = sem Value)
/// + poePowered ("true"/"false") e poePowerW (best-effort — PoE indisponível
/// não derruba a qualidade, mesmo molde do identification do OnvifStorage).
/// API não suportada → falha pt-BR "indisponível neste modelo". Opt-in —
/// guard Phone-only. Sem chamada ativa E sem PoE → falha honesta.
/// </summary>
public sealed class YealinkCallQualityProvider(IYealinkPhoneClient yealinkClient)
    : IMonitoringProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.YealinkCallQuality.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, Name, StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino da API Yealink é obrigatório.");

        target = target.Trim();
        var stopwatch = Stopwatch.StartNew();

        YealinkCallQualityInfo? quality;
        try
        {
            quality = await yealinkClient.GetCallQualityAsync(
                target, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar API Yealink: {exception.Message}");
        }

        var values = new Dictionary<string, string>();
        if (quality is not null)
        {
            if (quality.Mos.HasValue)
                values["mos"] = quality.Mos.Value.ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (quality.JitterMs.HasValue)
                values["jitterMs"] = quality.JitterMs.Value.ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (quality.LatencyMs.HasValue)
                values["latencyMs"] = quality.LatencyMs.Value.ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (quality.PacketLossPercent.HasValue)
                values["packetLossPercent"] = quality.PacketLossPercent.Value.ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        // PoE (ficha §E) é best-effort: indisponível não falha a qualidade
        // (mesmo molde do identification do OnvifStorageProvider).
        try
        {
            var poe = await yealinkClient.GetPoeStatusAsync(
                target, DefaultTimeout, cancellationToken);
            if (poe is not null)
            {
                if (poe.Powered.HasValue)
                    values["poePowered"] = poe.Powered.Value.ToString().ToLowerInvariant();
                if (poe.PowerWatts.HasValue)
                    values["poePowerW"] = poe.PowerWatts.Value.ToString(
                        "0.0", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // informativo — indisponível não falha o check.
        }

        if (values.Count == 0)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Qualidade de chamada via API Yealink indisponível "
                + "(sem chamada ativa ou modelo não suportado).");
        }

        stopwatch.Stop();
        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }
}
