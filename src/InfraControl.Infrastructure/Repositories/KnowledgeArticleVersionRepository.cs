using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F24-T003/K3 — snapshots de versão. Nomes de autor/aprovador
/// resolvidos no join (mesmo critério do artigo/K1 — o front não tem
/// como resolver sem a policy do /admin/users).
/// </summary>
public sealed class KnowledgeArticleVersionRepository(AppDbContext dbContext)
    : IKnowledgeArticleVersionRepository
{
    public Task<KnowledgeArticleVersion?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleVersions
            .SingleOrDefaultAsync(version => version.Id == id, cancellationToken);

    public async Task AddAsync(
        KnowledgeArticleVersion entity, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleVersions.AddAsync(entity, cancellationToken);

    public void Remove(KnowledgeArticleVersion entity)
        => dbContext.KnowledgeArticleVersions.Remove(entity);

    public async Task<IReadOnlyCollection<KnowledgeArticleVersionWithActors>> ListByArticleAsync(
        Guid articleId, CancellationToken cancellationToken = default)
    {
        var versions = await dbContext.KnowledgeArticleVersions.AsNoTracking()
            .Where(version => version.ArticleId == articleId)
            .ToListAsync(cancellationToken);

        // Ordem numérica (major, minor) desc — CreateDateTime pode
        // empatar no mesmo instante (duas edições no mesmo tick) e a
        // string "1.10" não ordena com "1.2".
        return await WithActorsAsync(
            versions
                .OrderByDescending(version => Order(version.Version))
                .ToArray(),
            cancellationToken);
    }

    public async Task<KnowledgeArticleVersion?> GetAsync(
        Guid articleId, string version, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleVersions.AsNoTracking()
            .SingleOrDefaultAsync(row =>
                row.ArticleId == articleId && row.Version == version,
                cancellationToken);

    public async Task<KnowledgeArticleVersion?> GetLatestAsync(
        Guid articleId, CancellationToken cancellationToken = default)
    {
        // "Corrente" = a versão que o artigo aponta — a numeração é a
        // fonte da verdade (única por artigo), não o timestamp.
        var current = await dbContext.KnowledgeArticles.AsNoTracking()
            .Where(article => article.Id == articleId)
            .Select(article => article.Version)
            .SingleOrDefaultAsync(cancellationToken);

        if (current is null)
            return null;

        return await dbContext.KnowledgeArticleVersions
            .SingleOrDefaultAsync(row =>
                row.ArticleId == articleId && row.Version == current,
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    private static (int Major, int Minor) Order(string version)
    {
        var parts = version.Split('.');
        return (
            int.TryParse(parts[0], out var major) ? major : 0,
            parts.Length > 1 && int.TryParse(parts[1], out var minor) ? minor : 0);
    }

    /// <summary>Um round-trip para os nomes de todos os ids envolvidos.</summary>
    private async Task<IReadOnlyCollection<KnowledgeArticleVersionWithActors>> WithActorsAsync(
        IReadOnlyCollection<KnowledgeArticleVersion> versions,
        CancellationToken cancellationToken)
    {
        if (versions.Count == 0)
            return [];

        var ids = versions
            .SelectMany(version => new[] { version.AuthorId, version.ApprovedById })
            .OfType<Guid>()
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
        if (ids.Length > 0)
        {
            var users = await dbContext.DomainUsers.AsNoTracking()
                .Where(user => ids.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName, user.Email })
                .ToListAsync(cancellationToken);

            names = users.ToDictionary(
                user => user.Id,
                user => string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.Email
                    : user.DisplayName);
        }

        static string? NameOf(IReadOnlyDictionary<Guid, string> lookup, Guid? id)
            => id is { } key && lookup.TryGetValue(key, out var name) ? name : null;

        return versions
            .Select(version => new KnowledgeArticleVersionWithActors(
                version,
                NameOf(names, version.AuthorId),
                NameOf(names, version.ApprovedById)))
            .ToArray();
    }
}
