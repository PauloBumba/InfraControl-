using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Task ↔ Device (F7-T003).</summary>
public sealed class TaskDevice : Entity
{
    public Guid TaskId { get; private set; }
    public Guid DeviceId { get; private set; }

    private TaskDevice() { }

    public static TaskDevice Create(Guid taskId, Guid deviceId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        return new TaskDevice { TaskId = taskId, DeviceId = deviceId };
    }
}
