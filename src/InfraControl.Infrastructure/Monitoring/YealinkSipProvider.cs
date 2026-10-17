using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T006a: check YealinkSip — registro SIP do telefone via API Yealink
/// (docs/47 §C). Usa a seam <see cref="IYealinkPhoneClient"/> (HTTP/JSON
/// mockável). Values: sipRegistered ("true"/"false" só quando a API
/// reconheceu o estado) + extension/sipServer/codec (quando expostos).
/// API não suportada → falha pt-BR "indisponível neste modelo". Opt-in —
/// não é default de Phone (default continua [Ping]).
/// </summary>
public sealed class YealinkSipProvider(IYealinkPhoneClient yealinkClient) : IMonitoringProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.YealinkSip.ToString();

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

        YealinkSipInfo? sip;
        try
        {
            sip = await yealinkClient.GetSipInfoAsync(
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

        if (sip is null)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Registro SIP via API Yealink indisponível neste modelo.");
        }

        var values = new Dictionary<string, string>();
        if (sip.Registered.HasValue)
            values["sipRegistered"] = sip.Registered.Value.ToString().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(sip.Extension))
            values["extension"] = sip.Extension;
        if (!string.IsNullOrWhiteSpace(sip.SipServer))
            values["sipServer"] = sip.SipServer;
        if (!string.IsNullOrWhiteSpace(sip.Codec))
            values["codec"] = sip.Codec;

        if (values.Count == 0)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Registro SIP via API Yealink indisponível neste modelo.");
        }

        stopwatch.Stop();
        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }
}
