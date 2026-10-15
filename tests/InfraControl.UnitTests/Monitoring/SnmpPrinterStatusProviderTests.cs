using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T002b: SnmpPrinterStatusProvider — bandejas (prtInputStatus),
/// tampa (prtCoverStatus) e alertas (prtAlertTable) via Printer MIB
/// (RFC 3805). Agregado por device, padrão F29-3.
/// </summary>
public sealed class SnmpPrinterStatusProviderTests
{
    private const string InputStatusOid = "1.3.6.1.2.1.43.8.2.1.9";
    private const string CoverStatusOid = "1.3.6.1.2.1.43.6.1.1.3";
    private const string AlertTableOid = "1.3.6.1.2.1.43.18";

    // prtInputStatus: idle(3), jammed(5), mediaLow(8), mediaEmpty(9),
    // mediaJammed(10), inputLevelEmpty(16).
    private static IReadOnlyList<SnmpVariable> IdleInputs(int count = 2)
        => [.. Enumerable.Range(1, count)
            .Select(i => new SnmpVariable($"{InputStatusOid}.{i}", "3"))];

    // prtCoverStatus: doorClosed(4), closed(8) = fechada; doorOpen(3),
    // interlockOpen(5), open(7) = aberta.
    private static IReadOnlyList<SnmpVariable> ClosedCovers(int count = 1)
        => [.. Enumerable.Range(1, count)
            .Select(i => new SnmpVariable($"{CoverStatusOid}.{i}", "4"))];

    // prtAlertTable idle: uma linha com code=unknown(2) = sem alerta ativo.
    private static IReadOnlyList<SnmpVariable> NoAlerts()
        =>
        [
            new($"{AlertTableOid}.1.1.1", "2"),
            new($"{AlertTableOid}.1.2.1", "2")
        ];

    private static IReadOnlyList<SnmpVariable> Alert(int code, int severity, int index = 1)
        =>
        [
            new($"{AlertTableOid}.1.1.{index}", code.ToString()),
            new($"{AlertTableOid}.1.2.{index}", severity.ToString())
        ];

    private static IReadOnlyList<SnmpVariable> Healthy(string oid) => oid switch
    {
        InputStatusOid => IdleInputs(),
        CoverStatusOid => ClosedCovers(),
        AlertTableOid => NoAlerts(),
        _ => []
    };

    private sealed class FakeSnmpClient(
        Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public List<string> AskedOids { get; } = [];

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            AskedOids.Add(oid);
            return Task.FromResult(walkFactory(oid));
        }
    }

    private sealed class ThrowingSnmpClient : ISnmpClient
    {
        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => throw new TimeoutException("SNMP timeout simulado.");
    }

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(Healthy));

        provider.Name.Should().Be("SnmpPrinterStatus");
        provider.CanHandle("SnmpPrinterStatus").Should().BeTrue();
        provider.CanHandle("snmpprinterstatus").Should().BeTrue();
        provider.CanHandle("SnmpToner").Should().BeFalse();
    }

    [Fact]
    public async Task HealthyPrinter_ShouldSucceedWithZeroCounts()
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(Healthy));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values["inputCount"].Should().Be("2");
        result.Values["inputEmptyCount"].Should().Be("0");
        result.Values["inputJammedCount"].Should().Be("0");
        result.Values["coverOpen"].Should().Be("false");
        result.Values["activeAlertCount"].Should().Be("0");
    }

    [Fact]
    public async Task CoverOpen_ShouldReportCoverOpenTrue()
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(oid =>
            oid == CoverStatusOid
                ? [new($"{CoverStatusOid}.1", "3")]
                : oid == InputStatusOid ? IdleInputs() : NoAlerts()));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Values["coverOpen"].Should().Be("true");
    }

    [Theory]
    [InlineData("9")]  // mediaEmpty
    [InlineData("16")] // inputLevelEmpty
    public async Task EmptyInputStatuses_ShouldCountAsEmpty(string status)
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(oid =>
            oid == InputStatusOid
                ? [new($"{InputStatusOid}.1", "3"), new($"{InputStatusOid}.2", status)]
                : oid == CoverStatusOid ? ClosedCovers() : NoAlerts()));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Values["inputCount"].Should().Be("2");
        result.Values["inputEmptyCount"].Should().Be("1");
    }

    [Theory]
    [InlineData("5")]  // jammed
    [InlineData("10")] // mediaJammed
    public async Task JammedInputStatuses_ShouldCountAsJammed(string status)
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(oid =>
            oid == InputStatusOid
                ? [new($"{InputStatusOid}.1", status)]
                : oid == CoverStatusOid ? ClosedCovers() : NoAlerts()));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Values["inputJammedCount"].Should().Be("1");
    }

    [Fact]
    public async Task ActiveAlertRow_ShouldBeCountedAndReportWorstSeverity()
    {
        // tonerLow-ish code 2001, severity critical(3).
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(oid =>
            oid == AlertTableOid ? Alert(code: 2001, severity: 3) : Healthy(oid)));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Values["activeAlertCount"].Should().Be("1");
        result.Values["worstAlertSeverity"].Should().Be("critical");
    }

    [Fact]
    public async Task IdleAlertRow_ShouldNotCountAsActive()
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(Healthy));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Values["activeAlertCount"].Should().Be("0",
            "linha idle (code=unknown) da prtAlertTable não é alerta ativo");
    }

    [Fact]
    public async Task AlertSeverityMajorAndMinor_ShouldReportWorstAsMajor()
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(oid =>
            oid == AlertTableOid
                ? [.. Alert(code: 500, severity: 5, index: 1),
                    .. Alert(code: 600, severity: 4, index: 2)]
                : Healthy(oid)));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Values["activeAlertCount"].Should().Be("2");
        result.Values["worstAlertSeverity"].Should().Be("major",
            "major(4) é pior que minor(5) — menor número = pior severidade real");
    }

    [Fact]
    public async Task AllThreeWalksEmpty_ShouldFailWithPtBrError()
    {
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(_ => []));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Printer-MIB não respondeu (prtInputStatus/prtCoverStatus/prtAlertTable vazios).");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingSnmp()
    {
        var snmp = new FakeSnmpClient(Healthy);
        var provider = new SnmpPrinterStatusProvider(snmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        snmp.AskedOids.Should().BeEmpty();
    }

    [Fact]
    public async Task SnmpThrows_ShouldFailWithPtBrWrappedError()
    {
        var provider = new SnmpPrinterStatusProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar prtInputStatus:");
    }

    [Fact]
    public async Task ShouldWalkTheThreePrinterMibColumns()
    {
        var snmp = new FakeSnmpClient(Healthy);
        var provider = new SnmpPrinterStatusProvider(snmp);

        await provider.CheckAsync("192.0.2.70");

        snmp.AskedOids.Should().ContainInOrder(
            InputStatusOid, CoverStatusOid, AlertTableOid);
    }

    [Fact]
    public async Task CoverAndAlertWalksFailing_ShouldStillSucceedWithInputs()
    {
        // Impressora com MIB parcial: só prtInputStatus responde — check
        // parcial vale mais que check morto (padrão F29-3 ifXTable).
        var provider = new SnmpPrinterStatusProvider(new FakeSnmpClient(oid =>
            oid == InputStatusOid ? IdleInputs() : throw new TimeoutException()));

        var result = await provider.CheckAsync("192.0.2.70");

        result.Succeeded.Should().BeTrue();
        result.Values["inputCount"].Should().Be("2");
        result.Values["coverOpen"].Should().Be("false");
        result.Values["activeAlertCount"].Should().Be("0");
    }
}
