using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Pdi.Commands;

// PDI (docs/29 §8, docs/30, ADR-029) — CRUD apenas: upsert por usuário
// (1 plano vigente), sem remoção (encerrar = Cancelled). Erros: 403
// (operador em PDI de outro / transição que não é o aceite), 404
// (usuário inexistente), 400 (estratégia/prazo/status/período/transição
// inválidos — 400 do domínio). Gestor e período: só o Admin define
// (entrada do Operador é ignorada, ADR-029 §3).

public sealed record UpsertPdiCommand(
    Guid UserId,
    string Strategy,
    DateOnly TargetDate,
    string Status,
    Guid? ManagerId = null,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null,
    string? Reason = null) : BaseCommand<PdiDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpsertPdiCommandHandler(
    IPdiRepository repository,
    ICurrentUserContext currentUser,
    ILogger<UpsertPdiCommandHandler> logger)
    : IRequestHandler<UpsertPdiCommand, PdiDto>
{
    public async Task<PdiDto> Handle(
        UpsertPdiCommand request, CancellationToken cancellationToken)
    {
        // Admin pode tudo; Operador só o próprio PDI (decisão do usuário,
        // 2026-10-03). A policy Operate do controller já barrou o Viewer.
        var isAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString());
        if (!isAdmin && currentUser.UserId != request.UserId)
            throw new UnauthorizedAccessException(
                "Você só pode editar o seu próprio PDI.");

        var actorId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Usuário autenticado sem identificação — não é possível salvar o PDI.");

        if (!Enum.TryParse<PdiStatus>(request.Status, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
            throw new ArgumentException(
                "Status do PDI inválido (use Draft, Planned, AwaitingAcceptance, " +
                "Active, InProgress, UnderEvaluation, Completed, Replanned ou Cancelled).",
                nameof(request.Status));

        var owner = await repository.GetOwnerAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Usuário '{request.UserId}' não encontrado.");

        var actor = new Domain.Events.PdiChangeActor(
            actorId,
            currentUser.UserName ?? owner.Email,
            owner.Email);

        var plan = await repository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (plan is null)
        {
            // "Admin gerencia todo o CRUD" (decisão do usuário, 2026-10-04):
            // criação é exclusiva do Admin — o Operador só edita o que existe.
            if (!isAdmin)
                throw new UnauthorizedAccessException(
                    "Apenas o administrador pode criar um PDI.");

            plan = IndividualDevelopmentPlan.Create(
                request.UserId, request.Strategy, request.TargetDate, status,
                request.ManagerId, request.PeriodStart, request.PeriodEnd, actor);
            await repository.AddAsync(plan, cancellationToken);
        }
        else
        {
            // Operador: só o aceite (AwaitingAcceptance → Active) muda o
            // status; qualquer outra transição é privativa do Admin.
            if (!isAdmin && status != plan.Status
                && !(plan.Status == PdiStatus.AwaitingAcceptance
                     && status == PdiStatus.Active))
                throw new UnauthorizedAccessException(
                    "Apenas o administrador pode alterar o status do PDI " +
                    "(exceto o aceite quando Aguardando aceite).");

            // Gestor/período: Admin define; entrada do Operador é ignorada
            // (mantém o valor vigente — ADR-029 §3).
            var managerId = isAdmin ? request.ManagerId : plan.ManagerId;
            var periodStart = isAdmin ? request.PeriodStart : plan.PeriodStart;
            var periodEnd = isAdmin ? request.PeriodEnd : plan.PeriodEnd;

            plan.Update(
                new PdiChanges(
                    request.Strategy, request.TargetDate, status,
                    managerId, periodStart, periodEnd, request.Reason),
                actor);
        }

        var history = plan.DrainPendingHistory();
        if (history.Count > 0)
            repository.AddHistoryRange(history);

        await repository.SaveChangesAsync(cancellationToken);

        string? managerName = plan.ManagerId is { } manager
            ? await repository.GetUserNameAsync(manager, cancellationToken)
            : null;

        logger.LogInformation(
            "PDI for {UserId} saved ({Status}, target {TargetDate}) by {ActorId}.",
            request.UserId, status, request.TargetDate, actorId);

        return PdiDto.From(plan, owner.Name, owner.Email, managerName);
    }
}
