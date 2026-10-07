using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Porta física de um switch — Switch(Device) 1:N SwitchPort (docs/03, F3-T002).
/// Nome único POR SWITCH (case-insensitive: pré-check no handler + índice
/// (device_id, LOWER(name)) via SQL bruto na migração). LinkUp null = ainda
/// não observado; o discovery SNMP da F4 (IF-MIB) é quem passa a preencher —
/// propriedades operacionais (velocidade/PoE/VLAN/tráfego) também vêm do
/// SNMP, não do cadastro manual (decisão do usuário na T002).
/// </summary>
public sealed class SwitchPort : AggregateRoot
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;

    /// <summary>Device que é o switch dono da porta.</summary>
    public Guid DeviceId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Estado de link; null = porta ainda não observada (F4).</summary>
    public bool? LinkUp { get; private set; }

    private SwitchPort() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new SwitchPortDeletedDomainEvent(Id, DeviceId, Name));

    public static SwitchPort Create(Guid deviceId, string name, string? description = null)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do switch é obrigatório.", nameof(deviceId));

        var port = new SwitchPort();
        port.DeviceId = deviceId;
        port.Update(name, description, port.LinkUp);
        port.ClearDomainEvents(); // descarta o Updated transitório do Create
        port.RaiseDomainEvent(new SwitchPortCreatedDomainEvent(port.Id, port.DeviceId, port.Name));
        return port;
    }

    public void Update(string name, string? description, bool? linkUp)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da porta é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da porta pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição da porta pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        LinkUp = linkUp;

        RaiseDomainEvent(new SwitchPortUpdatedDomainEvent(Id, DeviceId, Name));
    }
}
