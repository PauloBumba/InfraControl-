using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T003: TaskSla — snapshot dos prazos no momento da aplicação
/// (docs/04: "O SLA aplicado deve preservar os prazos históricos").
/// </summary>
public sealed class TaskSlaTests
{
    [Fact]
    public void Apply_ShouldSnapshotPolicyMinutesAndComputeDueDates()
    {
        var task = TaskItem.Create("Task com SLA", TaskKind.Incident);
        var policy = SlaPolicy.Create("Padrão", 30, 480);

        var sla = TaskSla.Apply(task, policy);

        sla.TaskId.Should().Be(task.Id);
        sla.SlaPolicyId.Should().Be(policy.Id);
        sla.ResponseMinutes.Should().Be(30);
        sla.ResolutionMinutes.Should().Be(480);
        sla.ResponseDueAt.Should().Be(task.CreateDateTime.AddMinutes(30));
        sla.ResolutionDueAt.Should().Be(task.CreateDateTime.AddMinutes(480));
    }

    [Fact]
    public void Apply_ShouldPreserveSnapshotWhenPolicyChangesLater()
    {
        var task = TaskItem.Create("Task com SLA", TaskKind.Incident);
        var policy = SlaPolicy.Create("Original", 30, 480);
        var sla = TaskSla.Apply(task, policy);

        policy.Update("Estendida", 60, 1440);

        sla.ResponseMinutes.Should().Be(30, "o snapshot não acompanha a política");
        sla.ResolutionMinutes.Should().Be(480);
        sla.ResponseDueAt.Should().Be(task.CreateDateTime.AddMinutes(30));
    }

    [Fact]
    public void Apply_NullTaskOrPolicy_ShouldThrow()
    {
        var task = TaskItem.Create("Task", TaskKind.Manual);
        var policy = SlaPolicy.Create("P", 30, 480);

        var actTask = () => TaskSla.Apply(null!, policy);
        var actPolicy = () => TaskSla.Apply(task, null!);

        actTask.Should().Throw<ArgumentNullException>();
        actPolicy.Should().Throw<ArgumentNullException>();
    }
}
