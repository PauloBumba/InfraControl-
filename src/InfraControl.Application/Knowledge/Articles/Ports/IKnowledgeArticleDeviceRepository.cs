using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Articles.Ports;

/// <summary>F10-T004: devices vinculados ao artigo (N:N com Device).</summary>
public interface IKnowledgeArticleDeviceRepository : IRepository<KnowledgeArticleDevice>
{
    Task<bool> ExistsAsync(
        Guid articleId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDevice?> FindAsync(
        Guid articleId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetDeviceIdsAsync(
        Guid articleId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
