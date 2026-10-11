using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

public sealed record CreateProjectCommand(
    string? Code = null,
    string? Title = null,
    string? Description = null,
    Guid? LeaderId = null,
    string? Priority = null,
    string? Justification = null,
    Guid? ManagerId = null,
    DateOnly? PlannedStartDate = null,
    DateOnly? PlannedEndDate = null) : BaseCommand<ProjectDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class CreateProjectCommandHandler(
    IProjectRepository projectRepository,
    ICurrentUserContext currentUser,
    ILogger<CreateProjectCommandHandler> logger)
    : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(request.Code)
            ? await projectRepository.GetNextCodeAsync(cancellationToken)
            : request.Code.Trim();

        if (request.LeaderId is not { } leaderId || leaderId == Guid.Empty)
            throw new ArgumentException("O líder técnico do projeto é obrigatório.", nameof(request.LeaderId));

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? throw new ArgumentException("O título do projeto é obrigatório.", nameof(request.Title))
            : request.Title.Trim();

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? throw new ArgumentException("A descrição do projeto é obrigatória.", nameof(request.Description))
            : request.Description.Trim();

        if (request.ManagerId == Guid.Empty)
            throw new ArgumentException("O gestor do projeto não pode ser vazio.", nameof(request.ManagerId));

        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } createdById)
            throw new UnauthorizedAccessException("Usuário autenticado não identificado para criação do projeto.");

        var project = Project.Create(
            code,
            title,
            description,
            leaderId,
            ParsePriority(request.Priority),
            request.Justification,
            request.ManagerId,
            request.PlannedStartDate,
            request.PlannedEndDate,
            createdById);

        if (await projectRepository.GetByCodeAsync(project.Code, cancellationToken) is not null)
            throw new ArgumentException($"Já existe um projeto com o código '{project.Code}'.", nameof(request.Code));

        await projectRepository.AddAsync(project, cancellationToken);
        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Project {ProjectId} created with code {Code}.", project.Id, project.Code);

        return ProjectDto.From(project);
    }

    internal static ProjectPriority ParsePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
            return ProjectPriority.Medium;

        if (!Enum.TryParse(priority.Trim(), ignoreCase: true, out ProjectPriority parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                $"Prioridade inválida: '{priority}'. Use low, medium, high ou critical.",
                nameof(priority));

        return parsed;
    }
}

public sealed record UpdateProjectCommand(
    Guid Id,
    string? Title = null,
    string? Description = null,
    string? Justification = null,
    string? Priority = null,
    Guid? LeaderId = null,
    Guid? ManagerId = null,
    DateOnly? PlannedStartDate = null,
    DateOnly? PlannedEndDate = null) : BaseCommand<ProjectDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class UpdateProjectCommandHandler(
    IProjectRepository projectRepository,
    ILogger<UpdateProjectCommandHandler> logger)
    : IRequestHandler<UpdateProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.Id}' não encontrado.");

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? throw new ArgumentException("O título do projeto é obrigatório.", nameof(request.Title))
            : request.Title.Trim();

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? throw new ArgumentException("A descrição do projeto é obrigatória.", nameof(request.Description))
            : request.Description.Trim();

        var leaderId = request.LeaderId is { } leader && leader != Guid.Empty
            ? leader
            : throw new ArgumentException("O líder técnico do projeto é obrigatório.", nameof(request.LeaderId));

        project.UpdateDetails(
            title,
            description,
            request.Justification,
            CreateProjectCommandHandler.ParsePriority(request.Priority),
            leaderId,
            request.ManagerId == Guid.Empty ? null : request.ManagerId);

        if (request.PlannedStartDate.HasValue || request.PlannedEndDate.HasValue)
            project.SetPlanningDates(request.PlannedStartDate, request.PlannedEndDate);

        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Project {ProjectId} updated.", project.Id);
        return ProjectDto.From(project);
    }
}

public sealed record DeleteProjectCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class DeleteProjectCommandHandler(
    IProjectRepository projectRepository,
    ILogger<DeleteProjectCommandHandler> logger)
    : IRequestHandler<DeleteProjectCommand>
{
    public async Task Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.Id}' não encontrado.");

        project.Delete();
        projectRepository.Remove(project);
        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Project {ProjectId} deleted.", project.Id);
    }
}

public sealed record TransitionProjectStatusCommand(
    Guid Id,
    string Status,
    string? Reason = null) : BaseCommand<ProjectDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class TransitionProjectStatusCommandHandler(
    IProjectRepository projectRepository,
    ILogger<TransitionProjectStatusCommandHandler> logger)
    : IRequestHandler<TransitionProjectStatusCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        TransitionProjectStatusCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.Id}' não encontrado.");

        var status = ParseStatus(request.Status);

        switch (status)
        {
            case ProjectStatus.Draft:
                project.TransitionTo(ProjectStatus.Draft);
                break;
            case ProjectStatus.Planned:
                if (project.Status == ProjectStatus.Draft)
                    project.TransitionTo(ProjectStatus.Planned);
                else if (project.Status == ProjectStatus.Planned)
                    break;
                else
                    throw new InvalidOperationException($"Não é possível mover um projeto do estado '{project.Status}' para '{ProjectStatus.Planned}'.");
                break;
            case ProjectStatus.InProgress:
                if (project.Status == ProjectStatus.OnHold)
                    project.Resume();
                else if (project.Status == ProjectStatus.Planned)
                    project.TransitionTo(ProjectStatus.InProgress);
                else if (project.Status == ProjectStatus.InProgress)
                    break;
                else
                    throw new InvalidOperationException($"Não é possível mover um projeto do estado '{project.Status}' para '{ProjectStatus.InProgress}'.");
                break;
            case ProjectStatus.OnHold:
                if (project.Status == ProjectStatus.InProgress)
                    project.PutOnHold(string.IsNullOrWhiteSpace(request.Reason) ? "Pausa do projeto." : request.Reason.Trim());
                else if (project.Status == ProjectStatus.OnHold)
                    break;
                else
                    throw new InvalidOperationException($"Não é possível pausar um projeto no estado '{project.Status}'.");
                break;
            case ProjectStatus.PendingFinalApproval:
                project.SubmitForApproval();
                break;
            case ProjectStatus.Cancelled:
                project.Cancel(string.IsNullOrWhiteSpace(request.Reason) ? "Cancelamento solicitado." : request.Reason.Trim());
                break;
            case ProjectStatus.Completed:
                throw new InvalidOperationException("Use o fluxo de aprovação admin para concluir o projeto.");
            default:
                throw new ArgumentException($"Estado inválido: '{request.Status}'.", nameof(request.Status));
        }

        await projectRepository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Project {ProjectId} transitioned to {Status}.", project.Id, project.Status);

        return ProjectDto.From(project);
    }

    internal static ProjectStatus ParseStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("O status do projeto é obrigatório.", nameof(status));

        if (!Enum.TryParse(status.Trim(), ignoreCase: true, out ProjectStatus parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                $"Status inválido: '{status}'. Use draft, planned, inprogress, pendingfinalapproval, completed, cancelled ou onhold.",
                nameof(status));

        return parsed;
    }
}
