using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// Payload de KnowledgeArticle devolvido pela API (F10-T002).
/// </summary>
public sealed record KnowledgeArticleDto(
    Guid Id,
    string Title,
    string Content,
    Guid? CategoryId,
    KnowledgeArticleStatus Status);
