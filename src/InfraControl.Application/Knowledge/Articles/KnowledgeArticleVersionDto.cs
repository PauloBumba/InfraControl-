using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// Linha do histórico de versões (F24-T003/K3, spec docs/32 §8) —
/// metadados sem o conteúdo (a lista fica leve); o snapshot completo
/// vai em <see cref="KnowledgeArticleVersionSnapshotDto"/>.
/// Nomes resolvidos no backend (mesmo critério do artigo — K1).
/// </summary>
public sealed record KnowledgeArticleVersionDto(
    Guid Id,
    string Version,
    string Reason,
    Guid? AuthorId,
    string? AuthorName,
    DateTime CreatedAt,
    Guid? ApprovedById,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    bool IsCurrent);

/// <summary>Snapshot completo de uma versão (a "alteração" da §8).</summary>
public sealed record KnowledgeArticleVersionSnapshotDto(
    Guid Id,
    string Version,
    string Title,
    string Content,
    string? Description,
    KnowledgeArticleType? Type,
    Guid? CategoryId,
    IReadOnlyList<string> Tags,
    string Reason,
    Guid? AuthorId,
    string? AuthorName,
    DateTime CreatedAt,
    Guid? ApprovedById,
    string? ApprovedByName,
    DateTime? ApprovedAt);

/// <summary>
/// Payload de comparação: os dois snapshots lado a lado (o diff em si
/// é decisão de quem consome — aqui o contrato devolve a matéria).
/// </summary>
public sealed record KnowledgeArticleVersionCompareDto(
    KnowledgeArticleVersionSnapshotDto From,
    KnowledgeArticleVersionSnapshotDto To);

public static class KnowledgeArticleVersionDtoFactory
{
    public static KnowledgeArticleVersionDto From(
        KnowledgeArticleVersionWithActors row, string? currentVersion)
        => new(
            row.Version.Id,
            row.Version.Version,
            row.Version.Reason,
            row.Version.AuthorId,
            row.AuthorName,
            row.Version.CreateDateTime,
            row.Version.ApprovedById,
            row.ApprovedByName,
            row.Version.ApprovedAt,
            row.Version.Version == currentVersion);

    public static KnowledgeArticleVersionSnapshotDto Snapshot(
        KnowledgeArticleVersion version, IReadOnlyDictionary<Guid, string>? names)
    {
        static string? NameOf(IReadOnlyDictionary<Guid, string>? lookup, Guid? id)
            => id is { } key && lookup is not null && lookup.TryGetValue(key, out var name)
                ? name
                : null;

        return Snapshot(
            version,
            NameOf(names, version.AuthorId),
            NameOf(names, version.ApprovedById));
    }

    public static KnowledgeArticleVersionSnapshotDto Snapshot(
        KnowledgeArticleVersion version, string? authorName, string? approvedByName)
        => new(
            version.Id,
            version.Version,
            version.Title,
            version.Content,
            version.Description,
            version.Type,
            version.CategoryId,
            version.Tags,
            version.Reason,
            version.AuthorId,
            authorName,
            version.CreateDateTime,
            version.ApprovedById,
            approvedByName,
            version.ApprovedAt);
}
