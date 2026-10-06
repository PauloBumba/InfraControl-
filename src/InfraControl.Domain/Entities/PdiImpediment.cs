using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Dificuldade/impedimento da fase (docs/30 §11 — entidade própria com
/// tipo, descrição, impacto, data, responsável, ação tomada, status e
/// resolvido em; §23 PDIImpediment; Fase 3 — ADR-033). O tipo é texto
/// livre no Fase 3; a tabela de apoio PDIImpedimentType (§23) entra
/// quando os indicadores (Fase 8) pedirem normalização.
/// </summary>
public sealed class PdiImpediment : Entity
{
    public const int TypeMaxLength = 100;
    public const int DescriptionMaxLength = 2000;
    public const int ImpactMaxLength = 500;
    public const int ActionMaxLength = 2000;

    public Guid PhaseId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? Impact { get; private set; }
    public DateOnly OccurredAt { get; private set; }

    /// <summary>Responsável pela ação — usuário livre (mesmo critério de ResponsibleId da fase).</summary>
    public Guid? ResponsibleId { get; private set; }

    public string? ActionTaken { get; private set; }
    public PdiImpedimentStatus Status { get; private set; }
    public DateOnly? ResolvedAt { get; private set; }

    private PdiImpediment() { }

    public static PdiImpediment Create(
        Guid phaseId,
        string type,
        string description,
        DateOnly occurredAt,
        string? impact = null,
        Guid? responsibleId = null,
        string? actionTaken = null)
    {
        if (phaseId == Guid.Empty)
            throw new ArgumentException(
                "A fase do impedimento é obrigatória.", nameof(phaseId));

        var impediment = new PdiImpediment
        {
            PhaseId = phaseId,
            OccurredAt = occurredAt,
            Status = PdiImpedimentStatus.Open
        };
        impediment.Apply(type, description, impact, responsibleId, actionTaken);
        return impediment;
    }

    public void Update(
        string type,
        string description,
        DateOnly occurredAt,
        string? impact,
        Guid? responsibleId,
        string? actionTaken,
        PdiImpedimentStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentException(
                "Status do impedimento inválido (use Open ou Resolved).",
                nameof(status));

        OccurredAt = occurredAt;
        Status = status;
        ResolvedAt = status switch
        {
            PdiImpedimentStatus.Resolved => ResolvedAt
                ?? DateOnly.FromDateTime(DateTime.UtcNow),
            _ => null
        };
        Apply(type, description, impact, responsibleId, actionTaken);
    }

    private void Apply(
        string type, string description, string? impact,
        Guid? responsibleId, string? actionTaken)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException(
                "O tipo do impedimento é obrigatório.", nameof(type));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A descrição do impedimento é obrigatória.", nameof(description));

        Type = PdiCompetency.Normalize(type, TypeMaxLength, nameof(type))!;
        Description = PdiCompetency.Normalize(
            description, DescriptionMaxLength, nameof(description))!;
        Impact = PdiCompetency.Normalize(impact, ImpactMaxLength, nameof(impact));
        ResponsibleId = responsibleId;
        ActionTaken = PdiCompetency.Normalize(actionTaken, ActionMaxLength, nameof(actionTaken));
        Touch();
    }
}
