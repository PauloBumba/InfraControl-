using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F7-T004: vínculo Task ↔ Incident — guards pt-BR.</summary>
public sealed class TaskIncidentTests
{
    [Fact]
    public void Create_ShouldPairTaskAndIncident()
    {
        var taskId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var link = TaskIncident.Create(taskId, incidentId);

        link.TaskId.Should().Be(taskId);
        link.IncidentId.Should().Be(incidentId);
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskIncident.Create(Guid.Empty, Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyIncidentId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskIncident.Create(Guid.NewGuid(), Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do incidente é obrigatório.*");
    }
}
