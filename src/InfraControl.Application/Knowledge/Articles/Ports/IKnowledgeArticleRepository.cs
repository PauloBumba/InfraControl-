using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Articles.Ports;

public interface IKnowledgeArticleRepository : IRepository<KnowledgeArticle>
{
    /// <summary>
    /// Busca por termo no título/conteúdo, mais recentes primeiro
    /// (CreateDateTime desc).
    /// </summary>
    Task<PagedResult<KnowledgeArticle>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
