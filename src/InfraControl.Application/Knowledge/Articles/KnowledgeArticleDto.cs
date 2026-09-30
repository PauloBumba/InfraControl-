using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// Payload de KnowledgeArticle devolvido pela API (F10-T002/T004).
/// Os ids dos vínculos (F10-T004) só vêm no GET de detalhe — a busca
/// não detalha (evita N+1 por página).
/// </summary>
public sealed record KnowledgeArticleDto(
    Guid Id,
    string Title,
    string Content,
    Guid? CategoryId,
    KnowledgeArticleStatus Status)
{
    public IReadOnlyCollection<Guid> DeviceIds { get; init; } = [];
    public IReadOnlyCollection<Guid> IncidentIds { get; init; } = [];
    public IReadOnlyCollection<Guid> ServiceIds { get; init; } = [];
}
