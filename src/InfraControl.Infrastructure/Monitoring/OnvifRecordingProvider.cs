using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T005b: check OnvifRecording — estado de gravação/video loss/tamper
/// da câmera/NVR via ONVIF (docs/46 §B/C). Seam <see cref="IOnvifClient"/>;
/// Values <c>recordingActive</c>/<c>videoLoss</c>/<c>tamper</c> como
/// "true"/"false" (só os campos que o device expôs). ONVIF não suportado →
/// falha pt-BR "indisponível neste modelo". Opt-in — guard Camera/Nvr.
/// </summary>
public sealed class OnvifRecordingProvider(IOnvifClient onvifClient) : IMonitoringProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.OnvifRecording.ToString();

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

        OnvifRecordingStatus? status;
        try
        {
            status = await onvifClient.GetRecordingStatusAsync(
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

        if (status is null)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Estado de gravação ONVIF indisponível neste modelo.");
        }

        var values = new Dictionary<string, string>();
        if (status.RecordingActive.HasValue)
            values["recordingActive"] = status.RecordingActive.Value.ToString().ToLowerInvariant();
        if (status.VideoLoss.HasValue)
            values["videoLoss"] = status.VideoLoss.Value.ToString().ToLowerInvariant();
        if (status.Tamper.HasValue)
            values["tamper"] = status.Tamper.Value.ToString().ToLowerInvariant();

        if (values.Count == 0)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Estado de gravação ONVIF indisponível neste modelo.");
        }

        stopwatch.Stop();
        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }
}
