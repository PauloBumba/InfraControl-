using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Monitoring.Events.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Xunit;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// Stats de eventos Zabbix por dia/severidade/status — agrupamento por dia
/// no fuso de Brasília sobre os eventos ingeridos (Source = Zabbix).
/// </summary>
public sealed class ZabbixEventStatsQueryTests
{
    private sealed class FakeEventRepository(IReadOnlyList<MonitoringEventStatRow> rows)
        : IMonitoringEventRepository
    {
        public Task<MonitoringEvent?> GetByIdAsync(
            Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<MonitoringEvent?>(null);

        public Task AddAsync(MonitoringEvent entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Remove(MonitoringEvent entity) { }

        public Task<MonitoringEvent?> GetBySourceAndExternalEventIdAsync(
            MonitoringSource source, string externalEventId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<MonitoringEvent?>(null);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<MonitoringEventStatRow>> ListStatsSinceAsync(
            MonitoringSource source, DateTime sinceUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(rows);
    }

    [Fact]
    public async Task Handle_ShouldGroupByDaySeverityAndStatus()
    {
        var rows = new[]
        {
            // 2026-11-15 02:00 UTC = 2026-11-14 23:00 em Brasília → dia 14.
            new MonitoringEventStatRow(
                new DateTime(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc), 4,
                MonitoringEventStatus.Problem),
            new MonitoringEventStatRow(
                new DateTime(2026, 11, 15, 2, 30, 0, DateTimeKind.Utc), 4,
                MonitoringEventStatus.Problem),
            new MonitoringEventStatRow(
                new DateTime(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc), 2,
                MonitoringEventStatus.Recovered),
        };

        var handler = new GetZabbixEventStatsQueryHandler(new FakeEventRepository(rows));
        var result = await handler.Handle(
            new GetZabbixEventStatsQuery(14), CancellationToken.None);

        result.Days.Should().Be(14);
        result.Points.Should().HaveCount(2);
        result.Points.Should().Contain(p =>
            p.Date == "2026-11-14" && p.Severity == 4
            && p.Status == "Problem" && p.Count == 2);
        result.Points.Should().Contain(p =>
            p.Date == "2026-11-15" && p.Severity == 2
            && p.Status == "Recovered" && p.Count == 1);
    }

    [Fact]
    public async Task Handle_EmptyWindow_ShouldReturnEmptyPoints()
    {
        var handler = new GetZabbixEventStatsQueryHandler(
            new FakeEventRepository(Array.Empty<MonitoringEventStatRow>()));

        var result = await handler.Handle(
            new GetZabbixEventStatsQuery(7), CancellationToken.None);

        result.Points.Should().BeEmpty();
    }
}
