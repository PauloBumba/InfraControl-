using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Objetivo de um PDI (docs/30 §5): "um PDI pode ter um ou vários
/// objetivos". Campos opcionais conforme "cada objetivo pode possuir";
/// <c>Title</c> é o mínimo para existir. Pertence ao dono do plano
/// (<c>UserId</c>), não ao AggregateRoot — o plano continua sendo o
/// agregado raiz (decisão do usuário, §25).
/// </summary>
public sealed class PdiObjective : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int SituationMaxLength = 1000;

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Competência do catálogo trabalhada por este objetivo (§5).</summary>
    public Guid? CompetencyId { get; private set; }

    public string? CurrentSituation { get; private set; }
    public string? DesiredSituation { get; private set; }
    public string? ExpectedResult { get; private set; }
    public string? Indicator { get; private set; }

    /// <summary>Prazo do objetivo (§5 — "prazo").</summary>
    public DateOnly? TargetDate { get; private set; }

    /// <summary>Posição na sequência de objetivos do plano.</summary>
    public int Order { get; private set; }

    private PdiObjective() { }

    public static PdiObjective Create(
        Guid userId,
        string title,
        string? description,
        Guid? competencyId,
        string? currentSituation,
        string? desiredSituation,
        string? expectedResult,
        string? indicator,
        DateOnly? targetDate,
        int order)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "O usuário dono do PDI é obrigatório.", nameof(userId));

        var objective = new PdiObjective { UserId = userId, Order = order };
        objective.Apply(
            title, description, competencyId, currentSituation, desiredSituation,
            expectedResult, indicator, targetDate);
        return objective;
    }

    public void Update(
        string title,
        string? description,
        Guid? competencyId,
        string? currentSituation,
        string? desiredSituation,
        string? expectedResult,
        string? indicator,
        DateOnly? targetDate)
        => Apply(
            title, description, competencyId, currentSituation, desiredSituation,
            expectedResult, indicator, targetDate);

    private void Apply(
        string title,
        string? description,
        Guid? competencyId,
        string? currentSituation,
        string? desiredSituation,
        string? expectedResult,
        string? indicator,
        DateOnly? targetDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "O título do objetivo é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título do objetivo pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        Title = title;
        Description = PdiCompetency.Normalize(description, DescriptionMaxLength, nameof(description));
        CompetencyId = competencyId;
        CurrentSituation = PdiCompetency.Normalize(currentSituation, SituationMaxLength, nameof(currentSituation));
        DesiredSituation = PdiCompetency.Normalize(desiredSituation, SituationMaxLength, nameof(desiredSituation));
        ExpectedResult = PdiCompetency.Normalize(expectedResult, SituationMaxLength, nameof(expectedResult));
        Indicator = PdiCompetency.Normalize(indicator, TitleMaxLength, nameof(indicator));
        TargetDate = targetDate;
        Touch();
    }
}
