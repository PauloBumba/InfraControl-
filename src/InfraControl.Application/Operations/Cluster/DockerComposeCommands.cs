using System.Text.RegularExpressions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Cluster;

// ------------------------------------------------------------------
// DTOs
// ------------------------------------------------------------------

/// <summary>Limites de recursos aplicados a um container (runtime).</summary>
public sealed record DockerResourceLimitsDto(
    string Name,
    long? MemoryBytes,
    double? CpuCores);

// ------------------------------------------------------------------
// Queries / Commands — escala Compose via compose-agent (docs/65 F3)
// ------------------------------------------------------------------

/// <summary>Serviços Compose do projeto com réplicas reais (agente).</summary>
public sealed record GetComposeServicesQuery
    : IQuery<IReadOnlyList<ComposeServiceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

/// <summary>Escala um serviço Compose para N réplicas (docker compose up --scale).</summary>
public sealed record ScaleComposeServiceCommand(string Service, int Replicas)
    : BaseCommand<ComposeScaleResult>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "DockerComposeService";
}

/// <summary>
/// Remove um container PARADO do projeto — spec docs/65 Fase 3 exige
/// dupla confirmação (ConfirmName digitado) + motivo + auditoria.
/// </summary>
public sealed record RemoveDockerContainerCommand(
    string Name,
    string Reason,
    string ConfirmName)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "DockerContainer";
}

/// <summary>
/// Aplica limites de CPU/memória em runtime (docker update). Vale até o
/// próximo recreate — persistir no compose é decisão separada.
/// </summary>
public sealed record UpdateDockerResourceLimitsCommand(
    string Name,
    long? MemoryBytes,
    double? CpuCores)
    : BaseCommand<DockerResourceLimitsDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "DockerContainer";
}

// ------------------------------------------------------------------
// Handlers
// ------------------------------------------------------------------

public sealed class GetComposeServicesQueryHandler(IComposeAgent agent)
    : IRequestHandler<GetComposeServicesQuery, IReadOnlyList<ComposeServiceDto>>
{
    public async Task<IReadOnlyList<ComposeServiceDto>> Handle(
        GetComposeServicesQuery request, CancellationToken cancellationToken) =>
        await agent.ListServicesAsync(cancellationToken)
        ?? throw new InvalidOperationException(
            "Agente de composição indisponível — verifique o serviço compose-agent no compose.");
}

public sealed class ScaleComposeServiceCommandHandler(IComposeAgent agent)
    : IRequestHandler<ScaleComposeServiceCommand, ComposeScaleResult>
{
    private static readonly Regex ServicePattern =
        new("^[a-z0-9][a-z0-9_-]{0,62}$", RegexOptions.Compiled);
    private const int MaxReplicas = 16;

    public async Task<ComposeScaleResult> Handle(
        ScaleComposeServiceCommand request, CancellationToken cancellationToken)
    {
        var service = request.Service?.Trim() ?? string.Empty;
        if (!ServicePattern.IsMatch(service))
            throw new ArgumentException("Nome de serviço inválido.");
        if (request.Replicas is < 0 or > MaxReplicas)
            throw new ArgumentException(
                $"O número de réplicas deve estar entre 0 e {MaxReplicas}.");

        var result = await agent.ScaleAsync(
            service, request.Replicas, cancellationToken)
            ?? throw new InvalidOperationException(
                "Agente de composição indisponível — verifique o serviço compose-agent no compose.");

        if (!result.Success)
            throw new InvalidOperationException(
                $"O Compose recusou a escala de '{request.Service}': {result.Output}");
        return result;
    }
}

public sealed class RemoveDockerContainerCommandHandler(IDockerControl docker)
    : IRequestHandler<RemoveDockerContainerCommand>
{
    public async Task Handle(
        RemoveDockerContainerCommand request, CancellationToken cancellationToken)
    {
        // Dupla confirmação: o nome digitado precisa ser idêntico ao alvo.
        var name = request.Name?.Trim() ?? string.Empty;
        if (!string.Equals(name, request.ConfirmName?.Trim(),
                StringComparison.Ordinal))
            throw new ArgumentException(
                "Confirmação não confere — digite o nome exato do container.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
            throw new ArgumentException(
                "Informe o motivo da remoção (mínimo 5 caracteres) — ele vai para a auditoria.");

        // Só containers DESTE projeto podem ser removidos pela plataforma.
        var containers = await docker.ListContainersAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Docker indisponível — verifique a montagem de /var/run/docker.sock no serviço api.");
        var target = containers.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            throw new KeyNotFoundException(
                $"Container '{request.Name}' não encontrado no projeto.");
        if (string.Equals(target.State, "running", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"O container '{target.Name}' está em execução — pare-o antes de remover.");

        var status = await docker.RemoveContainerAsync(target.Name, cancellationToken);
        switch (status)
        {
            case DockerContainerUpdateStatus.Done:
                break;
            case DockerContainerUpdateStatus.NotFound:
                throw new KeyNotFoundException(
                    $"Container '{target.Name}' não encontrado.");
            default:
                throw new InvalidOperationException(
                    $"O Docker recusou a remoção de '{target.Name}' (o container pode ter voltado a executar).");
        }
    }
}

public sealed class UpdateDockerResourceLimitsCommandHandler(IDockerControl docker)
    : IRequestHandler<UpdateDockerResourceLimitsCommand, DockerResourceLimitsDto>
{
    private const long MinMemoryBytes = 16L * 1024 * 1024;   // 16 MiB
    private const long MaxMemoryBytes = 16L * 1024 * 1024 * 1024 * 1024; // 16 TiB
    private const double MinCpuCores = 0.1;
    private const double MaxCpuCores = 64.0;

    public async Task<DockerResourceLimitsDto> Handle(
        UpdateDockerResourceLimitsCommand request, CancellationToken cancellationToken)
    {
        if (request.MemoryBytes is null && request.CpuCores is null)
            throw new ArgumentException(
                "Informe ao menos um limite: memória (bytes) ou CPU (núcleos).");
        if (request.MemoryBytes is < MinMemoryBytes or > MaxMemoryBytes)
            throw new ArgumentException(
                "Memória inválida — use entre 16 MiB e 16 TiB.");
        if (request.CpuCores is < MinCpuCores or > MaxCpuCores)
            throw new ArgumentException(
                "CPU inválida — use entre 0,1 e 64 núcleos.");

        var name = request.Name?.Trim() ?? string.Empty;
        var containers = await docker.ListContainersAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Docker indisponível — verifique a montagem de /var/run/docker.sock no serviço api.");
        var target = containers.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            throw new KeyNotFoundException(
                $"Container '{request.Name}' não encontrado no projeto.");

        var status = await docker.UpdateResourceLimitsAsync(
            target.Name, request.MemoryBytes, request.CpuCores, cancellationToken);
        return status switch
        {
            DockerContainerUpdateStatus.Done => new DockerResourceLimitsDto(
                target.Name, request.MemoryBytes, request.CpuCores),
            DockerContainerUpdateStatus.NotFound => throw new KeyNotFoundException(
                $"Container '{target.Name}' não encontrado."),
            _ => throw new InvalidOperationException(
                $"O Docker recusou a atualização de limites de '{target.Name}'."),
        };
    }
}
