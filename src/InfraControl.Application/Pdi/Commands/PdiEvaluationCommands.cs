using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Pdi.Commands;

// Avaliação final do PDI (docs/30 §15.3, §16, §17, Fase 6 — ADR-036):
// autoavaliação do colaborador (leva o plano a Em avaliação), avaliação
// do gestor/Admin (exige a autoavaliação) e resultado final único em 4
// estados (§16) que conclui — ou replaneja — o plano.

public sealed record SavePdiAssessmentCommand(
    Guid UserId,
    string Content) : BaseCommand<PdiAssessmentDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class SavePdiAssessmentCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<SavePdiAssessmentCommandHandler> logger)
    : IRequestHandler<SavePdiAssessmentCommand, PdiAssessmentDto>
{
    public async Task<PdiAssessmentDto> Handle(
        SavePdiAssessmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Usuário autenticado sem identificação — não é possível salvar a avaliação.");

        var plan = await plans.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");

        // Lado determinado pelo ator (§15.3): dono = Self;
        // gestor definido ou Admin = Manager; resto = 403.
        var side = actorId == plan.UserId
            ? PdiAssessmentSide.Self
            : PdiAccess.IsAdmin(currentUser)
              || (plan.ManagerId is { } managerId && managerId == actorId)
                ? PdiAssessmentSide.Manager
                : throw new UnauthorizedAccessException(
                    "Apenas o dono, o gestor ou um administrador pode registrar a avaliação.");

        // Resultado final (ou encerramento) congela a avaliação.
        if (plan.Status is PdiStatus.Completed or PdiStatus.Cancelled)
            throw new InvalidOperationException(
                "O PDI foi concluído ou cancelado — a avaliação não pode mais ser alterada.");

        if (side == PdiAssessmentSide.Self)
        {
            if (plan.Status is not (PdiStatus.InProgress
                    or PdiStatus.UnderEvaluation))
                throw new InvalidOperationException(
                    "A autoavaliação só pode ser registrada durante a " +
                    "execução ou a avaliação do PDI.");
        }
        else
        {
            // §15.3: gestor só avalia depois da autoavaliação, com o
            // plano já em Em avaliação (efeito colateral do Self).
            var selfAssessment = await repository.GetAssessmentAsync(
                plan.Id, PdiAssessmentSide.Self, cancellationToken);
            if (selfAssessment is null)
                throw new ArgumentException(
                    "A autoavaliação é obrigatória antes da avaliação do gestor.",
                    nameof(request));

            if (plan.Status != PdiStatus.UnderEvaluation)
                throw new InvalidOperationException(
                    "O PDI precisa estar em avaliação (após a autoavaliação).");
        }

        var existing = await repository.GetAssessmentAsync(
            plan.Id, side, cancellationToken);

        Domain.Entities.PdiAssessment assessment;
        if (existing is not null)
        {
            existing.Update(request.Content);
            assessment = existing;
        }
        else
        {
            assessment = PdiAssessment.Create(
                plan.Id, side, actorId, request.Content);
            repository.AddAssessment(assessment);

            if (side == PdiAssessmentSide.Self
                && plan.Status == PdiStatus.InProgress)
            {
                // Efeito colateral do §15.3: enviar a autoavaliação
                // move o plano para Em avaliação (status-only, sem
                // motivo — mesma regra do aceite da §14).
                var owner = await plans.GetOwnerAsync(
                        plan.UserId, cancellationToken)
                    ?? throw new KeyNotFoundException(
                        $"Usuário '{plan.UserId}' não encontrado.");
                plan.Update(
                    new PdiChanges(
                        plan.Strategy, plan.TargetDate,
                        PdiStatus.UnderEvaluation,
                        plan.ManagerId, plan.PeriodStart, plan.PeriodEnd,
                        Reason: null),
                    new PdiChangeActor(
                        actorId, currentUser.UserName ?? owner.Email,
                        owner.Email));
            }

            plan.RecordAssessmentCreated(actorId, side);
            plans.AddHistoryRange(plan.DrainPendingHistory());
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI assessment {Side} saved for plan {PlanId} by {Actor}.",
            side, plan.Id, actorId);

        var authorName = await plans.GetUserNameAsync(
            assessment.AuthorId, cancellationToken);
        return PdiAssessmentDto.From(assessment, plan.UserId, authorName);
    }
}

public sealed record FinalizePdiResultCommand(
    Guid UserId,
    string Outcome,
    string? Notes) : BaseCommand<PdiResultDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "PDI";
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class FinalizePdiResultCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<FinalizePdiResultCommandHandler> logger)
    : IRequestHandler<FinalizePdiResultCommand, PdiResultDto>
{
    public async Task<PdiResultDto> Handle(
        FinalizePdiResultCommand request, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Usuário autenticado sem identificação — não é possível finalizar o resultado.");

        var owner = await plans.GetOwnerAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Usuário '{request.UserId}' não encontrado.");

        var plan = await plans.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");

        // §16: resultado é do gestor/Admin — o colaborador não finaliza.
        PdiAccess.EnsureCanEvaluate(currentUser, plan);

        if (!Enum.TryParse<PdiResultOutcome>(
                request.Outcome, ignoreCase: true, out var outcome)
            || !Enum.IsDefined(outcome))
            throw new ArgumentException(
                "Resultado inválido (use Atingido, ParcialmenteAtingido, " +
                "NaoAtingido ou Replanejado).",
                nameof(request));

        if (plan.Status != PdiStatus.UnderEvaluation)
            throw new InvalidOperationException(
                "O resultado final só pode ser registrado com o PDI em avaliação.");

        var managerAssessment = await repository.GetAssessmentAsync(
            plan.Id, PdiAssessmentSide.Manager, cancellationToken);
        if (managerAssessment is null)
            throw new ArgumentException(
                "A avaliação do gestor é obrigatória antes do resultado.",
                nameof(request));

        var alreadyFinalized = await repository.GetResultAsync(
            plan.Id, cancellationToken);
        if (alreadyFinalized is not null)
            throw new InvalidOperationException(
                "O resultado final já foi registrado para este PDI.");

        var result = PdiResult.Create(
            plan.Id, outcome, request.Notes, actorId);
        repository.AddResult(result);

        // §16/§3: conclui o plano — ou o devolve ao ciclo quando o
        // resultado é "Replanejado" (seta nova da ADR-036).
        plan.Update(
            new PdiChanges(
                plan.Strategy, plan.TargetDate,
                outcome == PdiResultOutcome.Replanejado
                    ? PdiStatus.Replanned
                    : PdiStatus.Completed,
                plan.ManagerId, plan.PeriodStart, plan.PeriodEnd,
                Reason: null),
            new PdiChangeActor(
                actorId, currentUser.UserName ?? owner.Email, owner.Email));

        plan.RecordResultFinalized(actorId, outcome);
        plans.AddHistoryRange(plan.DrainPendingHistory());

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI result {Outcome} finalized for plan {PlanId} by {Actor}.",
            outcome, plan.Id, actorId);

        var finalizedName = await plans.GetUserNameAsync(
            result.FinalizedById, cancellationToken);
        return PdiResultDto.From(result, plan.UserId, finalizedName);
    }
}
