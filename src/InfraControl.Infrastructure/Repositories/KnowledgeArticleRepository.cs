using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Common;
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
<<<<<<< HEAD
        bool pendingReview,
        Guid? categoryId = null,
        Guid? authorId = null,
        KnowledgeArticleType? type = null,
        string? tag = null,
=======
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeArticles
            .AsNoTracking()
            .Include(article => article.Tags) // K1: dto traz as tags (1 query)
            .AsQueryable();

        // F24-T007/K7 (docs/32 §13): termo também casa na descrição
        // (resumo do que o artigo cobre) — campo que existe no modelo.
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(article =>
                article.Title.ToLower().Contains(normalized)
                || article.Content.ToLower().Contains(normalized)
                || (article.Description != null
                    && article.Description.ToLower().Contains(normalized)));
        }

<<<<<<< HEAD
        // F24-T007/K7: filtros estruturados AND — só campos que existem
        // no modelo (sistema/setor, da spec §13, não existem — fora).
        if (categoryId is not null)
            query = query.Where(article => article.CategoryId == categoryId);

        if (authorId is not null)
            query = query.Where(article => article.AuthorId == authorId);

        if (type is not null)
            query = query.Where(article => article.Type == type);

        // Tags são normalizadas pelo domínio (trim+lowercase) — o filtro
        // repete a normalização para casa com o que está gravado.
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            query = query.Where(article => article.Tags.Contains(normalizedTag));
        }

        // F24-T002/K2 (ADR-034 §4): nunca-publicados escondidos de quem
        // não escreve — coleção do domínio vira parâmetro de IN.
        if (!includeUnpublished)
            query = query.Where(article =>
                !KnowledgeArticleStatusRules.UnpublishedStatuses.Contains(article.Status));

        // F24-T005/K5 (docs/32 §12): só os com revisão vencida —
        // "Knowledge necessita de revisão". `now` capturado fora (EF
        // não traduz AppTimeProvider na expressão).
        if (pendingReview)
        {
            var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
            query = query.Where(article =>
                article.NextReviewAt != null && article.NextReviewAt <= now);
        }
=======
        if (!includeUnpublished)
            query = query.Where(article =>
                !KnowledgeArticleStatusExtensions.HiddenFromReaders
                    .Contains(article.Status));
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

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

<<<<<<< HEAD
    // F24-T001/K1 — identificação: responsável (404 pt-BR no command) e
    // nomes de autor/responsável resolvidos no backend (DisplayName,
    // fallback e-mail; id sem usuário sai do mapa → nome null).

    public Task<bool> UserExistsAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => dbContext.DomainUsers.AnyAsync(
            user => user.Id == userId, cancellationToken);

=======
    // F24-T001/K1 — precedente TaskAssigneeRepository.GetUserNamesAsync:
    // usuário excluído some do dicionário, o chamador trata como nome não
    // encontrado.
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
<<<<<<< HEAD
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, string>();

        var users = await dbContext.DomainUsers.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName, user.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            user => user.Id,
            user => string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.Email
                : user.DisplayName);
    }
=======
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
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
}
