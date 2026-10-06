using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Estratégia de um objetivo (docs/30 §6 — "como vamos desenvolver essa
/// competência?"; "uma estratégia pode gerar várias fases").
/// </summary>
public sealed class PdiStrategy : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public Guid ObjectiveId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Order { get; private set; }

    private PdiStrategy() { }

    public static PdiStrategy Create(
        Guid objectiveId, string title, string? description, int order)
    {
        if (objectiveId == Guid.Empty)
            throw new ArgumentException(
                "O objetivo da estratégia é obrigatório.", nameof(objectiveId));

        var strategy = new PdiStrategy { ObjectiveId = objectiveId, Order = order };
        strategy.Apply(title, description);
        return strategy;
    }

    public void Update(string title, string? description)
        => Apply(title, description);

    private void Apply(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "O título da estratégia é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título da estratégia pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        Title = title;
        Description = PdiCompetency.Normalize(description, DescriptionMaxLength, nameof(description));
        Touch();
    }
}
