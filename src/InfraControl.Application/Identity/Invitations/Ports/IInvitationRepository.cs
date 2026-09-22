using InfraControl.Domain.Entities;

namespace InfraControl.Application.Identity.Invitations.Ports;

public interface IInvitationRepository
{
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task<Invitation?> GetAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
