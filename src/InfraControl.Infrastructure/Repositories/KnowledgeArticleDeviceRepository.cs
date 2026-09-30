using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F10-T004: devices do artigo (chave composta).</summary>
public sealed class KnowledgeArticleDeviceRepository(AppDbContext dbContext)
    : IKnowledgeArticleDeviceRepository
{
    public Task<KnowledgeArticleDevice?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (ArticleId, DeviceId) é unmapped (padrão TaskDevice).
        => throw new NotSupportedException(
            "KnowledgeArticleDevice usa FindAsync(articleId, deviceId) — a chave é o par composto.");

    public async Task AddAsync(
        KnowledgeArticleDevice entity, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleDevices.AddAsync(entity, cancellationToken);

    public void Remove(KnowledgeArticleDevice entity)
        => dbContext.KnowledgeArticleDevices.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid articleId, Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleDevices.AnyAsync(
            link => link.ArticleId == articleId && link.DeviceId == deviceId,
            cancellationToken);

    public Task<KnowledgeArticleDevice?> FindAsync(
        Guid articleId, Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleDevices.FirstOrDefaultAsync(
            link => link.ArticleId == articleId && link.DeviceId == deviceId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetDeviceIdsAsync(
        Guid articleId, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleDevices.AsNoTracking()
            .Where(link => link.ArticleId == articleId)
            .Select(link => link.DeviceId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
