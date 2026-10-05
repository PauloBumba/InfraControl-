using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Pdi.Commands;

// PDI (docs/29 §8) — CRUD apenas: upsert por usuário (1 plano vigente),
// sem remoção (encerrar = Cancelled). Erros: 403 (operador em PDI de
// outro), 404 (usuário inexistente), 400 (estratégia/prazo/status).

public sealed record UpsertPdiCommand(
    Guid UserId,
    string Strategy,
    DateOnly TargetDate,
    string Status) : BaseCommand<PdiDto>, IAuthorizedRequest
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
                "Status do PDI inválido (use InProgress, Completed ou Cancelled).",
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

            plan = Domain.Entities.IndividualDevelopmentPlan.Create(
                request.UserId, request.Strategy, request.TargetDate, status, actor);
            await repository.AddAsync(plan, cancellationToken);
        }
        else
        {
            plan.Update(request.Strategy, request.TargetDate, status, actor);
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI for {UserId} saved ({Status}, target {TargetDate}) by {ActorId}.",
            request.UserId, status, request.TargetDate, actorId);

        return PdiDto.From(plan, owner.Name, owner.Email);
    }
}
