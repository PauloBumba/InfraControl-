using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles.Ports;

public interface IKnowledgeArticleRepository : IRepository<KnowledgeArticle>
{
    /// <summary>
    /// Busca por termo no título/conteúdo/descrição, mais recentes
    /// primeiro (CreateDateTime desc). <paramref name="includeUnpublished"/> =
    /// false esconde os nunca-publicados (Draft/InReview/Approved/
    /// Rejected) de quem não escreve — ADR-034 §4 (regra F10-T003
    /// generalizada). <paramref name="pendingReview"/> = true devolve
    /// só os com revisão vencida (F24-T005/K5, docs/32 §12).
    /// F24-T007/K7 (docs/32 §13): filtros estruturados AND — categoria,
    /// autor, tipo e tag (tag normaliza como o domínio: trim+lowercase);
    /// "sistema"/"setor" não existem no modelo e ficam fora.
    /// </summary>
    Task<PagedResult<KnowledgeArticle>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        bool includeUnpublished,
        bool pendingReview,
        Guid? categoryId = null,
        Guid? authorId = null,
        KnowledgeArticleType? type = null,
        string? tag = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F24-T001/K1: validação de responsável no command (404 pt-BR) —
    /// molde de <c>ITaskAssigneeRepository.UserExistsAsync</c>.
    /// </summary>
    Task<bool> UserExistsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// F24-T001/K1: nomes resolvidos para o DTO (DisplayName; fallback
    /// e-mail). Ids sem usuário (removido) ficam de fora → nome null.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
