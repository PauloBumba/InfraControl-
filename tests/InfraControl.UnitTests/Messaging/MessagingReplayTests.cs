using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Messaging.Failures;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Moq;

namespace InfraControl.UnitTests.Messaging;

/// <summary>F55-T005: replay seletivo/idempotente de deliveries Failed.</summary>
public sealed class MessagingReplayTests
{
    private sealed class FakeDeliveryRepo(NotificationDelivery? delivery)
        : INotificationDeliveryRepository
    {
        public Task<NotificationDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(delivery is not null && delivery.Id == id ? delivery : null);
        public Task AddAsync(NotificationDelivery e, CancellationToken ct = default)
            => Task.CompletedTask;
        public void Remove(NotificationDelivery e) { }
        public Task<IReadOnlyCollection<PendingDeliveryItem>> ListPendingByChannelAsync(
            NotificationChannel c, int l, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<PendingDeliveryItem>>([]);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static NotificationDelivery FailedDlq()
    {
        var d = NotificationDelivery.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationChannel.WhatsApp);
        d.RegisterFailedAttempt("boom", DateTime.UtcNow, maxAttempts: 1,
            TimeSpan.FromSeconds(30));
        return d; // AttemptCount=1 >= max → Failed sem NextAttemptAt (DLQ)
    }

    private static NotificationDelivery FailedRetry()
    {
        var d = NotificationDelivery.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationChannel.Email);
        d.RegisterFailedAttempt("boom", DateTime.UtcNow, maxAttempts: 3,
            TimeSpan.FromSeconds(30));
        return d; // Failed com NextAttemptAt futuro (retry agendado)
    }

    [Fact]
    public async Task Replay_DlqDelivery_ReturnsToPendingNow()
    {
        var delivery = FailedDlq();
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.NextAttemptAt.Should().BeNull();

        var handler = new ReplayNotificationDeliveryCommandHandler(
            new FakeDeliveryRepo(delivery), Mock.Of<IUnitOfWork>());
        var before = DateTime.UtcNow;

        var result = await handler.Handle(
            new ReplayNotificationDeliveryCommand(delivery.Id, "reprocessar"), default);

        result.Success.Should().BeTrue();
        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.NextAttemptAt.Should().NotBeNull()
            .And.BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        delivery.AttemptCount.Should().Be(1); // histórico preservado
        delivery.LastError.Should().Be("boom");
    }

    [Fact]
    public async Task Replay_SentDelivery_FailsHonestly()
    {
        var delivery = NotificationDelivery.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationChannel.Telegram);
        delivery.MarkSent(DateTime.UtcNow);

        var handler = new ReplayNotificationDeliveryCommandHandler(
            new FakeDeliveryRepo(delivery), Mock.Of<IUnitOfWork>());

        var result = await handler.Handle(
            new ReplayNotificationDeliveryCommand(delivery.Id, null), default);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Apenas deliveries Failed");
        delivery.Status.Should().Be(DeliveryStatus.Sent);
    }

    [Fact]
    public async Task Replay_MissingDelivery_Fails()
    {
        var handler = new ReplayNotificationDeliveryCommandHandler(
            new FakeDeliveryRepo(null), Mock.Of<IUnitOfWork>());

        var result = await handler.Handle(
            new ReplayNotificationDeliveryCommand(Guid.NewGuid(), null), default);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("não encontrada");
    }

    [Fact]
    public void RequeueForReplay_OnlyFailed_ThrowsOtherwise()
    {
        var pending = NotificationDelivery.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationChannel.WhatsApp);
        var act = () => pending.RequeueForReplay(DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }
}
