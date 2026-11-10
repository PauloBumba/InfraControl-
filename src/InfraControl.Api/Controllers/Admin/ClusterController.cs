using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Operations.Cluster;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// Cluster da plataforma (spec evolução): registro administrável de
/// instâncias/réplicas (API, frontend, workers, bancos, filas,
/// integrações), probe de saúde HTTP e visão de distribuição de
/// requisições por papel — tudo editável pelo admin sem mudar código.
/// Somente Admin.
/// </summary>
[Route("api/v1/admin/cluster")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class ClusterController(ISender sender) : BaseController
{
    /// <summary>Lista todas as instâncias registradas.</summary>
    [HttpGet("instances")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchServiceInstancesQuery(), cancellationToken)));

    /// <summary>Overview: instâncias + distribuição por papel + contadores up/down.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetClusterOverviewQuery(), cancellationToken)));

    /// <summary>Cria ou atualiza uma instância (id nulo = create).</summary>
    [HttpPut("instances")]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertServiceInstanceRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new UpsertServiceInstanceRequest();
        return Ok(ApiResponse.Ok(await sender.Send(
            new UpsertServiceInstanceCommand(
                request.Id, request.Kind, request.Name, request.Host,
                request.Port, request.HealthUrl, request.Weight,
                request.IsActive, request.Notes),
            cancellationToken)));
    }

    /// <summary>Remove uma instância do registro.</summary>
    [HttpDelete("instances/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteServiceInstanceCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Probe de saúde de uma instância ("verificar agora").</summary>
    [HttpPost("instances/{id:guid}/check")]
    public async Task<IActionResult> Check(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CheckServiceInstanceHealthCommand(id), cancellationToken)));

    /// <summary>Probe de saúde de todas as instâncias ativas.</summary>
    [HttpPost("check-all")]
    public async Task<IActionResult> CheckAll(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CheckAllInstancesHealthCommand(), cancellationToken)));

    /// <summary>
    /// Tráfego interno da plataforma: volume, erros e latência (P50/P95/P99
    /// aproximados por histograma) por rota — dados reais coletados pelo
    /// middleware de métricas e persistidos no Postgres.
    /// </summary>
    [HttpGet("traffic")]
    public async Task<IActionResult> Traffic(
        [FromQuery] int minutes = 60,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTrafficMetricsQuery(minutes), cancellationToken)));

    /// <summary>Análise do cluster assistida por IA (gargalos + recomendações).</summary>
    [HttpPost("insights")]
    public async Task<IActionResult> Insights(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetClusterInsightsCommand(), cancellationToken)));

    /// <summary>
    /// Containers Docker reais do host (socket montado na API). null =
    /// Docker indisponível — a UI mostra aviso em vez de quebrar.
    /// ?stats=true enriquece com CPU%/memória (one-shot por container, só
    /// para os que estão rodando; chamadas em paralelo).
    /// </summary>
    [HttpGet("docker/containers")]
    public async Task<IActionResult> DockerContainers(
        [FromServices] Application.Operations.Cluster.Ports.IDockerControl docker,
        [FromQuery] bool stats,
        CancellationToken cancellationToken)
    {
        var containers = await docker.ListContainersAsync(cancellationToken);
        if (containers is null)
            return Ok(ApiResponse.Ok(new { available = false, containers = Array.Empty<object>() }));

        if (stats)
        {
            // Enriquece em paralelo: stats one-shot (só running) + inspect
            // (StartedAt/RestartCount/Health) para todos os estados.
            var enriched = await Task.WhenAll(containers.Select(async c =>
            {
                var inspectTask = docker.GetContainerInspectAsync(c.Id, cancellationToken);
                Application.Operations.Cluster.Ports.DockerContainerStats? s = null;
                if (string.Equals(c.State, "running", StringComparison.OrdinalIgnoreCase))
                    s = await docker.GetContainerStatsAsync(c.Id, cancellationToken);
                var inspect = await inspectTask;

                return new Application.Operations.Cluster.Ports.DockerContainerDto(
                    c.Id, c.Name, c.Image, c.State, c.Status,
                    s?.MemoryUsageBytes ?? c.MemoryUsageBytes,
                    s?.CpuPercent ?? c.CpuPercent,
                    inspect?.StartedAt, inspect?.RestartCount, inspect?.HealthStatus,
                    s?.MemoryLimitBytes, s?.MemoryPercent,
                    s?.NetworkRxBytes, s?.NetworkTxBytes,
                    s?.BlockReadBytes, s?.BlockWriteBytes, s?.PidsCurrent,
                    inspect?.CpuLimitCores);
            }));
            containers = enriched;
        }

        return Ok(ApiResponse.Ok(new { available = true, containers }));
    }

    /// <summary>
    /// Topologia Docker dinâmica do projeto: containers (serviço Compose,
    /// redes, portas publicadas, health, reinícios) agrupados por rede —
    /// dados reais do Engine a cada chamada, não snapshot estático.
    /// </summary>
    [HttpGet("docker/topology")]
    public async Task<IActionResult> DockerTopology(
        [FromServices] Application.Operations.Cluster.Ports.IDockerControl docker,
        CancellationToken cancellationToken)
    {
        var topology = await docker.GetTopologyAsync(cancellationToken);
        if (topology is null)
            return Ok(ApiResponse.Ok(new { available = false, containers = Array.Empty<object>(), networks = Array.Empty<object>() }));
        return Ok(ApiResponse.Ok(new { available = true, topology }));
    }

    /// <summary>
    /// Últimas linhas de log do container (?tail=N, default 200, máx 2000).
    /// Leitura privilegiada mas sem efeito colateral — auditada pelo
    /// AccessPolicyMiddleware como o restante do módulo admin/cluster.
    /// </summary>
    [HttpGet("docker/containers/{name}/logs")]
    public async Task<IActionResult> DockerContainerLogs(
        string name,
        [FromServices] Application.Operations.Cluster.Ports.IDockerControl docker,
        [FromQuery] int tail = 200,
        CancellationToken cancellationToken = default)
    {
        var logs = await docker.GetContainerLogsAsync(name, tail, cancellationToken);
        if (logs is null)
            return NotFound(ApiResponse.Fail(new[] { $"Container '{name}' não encontrado ou logs indisponíveis." }));
        return Ok(ApiResponse.Ok(new { name, tail = Math.Clamp(tail, 1, 2000), logs }));
    }

    /// <summary>
    /// Investigação completa de containers por IA: para cada alvo envia
    /// estado, imagem, serviço Compose, inspect (início/reinícios/health/
    /// limites), métricas instantâneas (CPU/mem/rede/I-O/PIDs) e logs.
    /// Body opcional {"container": "nome"} limita a um container
    /// (300 linhas de log); {"service": "nome"} analisa todas as réplicas
    /// de um serviço Compose (120 linhas cada); sem body, até 15 containers
    /// do projeto (80 linhas cada).
    /// </summary>
    [HttpPost("docker/logs-insight")]
    public async Task<IActionResult> DockerLogsInsight(
        [FromBody] DockerLogsInsightRequest? request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDockerLogsInsightCommand(request?.Container, request?.Service),
            cancellationToken)));

    /// <summary>Liga/desliga/reinicia um container (Administer + auditado).</summary>
    [HttpPost("docker/containers/{name}/action")]
    public async Task<IActionResult> DockerContainerAction(
        string name,
        [FromBody] DockerActionRequest request,
        [FromServices] Application.Operations.Cluster.Ports.IDockerControl docker,
        CancellationToken cancellationToken)
    {
        var action = request?.Action?.ToLowerInvariant() switch
        {
            "start" => Application.Operations.Cluster.Ports.DockerContainerAction.Start,
            "stop" => Application.Operations.Cluster.Ports.DockerContainerAction.Stop,
            "restart" => Application.Operations.Cluster.Ports.DockerContainerAction.Restart,
            _ => (Application.Operations.Cluster.Ports.DockerContainerAction?)null,
        };
        if (action is null)
            throw new ArgumentException("Ação inválida — use start, stop ou restart.");

        var ok = await docker.ControlAsync(name, action.Value, cancellationToken);
        if (!ok)
            throw new KeyNotFoundException($"Container '{name}' não encontrado ou ação falhou.");

        // Auditoria da operação privilegiada (AuditBehavior grava a AuditEntry).
        await sender.Send(new RecordDockerActionAuditCommand(name, request!.Action), cancellationToken);
        return Ok(ApiResponse.Ok(new { name, action = request.Action, success = true }));
    }

    /// <summary>
    /// Remove um container PARADO do projeto (spec docs/65 Fase 3): exige
    /// dupla confirmação (confirmName digitado igual ao nome) + motivo —
    /// ambos auditados via RemoveDockerContainerCommand.
    /// </summary>
    [HttpPost("docker/containers/{name}/remove")]
    public async Task<IActionResult> DockerContainerRemove(
        string name,
        [FromBody] DockerRemoveRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveDockerContainerCommand(
            name, request?.Reason ?? string.Empty, request?.ConfirmName ?? string.Empty),
            cancellationToken);
        return Ok(ApiResponse.Ok(new { name, removed = true }));
    }

    /// <summary>
    /// Limites de CPU/memória em runtime (docker update — HostConfig).
    /// Vale até o próximo recreate do container; persistir no compose é
    /// decisão separada. Ao menos um dos dois é obrigatório.
    /// </summary>
    [HttpPost("docker/containers/{name}/resources")]
    public async Task<IActionResult> DockerContainerResources(
        string name,
        [FromBody] DockerResourcesRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateDockerResourceLimitsCommand(
                name, request?.MemoryBytes, request?.CpuCores),
            cancellationToken)));

    /// <summary>
    /// Serviços Compose do projeto com réplicas reais — via compose-agent
    /// (container dedicado que executa `docker compose`). null/agente fora
    /// do ar → 503 com mensagem clara.
    /// </summary>
    [HttpGet("compose/services")]
    public async Task<IActionResult> ComposeServices(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetComposeServicesQuery(), cancellationToken)));

    /// <summary>
    /// Escala um serviço Compose para N réplicas (0..16) via agente —
    /// `docker compose up -d --no-deps --scale svc=N svc`. Administer +
    /// auditado.
    /// </summary>
    [HttpPost("compose/services/{service}/scale")]
    public async Task<IActionResult> ComposeScale(
        string service,
        [FromBody] ComposeScaleRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ScaleComposeServiceCommand(service, request?.Replicas ?? -1),
            cancellationToken)));
}

public sealed class DockerActionRequest
{
    public string Action { get; set; } = string.Empty;
}

public sealed class DockerLogsInsightRequest
{
    public string? Container { get; set; }
    public string? Service { get; set; }
}

public sealed class DockerRemoveRequest
{
    public string Reason { get; set; } = string.Empty;
    public string ConfirmName { get; set; } = string.Empty;
}

public sealed class DockerResourcesRequest
{
    public long? MemoryBytes { get; set; }
    public double? CpuCores { get; set; }
}

public sealed class ComposeScaleRequest
{
    public int Replicas { get; set; } = -1;
}

public sealed class UpsertServiceInstanceRequest
{
    public Guid? Id { get; set; }
    public string Kind { get; set; } = "custom";
    public string Name { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? HealthUrl { get; set; }
    public int Weight { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
