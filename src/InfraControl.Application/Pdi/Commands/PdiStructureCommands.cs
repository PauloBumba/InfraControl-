using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Pdi.Commands;

// Estrutura do desenvolvimento (docs/30 §5-§8, Fase 2, ADR-032):
// objetivo → estratégia → fase → atividade, cada nível com prazos.
// Escopo Operate (controller) + dono do plano ou Admin (PdiAccess);
// exclusão com children → 409 (ADR-029 §3 — mesmo padrão de portas);
// 404 = nó/pai/plano inexistente, 400 = campo/competência/responsável
// inválido. Sem eventos de histórico aqui — auditoria vem na Fase 5.

// ------------------------------------------------------------------
// Objetivos
// ------------------------------------------------------------------

public sealed record CreatePdiObjectiveCommand(
    Guid UserId,
    string Title,
    string? Description = null,
    Guid? CompetencyId = null,
    string? CurrentSituation = null,
    string? DesiredSituation = null,
    string? ExpectedResult = null,
    string? Indicator = null,
    DateOnly? TargetDate = null) : BaseCommand<PdiObjectiveNodeDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiObjectiveCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiObjectiveCommandHandler> logger)
    : IRequestHandler<CreatePdiObjectiveCommand, PdiObjectiveNodeDto>
{
    public async Task<PdiObjectiveNodeDto> Handle(
        CreatePdiObjectiveCommand request, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        string? competencyName = null;
        if (request.CompetencyId is { } competencyId)
        {
            var competency = await repository.GetCompetencyAsync(competencyId, cancellationToken)
                ?? throw new ArgumentException(
                    $"Competência '{competencyId}' não encontrada.",
                    nameof(request.CompetencyId));
            competencyName = competency.Name;
        }

        var order = await repository.NextObjectiveOrderAsync(plan.UserId, cancellationToken);
        var objective = PdiObjective.Create(
            plan.UserId, request.Title, request.Description, request.CompetencyId,
            request.CurrentSituation, request.DesiredSituation, request.ExpectedResult,
            request.Indicator, request.TargetDate, order);

        repository.AddObjective(objective);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI objective {Id} ('{Title}') created for {UserId}.",
            objective.Id, objective.Title, plan.UserId);

        return new PdiObjectiveNodeDto(
            objective.Id, objective.Title, objective.Description,
            objective.CompetencyId, competencyName, objective.CurrentSituation,
            objective.DesiredSituation, objective.ExpectedResult, objective.Indicator,
            objective.TargetDate, objective.Order);
    }
}

public sealed record UpdatePdiObjectiveCommand(
    Guid Id,
    string Title,
    string? Description = null,
    Guid? CompetencyId = null,
    string? CurrentSituation = null,
    string? DesiredSituation = null,
    string? ExpectedResult = null,
    string? Indicator = null,
    DateOnly? TargetDate = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdatePdiObjectiveCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<UpdatePdiObjectiveCommandHandler> logger)
    : IRequestHandler<UpdatePdiObjectiveCommand>
{
    public async Task Handle(
        UpdatePdiObjectiveCommand request, CancellationToken cancellationToken)
    {
        var objective = await repository.GetObjectiveAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Objetivo '{request.Id}' não encontrado.");
        var plan = await plans.GetByUserIdAsync(objective.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{objective.UserId}' não encontrado.");
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        if (request.CompetencyId is { } competencyId
            && await repository.GetCompetencyAsync(competencyId, cancellationToken) is null)
            throw new ArgumentException(
                $"Competência '{competencyId}' não encontrada.",
                nameof(request.CompetencyId));

        objective.Update(
            request.Title, request.Description, request.CompetencyId,
            request.CurrentSituation, request.DesiredSituation, request.ExpectedResult,
            request.Indicator, request.TargetDate);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI objective {Id} ('{Title}') updated.",
            objective.Id, objective.Title);
    }
}

public sealed record DeletePdiObjectiveCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeletePdiObjectiveCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiObjectiveCommandHandler> logger)
    : IRequestHandler<DeletePdiObjectiveCommand>
{
    public async Task Handle(
        DeletePdiObjectiveCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForObjectiveAsync(
            repository, plans, request.Id, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var objective = await repository.GetObjectiveAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Objetivo '{request.Id}' não encontrado.");

        if (await repository.HasStrategiesAsync(objective.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir o objetivo: existem estratégias vinculadas.");

        repository.RemoveObjective(objective);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI objective {Id} deleted.", objective.Id);
    }
}

// ------------------------------------------------------------------
// Estratégias
// ------------------------------------------------------------------

public sealed record CreatePdiStrategyCommand(
    Guid ObjectiveId,
    string Title,
    string? Description = null) : BaseCommand<PdiStrategyNodeDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiStrategyCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiStrategyCommandHandler> logger)
    : IRequestHandler<CreatePdiStrategyCommand, PdiStrategyNodeDto>
{
    public async Task<PdiStrategyNodeDto> Handle(
        CreatePdiStrategyCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForObjectiveAsync(
            repository, plans, request.ObjectiveId, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var order = await repository.NextStrategyOrderAsync(
            request.ObjectiveId, cancellationToken);
        var strategy = PdiStrategy.Create(
            request.ObjectiveId, request.Title, request.Description, order);

        repository.AddStrategy(strategy);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI strategy {Id} ('{Title}') created for objective {ObjectiveId}.",
            strategy.Id, strategy.Title, request.ObjectiveId);

        return new PdiStrategyNodeDto(
            strategy.Id, strategy.Title, strategy.Description, strategy.Order);
    }
}

public sealed record UpdatePdiStrategyCommand(
    Guid Id,
    string Title,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdatePdiStrategyCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<UpdatePdiStrategyCommandHandler> logger)
    : IRequestHandler<UpdatePdiStrategyCommand>
{
    public async Task Handle(
        UpdatePdiStrategyCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForStrategyAsync(
            repository, plans, request.Id, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var strategy = await repository.GetStrategyAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Estratégia '{request.Id}' não encontrado.");

        strategy.Update(request.Title, request.Description);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI strategy {Id} ('{Title}') updated.",
            strategy.Id, strategy.Title);
    }
}

public sealed record DeletePdiStrategyCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeletePdiStrategyCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiStrategyCommandHandler> logger)
    : IRequestHandler<DeletePdiStrategyCommand>
{
    public async Task Handle(
        DeletePdiStrategyCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForStrategyAsync(
            repository, plans, request.Id, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var strategy = await repository.GetStrategyAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Estratégia '{request.Id}' não encontrado.");

        if (await repository.HasPhasesAsync(strategy.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir a estratégia: existem fases vinculadas.");

        repository.RemoveStrategy(strategy);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI strategy {Id} deleted.", strategy.Id);
    }
}

// ------------------------------------------------------------------
// Fases
// ------------------------------------------------------------------

public sealed record CreatePdiPhaseCommand(
    Guid StrategyId,
    string Title,
    string? Description = null,
    Guid? ResponsibleId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? CompletionCriteria = null,
    string Status = "NotStarted",
    int Percent = 0) : BaseCommand<PdiPhaseNodeDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiPhaseCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiPhaseCommandHandler> logger)
    : IRequestHandler<CreatePdiPhaseCommand, PdiPhaseNodeDto>
{
    public async Task<PdiPhaseNodeDto> Handle(
        CreatePdiPhaseCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForStrategyAsync(
            repository, plans, request.StrategyId, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var status = ParseStatus(request.Status);
        var responsibleName = await ResolveResponsibleAsync(
            plans, request.ResponsibleId, cancellationToken);

        var order = await repository.NextPhaseOrderAsync(
            request.StrategyId, cancellationToken);
        var phase = PdiPhase.Create(
            request.StrategyId, request.Title, request.Description,
            request.ResponsibleId, request.StartDate, request.EndDate,
            request.CompletionCriteria, status, request.Percent, order);

        repository.AddPhase(phase);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI phase {Id} ('{Title}') created for strategy {StrategyId}.",
            phase.Id, phase.Title, request.StrategyId);

        return ToNode(phase, responsibleName);
    }

    internal static PdiPhaseStatus ParseStatus(string status)
    {
        if (!Enum.TryParse<PdiPhaseStatus>(status, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                "Status da fase inválido (use NotStarted, InProgress ou Completed).",
                nameof(status));
        return parsed;
    }

    internal static async Task<string?> ResolveResponsibleAsync(
        IPdiRepository plans, Guid? responsibleId, CancellationToken cancellationToken)
    {
        if (responsibleId is not { } responsible)
            return null;

        return await plans.GetUserNameAsync(responsible, cancellationToken)
            ?? throw new ArgumentException(
                $"Responsável '{responsible}' não encontrado.",
                nameof(responsibleId));
    }

    internal static PdiPhaseNodeDto ToNode(PdiPhase phase, string? responsibleName)
        => new(
            phase.Id, phase.Title, phase.Description, phase.ResponsibleId,
            responsibleName, phase.StartDate, phase.EndDate, phase.CompletionCriteria,
            phase.Status.ToString(), phase.Percent, phase.Order);
}

public sealed record UpdatePdiPhaseCommand(
    Guid Id,
    string Title,
    string? Description = null,
    Guid? ResponsibleId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? CompletionCriteria = null,
    string Status = "NotStarted",
    int Percent = 0) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdatePdiPhaseCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<UpdatePdiPhaseCommandHandler> logger)
    : IRequestHandler<UpdatePdiPhaseCommand>
{
    public async Task Handle(
        UpdatePdiPhaseCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.Id, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var phase = await repository.GetPhaseAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Fase '{request.Id}' não encontrada.");

        var status = CreatePdiPhaseCommandHandler.ParseStatus(request.Status);
        _ = await CreatePdiPhaseCommandHandler.ResolveResponsibleAsync(
            plans, request.ResponsibleId, cancellationToken);

        phase.Update(
            request.Title, request.Description, request.ResponsibleId,
            request.StartDate, request.EndDate, request.CompletionCriteria,
            status, request.Percent);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI phase {Id} ('{Title}') updated.", phase.Id, phase.Title);
    }
}

public sealed record DeletePdiPhaseCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeletePdiPhaseCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiPhaseCommandHandler> logger)
    : IRequestHandler<DeletePdiPhaseCommand>
{
    public async Task Handle(
        DeletePdiPhaseCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.Id, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var phase = await repository.GetPhaseAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Fase '{request.Id}' não encontrada.");

        if (await repository.HasActivitiesAsync(phase.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir a fase: existem atividades vinculadas.");

        repository.RemovePhase(phase);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI phase {Id} deleted.", phase.Id);
    }
}

// ------------------------------------------------------------------
// Atividades
// ------------------------------------------------------------------

public sealed record CreatePdiActivityCommand(
    Guid PhaseId,
    string Title,
    DateOnly? DueDate = null) : BaseCommand<PdiActivityNodeDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiActivityCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiActivityCommandHandler> logger)
    : IRequestHandler<CreatePdiActivityCommand, PdiActivityNodeDto>
{
    public async Task<PdiActivityNodeDto> Handle(
        CreatePdiActivityCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.PhaseId, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var order = await repository.NextActivityOrderAsync(
            request.PhaseId, cancellationToken);
        var activity = PdiActivity.Create(
            request.PhaseId, request.Title, request.DueDate, order);

        repository.AddActivity(activity);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI activity {Id} ('{Title}') created for phase {PhaseId}.",
            activity.Id, activity.Title, request.PhaseId);

        return new PdiActivityNodeDto(
            activity.Id, activity.Title, activity.DueDate, activity.Completed,
            activity.Order);
    }
}

public sealed record UpdatePdiActivityCommand(
    Guid Id,
    string Title,
    DateOnly? DueDate = null,
    bool Completed = false) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdatePdiActivityCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<UpdatePdiActivityCommandHandler> logger)
    : IRequestHandler<UpdatePdiActivityCommand>
{
    public async Task Handle(
        UpdatePdiActivityCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForActivityAsync(
            repository, plans, request.Id, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var activity = await repository.GetActivityAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Atividade '{request.Id}' não encontrada.");

        activity.Update(request.Title, request.DueDate, request.Completed);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI activity {Id} ('{Title}') updated.", activity.Id, activity.Title);
    }
}

public sealed record DeletePdiActivityCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeletePdiActivityCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiActivityCommandHandler> logger)
    : IRequestHandler<DeletePdiActivityCommand>
{
    public async Task Handle(
        DeletePdiActivityCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForActivityAsync(
            repository, plans, request.Id, cancellationToken);
        PdiAccess.EnsureCanManage(currentUser, plan.UserId);

        var activity = await repository.GetActivityAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Atividade '{request.Id}' não encontrada.");

        repository.RemoveActivity(activity);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI activity {Id} deleted.", activity.Id);
    }
}
