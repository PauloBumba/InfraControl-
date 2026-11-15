using FluentAssertions;
using InfraControl.Application.Notifications.Stats;
using InfraControl.Application.Notifications.Stats.Ports;
using InfraControl.Domain.Enums;
using Xunit;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// Stats de entregas por dia/canal/status — agrupamento real por dia no
/// fuso de Brasília e contagem por status.
/// </summary>
public sealed class NotificationDeliveryStatsQueryTests
{
    private sealed class FakeStore(IReadOnlyList<NotificationDeliveryStatRow> rows)
        : INotificationDeliveryStatsStore
    {
        public Task<IReadOnlyList<NotificationDeliveryStatRow>> ListSinceAsync(
            DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(rows);
    }

    [Fact]
    public async Task Handle_ShouldGroupBySaoPauloDayChannelAndStatus()
    {
        // 2026-11-15 01:30 UTC = 2026-11-14 22:30 em Brasília → dia 14.
        var rows = new[]
        {
            new NotificationDeliveryStatRow(
                new DateTime(2026, 11, 15, 1, 30, 0, DateTimeKind.Utc),
                NotificationChannel.WhatsApp, DeliveryStatus.Sent),
            new NotificationDeliveryStatRow(
                new DateTime(2026, 11, 15, 1, 45, 0, DateTimeKind.Utc),
                NotificationChannel.WhatsApp, DeliveryStatus.Sent),
            new NotificationDeliveryStatRow(
                new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc),
                NotificationChannel.Email, DeliveryStatus.Failed),
        };

        var handler = new GetNotificationDeliveryStatsQueryHandler(new FakeStore(rows));
        var result = await handler.Handle(
            new GetNotificationDeliveryStatsQuery(14), CancellationToken.None);

        result.Days.Should().Be(14);
        result.Points.Should().HaveCount(2);
        result.Points.Should().Contain(p =>
            p.Date == "2026-11-14" && p.Channel == "WhatsApp"
            && p.Status == "Sent" && p.Count == 2);
        result.Points.Should().Contain(p =>
            p.Date == "2026-11-15" && p.Channel == "Email"
            && p.Status == "Failed" && p.Count == 1);
    }

    [Fact]
    public async Task Handle_EmptyWindow_ShouldReturnEmptyPoints()
    {
        var handler = new GetNotificationDeliveryStatsQueryHandler(
            new FakeStore(Array.Empty<NotificationDeliveryStatRow>()));

        var result = await handler.Handle(
            new GetNotificationDeliveryStatsQuery(7), CancellationToken.None);

        result.Days.Should().Be(7);
        result.Points.Should().BeEmpty();
    }

    [Fact]
    public void NormalizedDays_ShouldClampToRange()
    {
        new GetNotificationDeliveryStatsQuery(0).NormalizedDays.Should().Be(14);
        new GetNotificationDeliveryStatsQuery(500).NormalizedDays.Should().Be(14);
        new GetNotificationDeliveryStatsQuery(30).NormalizedDays.Should().Be(30);
    }
}
