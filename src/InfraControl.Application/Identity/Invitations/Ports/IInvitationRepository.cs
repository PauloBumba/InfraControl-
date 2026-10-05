using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Identity.Invitations.Ports;

public interface IInvitationRepository : IRepository<Invitation>
{
    Task<Invitation?> GetAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<List<Invitation>> SearchAsync(
        InvitationStatus? status = null,
        string? email = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        InvitationStatus? status = null,
        string? email = null,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
