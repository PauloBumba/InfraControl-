using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Rack físico dentro de um Location — Location 1:N Rack, Rack 1:N Device
/// (F2-T003). Nome único POR LOCAL (case-insensitive: pré-check no handler +
/// índice (location_id, LOWER(name)) na migração).
/// </summary>
public sealed class Rack : AggregateRoot
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;

    public Guid LocationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Rack() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new RackDeletedDomainEvent(Id, LocationId, Name));

    public static Rack Create(Guid locationId, string name, string? description = null)
    {
        if (locationId == Guid.Empty)
            throw new ArgumentException("O id do local é obrigatório.", nameof(locationId));

        var rack = new Rack();
        rack.LocationId = locationId;
        rack.Update(name, description);
        rack.ClearDomainEvents(); // descarta o Updated transitório do Create
        rack.RaiseDomainEvent(new RackCreatedDomainEvent(rack.Id, rack.LocationId, rack.Name));
        return rack;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do rack é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome do rack pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição do rack pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        RaiseDomainEvent(new RackUpdatedDomainEvent(Id, LocationId, Name));
    }
}
