using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F54-T001: itens de um host Zabbix (item.get + lastvalue) e série
/// temporal em lote (history.get itemids[], tabelas unsigned+float).
/// </summary>
public sealed class ZabbixHostMetricsQueriesTests
{
    private const string HostId = "10084";

    [Fact]
    public async Task HostItems_ReturnsItemsWithLastValues()
    {
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.ListItemsAsync(HostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ZabbixItemDto("501", HostId, "CPU util", "system.cpu.util[,idle]", 0, true, false, "%"),
                new ZabbixItemDto("502", HostId, "Tráfego in", "net.if.in[eth0]", 0, true, true, "B"),
            ]);
        client.Setup(c => c.GetItemsLastValuesAsync(HostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ZabbixItemLastValueDto("501", "system.cpu.util[,idle]", "42.5", DateTime.UtcNow),
            ]);

        var handler = new GetZabbixHostItemsQueryHandler(client.Object);
        var result = await handler.Handle(
            new GetZabbixHostItemsQuery(HostId), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].ItemId.Should().Be("501");
        result[0].LastValue.Should().Be("42.5");
        result[0].LastClockUtc.Should().NotBeNull();
        result[1].LastValue.Should().BeNull();
        result[1].Unsupported.Should().BeTrue();
    }

    [Fact]
    public async Task HostItems_EmptyHostId_Throws()
    {
        var handler = new GetZabbixHostItemsQueryHandler(Mock.Of<IZabbixClient>());
        var act = async () => await handler.Handle(
            new GetZabbixHostItemsQuery(" "), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Series_FetchesBothHistoryTables_AndDeduplicates()
    {
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.ListItemsAsync(HostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ZabbixItemDto("501", HostId, "CPU", "system.cpu.util", 0, true, false, "%"),
                new ZabbixItemDto("502", HostId, "Uptime", "system.uptime", 0, true, false, "s"),
            ]);

        var t1 = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        client.Setup(c => c.GetHistoryBatchAsync(
                It.Is<IReadOnlyList<string>>(ids => ids.Contains("501") && ids.Contains("502")),
                3, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ZabbixHistoryValueDto("501", t1, "10"),
                new ZabbixHistoryValueDto("501", t1, "10"), // duplicado entre tabelas
            ]);
        client.Setup(c => c.GetHistoryBatchAsync(
                It.IsAny<IReadOnlyList<string>>(), 0,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ZabbixHistoryValueDto("501", t1.AddMinutes(5), "11.5"),
                new ZabbixHistoryValueDto("502", t1, "999"),
            ]);

        var handler = new GetZabbixHostSeriesQueryHandler(client.Object);
        var result = await handler.Handle(
            new GetZabbixHostSeriesQuery(HostId, "501,502", t1, t1.AddHours(1)),
            CancellationToken.None);

        result.Series.Should().HaveCount(2);
        var cpu = result.Series.Single(s => s.ItemId == "501");
        cpu.Points.Should().HaveCount(2); // dedup removeu a repetição
        cpu.Points.Select(p => p.Value).Should().Equal("10", "11.5");
        cpu.Points.Should().BeInAscendingOrder(p => p.ClockUtc);
        result.Series.Single(s => s.ItemId == "502").Points.Should().ContainSingle();
    }

    [Fact]
    public async Task Series_IgnoresItemsNotBelongingToHost_AndUnsupported()
    {
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.ListItemsAsync(HostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ZabbixItemDto("501", HostId, "CPU", "system.cpu.util", 0, true, false, "%"),
                new ZabbixItemDto("777", "other-host", "Alheio", "icmpping", 0, true, false, ""),
            ]);
        client.Setup(c => c.GetHistoryBatchAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetZabbixHostSeriesQueryHandler(client.Object);
        var result = await handler.Handle(
            new GetZabbixHostSeriesQuery(HostId, "501,777", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow),
            CancellationToken.None);

        // Só o item do host entrou; o alheio foi filtrado pela rota do host.
        client.Verify(c => c.GetHistoryBatchAsync(
            It.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids.Contains("501")),
            It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        result.Series.Should().ContainSingle(s => s.ItemId == "501");
    }

    [Fact]
    public async Task Series_NoMatchingItems_ReturnsEmptyWithoutHistoryCall()
    {
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.ListItemsAsync(HostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetZabbixHostSeriesQueryHandler(client.Object);
        var result = await handler.Handle(
            new GetZabbixHostSeriesQuery(HostId, "999", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow),
            CancellationToken.None);

        result.Series.Should().BeEmpty();
        client.Verify(c => c.GetHistoryBatchAsync(
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Series_TooManyItems_Throws()
    {
        var handler = new GetZabbixHostSeriesQueryHandler(Mock.Of<IZabbixClient>());
        var ids = string.Join(",", Enumerable.Range(1, 21).Select(i => i.ToString()));
        var act = async () => await handler.Handle(
            new GetZabbixHostSeriesQuery(HostId, ids, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow),
            CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Series_RangeOver92Days_Throws()
    {
        var handler = new GetZabbixHostSeriesQueryHandler(Mock.Of<IZabbixClient>());
        var act = async () => await handler.Handle(
            new GetZabbixHostSeriesQuery(HostId, "1",
                DateTime.UtcNow.AddDays(-100), DateTime.UtcNow),
            CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
