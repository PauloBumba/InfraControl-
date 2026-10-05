using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Identity.Invitations.Queries;

public sealed record SearchInvitationsQuery(
    InvitationStatus? Status = null,
    string? Email = null,
    int Skip = 0,
    int Take = 50) : IQuery<PagedResult<InvitationDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed record InvitationDto(
    Guid Id,
    string Email,
    InvitationStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    Guid CreatedBy,
    IReadOnlyCollection<string> Roles,
    DateTime? DecidedAt);

public sealed class SearchInvitationsQueryHandler(
    IInvitationRepository repository) : IRequestHandler<SearchInvitationsQuery, PagedResult<InvitationDto>>
{
    public async Task<PagedResult<InvitationDto>> Handle(SearchInvitationsQuery request, CancellationToken cancellationToken)
    {
        var invitations = await repository.SearchAsync(
            request.Status,
            request.Email,
            request.Skip,
            request.Take,
            cancellationToken);

        var totalCount = await repository.CountAsync(
            request.Status,
            request.Email,
            cancellationToken);

        var dtos = invitations.Select(invitation => new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.CreateDateTime,
            invitation.CreatedBy,
            invitation.Roles.Select(role => role.ToString()).ToArray(),
            invitation.DecidedAt
        )).ToList();

        return new PagedResult<InvitationDto>(dtos, totalCount, request.Skip, request.Take);
    }
}
