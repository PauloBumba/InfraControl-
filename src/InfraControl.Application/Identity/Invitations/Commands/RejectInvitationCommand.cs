using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Invitations.Commands;

public sealed record RejectInvitationCommand(Guid InvitationId, string Token) : IRequest;

public sealed class RejectInvitationCommandHandler(
    IInvitationRepository repository,
    ILogger<RejectInvitationCommandHandler> logger) : IRequestHandler<RejectInvitationCommand>
{
    public async Task Handle(RejectInvitationCommand request, CancellationToken cancellationToken)
    {
        if (request.InvitationId == Guid.Empty)
            throw new ArgumentException("An invitation id is required.", nameof(request.InvitationId));

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("A token is required.", nameof(request.Token));

        var invitation = await repository.GetAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invitation '{request.InvitationId}' was not found.");

        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        invitation.Reject(InvitationToken.Hash(request.Token.Trim()), utcNow);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Invitation {InvitationId} rejected for {Email}.", invitation.Id, invitation.Email);
    }
}
