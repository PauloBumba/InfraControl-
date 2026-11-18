using InfraControl.Application.Messaging.Ports;
using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Messaging.Workers;

/// <summary>
/// F55-T003: Worker Management — visão honesta do que existe de verdade:
/// consumidores ativos do broker (Management API) × containers da
/// aplicação (Docker Engine via <see cref="IDockerControl"/>). Campo sem
/// fonte real fica null — a UI mostra "indisponível", nunca inventa.
/// </summary>
public sealed record MessagingWorkerDto(
    string Id,
    string Kind,               // "container" | "consumer-group"
    string Name,               // container_name ou fila consumida
    string? Detail,            // imagem / exchange→fila
    string? State,             // running/paused/unavailable
    string? ConsumedQueue,
    int ConsumerCount,
    double? CpuPercent,
    long? MemoryUsageBytes,
    double? DeliverRatePerSecond,
    long? DeliveredTotal,
    long? MessagesReady,
    long? MessagesUnacked,
    string? PeerHost,
    string? ConnectionName,
    bool BrokerReachable,
    bool DockerReachable,
    string? PausedAtUtc);

public sealed record MessagingWorkersResult(
    bool BrokerReachable,
    string? BrokerUnavailableReason,
    bool DockerReachable,
    IReadOnlyList<MessagingWorkerDto> Workers);

public sealed record GetMessagingWorkersQuery
    : BaseQuery<MessagingWorkersResult>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingWorkersQueryHandler(
    IRabbitMqManagementClient broker,
    IDockerControl docker,
    IMessagingPauseStore pauses)
    : IRequestHandler<GetMessagingWorkersQuery, MessagingWorkersResult>
{
    public async Task<MessagingWorkersResult> Handle(
        GetMessagingWorkersQuery request, CancellationToken cancellationToken)
    {
        var consumersTask = broker.GetConsumersAsync(cancellationToken);
        var containersTask = docker.ListContainersAsync(cancellationToken);

        var consumers = await consumersTask;
        var containers = await containersTask;

        var workers = new List<MessagingWorkerDto>();

        // Instâncias de container da aplicação (workers/api rodam os consumers).
        if (containers is not null)
        {
            foreach (var c in containers.Where(IsMessagingContainer))
            {
                var stats = await docker.GetContainerStatsAsync(c.Id, cancellationToken);
                workers.Add(new MessagingWorkerDto(
                    Id: $"container:{c.Id}",
                    Kind: "container",
                    Name: c.Name,
                    Detail: c.Image,
                    State: c.State,
                    ConsumedQueue: null,
                    ConsumerCount: 0,
                    CpuPercent: stats?.CpuPercent,
                    MemoryUsageBytes: stats?.MemoryUsageBytes,
                    DeliverRatePerSecond: null,
                    DeliveredTotal: null,
                    MessagesReady: null,
                    MessagesUnacked: null,
                    PeerHost: null,
                    ConnectionName: null,
                    BrokerReachable: consumers.Reachable,
                    DockerReachable: true,
                    PausedAtUtc: null));
            }
        }

        // Consumers reais do broker agrupados por fila — "quem consome o quê".
        if (consumers.Reachable)
        {
            foreach (var group in consumers.Items
                         .GroupBy(c => c.QueueName)
                         .OrderBy(g => g.Key))
            {
                var first = group.First();
                var pause = await pauses.GetAsync(group.Key, cancellationToken);
                workers.Add(new MessagingWorkerDto(
                    Id: $"consumer:{group.Key}",
                    Kind: "consumer-group",
                    Name: group.Key,
                    Detail: $"{group.Count()} consumer(s) · prefetch {first.PrefetchCount}",
                    State: pause is not null
                        ? "paused"
                        : group.All(c => c.Active) ? "running" : "degraded",
                    ConsumedQueue: group.Key,
                    ConsumerCount: group.Count(),
                    CpuPercent: null,
                    MemoryUsageBytes: null,
                    DeliverRatePerSecond: null,
                    DeliveredTotal: null,
                    MessagesReady: null,
                    MessagesUnacked: null,
                    PeerHost: first.PeerHost,
                    ConnectionName: first.ConnectionName,
                    BrokerReachable: true,
                    DockerReachable: containers is not null,
                    PausedAtUtc: pause?.PausedAtUtc.ToString("O")));
            }
        }

        // Pausas ativas: a fila pausada não tem consumers no broker, então
        // precisa aparecer como entrada própria para a UI poder retomar.
        var activePauses = await pauses.ListAsync(cancellationToken);
        var groupedQueues = consumers.Reachable
            ? consumers.Items.Select(c => c.QueueName).ToHashSet()
            : [];
        foreach (var pause in activePauses.Where(p => !groupedQueues.Contains(p.QueueName)))
        {
            workers.Add(new MessagingWorkerDto(
                Id: $"paused:{pause.QueueName}",
                Kind: "consumer-group",
                Name: pause.QueueName,
                Detail: "consumo pausado via Management API",
                State: "paused",
                ConsumedQueue: pause.QueueName,
                ConsumerCount: 0,
                CpuPercent: null,
                MemoryUsageBytes: null,
                DeliverRatePerSecond: null,
                DeliveredTotal: null,
                MessagesReady: null,
                MessagesUnacked: null,
                PeerHost: null,
                ConnectionName: null,
                BrokerReachable: consumers.Reachable,
                DockerReachable: containers is not null,
                PausedAtUtc: pause.PausedAtUtc.ToString("O")));
        }

        return new MessagingWorkersResult(
            BrokerReachable: consumers.Reachable,
            BrokerUnavailableReason: consumers.UnavailableReason,
            DockerReachable: containers is not null,
            Workers: workers);
    }

    private static bool IsMessagingContainer(DockerContainerDto c)
    {
        var name = c.Name.ToLowerInvariant();
        return name.Contains("worker") || name.Contains("api") || name.Contains("frontend");
    }
}
