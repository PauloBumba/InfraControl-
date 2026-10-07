using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Interface de rede de um Device — Device 1:N NetworkInterface,
/// NetworkInterface 1:N IpAddress (F2-T006). Nome único POR DEVICE
/// (case-insensitive: pré-check no handler + índice (device_id, LOWER(name))
/// na migração). MAC vira entidade própria em F2-T005.
/// </summary>
public sealed class NetworkInterface : AggregateRoot
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;

    public Guid DeviceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private NetworkInterface() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new NetworkInterfaceDeletedDomainEvent(Id, DeviceId, Name));

    public static NetworkInterface Create(Guid deviceId, string name, string? description = null)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        var nic = new NetworkInterface();
        nic.DeviceId = deviceId;
        nic.Update(name, description);
        nic.ClearDomainEvents(); // descarta o Updated transitório do Create
        nic.RaiseDomainEvent(new NetworkInterfaceCreatedDomainEvent(nic.Id, nic.DeviceId, nic.Name));
        return nic;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da interface é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da interface pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição da interface pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        RaiseDomainEvent(new NetworkInterfaceUpdatedDomainEvent(Id, DeviceId, Name));
    }
}
