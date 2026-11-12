using System.Diagnostics;
using System.Text;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Chat;
using InfraControl.Application.AI.Context;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Cluster;

// ------------------------------------------------------------------
// DTOs
// ------------------------------------------------------------------

public sealed record ServiceInstanceDto(
    Guid Id,
    string Kind,
    string Name,
    string? Host,
    int? Port,
    string? HealthUrl,
    int Weight,
    bool IsActive,
    string? Notes,
    string? LastStatus,
    DateTime? LastCheckedAt);

public sealed record KindDistributionDto(
    string Kind,
    int InstanceCount,
    int TotalWeight,
    IReadOnlyList<InstanceShareDto> Instances);

public sealed record InstanceShareDto(
    Guid Id,
    string Name,
    int Weight,
    double SharePercent);

public sealed record ClusterOverviewDto(
    IReadOnlyList<ServiceInstanceDto> Instances,
    IReadOnlyList<KindDistributionDto> Distribution,
    int UpCount,
    int DownCount,
    int UnknownCount);

public sealed record ClusterInsightDto(
    string Analysis,
    string Provider,
    string Model,
    bool UsedFallback,
    long DurationMs);

// ------------------------------------------------------------------
// Queries / Commands
// ------------------------------------------------------------------

public sealed record SearchServiceInstancesQuery
    : IQuery<IReadOnlyList<ServiceInstanceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed record GetClusterOverviewQuery
    : IQuery<ClusterOverviewDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed record UpsertServiceInstanceCommand(
    Guid? Id,
    string Kind,
    string Name,
    string? Host,
    int? Port,
    string? HealthUrl,
    int Weight,
    bool IsActive,
    string? Notes)
    : BaseCommand<ServiceInstanceDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "ServiceInstance";
}

public sealed record DeleteServiceInstanceCommand(Guid Id)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "ServiceInstance";
}

/// <summary>Probe de saúde de UMA instância (botão "verificar agora").</summary>
public sealed record CheckServiceInstanceHealthCommand(Guid Id)
    : BaseCommand<ServiceInstanceDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "ServiceInstance";
    public IReadOnlyCollection<string> CacheKeysToInvalidate => [];
}

/// <summary>Probe de saúde de TODAS as instâncias ativas.</summary>
public sealed record CheckAllInstancesHealthCommand
    : BaseCommand<ClusterOverviewDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "ServiceInstance";
}

/// <summary>Análise do cluster assistida por IA (spec: "a IA ajuda o admin").</summary>
public sealed record GetClusterInsightsCommand
    : BaseCommand<ClusterInsightDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

// ------------------------------------------------------------------
// Handlers
// ------------------------------------------------------------------

public sealed class SearchServiceInstancesQueryHandler(IServiceInstanceRepository repo)
    : IRequestHandler<SearchServiceInstancesQuery, IReadOnlyList<ServiceInstanceDto>>
{
    public async Task<IReadOnlyList<ServiceInstanceDto>> Handle(
        SearchServiceInstancesQuery request, CancellationToken cancellationToken) =>
        (await repo.ListAsync(cancellationToken)).Select(ToDto).ToArray();

    internal static ServiceInstanceDto ToDto(ServiceInstance i) => new(
        i.Id, i.Kind, i.Name, i.Host, i.Port, i.HealthUrl, i.Weight,
        i.IsActive, i.Notes, i.LastStatus, i.LastCheckedAt);
}

public sealed class UpsertServiceInstanceCommandHandler(IServiceInstanceRepository repo)
    : IRequestHandler<UpsertServiceInstanceCommand, ServiceInstanceDto>
{
    public async Task<ServiceInstanceDto> Handle(
        UpsertServiceInstanceCommand request, CancellationToken cancellationToken)
    {
        ServiceInstance instance;
        if (request.Id is { } id)
        {
            instance = await repo.FindAsync(id, cancellationToken)
                ?? throw new KeyNotFoundException("Instância não encontrada.");
            instance.Update(
                request.Kind, request.Name, request.Host, request.Port,
                request.HealthUrl, request.Weight, request.IsActive, request.Notes);
        }
        else
        {
            instance = ServiceInstance.Create(
                request.Kind, request.Name, request.Host, request.Port,
                request.HealthUrl, request.Weight, request.IsActive, request.Notes);
            await repo.AddAsync(instance, cancellationToken);
        }

        await repo.SaveChangesAsync(cancellationToken);
        return SearchServiceInstancesQueryHandler.ToDto(instance);
    }
}

public sealed class DeleteServiceInstanceCommandHandler(IServiceInstanceRepository repo)
    : IRequestHandler<DeleteServiceInstanceCommand>
{
    public async Task Handle(
        DeleteServiceInstanceCommand request, CancellationToken cancellationToken)
    {
        var instance = await repo.FindAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Instância não encontrada.");
        repo.Remove(instance);
        await repo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CheckServiceInstanceHealthCommandHandler(
    IServiceInstanceRepository repo,
    IServiceHealthProbe probe)
    : IRequestHandler<CheckServiceInstanceHealthCommand, ServiceInstanceDto>
{
    public async Task<ServiceInstanceDto> Handle(
        CheckServiceInstanceHealthCommand request, CancellationToken cancellationToken)
    {
        var instance = await repo.FindAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Instância não encontrada.");

        var status = await ProbeAsync(instance, probe, cancellationToken);
        instance.RecordHealthCheck(status);
        await repo.SaveChangesAsync(cancellationToken);
        return SearchServiceInstancesQueryHandler.ToDto(instance);
    }

    internal static async Task<string> ProbeAsync(
        ServiceInstance instance,
        IServiceHealthProbe probe,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instance.HealthUrl))
            return "unknown";
        var healthy = await probe.IsHealthyAsync(instance.HealthUrl, cancellationToken);
        return healthy ? "up" : "down";
    }
}

public sealed class CheckAllInstancesHealthCommandHandler(
    IServiceInstanceRepository repo,
    IServiceHealthProbe probe)
    : IRequestHandler<CheckAllInstancesHealthCommand, ClusterOverviewDto>
{
    public async Task<ClusterOverviewDto> Handle(
        CheckAllInstancesHealthCommand request, CancellationToken cancellationToken)
    {
        var instances = await repo.ListAsync(cancellationToken);
        foreach (var instance in instances.Where(i => i.IsActive))
        {
            var status = await CheckServiceInstanceHealthCommandHandler
                .ProbeAsync(instance, probe, cancellationToken);
            instance.RecordHealthCheck(status);
        }
        await repo.SaveChangesAsync(cancellationToken);
        return ClusterOverviewBuilder.Build(instances);
    }
}

public sealed class GetClusterOverviewQueryHandler(IServiceInstanceRepository repo)
    : IRequestHandler<GetClusterOverviewQuery, ClusterOverviewDto>
{
    public async Task<ClusterOverviewDto> Handle(
        GetClusterOverviewQuery request, CancellationToken cancellationToken) =>
        ClusterOverviewBuilder.Build(await repo.ListAsync(cancellationToken));
}

internal static class ClusterOverviewBuilder
{
    public static ClusterOverviewDto Build(IReadOnlyList<ServiceInstance> instances)
    {
        var dtos = instances.Select(SearchServiceInstancesQueryHandler.ToDto).ToArray();

        var distribution = instances
            .GroupBy(i => i.Kind)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var active = g.Where(i => i.IsActive).ToArray();
                var totalWeight = active.Sum(i => i.Weight);
                var shares = active.Select(i => new InstanceShareDto(
                    i.Id, i.Name, i.Weight,
                    totalWeight > 0 ? Math.Round(100.0 * i.Weight / totalWeight, 1) : 0)).ToArray();
                return new KindDistributionDto(g.Key, g.Count(), totalWeight, shares);
            })
            .ToArray();

        return new ClusterOverviewDto(
            dtos,
            distribution,
            dtos.Count(d => d.LastStatus == "up"),
            dtos.Count(d => d.LastStatus == "down"),
            dtos.Count(d => d.LastStatus is null or "unknown"));
    }
}

public sealed class GetClusterInsightsCommandHandler(
    IServiceInstanceRepository repo,
    IDockerControl docker,
    IAiGateway gateway,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetClusterInsightsCommand, ClusterInsightDto>
{
    public async Task<ClusterInsightDto> Handle(
        GetClusterInsightsCommand request, CancellationToken cancellationToken)
    {
        var overview = ClusterOverviewBuilder.Build(await repo.ListAsync(cancellationToken));
        var dockerSection = await DockerSectionAsync(cancellationToken);

        var prompt = $$"""
            Você é o assistente de capacidade do cluster InfraControl. Analise o estado
            das instâncias abaixo e responda em português, de forma objetiva:
            1) gargalos prováveis (instância down, peso desbalanceado, tipo sem redundância);
            2) recomendações concretas (adicionar réplica, ajustar peso, criar instância);
            3) risco de indisponibilidade por papel (api/frontend/worker/database/cache/queue/integration).
            Não invente métricas — use apenas os dados fornecidos.

            Instâncias:
            {{string.Join("\n", overview.Instances.Select(i =>
                $"- [{i.Kind}] {i.Name} host={i.Host ?? "?"}:{i.Port?.ToString() ?? "?"} " +
                $"status={i.LastStatus ?? "nunca verificado"} peso={i.Weight} ativo={i.IsActive}"))}}

            Distribuição por papel:
            {{string.Join("\n", overview.Distribution.Select(d =>
                $"- {d.Kind}: {d.InstanceCount} instância(s), peso total {d.TotalWeight}"))}}

            Containers Docker reais do host (estado + métricas instantâneas;
            seção vazia quando o socket não está acessível):
            {{dockerSection}}
            """;

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Reporting,
                prompt,
                SystemPrompt: "Você é um SRE analisando a topologia de um cluster de aplicações. Responda em markdown curto.",
                Actor: currentUser.UserName),
            cancellationToken);

        return new ClusterInsightDto(
            completion.Text,
            completion.ProviderName,
            completion.Model,
            completion.UsedFallback,
            completion.DurationMs);
    }

    /// <summary>
    /// Estado real dos containers Docker (lista + CPU/mem dos que rodam).
    /// Socket indisponível ⇒ texto explícito para a IA não inventar.
    /// </summary>
    private async Task<string> DockerSectionAsync(CancellationToken cancellationToken)
    {
        var containers = await docker.ListContainersAsync(cancellationToken);
        if (containers is null)
            return "(Docker indisponível — socket não montado ou sem permissão)";
        if (containers.Count == 0)
            return "(nenhum container no host)";

        var lines = new List<string>();
        foreach (var c in containers)
        {
            var stats = string.Equals(c.State, "running", StringComparison.OrdinalIgnoreCase)
                ? await docker.GetContainerStatsAsync(c.Id, cancellationToken)
                : null;
            var metrics = stats is null
                ? string.Empty
                : $" cpu={stats.CpuPercent?.ToString("0.#") ?? "?"}% mem={FormatMb(stats.MemoryUsageBytes)}";
            lines.Add($"- {c.Name} [{c.State}] {c.Status}{metrics}");
        }
        return string.Join("\n", lines);
    }

    private static string FormatMb(long? bytes)
        => bytes is null ? "?" : $"{bytes / 1024.0 / 1024.0:0.#}MB";
}

// ------------------------------------------------------------------
// Investigação de logs com IA (spec: "MCP de logs do cluster")
// ------------------------------------------------------------------

/// <summary>
/// Investigação completa de containers por IA: para cada alvo envia estado,
/// imagem, serviço Compose, inspect (início/reinícios/health/limites),
/// métricas instantâneas (CPU/mem/rede/I-O/PIDs) e logs recentes.
/// Escopo: um container, um serviço Compose inteiro (todas as réplicas) ou
/// o projeto (até 15 containers).
/// </summary>
public sealed record GetDockerLogsInsightCommand(
    string? ContainerName = null,
    string? ServiceName = null)
    : BaseCommand<ClusterInsightDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetDockerLogsInsightCommandHandler(
    IDockerControl docker,
    IComposeAgent composeAgent,
    IAiGateway gateway,
    IRetrievalService retrieval,
    IContextBuilder contextBuilder,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetDockerLogsInsightCommand, ClusterInsightDto>
{
    private const int TailLines = 80;
    private const int TailLinesSingle = 300;
    private const int TailLinesService = 120;
    private const int MaxContainers = 15;
    private const int MaxServiceContainers = 8;

    /// <summary>Alvo da investigação: identidade + contexto já resolvido.</summary>
    private sealed record InsightTarget(
        string Name,
        string State,
        string Status,
        string? Image,
        string? Service);

    public async Task<ClusterInsightDto> Handle(
        GetDockerLogsInsightCommand request, CancellationToken cancellationToken)
    {
        var containers = await docker.ListContainersAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Docker indisponível — verifique a montagem de /var/run/docker.sock no serviço api.");

        // Topologia uma vez: serviço Compose por container (contexto p/ a IA).
        var topology = await docker.GetTopologyAsync(cancellationToken);
        var serviceByContainer = (topology?.Containers ?? [])
            .GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Service, StringComparer.OrdinalIgnoreCase);

        string? ServiceOf(string name)
            => serviceByContainer.TryGetValue(name.TrimStart('/'), out var s) ? s : null;

        DockerContainerDto? FindContainer(string name)
            => containers.FirstOrDefault(c =>
                string.Equals(c.Name, name.TrimStart('/'), StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        // Alvo: container único, serviço inteiro ou projeto.
        IReadOnlyList<InsightTarget> targets;
        if (!string.IsNullOrWhiteSpace(request.ServiceName))
        {
            var services = await composeAgent.ListServicesAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Agente de composição indisponível — verifique o serviço compose-agent no compose.");
            var service = services.FirstOrDefault(s =>
                string.Equals(s.Service, request.ServiceName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            if (service is null)
                throw new KeyNotFoundException(
                    $"Serviço '{request.ServiceName}' não encontrado no projeto Compose.");
            targets = service.Containers
                .Take(MaxServiceContainers)
                .Select(c => new InsightTarget(
                    c.Name, c.State, c.Status, FindContainer(c.Name)?.Image, service.Service))
                .ToArray();
            if (targets.Count == 0)
                throw new KeyNotFoundException(
                    $"O serviço '{service.Service}' não tem containers para analisar.");
        }
        else if (!string.IsNullOrWhiteSpace(request.ContainerName))
        {
            var name = request.ContainerName.Trim();
            var found = FindContainer(name);
            targets = found is null
                ? []
                : [new InsightTarget(
                    found.Name, found.State, found.Status, found.Image, ServiceOf(found.Name))];
            if (targets.Count == 0)
                throw new KeyNotFoundException(
                    $"Container '{name}' não encontrado no projeto.");
        }
        else
        {
            targets = containers
                .Select(c => new InsightTarget(c.Name, c.State, c.Status, c.Image, ServiceOf(c.Name)))
                .Take(MaxContainers)
                .ToArray();
        }

        var tailLines = request.ContainerName is not null
            ? TailLinesSingle
            : request.ServiceName is not null ? TailLinesService : TailLines;

        // RAG (ADR-038): conhecimento recuperado (KB/runbooks/incidentes)
        // ancora a análise — evidências citáveis [E#], nunca invenção.
        var term = request.ContainerName ?? request.ServiceName ?? "containers docker infraestrutura";
        var retrievalResult = await retrieval.RetrieveAsync(
            new RetrievalRequest(term, Limit: 6), cancellationToken);
        var evidenceSet = contextBuilder.Build(
            retrievalResult.Hits, Activity.Current?.TraceId.ToString(), tool: "docker-insight");
        var evidenceBlock = FormatEvidences(evidenceSet);

        // Coleta uma vez por container; o formato é refeito com orçamento
        // decrescente de chars de log até caber no limite do AI Gateway.
        var dossiers = await Task.WhenAll(
            targets.Select(t => CollectAsync(t, tailLines, cancellationToken)));

        var sectionBudget = Math.Max(4000, 10000 - evidenceBlock.Length);
        const int HardCap = 11500;
        var logCharLimit = 8000;
        string promptBody;
        while (true)
        {
            promptBody = string.Join("\n\n",
                dossiers.Select(d => FormatSection(d, logCharLimit)));
            if (promptBody.Length <= sectionBudget || logCharLimit <= 80)
                break;
            logCharLimit = Math.Max(80, logCharLimit / 2);
        }
        if (promptBody.Length + evidenceBlock.Length > HardCap)
        {
            var keep = Math.Max(0, HardCap - evidenceBlock.Length - 120);
            promptBody = promptBody[..Math.Min(promptBody.Length, keep)] +
                "\n…(contexto truncado — investigue um container ou serviço para mais detalhe)";
        }

        var prompt = $$"""
            Você é um SRE investigando a saúde de containers Docker do cluster
            InfraControl. Para cada container você recebe: estado, imagem,
            serviço Compose, início, reinícios, healthcheck, limites de
            CPU/memória e métricas instantâneas (CPU%, memória usada/limite,
            rede RX/TX, I/O disco, PIDs), seguidos dos logs recentes. Cruze os
            sinais — ex.: reinícios altos + OOM, CPU no limite + timeouts nos
            logs, memória no teto + degradação. Responda em português:
            1) problemas reais encontrados (cite o container e a evidência);
            2) padrões de falha (restart, OOM, timeout, permissão, conexão,
               saturação de recurso);
            3) recomendação objetiva por container (corrigir config, escalar,
               investigar dependência, ignorar ruído conhecido).
            Ignore ruído cosmético (healthchecks, probes). Não invente
            problemas que não estejam nos dados.
            Se houver Evidências de conhecimento, use-as como base das
            recomendações e cite-as como [E1], [E2]…; se não houver evidência
            ou dado suficiente para uma conclusão, declare a incerteza em
            vez de supor.

            {{promptBody}}

            {{evidenceBlock}}
            """;

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Reporting,
                prompt,
                SystemPrompt: "Você é um SRE analisando containers Docker (estado, métricas, logs e evidências de conhecimento). Responda em markdown curto e objetivo; cite evidências [E#] e nunca invente fatos.",
                TraceId: Activity.Current?.TraceId.ToString(),
                Actor: currentUser.UserName),
            cancellationToken);

        return new ClusterInsightDto(
            completion.Text,
            completion.ProviderName,
            completion.Model,
            completion.UsedFallback,
            completion.DurationMs);
    }

    /// <summary>Dossiê coletado de um container: alvo + inspect + métricas + logs.</summary>
    private sealed record ContainerDossier(
        InsightTarget Target,
        DockerContainerStats? Stats,
        DockerContainerInspectDto? Inspect,
        string? Logs);

    /// <summary>
    /// Coleta o dossiê de um container: stats/inspect/logs em paralelo.
    /// Falhas individuais viram null — a IA nunca recebe exceção no lugar
    /// do dado (o formato mostra "?"/"(sem …)").
    /// </summary>
    private async Task<ContainerDossier> CollectAsync(
        InsightTarget target, int tailLines, CancellationToken cancellationToken)
    {
        var running = string.Equals(target.State, "running", StringComparison.OrdinalIgnoreCase);
        var statsTask = running
            ? docker.GetContainerStatsAsync(target.Name, cancellationToken)
            : Task.FromResult<DockerContainerStats?>(null);
        var inspectTask = docker.GetContainerInspectAsync(target.Name, cancellationToken);
        var logsTask = docker.GetContainerLogsAsync(target.Name, tailLines, cancellationToken);
        await Task.WhenAll(statsTask, inspectTask, logsTask);

        return new ContainerDossier(
            target, statsTask.Result, inspectTask.Result, logsTask.Result);
    }

    /// <summary>
    /// Formata o dossiê para o prompt. O tail de log é truncado em
    /// <paramref name="logCharLimit"/> chars mantendo o FINAL (o trecho
    /// mais recente é o relevante); métricas/identidade nunca são cortadas.
    /// </summary>
    private static string FormatSection(ContainerDossier d, int logCharLimit)
    {
        var target = d.Target;
        var stats = d.Stats;
        var inspect = d.Inspect;

        var identity = $"Imagem: {target.Image ?? "?"} · Serviço Compose: {target.Service ?? "—"}" +
            $" · Início: {inspect?.StartedAt ?? "?"} · Reinícios: {inspect?.RestartCount.ToString() ?? "?"}" +
            $" · Health: {inspect?.HealthStatus ?? "—"}";

        var resources = stats is null
            ? "(sem métricas — container parado ou stats indisponível)"
            : $"CPU {Pct(stats.CpuPercent)} (limite {inspect?.CpuLimitCores?.ToString("0.#") ?? "?"} cores)" +
              $" · Memória {FormatBytes(stats.MemoryUsageBytes)}/{FormatBytes(stats.MemoryLimitBytes ?? inspect?.MemoryLimitBytes)} ({Pct(stats.MemoryPercent)})" +
              $" · Rede ↓{FormatBytes(stats.NetworkRxBytes)}/↑{FormatBytes(stats.NetworkTxBytes)}" +
              $" · I/O disco leitura {FormatBytes(stats.BlockReadBytes)}/escrita {FormatBytes(stats.BlockWriteBytes)}" +
              $" · PIDs {stats.PidsCurrent?.ToString() ?? "?"}";

        var logText = d.Logs ?? "(sem logs)";
        if (logText.Length > logCharLimit)
        {
            logText = "…(início dos logs truncado)\n" +
                logText[^Math.Max(0, logCharLimit - 40)..];
        }

        return $"### {target.Name} [{target.State}] {target.Status}\n{identity}\n{resources}\n" +
            $"--- logs (últimas {logCharLimit} chars) ---\n{logText}";
    }

    /// <summary>
    /// Evidências do Context Engine (ADR-038) formatadas para o prompt:
    /// citáveis como [E#], dados truncados para caber no orçamento do gateway.
    /// </summary>
    private static string FormatEvidences(EvidenceSet evidenceSet)
    {
        if (evidenceSet.IsEmpty)
        {
            return "Evidências de conhecimento: NENHUMA recuperada — " +
                "baseie-se apenas nos dados dos containers e declare incerteza.";
        }

        var builder = new StringBuilder("Evidências de conhecimento (cite como [E1], [E2]…):");
        for (var i = 0; i < evidenceSet.Evidences.Count; i++)
        {
            var e = evidenceSet.Evidences[i];
            var data = e.Data.Length > 500 ? e.Data[..500] + "…" : e.Data;
            builder.Append($"\n[E{i + 1}] ({e.Source} · {e.Confidence} · {e.TimestampUtc:yyyy-MM-dd}) {data}");
        }
        return builder.ToString();
    }

    private static string Pct(double? value) => value is null ? "?" : $"{value:0.#}%";

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null) return "?";
        var b = (double)bytes;
        if (b >= 1073741824) return $"{b / 1073741824:0.#}GB";
        if (b >= 1048576) return $"{b / 1048576:0.#}MB";
        if (b >= 1024) return $"{b / 1024:0.#}KB";
        return $"{b:0}B";
    }
}
