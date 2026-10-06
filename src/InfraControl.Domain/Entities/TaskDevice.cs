using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Task ↔ Device (F7-T003).</summary>
public sealed class TaskDevice : AggregateRoot
{
    public Guid TaskId { get; private set; }
    public Guid DeviceId { get; private set; }

    private TaskDevice() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskDeviceDeletedDomainEvent(TaskId, DeviceId));

    public static TaskDevice Create(Guid taskId, Guid deviceId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        var link = new TaskDevice { TaskId = taskId, DeviceId = deviceId };
        link.RaiseDomainEvent(new TaskDeviceCreatedDomainEvent(link.TaskId, link.DeviceId));
        return link;
    }
}
