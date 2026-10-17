namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Seam da API Yealink para os checks de telefone IP (F30-T006 — docs/47):
/// HTTP/JSON por trás; permite testar os providers sem dispositivo real
/// (mesmo molde de ISnmpClient/IOnvifClient). <c>null</c> = API não
/// suportada/resposta não reconhecida ("indisponível neste modelo");
/// erros de transporte/timeout lançam exceção.
/// </summary>
public interface IYealinkPhoneClient
{
    /// <summary>Registro SIP da linha (docs/47 §C).</summary>
    Task<YealinkSipInfo?> GetSipInfoAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Qualidade de chamada (docs/47 §D) — métricas só existem durante
    /// chamada ativa; ausência = campos <c>null</c> no registro.
    /// </summary>
    Task<YealinkCallQualityInfo?> GetCallQualityAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>Estado/potência PoE do telefone (docs/47 §E).</summary>
    Task<YealinkPoeInfo?> GetPoeStatusAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Registro SIP Yealink — <c>null</c> por campo = API não expôs aquele
/// dado; <see cref="Registered"/> <c>null</c> = estado não reconhecível.
/// </summary>
public sealed record YealinkSipInfo(
    bool? Registered,
    string? Extension,
    string? SipServer,
    string? Codec);

/// <summary>
/// Qualidade de chamada (MOS/jitter/latência/perda) — <c>null</c> por
/// campo = sem chamada ativa ou API não expôs o dado.
/// </summary>
public sealed record YealinkCallQualityInfo(
    decimal? Mos,
    decimal? JitterMs,
    decimal? LatencyMs,
    decimal? PacketLossPercent);

/// <summary>PoE do telefone (docs/47 §E).</summary>
public sealed record YealinkPoeInfo(bool? Powered, decimal? PowerWatts);
