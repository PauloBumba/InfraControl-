using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Articles.Ports;

public interface IKnowledgeArticleRepository : IRepository<KnowledgeArticle>
{
    /// <summary>
    /// Busca por termo no título/conteúdo, mais recentes primeiro
    /// (CreateDateTime desc). <paramref name="includeUnpublished"/> = false
    /// esconde os estados não publicados de quem não escreve — regra de
    /// visibilidade de KnowledgeArticleStatusExtensions.HiddenFromReaders
    /// (F10-T003/K2 ADR-039 §3).
    /// </summary>
    Task<PagedResult<KnowledgeArticle>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        bool includeUnpublished,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve DisplayName dos usuários (join em DomainUsers) — nomes do
    /// autor/responsável nas queries (F24-T001/K1); precedente
    /// ITaskAssigneeRepository/IPdiRepository (cada módulo resolve os
    /// seus).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>404 pt-BR do responsável inexistente (F24-T001/K1).</summary>
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);
}
