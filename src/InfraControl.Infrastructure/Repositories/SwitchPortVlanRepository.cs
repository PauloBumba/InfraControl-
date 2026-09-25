using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Network.SwitchPortVlans.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class SwitchPortVlanRepository(AppDbContext dbContext) : ISwitchPortVlanRepository
{
    public async Task AddAsync(SwitchPortVlan link, CancellationToken cancellationToken = default)
    {
        await dbContext.SwitchPortVlans.AddAsync(link, cancellationToken);
    }

    public Task<SwitchPortVlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.SwitchPortVlans
            .SingleOrDefaultAsync(link => link.Id == id, cancellationToken);

    public void Remove(SwitchPortVlan link)
        => dbContext.SwitchPortVlans.Remove(link);

    public Task<SwitchPortVlan?> GetPairAsync(
        Guid switchPortId,
        Guid vlanId,
        CancellationToken cancellationToken = default)
        => dbContext.SwitchPortVlans
            .SingleOrDefaultAsync(
                link => link.SwitchPortId == switchPortId && link.VlanId == vlanId,
                cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetVlanIdsByPortAsync(
        Guid switchPortId,
        CancellationToken cancellationToken = default)
        => await dbContext.SwitchPortVlans
            .AsNoTracking()
            .Where(link => link.SwitchPortId == switchPortId)
            .Select(link => link.VlanId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetPortIdsByVlanAsync(
        Guid vlanId,
        CancellationToken cancellationToken = default)
        => await dbContext.SwitchPortVlans
            .AsNoTracking()
            .Where(link => link.VlanId == vlanId)
            .Select(link => link.SwitchPortId)
            .ToListAsync(cancellationToken);

    public Task<bool> HasVlanLinksAsync(Guid switchPortId, CancellationToken cancellationToken = default)
        => dbContext.SwitchPortVlans
            .AnyAsync(link => link.SwitchPortId == switchPortId, cancellationToken);

    public Task<bool> HasPortLinksAsync(Guid vlanId, CancellationToken cancellationToken = default)
        => dbContext.SwitchPortVlans
            .AnyAsync(link => link.VlanId == vlanId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
