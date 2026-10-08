using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.DomainEvents;

/// <summary>
/// ADR-030 §5 — Lote 3 (Knowledge, SLA, Service, Invitation): os métodos
/// do agregado levantam Created/Updated/Deleted no namespace
/// InfraControl.Domain.Events (constraint do outbox).
/// </summary>
public sealed class CrudDomainEventLote3Tests
{
    // ── KnowledgeArticle ─────────────────────────────────────────────────

    [Fact]
    public void KnowledgeArticle_ShouldRaiseCreatedUpdatedDeleted()
    {
        var article = KnowledgeArticle.Create("Como resetar switch", "passo 1", null, "Procedimento");
        article.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleCreatedDomainEvent>()
            .Which.Title.Should().Be("Como resetar switch");

        article.ClearDomainEvents();
        article.Update("Como resetar switch (v2)", "passo 2", null, "Procedimento");
        article.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleUpdatedDomainEvent>();

        article.ClearDomainEvents();
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleUpdatedDomainEvent>()
            .Which.Status.Should().Be(KnowledgeArticleStatus.InReview);

        article.ClearDomainEvents();
        article.Delete();
        article.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleDeletedDomainEvent>();
    }

    // ── KnowledgeCategory ────────────────────────────────────────────────

    [Fact]
    public void KnowledgeCategory_ShouldRaiseCreatedUpdatedDeleted()
    {
        var category = KnowledgeCategory.Create("Redes", null);
        category.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeCategoryCreatedDomainEvent>();

        category.ClearDomainEvents();
        category.Update("Redes LAN", null);
        category.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeCategoryUpdatedDomainEvent>();

        category.ClearDomainEvents();
        category.Delete();
        category.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeCategoryDeletedDomainEvent>();
    }

    // ── vínculos KnowledgeArticle* (Created/Deleted) ─────────────────────

    [Fact]
    public void KnowledgeArticleDevice_ShouldRaiseCreatedAndDeleted()
    {
        var link = KnowledgeArticleDevice.Create(Guid.NewGuid(), Guid.NewGuid());
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleDeviceCreatedDomainEvent>();

        link.ClearDomainEvents();
        link.Delete();
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleDeviceDeletedDomainEvent>();
    }

    [Fact]
    public void KnowledgeArticleIncident_ShouldRaiseCreatedAndDeleted()
    {
        var link = KnowledgeArticleIncident.Create(Guid.NewGuid(), Guid.NewGuid());
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleIncidentCreatedDomainEvent>();

        link.ClearDomainEvents();
        link.Delete();
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleIncidentDeletedDomainEvent>();
    }

    [Fact]
    public void KnowledgeArticleService_ShouldRaiseCreatedAndDeleted()
    {
        var link = KnowledgeArticleService.Create(Guid.NewGuid(), Guid.NewGuid());
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleServiceCreatedDomainEvent>();

        link.ClearDomainEvents();
        link.Delete();
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<KnowledgeArticleServiceDeletedDomainEvent>();
    }

    // ── Service ──────────────────────────────────────────────────────────

    [Fact]
    public void Service_ShouldRaiseCreatedUpdatedDeleted()
    {
        var service = Service.Create("Core Banking", null);
        service.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceCreatedDomainEvent>();

        service.ClearDomainEvents();
        service.Update("Core Banking (prod)", null);
        service.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceUpdatedDomainEvent>();

        service.ClearDomainEvents();
        service.Delete();
        service.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceDeletedDomainEvent>();
    }

    // ── SlaPolicy ────────────────────────────────────────────────────────

    [Fact]
    public void SlaPolicy_ShouldRaiseCreatedUpdatedDeleted()
    {
        var policy = SlaPolicy.Create("P1", 15, 60);
        policy.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SlaPolicyCreatedDomainEvent>()
            .Which.ResponseMinutes.Should().Be(15);

        policy.ClearDomainEvents();
        policy.Update("P1 crítico", 10, 30, null);
        policy.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SlaPolicyUpdatedDomainEvent>();

        policy.ClearDomainEvents();
        policy.Delete();
        policy.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SlaPolicyDeletedDomainEvent>();
    }

    // ── Slo ──────────────────────────────────────────────────────────────

    [Fact]
    public void Slo_ShouldRaiseCreatedUpdatedDeleted()
    {
        var service = Service.Create("Pagamentos", null);
        var slo = Slo.Create("Disponibilidade 99,9%", 99.9m, 30, service.Id);
        slo.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SloCreatedDomainEvent>()
            .Which.TargetPercent.Should().Be(99.9m);

        slo.ClearDomainEvents();
        slo.Update("Disponibilidade 99,95%", 99.95m, 30, service.Id);
        slo.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SloUpdatedDomainEvent>();

        slo.ClearDomainEvents();
        slo.Delete();
        slo.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SloDeletedDomainEvent>();
    }

    // ── SloBudget / SloMeasurement (sem PUT — Created/Deleted) ──────────

    [Fact]
    public void SloBudget_ShouldRaiseCreatedAndDeleted()
    {
        var budget = SloBudget.Create(
            Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 25m);
        budget.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SloBudgetCreatedDomainEvent>()
            .Which.ConsumedPercent.Should().Be(25m);

        budget.ClearDomainEvents();
        budget.Delete();
        budget.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SloBudgetDeletedDomainEvent>();
    }

    [Fact]
    public void SloMeasurement_ShouldRaiseCreatedAndDeleted()
    {
        var measurement = SloMeasurement.Create(Guid.NewGuid(), DateTime.UtcNow, 99.5m);
        measurement.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SloMeasurementCreatedDomainEvent>()
            .Which.ValuePercent.Should().Be(99.5m);

        measurement.ClearDomainEvents();
        measurement.Delete();
        measurement.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SloMeasurementDeletedDomainEvent>();
    }

    // ── Invitation (Updated em SetRoles e Cancel) ────────────────────────

    [Fact]
    public void Invitation_SetRoles_ShouldRaiseUpdated()
    {
        var invitation = Invitation.Create(
            "x@y.z", "hash", DateTime.UtcNow.AddDays(7), Guid.NewGuid(), [UserRole.Viewer]);
        invitation.ClearDomainEvents();

        invitation.SetRoles([UserRole.Operator]);

        invitation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvitationUpdatedDomainEvent>()
            .Which.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public void Invitation_Cancel_ShouldRaiseUpdated()
    {
        var invitation = Invitation.Create(
            "x@y.z", "hash", DateTime.UtcNow.AddDays(7), Guid.NewGuid(), [UserRole.Viewer]);
        invitation.ClearDomainEvents();

        invitation.Cancel();

        invitation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvitationUpdatedDomainEvent>()
            .Which.Status.Should().Be(InvitationStatus.Cancelled);
    }
}
