using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Provider TCP do monitoring (F5-T006 — escopo aprovado: porta fixa
/// 37777, default de câmera). Sonda {ip}:37777 via ITcpProbeClient;
/// handshake completo = sucesso com Values port; recusa/timeout = falha
/// com o detalhe em Values tcp_error.
/// </summary>
public sealed class TcpMonitoringProvider(ITcpProbeClient probeClient) : IMonitoringProvider
{
    public const int DefaultPort = 37777;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public string Name => CheckKind.Tcp.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(
            providerKey, CheckKind.Tcp.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do TCP é obrigatório.");

        target = target.Trim();
        var stopwatch = Stopwatch.StartNew();

        TcpProbeReply reply;
        try
        {
            reply = await probeClient.SendAsync(
                target, DefaultPort, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar TCP: {exception.Message}");
        }

        stopwatch.Stop();

        if (reply.Success)
            return new MonitoringProviderResult(true, stopwatch.Elapsed, null,
                new Dictionary<string, string> { ["port"] = DefaultPort.ToString() });

        return new MonitoringProviderResult(
            false,
            stopwatch.Elapsed,
            $"Porta {DefaultPort} inacessível em {target}.",
            new Dictionary<string, string>
            {
                ["tcp_error"] = reply.Error ?? "Sem resposta."
            });
    }
}
