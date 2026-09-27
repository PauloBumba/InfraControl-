using InfraControl.Application.Alerts.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F6-T004: BFS não-dirigido pelas Connections a partir dos vizinhos de
/// um device até achar o primeiro (mais próximo) com Incident Open.
/// Grafo em memória (2 queries LINQ — Sqlite e PG sem dialeto); LAN é
/// pequeno, não precisa de CTE recursiva.
/// </summary>
public sealed class UpstreamCorrelationRepository(AppDbContext dbContext)
    : IUpstreamCorrelationRepository
{
    public async Task<UpstreamIncident?> FindOpenIncidentUpstreamAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        // Arestas device↔device: interface dona (DeviceId) de cada ponta.
        var edges = await dbContext.Connections.AsNoTracking()
            .Join(dbContext.NetworkInterfaces.AsNoTracking(),
                connection => connection.SourceInterfaceId,
                nic => nic.Id,
                (connection, nic) => new { nic.DeviceId, connection.TargetInterfaceId })
            .Join(dbContext.NetworkInterfaces.AsNoTracking(),
                pair => pair.TargetInterfaceId,
                nic => nic.Id,
                (pair, target) => new { From = pair.DeviceId, To = target.DeviceId })
            .ToListAsync(cancellationToken);

        var adjacency = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var edge in edges)
        {
            Add(adjacency, edge.From, edge.To);
            Add(adjacency, edge.To, edge.From); // grafo não-dirigido
        }

        if (!adjacency.TryGetValue(deviceId, out var start))
            return null;

        // BFS por níveis: nível atual = vizinhos ainda não visitados;
        // a cada nível procura Incident Open na ordem de distância.
        var visited = new HashSet<Guid> { deviceId };
        var current = new List<Guid>();
        foreach (var neighbor in start)
        {
            if (visited.Add(neighbor))
                current.Add(neighbor);
        }

        while (current.Count > 0)
        {
            var incidents = await dbContext.Incidents.AsNoTracking()
                .Where(incident =>
                    incident.Status == IncidentStatus.Open
                    && current.Contains(incident.DeviceId.Value))
                .ToListAsync(cancellationToken);

            var hit = incidents
                .OrderBy(incident => current.IndexOf(incident.DeviceId!.Value))
                .FirstOrDefault();

            if (hit is not null)
                return new UpstreamIncident(hit.DeviceId!.Value, hit.Id);

            var next = new List<Guid>();
            foreach (var candidate in current)
            {
                if (!adjacency.TryGetValue(candidate, out var neighbors))
                    continue;

                foreach (var neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                        next.Add(neighbor);
                }
            }

            current = next;
        }

        return null;
    }

    private static void Add(
        Dictionary<Guid, HashSet<Guid>> adjacency, Guid from, Guid to)
    {
        if (!adjacency.TryGetValue(from, out var neighbors))
            adjacency[from] = neighbors = [];
        neighbors.Add(to);
    }
}
