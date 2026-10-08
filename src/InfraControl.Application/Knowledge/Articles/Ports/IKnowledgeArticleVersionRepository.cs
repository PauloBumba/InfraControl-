using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Articles.Ports;

/// <summary>
/// Snapshots de versão do artigo (F24-T003/K3, spec docs/32 §8).
/// Histórico ordenado por <b>número de versão</b> (major, minor) da
/// mais recente para a mais antiga — o timestamp pode empatar.
/// </summary>
public interface IKnowledgeArticleVersionRepository
    : IRepository<KnowledgeArticleVersion>
{
    /// <summary>Versões do artigo + nomes resolvidos (autor/aprovador).</summary>
    Task<IReadOnlyCollection<KnowledgeArticleVersionWithActors>> ListByArticleAsync(
        Guid articleId, CancellationToken cancellationToken = default);

    /// <summary>Snapshot de uma versão específica (404 no handler se nulo).</summary>
    Task<KnowledgeArticleVersion?> GetAsync(
        Guid articleId, string version, CancellationToken cancellationToken = default);

    /// <summary>Versão corrente (a que o carimbo de aprovação atinge).</summary>
    Task<KnowledgeArticleVersion?> GetLatestAsync(
        Guid articleId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Linha do histórico com os nomes já resolvidos no backend.</summary>
public sealed record KnowledgeArticleVersionWithActors(
    KnowledgeArticleVersion Version,
    string? AuthorName,
    string? ApprovedByName);
