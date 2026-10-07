using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Instância de check por device (F5-T001) — nasce do DeviceProfile
/// (ADR-014 §4: F5-T001 cria <c>MonitoringCheck</c> e resolve o provider).
/// Sincronizada nos handlers de create e de update de perfil; linha perdida
/// quando o device é excluído (FK cascade). Índice único (DeviceId, Kind).
/// </summary>
public sealed class MonitoringCheck : AggregateRoot
{
    public Guid DeviceId { get; private set; }
    public CheckKind Kind { get; private set; }
    public bool Enabled { get; private set; }

    private MonitoringCheck() { }

    public static MonitoringCheck Create(Guid deviceId, CheckKind kind, bool enabled = true)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        var check = new MonitoringCheck
        {
            DeviceId = deviceId,
            Kind = kind,
            Enabled = enabled
        };
        check.RaiseDomainEvent(new MonitoringCheckCreatedDomainEvent(
            check.Id, check.DeviceId, check.Kind, check.Enabled));
        return check;
    }

    /// <summary>ADR-030 §1 — único campo mutável da check (toggle).</summary>
    /// <summary>ADR-030 §5 — único campo mutável (toggle).</summary>
    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        RaiseDomainEvent(new MonitoringCheckUpdatedDomainEvent(Id, DeviceId, Enabled));
    }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new MonitoringCheckDeletedDomainEvent(Id, DeviceId, Kind));
}
