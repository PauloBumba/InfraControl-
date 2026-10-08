using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
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
            .Include(article => article.Tags) // K1: substituição no PUT precisa das atuais
            .SingleOrDefaultAsync(article => article.Id == id, cancellationToken);

    public void Remove(KnowledgeArticle article)
        => dbContext.KnowledgeArticles.Remove(article);

    public async Task<PagedResult<KnowledgeArticle>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        bool includeUnpublished,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeArticles
            .AsNoTracking()
            .Include(article => article.Tags) // K1: dto traz as tags (1 query)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(article =>
                article.Title.ToLower().Contains(normalized)
                || article.Content.ToLower().Contains(normalized));
        }

        if (!includeUnpublished)
            query = query.Where(article =>
                !KnowledgeArticleStatusExtensions.HiddenFromReaders
                    .Contains(article.Status));

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

    // F24-T001/K1 — precedente TaskAssigneeRepository.GetUserNamesAsync:
    // usuário excluído some do dicionário, o chamador trata como nome não
    // encontrado.
    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, string>();

        var users = await dbContext.DomainUsers.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => user.Id, user => user.DisplayName);
    }

    public Task<bool> UserExistsAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => dbContext.DomainUsers.AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
}
