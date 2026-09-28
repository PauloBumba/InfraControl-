using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Serviço de TI prestando (SLO mede objetivo de serviço — docs/04).
/// Raiz do módulo SLA/SLO: SlaPolicy (F8-T002) e Slo (F8-T005) referenciam
/// Service. O nome é único (case-insensitive: pré-check no handler +
/// índice único em lower(name) na migração) — padrão Location.
/// </summary>
public sealed class Service : AggregateRoot
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Service() { }

    public static Service Create(string name, string? description = null)
    {
        var service = new Service();
        service.Update(name, description);
        return service;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do serviço é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome do serviço pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição do serviço pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
