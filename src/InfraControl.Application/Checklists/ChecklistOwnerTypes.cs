using InfraControl.Domain.Enums;

namespace InfraControl.Application.Checklists;

/// <summary>
/// Parse do dono do checklist (F50-T007/ADR-067) — mesmo formato do
/// `KnowledgeArticleOriginCommands.ParseType`: valor do enum ignoreCase
/// com 400 pt-BR apontando os valores válidos.
/// </summary>
public static class ChecklistOwnerTypes
{
    public static ChecklistOwnerType Parse(string type)
        => Enum.TryParse<ChecklistOwnerType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"Tipo inválido: '{type}'. " +
                "Use 'device', 'location', 'incident' ou 'project'.",
                nameof(type));
}
