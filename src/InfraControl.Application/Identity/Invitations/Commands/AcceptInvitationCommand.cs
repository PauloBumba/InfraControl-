using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Invitations.Commands;

public sealed record AcceptInvitationCommand(Guid InvitationId, string Token) : IRequest<AcceptInvitationResult>;

public sealed record AcceptInvitationResult(Guid InvitationId, string Email, IReadOnlyCollection<UserRole> Roles);

public sealed class AcceptInvitationCommandHandler(
    IInvitationRepository repository,
    IInvitationAccountPort accountPort,
    ILogger<AcceptInvitationCommandHandler> logger) : IRequestHandler<AcceptInvitationCommand, AcceptInvitationResult>
{
    public async Task<AcceptInvitationResult> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        if (request.InvitationId == Guid.Empty)
            throw new ArgumentException("An invitation id is required.", nameof(request.InvitationId));

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("A token is required.", nameof(request.Token));

        var invitation = await repository.GetAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invitation '{request.InvitationId}' was not found.");

        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        invitation.Accept(InvitationToken.Hash(request.Token.Trim()), utcNow);

        // O aceite provisiona a conta INATIVA; ela só fica ativa no passo
        // de definição de senha (SetInvitationPasswordCommand).
        var userId = await accountPort.ProvisionInactiveAccountAsync(
            invitation.Email, invitation.Roles, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Invitation {InvitationId} accepted for {Email}; account {UserId} provisioned inactive.",
            invitation.Id, invitation.Email, userId);

        return new AcceptInvitationResult(invitation.Id, invitation.Email, invitation.Roles);
    }
}
