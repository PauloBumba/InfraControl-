using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// VLAN 802.1Q (docs/03: SwitchPort N:N Vlan — a vinculação N:N é F3-T004).
/// Número = tag 1..4094, única global; nome único global case-insensitive
/// (pré-check no handler + índice (LOWER(name)) via SQL bruto na migração).
/// O exemplo de seeds (Default, Radiologia, Telefonia...) é do cliente final.
/// </summary>
public sealed class Vlan : AggregateRoot
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;
    public const int MinNumber = 1;
    public const int MaxNumber = 4094;

    public string Name { get; private set; } = string.Empty;
    public int VlanNumber { get; private set; }
    public string? Description { get; private set; }

    private Vlan() { }

    public static Vlan Create(string name, int vlanNumber, string? description = null)
    {
        var vlan = new Vlan();
        vlan.Update(name, vlanNumber, description);
        return vlan;
    }

    public void Update(string name, int vlanNumber, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da VLAN é obrigatório.", nameof(name));

        if (vlanNumber is < MinNumber or > MaxNumber)
            throw new ArgumentException(
                $"O número da VLAN deve estar entre {MinNumber} e {MaxNumber}.", nameof(vlanNumber));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da VLAN pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição da VLAN pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        Name = name;
        VlanNumber = vlanNumber;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
