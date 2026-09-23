using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Invitations.Commands;

/// <summary>
/// Ativação do convite: valida o token (single-use), define a senha da conta
/// provisionada no aceite e habilita o usuário. É o ÚNICO passo que torna a
/// conta ativa no fluxo de convite.
/// </summary>
public sealed record SetInvitationPasswordCommand(
    Guid InvitationId,
    string Token,
    string Password) : BaseCommand<SetInvitationPasswordResult>;

public sealed record SetInvitationPasswordResult(Guid InvitationId, string Email);

public sealed class SetInvitationPasswordCommandHandler(
    IInvitationRepository repository,
    IInvitationAccountPort accountPort,
    ILogger<SetInvitationPasswordCommandHandler> logger)
    : IRequestHandler<SetInvitationPasswordCommand, SetInvitationPasswordResult>
{
    public async Task<SetInvitationPasswordResult> Handle(
        SetInvitationPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (request.InvitationId == Guid.Empty)
            throw new ArgumentException("O id do convite é obrigatório.", nameof(request.InvitationId));

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("O token é obrigatório.", nameof(request.Token));

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("A senha é obrigatória.", nameof(request.Password));

        var invitation = await repository.GetAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Convite '{request.InvitationId}' não encontrado.");

        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        invitation.EnsureActivatable(InvitationToken.Hash(request.Token.Trim()), utcNow);

        var activation = await accountPort.SetPasswordAndActivateAsync(
            invitation.Email, request.Password, cancellationToken);

        if (!activation.Succeeded)
            throw new RequestValidationException(activation.Errors);

        invitation.MarkActivated();
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Invitation {InvitationId} activated and account enabled for {Email}.",
            invitation.Id, invitation.Email);

        return new SetInvitationPasswordResult(invitation.Id, invitation.Email);
    }
}
