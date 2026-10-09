using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Domain.Common.Events;
using InfraControl.Infrastructure.Services;

namespace InfraControl.UnitTests.Messaging;

/// <summary>
/// ADR-005 + ADR-030 §5 — contrato outbox de TODOS os domain events:
/// o OutboxDispatcherWorker resolve o tipo pelo constraint
/// `InfraControl.Domain.Events.{EventType}, InfraControl.Domain` e o
/// payload round-trippa com o mesmo envelope (OutboxJson.Options) usado
/// pelo writer (DomainEventService). O consumer depende do EventId
/// original para a idempotência (docs/06) — ele precisa sobreviver ao
/// JSON.
/// </summary>
public sealed class OutboxEventContractTests
{
    private static readonly Assembly DomainAssembly =
        typeof(InfraControl.Domain.Entities.Device).Assembly;

    private static IEnumerable<Type> AllDomainEvents()
        => DomainAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IDomainEvent).IsAssignableFrom(t)
                && t.Namespace == "InfraControl.Domain.Events");

    [Fact]
    public void AllEvents_ShouldBeResolvable_ByOutboxDispatcherConstraint()
    {
        var events = AllDomainEvents().ToList();
        events.Should().NotBeEmpty();

        foreach (var eventType in events)
        {
            // Constraint exato do OutboxDispatcherWorker.ResolveEventType.
            var resolved = Type.GetType(
                $"InfraControl.Domain.Events.{eventType.Name}, InfraControl.Domain");

            resolved.Should().Be(eventType,
                $"{eventType.FullName} deve morar em InfraControl.Domain.Events");
        }
    }

    [Fact]
    public void AllEvents_ShouldRoundTripPayload_KeepingEventIdAndEventType()
    {
        foreach (var eventType in AllDomainEvents())
        {
            var instance = CreateInstance(eventType);

            var payload = JsonSerializer.Serialize(
                instance, eventType, OutboxJson.Options);
            var roundTripped = JsonSerializer.Deserialize(
                payload, eventType, OutboxJson.Options);

            roundTripped.Should().NotBeNull($"{eventType.Name} deve deserializar");
            ((IDomainEvent)roundTripped!).EventType.Should().Be(
                ((IDomainEvent)instance).EventType, $"{eventType.Name} preserva EventType");
            roundTripped.Should().BeOfType(eventType);
            roundTripped!.GetType().GetProperty("EventId")!
                .GetValue(roundTripped).Should().Be(
                    instance.GetType().GetProperty("EventId")!.GetValue(instance),
                    "o consumer usa o EventId original para idempotência (docs/06)");
        }
    }

    private static object CreateInstance(Type eventType)
    {
        var ctor = eventType.GetConstructors().Single();
        var args = ctor.GetParameters().Select(p => DefaultValue(p.ParameterType)).ToArray();
        return ctor.Invoke(args);
    }

    private static object? DefaultValue(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (type != underlying && !type.IsValueType)
            return null;

        if (underlying == typeof(string))
            return "contract-test";
        if (underlying == typeof(Guid))
            return Guid.NewGuid();
        if (underlying == typeof(DateTime))
            return DateTime.UtcNow;
        if (underlying == typeof(bool))
            return true;
        if (underlying == typeof(decimal))
            return 1.5m;
        if (underlying == typeof(int))
            return 3;
        if (underlying.IsEnum)
            return Enum.GetValues(underlying).GetValue(0);
        if (underlying == typeof(Guid?) || Nullable.GetUnderlyingType(type) is not null)
            return null;
        return underlying.IsValueType ? Activator.CreateInstance(underlying) : null;
    }
}
