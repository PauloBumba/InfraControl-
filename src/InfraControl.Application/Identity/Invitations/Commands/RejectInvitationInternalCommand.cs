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
/// Recusa de convite em tempo real dentro da plataforma (usuário já autenticado).
/// Diferente do fluxo de email que usa token, este usa o email do usuário logado.
/// </summary>
public sealed record RejectInvitationInternalCommand(Guid InvitationId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => [];
}

public sealed class RejectInvitationInternalCommandHandler(
    IInvitationRepository repository,
    ICurrentUserContext currentUserContext,
    ILogger<RejectInvitationInternalCommandHandler> logger) : IRequestHandler<RejectInvitationInternalCommand>
{
    public async Task Handle(RejectInvitationInternalCommand request, CancellationToken cancellationToken)
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

        // Recusa o convite (sem token pois já validamos o email do usuário autenticado)
        invitation.RejectInternal(utcNow);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Invitation {InvitationId} rejected by user {UserId}.", request.InvitationId, currentUserContext.UserId.Value);
    }
}
