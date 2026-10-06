using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Application.Identity.Invitations.Queries;
using InfraControl.BuildingBlocks.Application;
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
    int ExpiresInDays = 7,
    string? BaseUrl = null) : BaseCommand<CreateInvitationResult>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed record CreateInvitationResult(Guid InvitationId, string Token);

public sealed class CreateInvitationCommandHandler(
    IInvitationRepository repository,
    IIdentityEmailSender emailSender,
    ILogger<CreateInvitationCommandHandler> logger) : IRequestHandler<CreateInvitationCommand, CreateInvitationResult>
{
    public async Task<CreateInvitationResult> Handle(CreateInvitationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("Um e-mail válido é obrigatório.", nameof(request.Email));

        if (request.Roles.Count == 0)
            throw new ArgumentException("Ao menos uma função é obrigatória.", nameof(request.Roles));

        if (request.CreatedBy == Guid.Empty)
            throw new ArgumentException("O id do convidante é obrigatório.", nameof(request.CreatedBy));

        var expiresInDays = Math.Clamp(request.ExpiresInDays, 1, 30);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existing = await repository.GetPendingByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Já existe um convite pendente para '{normalizedEmail}'.");

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

        // F1-T017: o convite é MANDADO POR E-MAIL pelo mesmo canal do
        // forgot-password (IIdentityEmailSender — adaptador de logging hoje;
        // provider real troca sem tocar Application). Com o TransactionBehavior
        // uma falha de envio desfaz a linha e o admin pode recriar sem esbarrar
        // no "convite pendente". Token é URL-safe (base64url), igual ao reset.
        var baseUrl = request.BaseUrl ?? string.Empty;
        // Página pública de aceite no frontend (pendência F1-T017 fechada) —
        // o link não bate mais na API (GET era 405): /invitations/{id}?token=.
        var acceptUrl = $"{baseUrl.TrimEnd('/')}/invitations/{invitation.Id}?token={token}";
        await emailSender.SendInvitationAsync(normalizedEmail, acceptUrl, cancellationToken);

        logger.LogInformation("Invitation {InvitationId} created for {Email}.", invitation.Id, normalizedEmail);

        return new CreateInvitationResult(invitation.Id, token);
    }
}

public sealed record CancelInvitationCommand(Guid InvitationId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class CancelInvitationCommandHandler(
    IInvitationRepository repository,
    ILogger<CancelInvitationCommandHandler> logger) : IRequestHandler<CancelInvitationCommand>
{
    public async Task Handle(CancelInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await repository.GetAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Convite '{request.InvitationId}' não encontrado.");

        invitation.Cancel();
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Invitation {InvitationId} cancelled.", request.InvitationId);
    }
}

/// <summary>
/// ADR-030 §1 — PUT do convite pendente: só as roles são editáveis
/// (email/expiração ficam no ciclo de cancelar e recriar — o token já
/// foi enviado ao e-mail original).
/// </summary>
public sealed record UpdateInvitationCommand(
    Guid InvitationId,
    IReadOnlyCollection<UserRole> Roles) : BaseCommand<InvitationDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class UpdateInvitationCommandHandler(
    IInvitationRepository repository,
    ILogger<UpdateInvitationCommandHandler> logger)
    : IRequestHandler<UpdateInvitationCommand, InvitationDto>
{
    public async Task<InvitationDto> Handle(
        UpdateInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await repository.GetByIdAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Convite '{request.InvitationId}' não encontrado.");

        // Domínio valida (Pending 409 / vazio 400) e muta a coleção em
        // memória; o replace físico fica no repositório (ver IInvitationRepository).
        invitation.SetRoles(request.Roles);
        await repository.ReplaceRolesAsync(
            invitation.Id, invitation.Roles.ToArray(), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Invitation {InvitationId} roles updated to {Roles}.",
            invitation.Id, string.Join(", ", invitation.Roles));

        return new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.CreateDateTime,
            invitation.CreatedBy,
            invitation.Roles.Select(role => role.ToString()).ToArray(),
            invitation.DecidedAt);
    }
}
