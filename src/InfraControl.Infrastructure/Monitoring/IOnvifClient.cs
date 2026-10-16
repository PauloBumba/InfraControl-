namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Seam de ONVIF para os checks de câmera/NVR (F30-T005 — docs/46):
/// SOAP 1.2/WS-Addressing por trás; permite testar os providers sem
/// dispositivo real (mesmo molde de ISnmpClient/IHttpProbeClient).
/// </summary>
public interface IOnvifClient
{
    /// <summary>
    /// Armazenamento do dispositivo (docs/46 §C). <c>null</c> = ONVIF não
    /// suportou (SOAP Fault) ou a resposta veio incompleta (sem Used/Free) —
    /// o provider traduz isso na falha "indisponível neste modelo".
    /// Erros de transporte/timeout lançam exceção (o provider converte em
    /// falha pt-BR).
    /// </summary>
    Task<OnvifStorageInfo?> GetStorageAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identificação do dispositivo (docs/46 §A) — best-effort; <c>null</c>
    /// em Fault/ausência, exceção em erro de transporte.
    /// </summary>
    Task<OnvifDeviceInformation?> GetDeviceInformationAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estado de gravação/video loss/tamper (docs/46 §B/C). <c>null</c> =
    /// ONVIF não suportou ou nenhum campo veio reconhecível; campos não
    /// expostos ficam <c>null</c> dentro do registro. Erros de
    /// transporte/timeout lançam exceção.
    /// </summary>
    Task<OnvifRecordingStatus?> GetRecordingStatusAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Capacidade total/livre em bytes + dias de retenção (quando o device
/// expõe). Usado = Total − Free (derivado no client quando só vier Free).
/// </summary>
public sealed record OnvifStorageInfo(long TotalBytes, long FreeBytes, int? RetentionDays);

/// <summary>Identificação ONVIF GetDeviceInformation (ficha §A).</summary>
public sealed record OnvifDeviceInformation(
    string Manufacturer,
    string Model,
    string FirmwareVersion);

/// <summary>
/// Estado ONVIF GetRecordingStatus (ficha §B/C) — <c>null</c> por campo =
/// device não expôs aquele dado.
/// </summary>
public sealed record OnvifRecordingStatus(
    bool? RecordingActive,
    bool? VideoLoss,
    bool? Tamper);
