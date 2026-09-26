using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Provider HTTP/HTTPS do monitoring (F5-T005 — decisões aprovadas:
/// HTTPS com fallback HTTP, qualquer resposta HTTP = sucesso e validação
/// de cert TLS). Sonda https://{ip}/; se não responder, tenta
/// http://{ip}/; IPv6 ganha colchetes. Falha nos dois esquemas grava os
/// dois erros em Values (https_error/http_error).
/// </summary>
public sealed class HttpWanMonitoringProvider(IHttpProbeClient probeClient)
    : IMonitoringProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public string Name => CheckKind.HttpWan.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(
            providerKey, CheckKind.HttpWan.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do HTTP é obrigatório.");

        target = target.Trim();
        var stopwatch = Stopwatch.StartNew();

        string httpsError;
        try
        {
            var httpsUrl = BuildUrl("https", target);
            var https = await probeClient.SendAsync(
                httpsUrl, DefaultTimeout, cancellationToken);
            if (https.Success)
            {
                stopwatch.Stop();
                return Success(httpsUrl, "https", https.StatusCode, stopwatch.Elapsed);
            }

            httpsError = https.Error ?? "Sem resposta.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar HTTP: {exception.Message}");
        }

        string httpError;
        try
        {
            var httpUrl = BuildUrl("http", target);
            var http = await probeClient.SendAsync(
                httpUrl, DefaultTimeout, cancellationToken);
            if (http.Success)
            {
                stopwatch.Stop();
                return Success(httpUrl, "http", http.StatusCode, stopwatch.Elapsed);
            }

            httpError = http.Error ?? "Sem resposta.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar HTTP: {exception.Message}");
        }

        stopwatch.Stop();
        return new MonitoringProviderResult(
            false,
            stopwatch.Elapsed,
            $"HTTP indisponível em {target}.",
            new Dictionary<string, string>
            {
                ["https_error"] = httpsError,
                ["http_error"] = httpError
            });
    }

    private static MonitoringProviderResult Success(
        string url,
        string scheme,
        int? statusCode,
        TimeSpan duration)
        => new(true, duration, null, new Dictionary<string, string>
        {
            ["url"] = url,
            ["scheme"] = scheme,
            ["status_code"] = statusCode?.ToString() ?? string.Empty
        });

    private static string BuildUrl(string scheme, string target)
    {
        // IPv6 (contém ':') precisa de colchetes na autoridade da URL.
        var host = target.Trim('[', ']');
        return host.Contains(':')
            ? $"{scheme}://[{host}]/"
            : $"{scheme}://{host}/";
    }
}
