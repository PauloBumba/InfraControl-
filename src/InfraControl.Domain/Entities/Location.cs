using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Local físico (data center, sala, andar...). Raiz do inventário —
/// Location 1:N Rack (F2-T002). O nome é único (case-insensitive: pré-check
/// no handler + índice único em lower(name) na migração).
/// </summary>
public sealed class Location : AggregateRoot
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Location() { }

    public static Location Create(string name, string? description = null)
    {
        var location = new Location();
        location.Update(name, description);
        return location;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do local é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome do local pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição do local pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
