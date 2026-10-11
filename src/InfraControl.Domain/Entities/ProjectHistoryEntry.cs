using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Entrada da timeline do Projeto — append-only (ADR-043 · docs/38 §3.4).
/// </summary>
public sealed class ProjectHistoryEntry : Entity
{
    public const int MaxReasonLength = 500;
    public const int MaxDetailLength = 500;

    public Guid ProjectId { get; private set; }
    public ProjectHistoryType Type { get; private set; }
    public Guid ActorId { get; private set; }
    public string? Detail { get; private set; }
    public string? Reason { get; private set; }

    private ProjectHistoryEntry() { }

    public static ProjectHistoryEntry Create(
        Guid projectId,
        ProjectHistoryType type,
        Guid actorId,
        string? detail = null,
        string? reason = null)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException(
                "O projeto do histórico é obrigatório.", nameof(projectId));
        if (!Enum.IsDefined(type))
            throw new ArgumentException(
                "O tipo do histórico é inválido.", nameof(type));
        if (actorId == Guid.Empty)
            throw new ArgumentException(
                "O ator do histórico é obrigatório.", nameof(actorId));

        return new ProjectHistoryEntry
        {
            ProjectId = projectId,
            Type = type,
            ActorId = actorId,
            Detail = Truncate(detail, MaxDetailLength),
            Reason = Truncate(reason, MaxReasonLength)
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
