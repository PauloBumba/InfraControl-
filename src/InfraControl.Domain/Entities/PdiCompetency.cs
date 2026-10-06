using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Catálogo de competências do PDI (docs/30 §23 — entidade de apoio
/// <c>Competency</c>; §4 diagnóstico). Nome único case-insensitive,
/// gerido pelo Admin; objetivos referenciam por <c>CompetencyId</c>.
/// </summary>
public sealed class PdiCompetency : Entity
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private PdiCompetency() { }

    public static PdiCompetency Create(string name, string? description)
    {
        var competency = new PdiCompetency();
        competency.Apply(name, description);
        return competency;
    }

    public void Update(string name, string? description)
        => Apply(name, description);

    private void Apply(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome da competência é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da competência pode ter no máximo {NameMaxLength} caracteres.",
                nameof(name));

        Name = name;
        Description = Normalize(description, DescriptionMaxLength, nameof(description));
        Touch();
    }

    internal static string? Normalize(string? value, int max, string param)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.Length > max)
            throw new ArgumentException(
                $"O texto pode ter no máximo {max} caracteres.", param);
        return value;
    }
}
