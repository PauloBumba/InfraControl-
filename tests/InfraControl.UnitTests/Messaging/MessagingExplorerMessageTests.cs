using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Messaging.Explorer;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Messaging;

/// <summary>F55-T004: sanitização de payload + mapeamento do explorer.</summary>
public sealed class MessagingExplorerTests
{
    [Fact]
    public void Sanitize_RedactsSensitiveKeys_Recursively()
    {
        var payload = """
        {"eventType":"X","user":{"password":"abc","name":"joao"},
         "headers":{"Authorization":"Bearer xyz","x":"ok"},
         "nested":[{"api_key":"k","value":1}]}
        """;

        var result = MessagingPayloadSanitizer.Sanitize(payload);

        result.Should().NotContain("abc").And.NotContain("xyz").And.NotContain("\"k\"");
        result.Should().Contain("[REDACTED]").And.Contain("joao").And.Contain("\"x\":\"ok\"");
    }

    [Fact]
    public void Sanitize_NonJsonPayload_TruncatesAndMasks()
    {
        var payload = new string('a', 9000);
        var result = MessagingPayloadSanitizer.Sanitize(payload);
        result.Should().Contain("[truncado]");
        result.Length.Should().BeLessThan(8100);
    }

    private sealed class FakeStore : IMessagingExplorerStore
    {
        public List<OutboxMessage> Outbox { get; } = [];
        public List<ProcessedMessage> Inbox { get; } = [];
        public List<NotificationDelivery> Deliveries { get; } = [];

        public Task<PagedResult<OutboxMessage>> SearchOutboxAsync(
            MessagingMessageFilter f, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<OutboxMessage>(
                Outbox.Where(m => f.MessageId == null || m.Id == f.MessageId).ToArray(),
                Outbox.Count, f.Page, f.PageSize));

        public Task<OutboxMessage?> GetOutboxAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Outbox.FirstOrDefault(m => m.Id == id));

        public Task<IReadOnlyList<ProcessedMessage>> GetInboxByEventIdAsync(
            Guid eventId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProcessedMessage>>(
                Inbox.Where(i => i.EventId == eventId).ToArray());

        public Task<PagedResult<NotificationDelivery>> SearchDeliveriesAsync(
            MessagingDeliveryFilter f, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<NotificationDelivery>(
                Deliveries.ToArray(), Deliveries.Count, f.Page, f.PageSize));

        public Task<NotificationDelivery?> GetDeliveryAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Deliveries.FirstOrDefault(d => d.Id == id));
    }

    [Fact]
    public async Task Detail_ReturnsSanitizedPayload_AndTimeline()
    {
        var store = new FakeStore();
        var occurred = DateTime.UtcNow.AddMinutes(-5);
        var message = new OutboxMessage(
            "IncidentCreated",
            """{"token":"segredo","device":"sw1"}""",
            occurred, correlationId: "corr-1");
        message.MarkFailed("timeout");
        message.MarkFailed("timeout2");
        // Força ProcessedAt via reflection-free path: MarkProcessed depois de falhar
        // é permitido? MarkProcessed limpa Error — usar estado failed é suficiente.
        store.Outbox.Add(message);
        store.Inbox.Add(new ProcessedMessage(message.Id, "NotificationEventConsumer"));

        var handler = new GetMessagingMessageDetailQueryHandler(store);
        var detail = await handler.Handle(
            new GetMessagingMessageDetailQuery(message.Id), default);

        detail.Should().NotBeNull();
        detail!.SanitizedPayload.Should().Contain("[REDACTED]").And.NotContain("segredo");
        detail.State.Should().Be("failed");
        detail.RetryCount.Should().Be(2);
        detail.Timeline.Should().Contain(t => t.Stage.Contains("Publicada"));
        detail.Timeline.Should().Contain(t => t.Stage.Contains("Inbox"));
        detail.ProcessingDurationSeconds.Should().BeNull(); // ainda não processada
    }

    [Fact]
    public async Task Detail_ProcessedMessage_HasDuration()
    {
        var store = new FakeStore();
        var occurred = DateTime.UtcNow.AddMinutes(-2);
        var message = new OutboxMessage("X", "{}", occurred);
        message.MarkProcessed();
        store.Outbox.Add(message);

        var handler = new GetMessagingMessageDetailQueryHandler(store);
        var detail = await handler.Handle(
            new GetMessagingMessageDetailQuery(message.Id), default);

        detail!.State.Should().Be("processed");
        detail.ProcessingDurationSeconds.Should().BeGreaterThan(0);
        detail.Timeline.Should().Contain(t => t.Stage == "Processada");
    }

    [Fact]
    public async Task Search_MapsState_AndPreview()
    {
        var store = new FakeStore();
        var pending = new OutboxMessage("Pending", "{}", DateTime.UtcNow);
        var done = new OutboxMessage("Done", "{}", DateTime.UtcNow.AddMinutes(-1));
        done.MarkProcessed();
        store.Outbox.AddRange(pending, done);

        var handler = new SearchMessagingMessagesQueryHandler(store);
        var result = await handler.Handle(
            new SearchMessagingMessagesQuery(null, null, null, null, null, null,
                null, null, 1, 25), default);

        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(i => i.State == "pending");
        result.Items.Should().Contain(i => i.State == "processed");
    }
}
