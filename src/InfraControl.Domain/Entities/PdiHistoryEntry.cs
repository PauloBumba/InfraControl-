using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>Tipo de evento do timeline do PDI (ADR-029).</summary>
public enum PdiHistoryType
{
    /// <summary>Plano criado.</summary>
    Created = 1,

    /// <summary>Campos alterados (estratégia/prazo/gestor/período) — <see cref="PdiHistoryEntry.Detail"/>.</summary>
    Updated = 2,

    /// <summary>Mudança de status — <see cref="PdiHistoryEntry.FromStatus"/>/<see cref="PdiHistoryEntry.ToStatus"/> + motivo opcional.</summary>
    StatusChanged = 3
}

/// <summary>
/// Entrada do histórico (timeline) do PDI — append-only, só leitura
/// pela API (docs/30 §17, ADR-029). <see cref="Common.Entities.Entity.CreateDateTime"/>
/// é o momento do evento (UTC, <c>AppTimeProvider</c>). Não confundir
/// com "histórico de ciclos" (snapshots de planos anteriores — fora do
/// escopo, 1 registro vigente por usuário).
/// </summary>
public sealed class PdiHistoryEntry : Entity
{
    public const int MaxReasonLength = 500;
    public const int MaxDetailLength = 500;

    /// <summary>Dono do plano (chave de agrupamento do timeline).</summary>
    public Guid UserId { get; private set; }

    public PdiHistoryType Type { get; private set; }

    /// <summary>Quem realizou a ação (admin ou próprio dono).</summary>
    public Guid ActorId { get; private set; }

    public PdiStatus? FromStatus { get; private set; }
    public PdiStatus? ToStatus { get; private set; }

    /// <summary>Motivo opcional da transição (replanejamento etc.).</summary>
    public string? Reason { get; private set; }

    /// <summary>Resumo em pt-BR dos campos alterados (tipo <c>Updated</c>).</summary>
    public string? Detail { get; private set; }

    private PdiHistoryEntry() { }

    internal static PdiHistoryEntry ForCreated(
        Guid userId, PdiStatus status, Guid actorId)
        => new()
        {
            UserId = userId,
            Type = PdiHistoryType.Created,
            ActorId = actorId,
            FromStatus = null,
            ToStatus = status
        };

    internal static PdiHistoryEntry ForStatusChanged(
        Guid userId, PdiStatus from, PdiStatus to, Guid actorId, string? reason)
        => new()
        {
            UserId = userId,
            Type = PdiHistoryType.StatusChanged,
            ActorId = actorId,
            FromStatus = from,
            ToStatus = to,
            Reason = Truncate(reason, MaxReasonLength)
        };

    internal static PdiHistoryEntry ForUpdated(
        Guid userId, Guid actorId, string detail)
        => new()
        {
            UserId = userId,
            Type = PdiHistoryType.Updated,
            ActorId = actorId,
            Detail = Truncate(detail, MaxDetailLength)
        };

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
