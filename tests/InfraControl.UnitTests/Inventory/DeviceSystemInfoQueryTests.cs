using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Moq;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F30-T001 (docs/42 §A L63-70): identificação do switch (firmware/uptime)
/// lida do HISTÓRICO de polls (<c>MonitoringResult</c>) — sem coluna nova em
/// <c>Device</c>, sem migração (decisão 1 da §7 do plano docs/51).
/// REGRA: sem histórico os campos são null — nunca 0, nunca string vazia; o
/// carimbo é o do poll que forneceu o dado, não o de agora.
/// </summary>
public sealed class DeviceSystemInfoQueryTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();

    [Fact]
    public void Query_ShouldFollowTheBuildingBlocksContract()
    {
        var query = new GetDeviceSystemInfoQuery(DeviceId);

        query.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
        query.CacheDuration.Should().NotBeNull("o histórico muda pouco — cache curto evita varrer a tabela");
    }

    [Fact]
    public async Task SuccessfulPoll_ShouldReturnUptimeSysDescrAndItsOwnTimestamp()
    {
        var capturedAt = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var poll = Poll(
            succeeded: true,
            values: new Dictionary<string, string>
            {
                ["sysDescr"] = "UniFiSwitch 8 POE, 6.2.26",
                ["uptimeSeconds"] = "86400"
            },
            capturedAt: capturedAt);

        var dto = await HandleAsync([poll]);

        dto.UptimeSeconds.Should().Be(86400);
        dto.SysDescr.Should().Be("UniFiSwitch 8 POE, 6.2.26");
        dto.CapturedAtUtc.Should().Be(capturedAt);
    }

    [Fact]
    public async Task LatestPollFailed_ShouldFallBackToTheLastSuccessfulOne()
    {
        var older = new DateTime(2026, 10, 14, 11, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

        var succeeded = Poll(
            succeeded: true,
            values: new Dictionary<string, string> { ["uptimeSeconds"] = "100" },
            capturedAt: older);
        var failed = Poll(
            succeeded: false, values: null, capturedAt: newer, error: "timeout simulado");

        // O repositório devolve o mais recente primeiro.
        var dto = await HandleAsync([failed, succeeded]);

        dto.UptimeSeconds.Should().Be(100, "o poll que falhou não pode apagar o último dado bom");
        dto.CapturedAtUtc.Should().Be(older, "e o carimbo é o daquele poll, não o do que falhou");
    }

    [Fact]
    public async Task NoHistoryAtAll_ShouldReturnAllNulls_NeverZero()
    {
        var dto = await HandleAsync([]);

        dto.UptimeSeconds.Should().BeNull();
        dto.SysDescr.Should().BeNull();
        dto.CapturedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task PollWithoutUptimeKey_ShouldNotInventZero()
    {
        var poll = Poll(
            succeeded: true,
            values: new Dictionary<string, string> { ["sysName"] = "switch-corredor" });

        var dto = await HandleAsync([poll]);

        dto.UptimeSeconds.Should().BeNull("o device não reportou uptime — nunca 0");
        dto.SysDescr.Should().BeNull();
        dto.CapturedAtUtc.Should().NotBeNull("o poll existiu, mesmo sem os campos");
    }

    [Fact]
    public async Task PollWithGarbageUptime_ShouldBeIgnoredNotParsed()
    {
        var poll = Poll(
            succeeded: true,
            values: new Dictionary<string, string> { ["uptimeSeconds"] = "not-a-number" });

        var dto = await HandleAsync([poll]);

        dto.UptimeSeconds.Should().BeNull();
    }

    [Fact]
    public async Task UnknownDevice_ShouldThrowInsteadOfReturningEmptyInfo()
    {
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(r => r.GetByIdAsync(DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);
        var results = new Mock<IMonitoringResultRepository>();
        var handler = new GetDeviceSystemInfoQueryHandler(devices.Object, results.Object);

        var act = () => handler.Handle(
            new GetDeviceSystemInfoQuery(DeviceId), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        results.Verify(
            r => r.GetRecentByDeviceAndKindAsync(
                It.IsAny<Guid>(), It.IsAny<CheckKind>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never, "não se consulta histórico de um device que não existe");
    }

    private static MonitoringResult Poll(
        bool succeeded,
        IReadOnlyDictionary<string, string>? values,
        DateTime? capturedAt = null,
        string? error = null)
    {
        var poll = MonitoringResult.Create(
            DeviceId, CheckKind.SnmpSystem, succeeded, durationMs: 12,
            error: error, values: values);

        if (capturedAt is not null)
            poll.CreateDateTime = capturedAt.Value;

        return poll;
    }

    private static async Task<DeviceSystemInfoDto> HandleAsync(
        IReadOnlyList<MonitoringResult> polls)
    {
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(r => r.GetByIdAsync(DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Device.Create(
                Guid.NewGuid(), "Switch-Corredor", DeviceType.Switch, ip: "192.0.2.10"));

        var results = new Mock<IMonitoringResultRepository>();
        results.Setup(r => r.GetRecentByDeviceAndKindAsync(
                DeviceId, CheckKind.SnmpSystem, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(polls);

        var handler = new GetDeviceSystemInfoQueryHandler(devices.Object, results.Object);
        return await handler.Handle(
            new GetDeviceSystemInfoQuery(DeviceId), CancellationToken.None);
    }
}
