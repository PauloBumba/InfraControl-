using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Domain.RemoteAccess;

namespace InfraControl.UnitTests.DomainEvents;

/// <summary>
/// ADR-030 §5 — Lote 2 (Tasks, Monitoring, Incident, RemoteAccess):
/// Created/Updated/Deleted pelos métodos do agregado (namespace
/// InfraControl.Domain.Events — constraint do outbox).
/// </summary>
public sealed class CrudDomainEventLote2Tests
{
    // ── TaskItem ─────────────────────────────────────────────────────────

    [Fact]
    public void TaskItem_ShouldRaiseCreatedUpdatedDeleted()
    {
        var task = TaskItem.Create("Trocar switch", TaskKind.Manual);
        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskItemCreatedDomainEvent>();

        task.ClearDomainEvents();
        task.Update("Trocar switch core", null);
        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskItemUpdatedDomainEvent>()
            .Which.Title.Should().Be("Trocar switch core");

        task.ClearDomainEvents();
        task.Delete();
        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskItemDeletedDomainEvent>();
    }

    [Fact]
    public void TaskItem_ChangeState_ShouldRaiseUpdated()
    {
        var task = TaskItem.Create("Trocar switch", TaskKind.Manual);
        task.ClearDomainEvents();

        task.ChangeState(TaskState.InProgress);

        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskItemUpdatedDomainEvent>()
            .Which.State.Should().Be(TaskState.InProgress);
    }

    // ── TaskComment ──────────────────────────────────────────────────────

    [Fact]
    public void TaskComment_ShouldRaiseCreatedUpdatedDeleted()
    {
        var taskId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var comment = TaskComment.Create(taskId, authorId, "feito");
        comment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskCommentCreatedDomainEvent>()
            .Which.TaskId.Should().Be(taskId);

        comment.ClearDomainEvents();
        comment.EditContent("feito com teste");
        comment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskCommentUpdatedDomainEvent>();

        comment.ClearDomainEvents();
        comment.Delete();
        comment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskCommentDeletedDomainEvent>();
    }

    // ── junções TaskAssignee/TaskDevice/TaskIncident (Created/Deleted) ───

    [Fact]
    public void TaskAssignee_ShouldRaiseCreatedAndDeleted()
    {
        var link = TaskAssignee.Create(Guid.NewGuid(), Guid.NewGuid());
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskAssigneeCreatedDomainEvent>();

        link.ClearDomainEvents();
        link.Delete();
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskAssigneeDeletedDomainEvent>();
    }

    [Fact]
    public void TaskDevice_ShouldRaiseCreatedAndDeleted()
    {
        var link = TaskDevice.Create(Guid.NewGuid(), Guid.NewGuid());
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskDeviceCreatedDomainEvent>();

        link.ClearDomainEvents();
        link.Delete();
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskDeviceDeletedDomainEvent>();
    }

    [Fact]
    public void TaskIncident_ShouldRaiseCreatedAndDeleted()
    {
        var link = TaskIncident.Create(Guid.NewGuid(), Guid.NewGuid());
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskIncidentCreatedDomainEvent>();

        link.ClearDomainEvents();
        link.Delete();
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskIncidentDeletedDomainEvent>();
    }

    // ── TaskSla / TaskSlaPause ───────────────────────────────────────────

    [Fact]
    public void TaskSla_ShouldRaiseCreatedAndUpdated()
    {
        var task = TaskItem.Create("com SLA", TaskKind.Manual);
        var policy = SlaPolicy.Create("P1", 15, 60);

        var sla = TaskSla.Apply(task, policy);
        sla.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskSlaCreatedDomainEvent>()
            .Which.SlaPolicyId.Should().Be(policy.Id);

        sla.ClearDomainEvents();
        sla.ExtendDueDates(TimeSpan.FromMinutes(10));
        sla.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskSlaUpdatedDomainEvent>();
    }

    [Fact]
    public void TaskSlaPause_ShouldRaiseCreatedAndUpdated()
    {
        var pause = TaskSlaPause.Start(Guid.NewGuid());
        pause.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskSlaPauseCreatedDomainEvent>();

        pause.ClearDomainEvents();
        pause.Close();
        pause.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskSlaPauseUpdatedDomainEvent>()
            .Which.EndAt.Should().NotBeNull();
    }

    // ── MonitoringCheck ──────────────────────────────────────────────────

    [Fact]
    public void MonitoringCheck_ShouldRaiseCreatedUpdatedDeleted()
    {
        var deviceId = Guid.NewGuid();
        var check = MonitoringCheck.Create(deviceId, CheckKind.Ping);
        check.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MonitoringCheckCreatedDomainEvent>()
            .Which.DeviceId.Should().Be(deviceId);

        check.ClearDomainEvents();
        check.SetEnabled(false);
        check.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MonitoringCheckUpdatedDomainEvent>()
            .Which.Enabled.Should().BeFalse();

        check.ClearDomainEvents();
        check.Delete();
        check.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MonitoringCheckDeletedDomainEvent>();
    }

    // ── DiscoveryRun (sem DELETE — ciclo por estado) ─────────────────────

    [Fact]
    public void DiscoveryRun_ShouldRaiseCreatedAndUpdated()
    {
        var deviceId = Guid.NewGuid();
        var run = DiscoveryRun.Start(deviceId);
        run.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DiscoveryRunCreatedDomainEvent>()
            .Which.DeviceId.Should().Be(deviceId);

        run.ClearDomainEvents();
        run.Complete();
        run.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DiscoveryRunUpdatedDomainEvent>()
            .Which.Status.Should().Be(DiscoveryRunStatus.Completed);
    }

    // ── Incident (Created já existia; Updated novo; sem DELETE) ──────────

    [Fact]
    public void Incident_Create_ShouldRaiseCreated()
    {
        var incident = Incident.Create("Sem energia", IncidentSeverity.High);
        incident.DomainEvents.Should().Contain(i => i is IncidentCreatedDomainEvent);
    }

    [Fact]
    public void Incident_ChangeStatus_ShouldRaiseUpdated()
    {
        var incident = Incident.Create("Sem energia", IncidentSeverity.High);
        incident.ClearDomainEvents();

        incident.ChangeStatus(IncidentStatus.Investigating);

        incident.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IncidentUpdatedDomainEvent>()
            .Which.Status.Should().Be(IncidentStatus.Investigating);
    }

    // ── RemoteSession (sem DELETE — histórico) ───────────────────────────

    [Fact]
    public void RemoteSession_ShouldRaiseCreatedAndUpdated()
    {
        var deviceId = Guid.NewGuid();
        var session = RemoteSession.Open(deviceId, Guid.NewGuid(), RemoteProtocol.Ssh);
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RemoteSessionCreatedDomainEvent>()
            .Which.DeviceId.Should().Be(deviceId);

        session.ClearDomainEvents();
        session.Close();
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RemoteSessionUpdatedDomainEvent>()
            .Which.Status.Should().Be(RemoteSessionStatus.Closed);
    }
}
