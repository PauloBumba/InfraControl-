using InfraControl.Application.Alerts.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Alerts.Commands;

/// <summary>
/// Atribui (ou transfere) o responsável pela tratativa do alerta.
/// Usuário inexistente → 404 pt-BR. Reatribuir = transferência.
/// </summary>
public sealed record AssignAlertCommand(Guid AlertId, Guid UserId)
    : BaseCommand<AlertDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AssignAlertCommandHandler(IAlertRepository repository)
    : IRequestHandler<AssignAlertCommand, AlertDto>
{
    public async Task<AlertDto> Handle(
        AssignAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await repository.GetByIdAsync(request.AlertId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Alerta '{request.AlertId}' não encontrado.");

        if (!await repository.UserExistsAsync(request.UserId, cancellationToken))
            throw new KeyNotFoundException(
                $"Usuário '{request.UserId}' não encontrado.");

        alert.Assign(request.UserId);
        await repository.SaveChangesAsync(cancellationToken);

        var names = await repository.GetUserNamesAsync(
            alert.AssignedToId is { } id ? [id] : [], cancellationToken);

        return AlertDto.From(
            alert,
            assignedToName: alert.AssignedToId is { } assigned
                ? names.GetValueOrDefault(assigned)
                : null);
    }
}

/// <summary>Remove o responsável do alerta.</summary>
public sealed record UnassignAlertCommand(Guid AlertId)
    : BaseCommand<AlertDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnassignAlertCommandHandler(IAlertRepository repository)
    : IRequestHandler<UnassignAlertCommand, AlertDto>
{
    public async Task<AlertDto> Handle(
        UnassignAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await repository.GetByIdAsync(request.AlertId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Alerta '{request.AlertId}' não encontrado.");

        alert.Unassign();
        await repository.SaveChangesAsync(cancellationToken);

        return AlertDto.From(alert);
    }
}
