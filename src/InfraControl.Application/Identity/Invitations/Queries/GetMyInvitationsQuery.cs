using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Invitations.Queries;

/// <summary>
/// Retorna os convites do usuário logado (baseado no email do contexto).
/// Usuário comum vê apenas seus convites.
/// </summary>
public sealed record GetMyInvitationsQuery() : IQuery<IReadOnlyCollection<InvitationSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => [];
}

public sealed record InvitationSummaryDto(
    Guid Id,
    string Email,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    IReadOnlyCollection<string> Roles,
    DateTime? DecidedAt,
    Guid? ProjectId = null,
    string? ProjectCode = null,
    string? ProjectTitle = null,
    string? ProjectRole = null);

public sealed class GetMyInvitationsQueryHandler(
    IInvitationRepository repository,
    ICurrentUserContext currentUserContext,
    ILogger<GetMyInvitationsQueryHandler> logger) : IRequestHandler<GetMyInvitationsQuery, IReadOnlyCollection<InvitationSummaryDto>>
{
    public async Task<IReadOnlyCollection<InvitationSummaryDto>> Handle(GetMyInvitationsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserContext.IsAuthenticated || currentUserContext.UserName is null)
            throw new UnauthorizedAccessException("Usuário não autenticado.");

        var email = currentUserContext.UserName.Trim().ToLowerInvariant();

        var invitations = await repository.ListByEmailAsync(email, cancellationToken: cancellationToken);

        var dtos = invitations.Select(invitation => new InvitationSummaryDto(
            invitation.Id,
            invitation.Email,
            invitation.Status.ToString(),
            invitation.ExpiresAt,
            invitation.CreateDateTime,
            invitation.Roles.Select(role => role.ToString()).ToArray(),
            invitation.DecidedAt,
            invitation.ProjectId,
            invitation.ProjectCode,
            invitation.ProjectTitle,
            invitation.ProjectRole?.ToString()
        )).ToList();

        logger.LogInformation("User {Email} retrieved {Count} invitations.", email, dtos.Count);

        return dtos;
    }
}
