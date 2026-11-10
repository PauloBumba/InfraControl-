using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Departamento/setor da empresa (spec evolução 2026-11-07 §3) — filho do
/// agregado CompanySettings; substituição integral a cada upsert.
/// </summary>
public sealed class CompanyDepartment : Entity
{
    public const int NameMaxLength = 120;
    public const int ResponsibleMaxLength = 120;

    public Guid CompanySettingsId { get; internal set; }
    public string Name { get; private set; } = string.Empty;
    public string? Responsible { get; private set; }

    private CompanyDepartment() { }

    public static CompanyDepartment Create(string name, string? responsible = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do departamento é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException($"O nome do departamento pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (responsible?.Length > ResponsibleMaxLength)
            throw new ArgumentException($"O responsável pode ter no máximo {ResponsibleMaxLength} caracteres.", nameof(responsible));

        return new CompanyDepartment
        {
            Name = name,
            Responsible = string.IsNullOrWhiteSpace(responsible) ? null : responsible.Trim(),
        };
    }
}
