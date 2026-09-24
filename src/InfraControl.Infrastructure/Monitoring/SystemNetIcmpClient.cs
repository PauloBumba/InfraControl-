using System.Net;
using System.Net.NetworkInformation;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// ICMP real via <see cref="System.Net.NetworkInformation.Ping"/> (F5-T003).
/// Falhas de rede viram <see cref="IcmpReply"/> com erro em pt-BR — nunca estouram.
/// </summary>
public sealed class SystemNetIcmpClient : IIcmpClient
{
    public async Task<IcmpReply> SendAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(target, out var address))
            return new IcmpReply(false, 0, $"Destino inválido: '{target}' não é um IP válido.");

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(
                address,
                timeout,
                [],
                new PingOptions(),
                cancellationToken);

            return reply.Status == IPStatus.Success
                ? new IcmpReply(true, reply.RoundtripTime, null)
                : new IcmpReply(false, 0, MapStatus(reply.Status));
        }
        catch (PingException ex)
        {
            return new IcmpReply(false, 0, $"Ping falhou: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return new IcmpReply(false, 0, $"Destino inválido: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return new IcmpReply(false, 0, $"Não foi possível executar o ping: {ex.Message}");
        }
    }

    private static string MapStatus(IPStatus status) => status switch
    {
        IPStatus.TimedOut => "Tempo esgotado (timeout).",
        IPStatus.DestinationHostUnreachable => "Host inacessível.",
        IPStatus.DestinationNetworkUnreachable => "Rede de destino inacessível.",
        IPStatus.DestinationUnreachable => "Destino inacessível.",
        IPStatus.HardwareError => "Erro de hardware no caminho.",
        IPStatus.TimeExceeded => "Tempo excedido no roteamento.",
        _ => $"Resposta ICMP: {status}."
    };
}
