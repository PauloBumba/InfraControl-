using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Categories.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class KnowledgeCategoryRepository(AppDbContext dbContext)
    : IKnowledgeCategoryRepository
{
    public async Task AddAsync(
        KnowledgeCategory category, CancellationToken cancellationToken = default)
    {
        await dbContext.KnowledgeCategories.AddAsync(category, cancellationToken);
    }

    public Task<KnowledgeCategory?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeCategories
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public void Remove(KnowledgeCategory category)
        => dbContext.KnowledgeCategories.Remove(category);

    public async Task<bool> HasNameAsync(
        string name, Guid excludeId = default, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return await dbContext.KnowledgeCategories.AnyAsync(
            category => category.Name.ToLower() == normalized && category.Id != excludeId,
            cancellationToken);
    }

    public async Task<PagedResult<KnowledgeCategory>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeCategories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(category =>
                category.Name.ToLower().Contains(normalized)
                || (category.Description != null
                    && category.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(category => category.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<KnowledgeCategory>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
