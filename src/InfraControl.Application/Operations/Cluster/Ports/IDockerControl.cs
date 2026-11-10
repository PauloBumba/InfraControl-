namespace InfraControl.Application.Operations.Cluster.Ports;

/// <summary>
/// Estado de um container Docker visto pela API. Os campos de detalhe
/// (HealthStatus, RestartCount, StartedAt) só são preenchidos quando a
/// listagem é enriquecida (?stats=true) — o caminho barato usa /containers/json.
/// </summary>
public sealed record DockerContainerDto(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    long? MemoryUsageBytes,
    double? CpuPercent,
    string? StartedAt = null,
    long? RestartCount = null,
    string? HealthStatus = null,
    long? MemoryLimitBytes = null,
    double? MemoryPercent = null,
    long? NetworkRxBytes = null,
    long? NetworkTxBytes = null,
    long? BlockReadBytes = null,
    long? BlockWriteBytes = null,
    long? PidsCurrent = null,
    double? CpuLimitCores = null);

/// <summary>
/// Métricas instantâneas de um container (one-shot /containers/{id}/stats).
/// Rede/I-O são somas dos counters do cgroup; PidsCurrent vem de pids_stats.
/// </summary>
public sealed record DockerContainerStats(
    double? CpuPercent,
    long? MemoryUsageBytes,
    long? MemoryLimitBytes = null,
    double? MemoryPercent = null,
    long? NetworkRxBytes = null,
    long? NetworkTxBytes = null,
    long? BlockReadBytes = null,
    long? BlockWriteBytes = null,
    long? PidsCurrent = null);

/// <summary>Detalhe de inspect: início, reinícios, healthcheck e limites.</summary>
public sealed record DockerContainerInspectDto(
    string? StartedAt,
    long RestartCount,
    string? HealthStatus,
    double? CpuLimitCores = null,
    long? MemoryLimitBytes = null);

/// <summary>Nó da topologia Docker dinâmica (container do projeto).</summary>
public sealed record DockerTopologyNodeDto(
    string Id,
    string Name,
    string Service,
    string Image,
    string State,
    IReadOnlyList<string> Networks,
    IReadOnlyList<string> Ports,
    string? HealthStatus,
    long RestartCount);

/// <summary>Rede Docker com os containers do projeto conectados a ela.</summary>
public sealed record DockerNetworkDto(
    string Name,
    IReadOnlyList<string> Containers);

/// <summary>Topologia ao vivo: containers + redes do projeto Compose.</summary>
public sealed record DockerTopologyDto(
    IReadOnlyList<DockerTopologyNodeDto> Containers,
    IReadOnlyList<DockerNetworkDto> Networks);

/// <summary>Ações de ciclo de vida permitidas (Administer + auditado).</summary>
public enum DockerContainerAction
{
    Start,
    Stop,
    Restart,
}

/// <summary>Resultado de remoção/limite de recursos de um container.</summary>
public enum DockerContainerUpdateStatus
{
    Done,
    NotFound,
    Failed,
}

/// <summary>
/// Controle real de containers via Docker Engine API (socket montado na API).
/// Implementação fica na Infrastructure; a Application só conhece o contrato.
/// </summary>
public interface IDockerControl
{
    /// <summary>Lista containers do projeto (todos os estados). Null = Docker indisponível.</summary>
    Task<IReadOnlyList<DockerContainerDto>?> ListContainersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Métricas instantâneas de um container (CPU%, memória, limites, rede,
    /// I/O, PIDs). Null quando o container não existe/está parado ou o stats
    /// falha — nunca lança.
    /// </summary>
    Task<DockerContainerStats?> GetContainerStatsAsync(
        string containerIdOrName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspect leve: StartedAt, RestartCount e HealthStatus. Null = falha.
    /// </summary>
    Task<DockerContainerInspectDto?> GetContainerInspectAsync(
        string containerIdOrName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Topologia dinâmica do projeto: containers com serviço Compose,
    /// redes e portas publicadas — dados reais do Engine a cada chamada.
    /// Null = Docker indisponível.
    /// </summary>
    Task<DockerTopologyDto?> GetTopologyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Últimas linhas de log do container (stdout+stderr, sem timestamps do
    /// Docker — o stream multiplexer é desembrulhado na implementação).
    /// Null = container inexistente/indisponível.
    /// </summary>
    Task<string?> GetContainerLogsAsync(
        string containerIdOrName,
        int tailLines,
        CancellationToken cancellationToken = default);

    /// <summary>Executa start/stop/restart. Retorna false se o container não existe.</summary>
    Task<bool> ControlAsync(
        string containerIdOrName,
        DockerContainerAction action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um container PARADO (docker rm sem force — o Engine recusa
    /// container em execução com 409). Spec docs/65 Fase 3: dupla
    /// confirmação + motivo + auditoria ficam na camada Application.
    /// </summary>
    Task<DockerContainerUpdateStatus> RemoveContainerAsync(
        string containerIdOrName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica limites de recursos em runtime (docker update — HostConfig
    /// Memory/NanoCpus). Vale até o próximo recreate do container; persistir
    /// no compose é decisão separada. Valores null mantêm o limite atual.
    /// </summary>
    Task<DockerContainerUpdateStatus> UpdateResourceLimitsAsync(
        string containerIdOrName,
        long? memoryBytes,
        double? cpuCores,
        CancellationToken cancellationToken = default);
}
