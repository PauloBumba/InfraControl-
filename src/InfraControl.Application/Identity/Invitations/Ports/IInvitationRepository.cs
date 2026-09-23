using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Identity.Invitations.Ports;

public interface IInvitationRepository : IRepository<Invitation>
{
    Task<Invitation?> GetAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
