using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T005a: check OnvifStorage — armazenamento/retenção da câmera/NVR
/// via ONVIF (docs/46 §C, prioridade 1: padrão universal). Usa a seam
/// <see cref="IOnvifClient"/> (SOAP mockável). Values: totalMB/usedMB/
/// availableMB/usedPercent (alertável, limiares 80/90 iguais aos do
/// servidor) + retentionDays (informativo, quando o device expõe) e
/// identificação best-effort manufacturer/model/firmware (ficha §A).
/// ONVIF não suportado → falha pt-BR "indisponível neste modelo".
/// Opt-in — não é default de Camera/Nvr.
/// </summary>
public sealed class OnvifStorageProvider(IOnvifClient onvifClient) : IMonitoringProvider
{
    private const double BytesPerMb = 1024d * 1024d;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.OnvifStorage.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, Name, StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do ONVIF é obrigatório.");

        target = target.Trim();
        var stopwatch = Stopwatch.StartNew();

        OnvifStorageInfo? storage;
        try
        {
            storage = await onvifClient.GetStorageAsync(
                target, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar ONVIF: {exception.Message}");
        }

        if (storage is null)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Armazenamento ONVIF indisponível neste modelo.");
        }

        if (storage.TotalBytes <= 0)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Armazenamento ONVIF inválido (capacidade zero).");
        }

        var usedBytes = Math.Max(0, storage.TotalBytes - storage.FreeBytes);
        var values = new Dictionary<string, string>
        {
            ["totalMB"] = ((long)Math.Round(storage.TotalBytes / BytesPerMb)).ToString(),
            ["usedMB"] = ((long)Math.Round(usedBytes / BytesPerMb)).ToString(),
            ["availableMB"] = ((long)Math.Round(storage.FreeBytes / BytesPerMb)).ToString(),
            ["usedPercent"] = ((int)Math.Round(
                usedBytes * 100d / storage.TotalBytes,
                MidpointRounding.AwayFromZero)).ToString()
        };

        if (storage.RetentionDays is >= 0)
            values["retentionDays"] = storage.RetentionDays!.Value.ToString();

        // Identificação (ficha §A) é best-effort: indisponível não derruba
        // o armazenamento (mesmo molde do processCount do SnmpServerCpu).
        try
        {
            var info = await onvifClient.GetDeviceInformationAsync(
                target, DefaultTimeout, cancellationToken);
            if (info is not null)
            {
                if (!string.IsNullOrWhiteSpace(info.Manufacturer))
                    values["manufacturer"] = info.Manufacturer;
                if (!string.IsNullOrWhiteSpace(info.Model))
                    values["model"] = info.Model;
                if (!string.IsNullOrWhiteSpace(info.FirmwareVersion))
                    values["firmware"] = info.FirmwareVersion;
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

        stopwatch.Stop();
        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }
}
