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

    public Task<Invitation?> GetByIdAsync(Guid invitationId, CancellationToken cancellationToken = default)
        => GetAsync(invitationId, cancellationToken);

    public void Remove(Invitation invitation)
        => dbContext.Invitations.Remove(invitation);

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

    public Task<List<Invitation>> SearchAsync(
        InvitationStatus? status = null,
        string? email = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invitations
            .Include(invitation => invitation.RoleAssignments)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(invitation => invitation.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            query = query.Where(invitation => invitation.Email.Contains(normalizedEmail));
        }

        return query
            .OrderByDescending(invitation => invitation.CreateDateTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        InvitationStatus? status = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invitations.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(invitation => invitation.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            query = query.Where(invitation => invitation.Email.Contains(normalizedEmail));
        }

        return query.CountAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
