using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Invitations.Commands;

public sealed record RejectInvitationCommand(Guid InvitationId, string Token) : BaseCommand;

public sealed class RejectInvitationCommandHandler(
    IInvitationRepository repository,
    ILogger<RejectInvitationCommandHandler> logger) : IRequestHandler<RejectInvitationCommand>
{
    public async Task Handle(RejectInvitationCommand request, CancellationToken cancellationToken)
    {
        if (request.InvitationId == Guid.Empty)
            throw new ArgumentException("O id do convite é obrigatório.", nameof(request.InvitationId));

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("O token é obrigatório.", nameof(request.Token));

        var invitation = await repository.GetAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Convite '{request.InvitationId}' não encontrado.");

        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        invitation.Reject(InvitationToken.Hash(request.Token.Trim()), utcNow);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Invitation {InvitationId} rejected for {Email}.", invitation.Id, invitation.Email);
    }
}
