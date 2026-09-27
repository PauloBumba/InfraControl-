using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F7-T003: vínculo Task ↔ Device — guards pt-BR.</summary>
public sealed class TaskDeviceTests
{
    [Fact]
    public void Create_ShouldPairTaskAndDevice()
    {
        var taskId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var link = TaskDevice.Create(taskId, deviceId);

        link.TaskId.Should().Be(taskId);
        link.DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskDevice.Create(Guid.Empty, Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyDeviceId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskDevice.Create(Guid.NewGuid(), Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do device é obrigatório.*");
    }
}
