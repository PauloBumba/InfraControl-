using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Invitations.Commands;

/// <summary>
/// Aceite de convite em tempo real dentro da plataforma (usuário já autenticado).
/// Diferente do fluxo de email que usa token, este usa o email do usuário logado.
/// </summary>
public sealed record AcceptInvitationInternalCommand(Guid InvitationId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => [];
}

public sealed class AcceptInvitationInternalCommandHandler(
    IInvitationRepository repository,
    ICurrentUserContext currentUserContext,
    IInvitationAccountPort accountPort,
    ILogger<AcceptInvitationInternalCommandHandler> logger) : IRequestHandler<AcceptInvitationInternalCommand>
{
    public async Task Handle(AcceptInvitationInternalCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserContext.IsAuthenticated || currentUserContext.UserId is null)
            throw new UnauthorizedAccessException("Usuário não autenticado.");

        if (currentUserContext.UserName is null)
            throw new UnauthorizedAccessException("E-mail do usuário não disponível.");

        var invitation = await repository.GetAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Convite '{request.InvitationId}' não encontrado.");

        // Verifica se o email do convite corresponde ao email do usuário logado
        var normalizedEmail = currentUserContext.UserName.Trim().ToLowerInvariant();
        if (invitation.Email != normalizedEmail)
            throw new UnauthorizedAccessException("Este convite não pertence ao seu e-mail.");

        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        // Aceita o convite (sem token pois já validamos o email do usuário autenticado)
        invitation.AcceptInternal(utcNow);

        // Adiciona as roles do convite ao usuário existente
        // O ProvisionInactiveAccountAsync já lida com usuário existente (sincroniza roles)
        await accountPort.ProvisionInactiveAccountAsync(invitation.Email, invitation.Roles, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Invitation {InvitationId} accepted by user {UserId}.", request.InvitationId, currentUserContext.UserId.Value);
    }
}
