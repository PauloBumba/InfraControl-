using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// ADR-057 (FAI-T011a): cadastro de providers de IA. <c>Upsert</c> de
/// rota carrega a linha rastreada e reusa o Id (índice único em
/// capability) — o <c>route</c> passado é cópia de trabalho; o rastreado
/// é a fonte de verdade do Id.
/// </summary>
public sealed class AiProviderRepository(AppDbContext dbContext)
    : IAiProviderRepository
{
    public async Task<AiProvider?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.AiProviders
            .SingleOrDefaultAsync(provider => provider.Id == id, cancellationToken);

    public async Task<AiProvider?> GetByNameAsync(
        string name, CancellationToken cancellationToken = default)
        => await dbContext.AiProviders
            .AsNoTracking()
            .SingleOrDefaultAsync(provider => provider.Name == name, cancellationToken);

    public async Task<IReadOnlyList<AiProvider>> ListAsync(
        CancellationToken cancellationToken = default)
        => await dbContext.AiProviders
            .AsNoTracking()
            .OrderBy(provider => provider.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        AiProvider entity, CancellationToken cancellationToken = default)
        => await dbContext.AiProviders.AddAsync(entity, cancellationToken);

    public void Remove(AiProvider entity)
        => dbContext.AiProviders.Remove(entity);

    public async Task<AiProviderCapability?> GetCapabilityRouteAsync(
        string capability, CancellationToken cancellationToken = default)
        => await dbContext.AiProviderCapabilities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                route => route.Capability == capability, cancellationToken);

    public async Task UpsertCapabilityRouteAsync(
        AiProviderCapability route, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.AiProviderCapabilities
            .SingleOrDefaultAsync(
                item => item.Capability == route.Capability, cancellationToken);

        if (existing is null)
        {
            await dbContext.AiProviderCapabilities.AddAsync(route, cancellationToken);
            return;
        }

        existing.Update(
            route.Capability,
            route.ProviderId,
            route.DefaultModel,
            route.FallbackProviderId,
            route.FallbackModel);
    }

    public async Task<bool> CapabilityRouteExistsAsync(
        string capability, CancellationToken cancellationToken = default)
        => await dbContext.AiProviderCapabilities
            .AsNoTracking()
            .AnyAsync(
                route => route.Capability == capability, cancellationToken);
}
