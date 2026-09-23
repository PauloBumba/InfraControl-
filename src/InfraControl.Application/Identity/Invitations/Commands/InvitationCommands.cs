using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Invitations.Commands;

public sealed record CreateInvitationCommand(
    string Email,
    IReadOnlyCollection<UserRole> Roles,
    Guid CreatedBy,
    int ExpiresInDays = 7) : BaseCommand<CreateInvitationResult>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.IdentityUsersManage.ToString()];
}

public sealed record CreateInvitationResult(Guid InvitationId, string Token);

public sealed class CreateInvitationCommandHandler(
    IInvitationRepository repository,
    ILogger<CreateInvitationCommandHandler> logger) : IRequestHandler<CreateInvitationCommand, CreateInvitationResult>
{
    public async Task<CreateInvitationResult> Handle(CreateInvitationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("A valid email is required.", nameof(request.Email));

        if (request.Roles.Count == 0)
            throw new ArgumentException("At least one role is required.", nameof(request.Roles));

        if (request.CreatedBy == Guid.Empty)
            throw new ArgumentException("An inviter id is required.", nameof(request.CreatedBy));

        var expiresInDays = Math.Clamp(request.ExpiresInDays, 1, 30);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existing = await repository.GetPendingByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"A pending invitation already exists for '{normalizedEmail}'.");

        var token = InvitationToken.Generate();
        var tokenHash = InvitationToken.Hash(token);
        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        var invitation = Invitation.Create(
            normalizedEmail,
            tokenHash,
            utcNow.AddDays(expiresInDays),
            request.CreatedBy,
            request.Roles.Distinct().ToArray());

        await repository.AddAsync(invitation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Invitation {InvitationId} created for {Email}.", invitation.Id, normalizedEmail);

        return new CreateInvitationResult(invitation.Id, token);
    }
}

public sealed record CancelInvitationCommand(Guid InvitationId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.IdentityUsersManage.ToString()];
}

public sealed class CancelInvitationCommandHandler(
    IInvitationRepository repository,
    ILogger<CancelInvitationCommandHandler> logger) : IRequestHandler<CancelInvitationCommand>
{
    public async Task Handle(CancelInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await repository.GetAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invitation '{request.InvitationId}' was not found.");

        invitation.Cancel();
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Invitation {InvitationId} cancelled.", request.InvitationId);
    }
}
