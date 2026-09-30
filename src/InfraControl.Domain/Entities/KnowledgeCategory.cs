using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Categoria da base de conhecimento (F10-T001, docs/04 — Knowledge
/// Base). Nome único case-insensitive (pré-check no handler + índice
/// único em lower(name), precedente da Location).
/// </summary>
public sealed class KnowledgeCategory : AggregateRoot
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private KnowledgeCategory() { }

    public static KnowledgeCategory Create(string name, string? description)
    {
        var category = new KnowledgeCategory();
        category.Update(name, description);
        return category;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome da categoria é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da categoria pode ter no máximo {NameMaxLength} caracteres.",
                nameof(name));

        if (description is not null)
        {
            description = description.Trim();
            if (description.Length > DescriptionMaxLength)
                throw new ArgumentException(
                    $"A descrição da categoria pode ter no máximo {DescriptionMaxLength} caracteres.",
                    nameof(description));

            if (description.Length == 0)
                description = null;
        }

        Name = name;
        Description = description;
    }
}
