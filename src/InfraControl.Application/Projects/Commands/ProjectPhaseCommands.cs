using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

// F28-T003 — estrutura de fases do Projeto (ADR-043 · docs/38 §3.2):
// Add / Update / Reorder / SetStatus. Validação de fase auto-inicia a
// próxima (espelho PDI ADR-035). Progresso global = média ponderada
// uniforme dos Percent das fases (ProjectProgressCalculator).

public sealed record AddProjectPhaseCommand(
    Guid ProjectId,
    string Title,
    string? Description = null,
    string? CompletionCriteria = null,
    Guid? ResponsibleId = null,
    DateOnly? PlannedStartDate = null,
    DateOnly? PlannedEndDate = null) : BaseCommand<ProjectPhaseDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class AddProjectPhaseCommandHandler(
    IProjectRepository projectRepository,
    IProjectPhaseRepository phaseRepository,
    ILogger<AddProjectPhaseCommandHandler> logger)
    : IRequestHandler<AddProjectPhaseCommand, ProjectPhaseDto>
{
    public async Task<ProjectPhaseDto> Handle(
        AddProjectPhaseCommand request, CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Projeto '{request.ProjectId}' não encontrado.");

        var existing = await phaseRepository.GetByProjectIdAsync(
            request.ProjectId, cancellationToken);
        var order = existing.Count == 0
            ? 1
            : existing.Max(phase => phase.Order) + 1;

        var phase = ProjectPhase.Create(
            request.ProjectId,
            request.Title,
            order,
            request.Description,
            request.CompletionCriteria,
            request.ResponsibleId,
            request.PlannedStartDate,
            request.PlannedEndDate);

        await phaseRepository.AddAsync(phase, cancellationToken);
        await phaseRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project phase {PhaseId} (order {Order}) added to project {ProjectId}.",
            phase.Id, phase.Order, phase.ProjectId);

        return ToDto(phase);
    }

    internal static ProjectPhaseDto ToDto(ProjectPhase phase) =>
        new(
            phase.Id,
            phase.ProjectId,
            phase.Title,
            phase.Description,
            phase.Order,
            phase.ResponsibleId,
            phase.PlannedStartDate,
            phase.PlannedEndDate,
            phase.ActualStartDate,
            phase.ActualEndDate,
            phase.CompletionCriteria,
            phase.Status.ToString(),
            phase.Percent);
}

public sealed record UpdateProjectPhaseCommand(
    Guid Id,
    string Title,
    string? Description = null,
    string? CompletionCriteria = null,
    Guid? ResponsibleId = null,
    DateOnly? PlannedStartDate = null,
    DateOnly? PlannedEndDate = null,
    int? Percent = null) : BaseCommand<ProjectPhaseDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class UpdateProjectPhaseCommandHandler(
    IProjectPhaseRepository phaseRepository,
    ILogger<UpdateProjectPhaseCommandHandler> logger)
    : IRequestHandler<UpdateProjectPhaseCommand, ProjectPhaseDto>
{
    public async Task<ProjectPhaseDto> Handle(
        UpdateProjectPhaseCommand request, CancellationToken cancellationToken)
    {
        var phase = await phaseRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Fase '{request.Id}' não encontrada.");

        phase.Update(
            request.Title,
            request.Description,
            request.CompletionCriteria,
            request.ResponsibleId,
            request.PlannedStartDate,
            request.PlannedEndDate);

        if (request.Percent is { } percent)
            phase.SetProgress(percent);

        await phaseRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project phase {PhaseId} ('{Title}') updated.",
            phase.Id, phase.Title);

        return AddProjectPhaseCommandHandler.ToDto(phase);
    }
}

public sealed record ReorderProjectPhasesCommand(
    Guid ProjectId,
    IReadOnlyList<Guid> PhaseIdsInOrder) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class ReorderProjectPhasesCommandHandler(
    IProjectRepository projectRepository,
    IProjectPhaseRepository phaseRepository,
    ILogger<ReorderProjectPhasesCommandHandler> logger)
    : IRequestHandler<ReorderProjectPhasesCommand>
{
    public async Task Handle(
        ReorderProjectPhasesCommand request, CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Projeto '{request.ProjectId}' não encontrado.");

        if (request.PhaseIdsInOrder is null || request.PhaseIdsInOrder.Count == 0)
            throw new ArgumentException(
                "A lista de fases para reordenação é obrigatória.",
                nameof(request.PhaseIdsInOrder));

        if (request.PhaseIdsInOrder.Distinct().Count() != request.PhaseIdsInOrder.Count)
            throw new ArgumentException(
                "A lista de fases para reordenação não pode conter duplicatas.",
                nameof(request.PhaseIdsInOrder));

        var phases = await phaseRepository.GetByProjectIdAsync(
            request.ProjectId, cancellationToken);

        if (phases.Count != request.PhaseIdsInOrder.Count)
            throw new ArgumentException(
                "A lista de fases deve incluir exatamente todas as fases do projeto.",
                nameof(request.PhaseIdsInOrder));

        var byId = phases.ToDictionary(phase => phase.Id);
        foreach (var phaseId in request.PhaseIdsInOrder)
        {
            if (!byId.ContainsKey(phaseId))
                throw new ArgumentException(
                    $"A fase '{phaseId}' não pertence ao projeto '{request.ProjectId}'.",
                    nameof(request.PhaseIdsInOrder));
        }

        for (var index = 0; index < request.PhaseIdsInOrder.Count; index++)
            byId[request.PhaseIdsInOrder[index]].SetOrder(index + 1);

        await phaseRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project {ProjectId} phases reordered ({Count} phases).",
            request.ProjectId, request.PhaseIdsInOrder.Count);
    }
}

/// <summary>
/// Transições de status da fase: Start, Submit, Validate, Reject.
/// </summary>
public sealed record SetPhaseStatusCommand(
    Guid Id,
    string Action,
    string? Reason = null) : BaseCommand<ProjectPhaseDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SetPhaseStatusCommandHandler(
    IProjectPhaseRepository phaseRepository,
    ILogger<SetPhaseStatusCommandHandler> logger)
    : IRequestHandler<SetPhaseStatusCommand, ProjectPhaseDto>
{
    public async Task<ProjectPhaseDto> Handle(
        SetPhaseStatusCommand request, CancellationToken cancellationToken)
    {
        var phase = await phaseRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Fase '{request.Id}' não encontrada.");

        var action = ParseAction(request.Action);
        var siblings = await phaseRepository.GetByProjectIdAsync(
            phase.ProjectId, cancellationToken);

        switch (action)
        {
            case ProjectPhaseAction.Start:
                EnsurePreviousPhasesCompleted(phase, siblings);
                phase.Start();
                break;

            case ProjectPhaseAction.Submit:
                phase.Submit();
                break;

            case ProjectPhaseAction.Validate:
                phase.Validate();
                // Validada → próxima fase (Order) começa automaticamente (ADR-043 / PDI).
                var next = siblings
                    .Where(candidate =>
                        candidate.Id != phase.Id
                        && candidate.Order > phase.Order
                        && candidate.Status == ProjectPhaseStatus.NotStarted)
                    .OrderBy(candidate => candidate.Order)
                    .FirstOrDefault();

                next?.Start();
                break;

            case ProjectPhaseAction.Reject:
                phase.Reject(request.Reason ?? string.Empty);
                break;
        }

        await phaseRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project phase {PhaseId} action {Action} applied (status {Status}).",
            phase.Id, action, phase.Status);

        return AddProjectPhaseCommandHandler.ToDto(phase);
    }

    internal static ProjectPhaseAction ParseAction(string action)
    {
        if (!Enum.TryParse<ProjectPhaseAction>(action, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                "Ação de fase inválida (use Start, Submit, Validate ou Reject).",
                nameof(action));

        return parsed;
    }

    private static void EnsurePreviousPhasesCompleted(
        ProjectPhase phase, IReadOnlyCollection<ProjectPhase> siblings)
    {
        var blocking = siblings
            .Where(candidate =>
                candidate.Order < phase.Order
                && candidate.Status != ProjectPhaseStatus.Completed)
            .OrderBy(candidate => candidate.Order)
            .FirstOrDefault();

        if (blocking is not null)
            throw new InvalidOperationException(
                $"Não é possível iniciar a fase '{phase.Title}': a fase anterior '{blocking.Title}' ainda não foi concluída.");
    }
}

internal enum ProjectPhaseAction
{
    Start = 0,
    Submit = 1,
    Validate = 2,
    Reject = 3,
}
