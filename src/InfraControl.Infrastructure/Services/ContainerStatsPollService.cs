using InfraControl.Application.Operations.Cluster.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Poll do Engine API a cada 30s e publica gauges OTLP por serviço Compose.
/// Substitui o cAdvisor para métricas de container — com containerd
/// snapshotter o cAdvisor não resolve os mounts e não lista container algum.
/// </summary>
public sealed class ContainerStatsPollService(
    IServiceScopeFactory scopeFactory,
    ILogger<ContainerStatsPollService> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(
        Math.Max(5, configuration.GetValue("ContainerMetrics:IntervalSeconds", 30)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var docker = scope.ServiceProvider.GetRequiredService<IDockerControl>();

            var containers = await docker.ListContainersAsync(cancellationToken);
            if (containers is null)
                return; // Docker indisponível — mantém o último snapshot.

            var running = containers.Where(c =>
                c.State.Equals("running", StringComparison.OrdinalIgnoreCase)).ToArray();

            var samples = new List<ContainerMetrics.Sample>(running.Length);
            foreach (var c in running)
            {
                var stats = await docker.GetContainerStatsAsync(c.Id, cancellationToken);
                samples.Add(new ContainerMetrics.Sample(
                    Service: ServiceName(c.Name),
                    Container: c.Name,
                    CpuPercent: stats?.CpuPercent ?? c.CpuPercent,
                    MemoryPercent: stats?.MemoryPercent ?? c.MemoryPercent,
                    MemoryBytes: stats?.MemoryUsageBytes ?? c.MemoryUsageBytes,
                    NetworkRxBytes: stats?.NetworkRxBytes,
                    NetworkTxBytes: stats?.NetworkTxBytes));
            }

            ContainerMetrics.Publish(samples);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Container stats poll failed.");
        }
    }

    /// <summary>
    /// "infracontrol--api-1" → "api"; "infracontrol-postgres" → "postgres".
    /// Remove o prefixo do projeto Compose e o sufixo de réplica "-N".
    /// </summary>
    internal static string ServiceName(string containerName)
    {
        var name = containerName.TrimStart('/');
        var project = (Environment.GetEnvironmentVariable("COMPOSE_PROJECT_NAME") ?? "infracontrol-")
            .TrimEnd('-');
        if (name.StartsWith(project + "-", StringComparison.OrdinalIgnoreCase))
            name = name[(project.Length + 1)..];
        name = name.TrimStart('-');

        var lastDash = name.LastIndexOf('-');
        if (lastDash > 0 && int.TryParse(name[(lastDash + 1)..], out _))
            name = name[..lastDash];
        return name;
    }
}
