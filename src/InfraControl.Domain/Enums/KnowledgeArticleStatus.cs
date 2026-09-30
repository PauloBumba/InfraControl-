namespace InfraControl.Domain.Enums;

/// <summary>
/// Estados do artigo da base de conhecimento (F10-T002, docs/04 —
/// Knowledge Base). As transições entre estados (Draft → Published →
/// Archived → Draft) vêm em F10-T003 (Publishing).
/// </summary>
public enum KnowledgeArticleStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}
