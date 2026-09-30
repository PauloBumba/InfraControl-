using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class KnowledgeArticleRepository(AppDbContext dbContext)
    : IKnowledgeArticleRepository
{
    public async Task AddAsync(
        KnowledgeArticle article, CancellationToken cancellationToken = default)
    {
        await dbContext.KnowledgeArticles.AddAsync(article, cancellationToken);
    }

    public Task<KnowledgeArticle?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticles
            .SingleOrDefaultAsync(article => article.Id == id, cancellationToken);

    public void Remove(KnowledgeArticle article)
        => dbContext.KnowledgeArticles.Remove(article);

    public async Task<PagedResult<KnowledgeArticle>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeArticles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(article =>
                article.Title.ToLower().Contains(normalized)
                || article.Content.ToLower().Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(article => article.CreateDateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<KnowledgeArticle>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
