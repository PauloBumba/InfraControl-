using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T003a: SnmpServerCpuProvider — CPU por core via hrProcessorLoad
/// (HOST-RESOURCES-MIB RFC 2790, universal Windows/Linux). Reporta o pior
/// core, a média, a contagem e (best-effort) o total de processos
/// (hrSystemProcesses — informativo, sem regra de alerta).
/// </summary>
public sealed class SnmpServerCpuProviderTests
{
    private const string ProcessorLoadOid = "1.3.6.1.2.1.25.3.3.1.2";
    private const string ProcessesOid = "1.3.6.1.2.1.25.1.6.0";

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

    private static IReadOnlyList<SnmpVariable> Load(
        params (int Index, string Value)[] rows)
        => [.. rows.Select(row => new SnmpVariable(
            $"{ProcessorLoadOid}.{row.Index}", row.Value))];

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpServerCpuProvider(
            new FakeSnmpClient(_ => Load((1, "10"))));

        provider.Name.Should().Be("SnmpServerCpu");
        provider.CanHandle("SnmpServerCpu").Should().BeTrue();
        provider.CanHandle("snmpservercpu").Should().BeTrue();
        provider.CanHandle("SnmpSystem").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportWorstCoreAverageAndCount()
    {
        var provider = new SnmpServerCpuProvider(new FakeSnmpClient(oid =>
            oid == ProcessesOid
                ? [new(ProcessesOid, "240")]
                : Load((1, "12"), (2, "95"), (3, "44"))));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["cpuPercent"].Should().Be("95", "o pior core é o alertável");
        result.Values["averagePercent"].Should().Be("50");
        result.Values["coreCount"].Should().Be("3");
        result.Values["corePercents"].Should().Be("12,95,44");
    }

    [Fact]
    public async Task SingleCore_ShouldReportSameWorstAndAverage()
    {
        var provider = new SnmpServerCpuProvider(
            new FakeSnmpClient(_ => Load((1, "63"))));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeTrue();
        result.Values!["cpuPercent"].Should().Be("63");
        result.Values["averagePercent"].Should().Be("63");
        result.Values["coreCount"].Should().Be("1");
    }

    [Fact]
    public async Task ZeroLoad_ShouldReportZero()
    {
        var provider = new SnmpServerCpuProvider(
            new FakeSnmpClient(_ => Load((1, "0"), (2, "0"))));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeTrue();
        result.Values!["cpuPercent"].Should().Be("0");
    }

    [Fact]
    public async Task GarbageOutOfRangeOrNegative_ShouldBeSkipped()
    {
        // hrProcessorLoad é 0..100 (RFC 2790): -1/150/lixo são descartados.
        var provider = new SnmpServerCpuProvider(new FakeSnmpClient(oid =>
            oid == ProcessesOid
                ? []
                : Load((1, "-1"), (2, "150"), (3, "abc"), (4, "80"))));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeTrue();
        result.Values!["cpuPercent"].Should().Be("80");
        result.Values["coreCount"].Should().Be("1");
    }

    [Fact]
    public async Task AllGarbage_ShouldFailWithPtBrError()
    {
        var provider = new SnmpServerCpuProvider(
            new FakeSnmpClient(_ => Load((1, "-1"), (2, "lixo"))));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhum hrProcessorLoad válido encontrado no servidor.");
    }

    [Fact]
    public async Task EmptyWalk_ShouldFailWithPtBrError()
    {
        var provider = new SnmpServerCpuProvider(new FakeSnmpClient(_ => []));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhum hrProcessorLoad válido encontrado no servidor.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingSnmp()
    {
        var snmp = new FakeSnmpClient(_ => Load((1, "10")));
        var provider = new SnmpServerCpuProvider(snmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        snmp.AskedOids.Should().BeEmpty();
    }

    [Fact]
    public async Task SnmpThrows_ShouldFailWithPtBrWrappedError()
    {
        var provider = new SnmpServerCpuProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
    }

    [Fact]
    public async Task ShouldWalkHrProcessorLoad()
    {
        var snmp = new FakeSnmpClient(oid =>
            oid == ProcessesOid ? [] : Load((1, "10")));
        var provider = new SnmpServerCpuProvider(snmp);

        await provider.CheckAsync("192.0.2.20");

        snmp.AskedOids.Should().Contain(ProcessorLoadOid,
            "a fonte é a hrProcessorTable do HOST-RESOURCES-MIB (RFC 2790)");
    }

    [Fact]
    public async Task ProcessCount_ShouldBeReportedWhenAvailable()
    {
        var provider = new SnmpServerCpuProvider(new FakeSnmpClient(oid =>
            oid == ProcessesOid
                ? [new(ProcessesOid, "240")]
                : Load((1, "33"))));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeTrue();
        result.Values!["processCount"].Should().Be("240");
    }

    [Fact]
    public async Task ProcessesWalkFails_ShouldStillSucceedWithoutProcessCount()
    {
        var provider = new SnmpServerCpuProvider(new FakeSnmpClient(oid =>
            oid == ProcessesOid
                ? throw new IOException("hrSystemProcesses indisponível.")
                : Load((1, "10"))));

        var result = await provider.CheckAsync("192.0.2.20");

        result.Succeeded.Should().BeTrue(
            "contagem de processos é best-effort — CPU é a fonte essencial");
        result.Values!.Should().NotContainKey("processCount");
        result.Values["cpuPercent"].Should().Be("10");
    }
}
