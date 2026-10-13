using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

// CRUD-Projetos (2026-10-12) — sub-agregado de impedimentos/risco do Projeto
// (ADR-043 · docs/38 §3.4), que antes era só leitura (repositório existia,
// nenhum command). Create / Update / Delete + transições Resolve/Reopen.

public sealed record CreateProjectImpedimentCommand(
    Guid ProjectId,
    string Title,
    string Description,
    string Category,
    string Severity,
    string? MitigationPlan = null) : BaseCommand<ProjectImpedimentDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class CreateProjectImpedimentCommandHandler(
    IProjectRepository projectRepository,
    IProjectImpedimentRepository impedimentRepository,
    ILogger<CreateProjectImpedimentCommandHandler> logger)
    : IRequestHandler<CreateProjectImpedimentCommand, ProjectImpedimentDto>
{
    public async Task<ProjectImpedimentDto> Handle(
        CreateProjectImpedimentCommand request, CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Projeto '{request.ProjectId}' não encontrado.");

        var impediment = ProjectImpediment.Create(
            request.ProjectId,
            request.Title,
            request.Description,
            ParseCategory(request.Category),
            ParseSeverity(request.Severity),
            request.MitigationPlan);

        await impedimentRepository.AddAsync(impediment, cancellationToken);
        await impedimentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project impediment {ImpedimentId} ('{Title}') added to project {ProjectId}.",
            impediment.Id, impediment.Title, impediment.ProjectId);

        return ToDto(impediment);
    }

    internal static ProjectImpedimentCategory ParseCategory(string category)
    {
        if (!Enum.TryParse<ProjectImpedimentCategory>(category, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                "Categoria de impedimento inválida (use Technical, Vendor, Budget ou SecurityAccess).",
                nameof(category));
        return parsed;
    }

    internal static ProjectImpedimentSeverity ParseSeverity(string severity)
    {
        if (!Enum.TryParse<ProjectImpedimentSeverity>(severity, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                "Severidade de impedimento inválida (use Low, Medium, High ou Critical).",
                nameof(severity));
        return parsed;
    }

    internal static ProjectImpedimentDto ToDto(ProjectImpediment impediment) =>
        new(
            impediment.Id,
            impediment.ProjectId,
            impediment.Title,
            impediment.Description,
            impediment.Category.ToString(),
            impediment.Severity.ToString(),
            impediment.MitigationPlan,
            impediment.Status.ToString(),
            impediment.IsResolved,
            impediment.ResolvedAt);
}

public sealed record UpdateProjectImpedimentCommand(
    Guid Id,
    string Title,
    string Description,
    string Category,
    string Severity,
    string? MitigationPlan = null) : BaseCommand<ProjectImpedimentDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class UpdateProjectImpedimentCommandHandler(
    IProjectImpedimentRepository impedimentRepository,
    ILogger<UpdateProjectImpedimentCommandHandler> logger)
    : IRequestHandler<UpdateProjectImpedimentCommand, ProjectImpedimentDto>
{
    public async Task<ProjectImpedimentDto> Handle(
        UpdateProjectImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await impedimentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");

        impediment.Update(
            request.Title,
            request.Description,
            CreateProjectImpedimentCommandHandler.ParseCategory(request.Category),
            CreateProjectImpedimentCommandHandler.ParseSeverity(request.Severity),
            request.MitigationPlan);

        await impedimentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project impediment {ImpedimentId} ('{Title}') updated.",
            impediment.Id, impediment.Title);

        return CreateProjectImpedimentCommandHandler.ToDto(impediment);
    }
}

public sealed record DeleteProjectImpedimentCommand(Guid Id) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class DeleteProjectImpedimentCommandHandler(
    IProjectImpedimentRepository impedimentRepository,
    ILogger<DeleteProjectImpedimentCommandHandler> logger)
    : IRequestHandler<DeleteProjectImpedimentCommand>
{
    public async Task Handle(
        DeleteProjectImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await impedimentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");

        impedimentRepository.Remove(impediment);
        await impedimentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project impediment {ImpedimentId} ('{Title}') deleted.",
            impediment.Id, impediment.Title);
    }
}

public sealed record ResolveProjectImpedimentCommand(Guid Id) : BaseCommand<ProjectImpedimentDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class ResolveProjectImpedimentCommandHandler(
    IProjectImpedimentRepository impedimentRepository,
    ILogger<ResolveProjectImpedimentCommandHandler> logger)
    : IRequestHandler<ResolveProjectImpedimentCommand, ProjectImpedimentDto>
{
    public async Task<ProjectImpedimentDto> Handle(
        ResolveProjectImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await impedimentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");

        impediment.Resolve();
        await impedimentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project impediment {ImpedimentId} resolved.", impediment.Id);

        return CreateProjectImpedimentCommandHandler.ToDto(impediment);
    }
}

public sealed record ReopenProjectImpedimentCommand(Guid Id) : BaseCommand<ProjectImpedimentDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class ReopenProjectImpedimentCommandHandler(
    IProjectImpedimentRepository impedimentRepository,
    ILogger<ReopenProjectImpedimentCommandHandler> logger)
    : IRequestHandler<ReopenProjectImpedimentCommand, ProjectImpedimentDto>
{
    public async Task<ProjectImpedimentDto> Handle(
        ReopenProjectImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await impedimentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");

        impediment.Reopen();
        await impedimentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project impediment {ImpedimentId} reopened.", impediment.Id);

        return CreateProjectImpedimentCommandHandler.ToDto(impediment);
    }
}
