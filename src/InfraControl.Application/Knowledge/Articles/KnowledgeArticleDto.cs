using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// Payload de KnowledgeArticle devolvido pela API (F10-T002/T004;
/// tipos e identificação K1 — F24-T001). Os ids dos vínculos (F10-T004)
/// só vêm no GET de detalhe — a busca não detalha (evita N+1 por página).
/// Nomes de autor/responsável são resolvidos só nas queries Search/Get
/// (join em DomainUsers, mesmo precedente de Tasks/PDI).
/// </summary>
public sealed record KnowledgeArticleDto(
    Guid Id,
    string Title,
    string Content,
    Guid? CategoryId,
    KnowledgeArticleStatus Status,
    string? Type = null,
    string? Description = null,
    Guid? AuthorId = null,
    Guid? ResponsibleId = null)
{
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public IReadOnlyCollection<Guid> DeviceIds { get; init; } = [];
    public IReadOnlyCollection<Guid> IncidentIds { get; init; } = [];
    public IReadOnlyCollection<Guid> ServiceIds { get; init; } = [];
    public string? AuthorName { get; init; }
    public string? ResponsibleName { get; init; }
}
