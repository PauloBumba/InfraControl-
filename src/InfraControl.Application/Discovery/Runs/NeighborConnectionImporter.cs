using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.Application.Network.Connections.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Discovery.Runs;

// F3-T005b: ingestão automática de Connections ao fim do run de discovery
// (decisões do usuário: gatilho AUTOMÁTICO, mesmo SaveChanges do run;
// erro de um achado não derruba o run). Casa achado Neighbor:
//   local    = (device do run, local_if_name)   — providers LLDP/CDP
//   vizinho  = (DeviceId do achado via F4-T010, port_desc/port_id/device_port)
// dedupe por par dirigido; self-loop/vizinho/interface ausente → pula.

/// <summary>Vizinho candidato a Connection: device já vinculado (F4-T010)
/// + atributos do achado (local_if_name, port_desc…).</summary>
public sealed record NeighborCandidate(
    Guid DeviceId,
    IReadOnlyDictionary<string, string> Values);

public interface INeighborConnectionImporter
{
    /// <summary>Tenta importar Connections para o device local do run;
    /// retorna quantas foram criadas (sem SaveChanges — o run persiste).</summary>
    Task<int> ImportAsync(
        Guid localDeviceId,
        IReadOnlyList<NeighborCandidate> candidates,
        CancellationToken cancellationToken = default);
}

public sealed class NeighborConnectionImporter(
    INetworkInterfaceRepository interfaceRepository,
    IConnectionRepository connectionRepository,
    ILogger<NeighborConnectionImporter> logger)
    : INeighborConnectionImporter
{
    private static readonly string[] RemotePortKeys =
        ["port_desc", "port_id", "device_port"];

    public async Task<int> ImportAsync(
        Guid localDeviceId,
        IReadOnlyList<NeighborCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        var created = 0;
        var seenPairs = new HashSet<(Guid Source, Guid Target)>();

        foreach (var candidate in candidates)
        {
            try
            {
                var values = candidate.Values;

                if (!values.TryGetValue("local_if_name", out var localIf)
                    || string.IsNullOrWhiteSpace(localIf))
                    continue;

                var remoteIf = FirstValue(values, RemotePortKeys);
                if (string.IsNullOrWhiteSpace(remoteIf))
                    continue;

                var localNic = await interfaceRepository.GetByNameAsync(
                    localDeviceId, localIf.Trim(), cancellationToken);
                if (localNic is null)
                    continue;

                var remoteNic = await interfaceRepository.GetByNameAsync(
                    candidate.DeviceId, remoteIf.Trim(), cancellationToken);
                if (remoteNic is null)
                    continue;

                if (localNic.Id == remoteNic.Id)
                    continue;

                if (!seenPairs.Add((localNic.Id, remoteNic.Id)))
                    continue;

                if (await connectionRepository.GetPairAsync(
                        localNic.Id, remoteNic.Id, cancellationToken) is not null)
                    continue;

                await connectionRepository.AddAsync(
                    Connection.Create(localNic.Id, remoteNic.Id), cancellationToken);
                created++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Um achado ruim não derruba a ingestão nem o run.
                logger.LogWarning(
                    exception,
                    "Ingestão pulou vizinho do device {DeviceId}.",
                    localDeviceId);
            }
        }

        return created;
    }

    private static string? FirstValue(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
