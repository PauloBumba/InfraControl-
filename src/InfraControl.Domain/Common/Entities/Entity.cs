using InfraControl.Domain.Common;

namespace InfraControl.Domain.Common.Entities;

public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTime CreateDateTime { get; set; }
    public DateTime UpdateDateTime { get; set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
        var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        CreateDateTime = now;
        UpdateDateTime = now;
    }

    protected Entity(Guid id)
    {
        Id = id;
        var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        CreateDateTime = now;
        UpdateDateTime = now;
    }

    public void Touch()
    {
        UpdateDateTime = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
