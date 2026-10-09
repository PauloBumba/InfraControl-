using FluentAssertions;
using InfraControl.Application.Inventory.Locations;
using InfraControl.Application.Inventory.Locations.Commands;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.IntegrationTests.Common;
using InfraControl.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace InfraControl.IntegrationTests.Operations;

/// <summary>
/// ADR-005 + ADR-030 §5 — CRUD operacional ponta-a-ponta: cada mutação
/// (Create/Update/Delete) grava o domain event correspondente na Outbox
/// na MESMA transação dos dados, com o constraint de tipo que o
/// OutboxDispatcherWorker usa para resolver o evento.
/// </summary>
public sealed class CrudOutboxIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task LocationCrud_ShouldWriteCreatedUpdatedDeletedEventsToOutbox()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        // ── Create ────────────────────────────────────────────────────────
        var created = await mediator.Send(
            new CreateLocationCommand("Sala Rack A", "andar 1"));
        created.Name.Should().Be("Sala Rack A");

        var createdEvents = await OutboxAsync(context, nameof(LocationCreatedDomainEvent));
        var createdPayload = createdEvents.Should().ContainSingle().Subject;
        Parse(createdPayload.Payload, "LocationId").GetGuid().Should().Be(created.Id);
        Parse(createdPayload.Payload, "Name").GetString().Should().Be("Sala Rack A");

        // ── Update ────────────────────────────────────────────────────────
        await mediator.Send(
            new UpdateLocationCommand(created.Id, "Sala Rack A2", "andar 2"));

        var updatedEvents = await OutboxAsync(context, nameof(LocationUpdatedDomainEvent));
        var updatedPayload = updatedEvents.Should().ContainSingle().Subject;
        Parse(updatedPayload.Payload, "LocationId").GetGuid().Should().Be(created.Id);

        // ── Delete ────────────────────────────────────────────────────────
        await mediator.Send(new DeleteLocationCommand(created.Id));

        var deletedEvents = await OutboxAsync(context, nameof(LocationDeletedDomainEvent));
        var deletedPayload = deletedEvents.Should().ContainSingle().Subject;
        Parse(deletedPayload.Payload, "LocationId").GetGuid().Should().Be(created.Id);

        // ── constraint do dispatcher ──────────────────────────────────────
        foreach (var eventType in new[]
                 {
                     nameof(LocationCreatedDomainEvent),
                     nameof(LocationUpdatedDomainEvent),
                     nameof(LocationDeletedDomainEvent),
                 })
        {
            Type.GetType($"InfraControl.Domain.Events.{eventType}, InfraControl.Domain")
                .Should().NotBeNull(
                    $"o OutboxDispatcherWorker.ResolveEventType precisa resolver '{eventType}'");
        }
    }

    private static async Task<List<InfraControl.Domain.Entities.OutboxMessage>> OutboxAsync(
        AppDbContext context, string eventType)
        => await context.OutboxMessages.AsNoTracking()
            .Where(m => m.EventType == eventType)
            .ToListAsync();

    private static JsonElement Parse(string payload, string property)
        => JsonDocument.Parse(payload).RootElement.GetProperty(property);
}
