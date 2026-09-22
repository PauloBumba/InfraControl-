using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class InvitationRepository(AppDbContext dbContext) : IInvitationRepository
{
    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        await dbContext.Invitations.AddAsync(invitation, cancellationToken);
    }

    public Task<Invitation?> GetAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return dbContext.Invitations
            .Include(invitation => invitation.RoleAssignments)
            .SingleOrDefaultAsync(invitation => invitation.Id == invitationId, cancellationToken);
    }

    public Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return dbContext.Invitations
            .Include(invitation => invitation.RoleAssignments)
            .Where(invitation => invitation.Email == normalizedEmail
                && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(invitation => invitation.CreateDateTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
