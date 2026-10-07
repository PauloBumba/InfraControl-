using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Pdi.Commands;

// Acompanhamento bilateral da fase (docs/30 §9-§11, §13, Fase 3,
// ADR-033): progresso/comentários e check-ins (relato do colaborador),
// resposta do gestor no check-in, impedimentos (§11 com status) e
// feedbacks (§13 append-only, exclusão Admin). Leitura = dono/gestor
// Admin; relato = dono/Admin; resposta = gestor/Admin. Sem eventos de
// histórico — auditoria vem na Fase 5.

// ------------------------------------------------------------------
// Progresso (append-only)
// ------------------------------------------------------------------

public sealed record CreatePdiProgressCommand(
    Guid PhaseId,
    int? Percent = null,
    string Comment = "") : BaseCommand<PdiProgressDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiProgressCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiProgressCommandHandler> logger)
    : IRequestHandler<CreatePdiProgressCommand, PdiProgressDto>
{
    public async Task<PdiProgressDto> Handle(
        CreatePdiProgressCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReport(currentUser, plan);

        var progress = PdiProgress.Create(
            request.PhaseId, currentUser.UserId!.Value,
            request.Percent, request.Comment);

        repository.AddProgress(progress);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI progress {Id} created for phase {PhaseId}.",
            progress.Id, progress.PhaseId);

        var authorName = await plans.GetUserNameAsync(
            progress.AuthorId, cancellationToken);
        return new PdiProgressDto(
            progress.Id, progress.PhaseId, progress.AuthorId, authorName,
            progress.Percent, progress.Comment, progress.CreateDateTime);
    }
}

public sealed record DeletePdiProgressCommand(Guid Id) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeletePdiProgressCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiProgressCommandHandler> logger)
    : IRequestHandler<DeletePdiProgressCommand>
{
    public async Task Handle(
        DeletePdiProgressCommand request, CancellationToken cancellationToken)
    {
        var progress = await repository.GetProgressAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Progresso '{request.Id}' não encontrado.");
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, progress.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReport(currentUser, plan);

        repository.RemoveProgress(progress);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI progress {Id} deleted.", progress.Id);
    }
}

// ------------------------------------------------------------------
// Check-ins (relato do colaborador + resposta do gestor)
// ------------------------------------------------------------------

public sealed record CreatePdiCheckInCommand(
    Guid PhaseId,
    DateOnly CheckInDate,
    string WhatDone,
    string? WhatLearned = null,
    string? Difficulty = null,
    string? NeedSupport = null) : BaseCommand<PdiCheckInDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiCheckInCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiCheckInCommandHandler> logger)
    : IRequestHandler<CreatePdiCheckInCommand, PdiCheckInDto>
{
    public async Task<PdiCheckInDto> Handle(
        CreatePdiCheckInCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReport(currentUser, plan);

        var checkIn = PdiCheckIn.Create(
            request.PhaseId, request.CheckInDate, currentUser.UserId!.Value,
            request.WhatDone, request.WhatLearned, request.Difficulty,
            request.NeedSupport);

        repository.AddCheckIn(checkIn);
        plan.RecordCheckInCreated(
            currentUser.UserId!.Value, request.CheckInDate, request.WhatDone);
        plans.AddHistoryRange(plan.DrainPendingHistory());
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI check-in {Id} created for phase {PhaseId}.",
            checkIn.Id, checkIn.PhaseId);

        return new PdiCheckInDto(
            checkIn.Id, checkIn.PhaseId, checkIn.CheckInDate, checkIn.CreatedBy,
            await plans.GetUserNameAsync(checkIn.CreatedBy, cancellationToken),
            checkIn.WhatDone, checkIn.WhatLearned, checkIn.Difficulty,
            checkIn.NeedSupport, checkIn.ManagerFeedback, checkIn.NextAction,
            checkIn.RespondedAt,
            checkIn.RespondedById is { } responderId
                ? await plans.GetUserNameAsync(responderId, cancellationToken)
                : null);
    }
}

public sealed record UpdatePdiCheckInCommand(
    Guid Id,
    string? WhatDone = null,
    string? WhatLearned = null,
    string? Difficulty = null,
    string? NeedSupport = null,
    string? ManagerFeedback = null,
    string? NextAction = null) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdatePdiCheckInCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<UpdatePdiCheckInCommandHandler> logger)
    : IRequestHandler<UpdatePdiCheckInCommand>
{
    public async Task Handle(
        UpdatePdiCheckInCommand request, CancellationToken cancellationToken)
    {
        var checkIn = await repository.GetCheckInAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Check-in '{request.Id}' não encontrado.");
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, checkIn.PhaseId, cancellationToken);

        // Lados distintos (§9): dono/Admin editam o relato; gestor/Admin
        // respondem. Quem não é nenhum dos dois → 403.
        var isAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString());
        var canManage = isAdmin || currentUser.UserId == plan.UserId;
        var canRespond = isAdmin
            || (plan.ManagerId is { } managerId && currentUser.UserId == managerId);

        if (!canManage && !canRespond)
            throw new UnauthorizedAccessException(
                "Você não tem permissão para editar este check-in.");

        if (canManage && request.WhatDone is not null)
            checkIn.EditRelato(
                request.WhatDone,
                request.WhatLearned ?? checkIn.WhatLearned,
                request.Difficulty ?? checkIn.Difficulty,
                request.NeedSupport ?? checkIn.NeedSupport);

        // Responder só quando o lado resposta é enviado (PUT por lado):
        // um PUT de relato não re-carimba RespondedAt/RespondedById.
        if (canRespond
            && (request.ManagerFeedback is not null
                || request.NextAction is not null))
        {
            checkIn.Respond(
                request.ManagerFeedback ?? checkIn.ManagerFeedback,
                request.NextAction ?? checkIn.NextAction,
                currentUser.UserId!.Value);
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI check-in {Id} updated.", checkIn.Id);
    }
}

public sealed record DeletePdiCheckInCommand(Guid Id) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeletePdiCheckInCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiCheckInCommandHandler> logger)
    : IRequestHandler<DeletePdiCheckInCommand>
{
    public async Task Handle(
        DeletePdiCheckInCommand request, CancellationToken cancellationToken)
    {
        var checkIn = await repository.GetCheckInAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Check-in '{request.Id}' não encontrado.");
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, checkIn.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReport(currentUser, plan);

        repository.RemoveCheckIn(checkIn);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI check-in {Id} deleted.", checkIn.Id);
    }
}

// ------------------------------------------------------------------
// Impedimentos (§11)
// ------------------------------------------------------------------

public sealed record CreatePdiImpedimentCommand(
    Guid PhaseId,
    string Type,
    string Description,
    DateOnly OccurredAt,
    string? Impact = null,
    Guid? ResponsibleId = null,
    string? ActionTaken = null) : BaseCommand<PdiImpedimentDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiImpedimentCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiImpedimentCommandHandler> logger)
    : IRequestHandler<CreatePdiImpedimentCommand, PdiImpedimentDto>
{
    public async Task<PdiImpedimentDto> Handle(
        CreatePdiImpedimentCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReport(currentUser, plan);

        var responsibleName = await CreatePdiPhaseCommandHandler.ResolveResponsibleAsync(
            plans, request.ResponsibleId, cancellationToken);

        var impediment = PdiImpediment.Create(
            request.PhaseId, request.Type, request.Description, request.OccurredAt,
            request.Impact, request.ResponsibleId, request.ActionTaken);

        repository.AddImpediment(impediment);
        plan.RecordImpedimentCreated(
            currentUser.UserId!.Value, request.Type, request.Description);
        plans.AddHistoryRange(plan.DrainPendingHistory());
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI impediment {Id} ('{Type}') created for phase {PhaseId}.",
            impediment.Id, impediment.Type, impediment.PhaseId);

        return new PdiImpedimentDto(
            impediment.Id, impediment.PhaseId, impediment.Type, impediment.Description,
            impediment.Impact, impediment.OccurredAt, impediment.ResponsibleId,
            responsibleName, impediment.ActionTaken, impediment.Status.ToString(),
            impediment.ResolvedAt);
    }
}

public sealed record UpdatePdiImpedimentCommand(
    Guid Id,
    string Type,
    string Description,
    DateOnly OccurredAt,
    string? Impact = null,
    Guid? ResponsibleId = null,
    string? ActionTaken = null,
    string Status = "Open") : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdatePdiImpedimentCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<UpdatePdiImpedimentCommandHandler> logger)
    : IRequestHandler<UpdatePdiImpedimentCommand>
{
    public async Task Handle(
        UpdatePdiImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await repository.GetImpedimentAsync(
            request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, impediment.PhaseId, cancellationToken);
        PdiAccess.EnsureCanAssist(currentUser, plan);

        _ = await CreatePdiPhaseCommandHandler.ResolveResponsibleAsync(
            plans, request.ResponsibleId, cancellationToken);

        if (!Enum.TryParse<PdiImpedimentStatus>(
                request.Status, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
            throw new ArgumentException(
                "Status do impedimento inválido (use Open ou Resolved).",
                nameof(request.Status));

        var wasResolved = impediment.Status == PdiImpedimentStatus.Resolved;

        impediment.Update(
            request.Type, request.Description, request.OccurredAt,
            request.Impact, request.ResponsibleId, request.ActionTaken, status);

        if (!wasResolved && status == PdiImpedimentStatus.Resolved)
        {
            plan.RecordImpedimentResolved(
                currentUser.UserId!.Value, request.Description);
            plans.AddHistoryRange(plan.DrainPendingHistory());
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI impediment {Id} updated ({Status}).",
            impediment.Id, impediment.Status);
    }
}

public sealed record DeletePdiImpedimentCommand(Guid Id) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeletePdiImpedimentCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiImpedimentCommandHandler> logger)
    : IRequestHandler<DeletePdiImpedimentCommand>
{
    public async Task Handle(
        DeletePdiImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await repository.GetImpedimentAsync(
            request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, impediment.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReport(currentUser, plan);

        repository.RemoveImpediment(impediment);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI impediment {Id} deleted.", impediment.Id);
    }
}

// ------------------------------------------------------------------
// Feedbacks (§13 — histórico, sem edição; exclusão Admin)
// ------------------------------------------------------------------

public sealed record CreatePdiFeedbackCommand(
    Guid PhaseId,
    Guid RecipientId,
    string Type,
    string Content,
    DateOnly FeedbackDate) : BaseCommand<PdiFeedbackDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreatePdiFeedbackCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<CreatePdiFeedbackCommandHandler> logger)
    : IRequestHandler<CreatePdiFeedbackCommand, PdiFeedbackDto>
{
    public async Task<PdiFeedbackDto> Handle(
        CreatePdiFeedbackCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.PhaseId, cancellationToken);
        PdiAccess.EnsureCanView(currentUser, plan);

        if (request.RecipientId != plan.UserId && request.RecipientId != plan.ManagerId)
            throw new ArgumentException(
                "O destinatário deve ser o dono ou o gestor do PDI.",
                nameof(request.RecipientId));

        var feedback = PdiFeedback.Create(
            request.PhaseId, currentUser.UserId!.Value, request.RecipientId,
            request.Type, request.Content, request.FeedbackDate);

        repository.AddFeedback(feedback);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI feedback {Id} created for phase {PhaseId}.",
            feedback.Id, feedback.PhaseId);

        return new PdiFeedbackDto(
            feedback.Id, feedback.PhaseId, feedback.AuthorId,
            await plans.GetUserNameAsync(feedback.AuthorId, cancellationToken),
            feedback.RecipientId,
            await plans.GetUserNameAsync(feedback.RecipientId, cancellationToken),
            feedback.Type, feedback.Content, feedback.FeedbackDate);
    }
}

public sealed record DeletePdiFeedbackCommand(Guid Id) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class DeletePdiFeedbackCommandHandler(
    IPdiStructureRepository repository,
    ICurrentUserContext currentUser,
    ILogger<DeletePdiFeedbackCommandHandler> logger)
    : IRequestHandler<DeletePdiFeedbackCommand>
{
    public async Task Handle(
        DeletePdiFeedbackCommand request, CancellationToken cancellationToken)
    {
        PdiAccess.EnsureIsAdmin(currentUser);

        var feedback = await repository.GetFeedbackAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Feedback '{request.Id}' não encontrado.");

        repository.RemoveFeedback(feedback);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("PDI feedback {Id} deleted (admin).", feedback.Id);
    }
}
