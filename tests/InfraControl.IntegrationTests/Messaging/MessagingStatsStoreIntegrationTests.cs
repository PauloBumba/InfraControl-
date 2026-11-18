using FluentAssertions;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Messaging;

/// <summary>
/// F55-T001: contagens do overview de mensageria em PostgreSQL real —
/// delta antes/depois do seed (o disparo de domain events no seed já
/// pode encher o outbox, então só o delta é determinístico).
/// </summary>
public sealed class MessagingStatsStoreIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task Stats_ShouldCountOutboxInboxAndDeliveries_ByDelta()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = new MessagingStatsStore(context);

        // Usuário/notificação primeiro: o seed dispara UserCreatedDomainEvent
        // (outbox real) — capturar o baseline depois evita contá-lo no delta.
        var user = User.Create(Guid.NewGuid(), "messaging-stats@exemplo.test", "Usuário Stats");
        context.DomainUsers.Add(user);
        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", "Mensagem.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var before = await store.GetStatsAsync();

        var pendingOutbox = new OutboxMessage(
            "UserCreatedDomainEvent", "{}", DateTime.UtcNow);
        var exhaustedOutbox = new OutboxMessage(
            "UserCreatedDomainEvent", "{}", DateTime.UtcNow);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            exhaustedOutbox.MarkFailed("falha permanente");
        }

        var processedOutbox = new OutboxMessage(
            "UserCreatedDomainEvent", "{}", DateTime.UtcNow);
        processedOutbox.MarkProcessed();

        context.OutboxMessages.AddRange(pendingOutbox, exhaustedOutbox, processedOutbox);
        context.ProcessedMessages.Add(
            new ProcessedMessage(Guid.NewGuid(), "NotificationConsumer"));

        // ux_notification_deliveries_dedup exige canal distinto por
        // (notificação, usuário) — uma delivery por canal.
        var pendingDelivery = NotificationDelivery.Create(
            notification.Id, user.Id, NotificationChannel.Email);
        var retryDelivery = NotificationDelivery.Create(
            notification.Id, user.Id, NotificationChannel.SignalR);
        retryDelivery.RegisterFailedAttempt(
            "erro transitório", DateTime.UtcNow, 5, TimeSpan.FromSeconds(30));
        var dlqDelivery = NotificationDelivery.Create(
            notification.Id, user.Id, NotificationChannel.Telegram);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            dlqDelivery.RegisterFailedAttempt(
                "falha permanente", DateTime.UtcNow, 5, TimeSpan.FromSeconds(30));
        }

        var sentDelivery = NotificationDelivery.Create(
            notification.Id, user.Id, NotificationChannel.Webhook);
        sentDelivery.MarkSent(DateTime.UtcNow);
        var suppressedDelivery = NotificationDelivery.Create(
            notification.Id, user.Id, NotificationChannel.Slack);
        suppressedDelivery.MarkSuppressed("janela de estabilização");

        context.NotificationDeliveries.AddRange(
            pendingDelivery, retryDelivery, dlqDelivery, sentDelivery, suppressedDelivery);
        await context.SaveChangesAsync();

        var after = await store.GetStatsAsync();

        (after.OutboxPending - before.OutboxPending).Should().Be(1);
        (after.OutboxExhausted - before.OutboxExhausted).Should().Be(1);
        (after.OutboxProcessedTotal - before.OutboxProcessedTotal).Should().Be(1);
        (after.InboxProcessedTotal - before.InboxProcessedTotal).Should().Be(1);

        (after.DeliveriesPending - before.DeliveriesPending).Should().Be(1);
        (after.DeliveriesRetryDue - before.DeliveriesRetryDue).Should().Be(1);
        (after.DeliveriesDlq - before.DeliveriesDlq).Should().Be(1);
        (after.DeliveriesSent - before.DeliveriesSent).Should().Be(1);
        (after.DeliveriesSuppressed - before.DeliveriesSuppressed).Should().Be(1);
        (after.DeliveriesFailed - before.DeliveriesFailed).Should().Be(2);

        after.AvgOutboxLatencySeconds.Should().NotBeNull();
        after.AvgOutboxLatencySeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Stats_WithNoData_ShouldReturnHonestZeroesAndNullLatency()
    {
        using var scope = CreateScope();
        var store = new MessagingStatsStore(
            scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var stats = await store.GetStatsAsync();

        // O seed da base dispara domain events reais → o outbox pode ter
        // pendente; o que não existe é delivery/inbox/processado.
        stats.OutboxExhausted.Should().Be(0);
        stats.InboxProcessedTotal.Should().Be(0);
        stats.DeliveriesPending.Should().Be(0);
        stats.DeliveriesDlq.Should().Be(0);
        stats.DeliveriesSent.Should().Be(0);
        // Sem processados no outbox → latência indisponível, nunca 0 inventado.
        stats.AvgOutboxLatencySeconds.Should().BeNull();
    }
}
