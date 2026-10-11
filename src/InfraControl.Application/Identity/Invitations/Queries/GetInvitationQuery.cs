using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Identity.Invitations.Queries;

/// <summary>
/// ADR-030 §2 — GET /admin/invitations/{id}: recuperação pontual do convite
/// com o MESMO DTO e a mesma 2ª camada (Administer) do Search.
/// </summary>
public sealed record GetInvitationQuery(Guid Id) : IQuery<InvitationDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class GetInvitationQueryHandler(
    IInvitationRepository repository) : IRequestHandler<GetInvitationQuery, InvitationDto>
{
    public async Task<InvitationDto> Handle(GetInvitationQuery request, CancellationToken cancellationToken)
    {
        var invitation = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Convite '{request.Id}' não encontrado.");

        return new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.CreateDateTime,
            invitation.CreatedBy,
            invitation.Roles.Select(role => role.ToString()).ToArray(),
            invitation.DecidedAt,
            invitation.ProjectId,
            invitation.ProjectCode,
            invitation.ProjectTitle,
            invitation.ProjectRole?.ToString());
    }
}
