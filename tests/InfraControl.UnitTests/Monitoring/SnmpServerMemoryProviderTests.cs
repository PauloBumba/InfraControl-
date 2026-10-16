using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T003b: SnmpServerMemoryProvider — memória física via hrStorageTable
/// (HOST-RESOURCES-MIB RFC 2790, universal Windows/Linux), classificando
/// linhas por hrStorageType (RAM=1, virtual/swap=2, discos=3+). Fallback:
/// hrMemorySize quando a tabela não traz linha RAM utilizável.
/// </summary>
public sealed class SnmpServerMemoryProviderTests
{
    private const string StorageTableOid = "1.3.6.1.2.1.25.2.3.1";
    private const string MemorySizeOid = "1.3.6.1.2.1.25.2.2";

    // colunas da hrStorageTable (RFC 2790): type=1 descr=3 alloc=4 size=5 used=6
    private const string ColType = "1";
    private const string ColDescr = "3";
    private const string ColAlloc = "4";
    private const string ColSize = "5";
    private const string ColUsed = "6";

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

    private static IEnumerable<SnmpVariable> Row(
        int index, string type, string alloc, string size, string used,
        string? descr = null)
    {
        yield return new($"{StorageTableOid}.{ColType}.{index}", type);
        yield return new($"{StorageTableOid}.{ColDescr}.{index}", descr ?? "row-{index}");
        yield return new($"{StorageTableOid}.{ColAlloc}.{index}", alloc);
        yield return new($"{StorageTableOid}.{ColSize}.{index}", size);
        yield return new($"{StorageTableOid}.{ColUsed}.{index}", used);
    }

    private static IReadOnlyList<SnmpVariable> Table(params IEnumerable<SnmpVariable>[] rows)
        => [.. rows.SelectMany(row => row)];

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpServerMemoryProvider(
            new FakeSnmpClient(_ => Table(Row(1, "1", "4096", "262144", "131072"))));

        provider.Name.Should().Be("SnmpServerMemory");
        provider.CanHandle("SnmpServerMemory").Should().BeTrue();
        provider.CanHandle("snmpservermemory").Should().BeTrue();
        provider.CanHandle("SnmpServerCpu").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportRamUsage()
    {
        // 1 GiB RAM (262144 × 4096 B) com 75% usada (196608 unidades).
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(_ =>
            Table(Row(1, "1", "4096", "262144", "196608", "Physical memory"))));

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["usedPercent"].Should().Be("75");
        result.Values["totalMB"].Should().Be("1024");
        result.Values["usedMB"].Should().Be("768");
        result.Values["availableMB"].Should().Be("256");
    }

    [Fact]
    public async Task MultipleRamRows_ShouldBeSummed()
    {
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "1", "1024", "1048576", "786432", "RAM A"), // 1 GiB / 768 MiB
                Row(2, "1", "1024", "1048576", "262144", "RAM B")))); // 1 GiB / 256 MiB

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeTrue();
        result.Values!["totalMB"].Should().Be("2048");
        result.Values["usedMB"].Should().Be("1024");
        result.Values["usedPercent"].Should().Be("50", "(768+256) MiB de 2048 MiB");
    }

    [Fact]
    public async Task SwapRow_ShouldReportSwapPercentInformatively()
    {
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "1", "1024", "1024", "256", "Physical memory"),
                Row(2, "2", "1024", "512", "512", "Swap space"))));

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeTrue();
        result.Values!["swapPercent"].Should().Be(
            "100", "swap é informativo no 1º lote — regra fica para prevenção");
    }

    [Fact]
    public async Task DiskRows_ShouldNotCountAsMemory()
    {
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "3", "512", "1048576", "524288", "/dev/sda1"),
                Row(2, "1", "1024", "1024", "512", "Physical memory"))));

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeTrue();
        result.Values!["totalMB"].Should().Be("1", "disco (type 3) não é memória");
    }

    [Fact]
    public async Task ZeroSizeOrGarbageRows_ShouldBeSkipped()
    {
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "1", "4096", "0", "0", "RAM inválida"),
                Row(2, "1", "4096", "1024", "lixo", "RAM lixo"),
                Row(3, "1", "1024", "1024", "512", "RAM boa"))));

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeTrue();
        result.Values!["usedPercent"].Should().Be("50");
    }

    [Fact]
    public async Task NoRamRows_ShouldFallbackToHrMemorySize()
    {
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(oid =>
            oid == MemorySizeOid
                ? [new($"{MemorySizeOid}.0", "2097152")] // 2 GiB em KB
                : Table(Row(1, "3", "512", "1048576", "100", "/dev/sda1"))));

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeTrue(
            "sem linha RAM a tabela ainda informa o total via hrMemorySize");
        result.Values!["totalMB"].Should().Be("2048");
        result.Values.Should().NotContainKey("usedPercent",
            "sem usado não há % — evaluator não avalia");
    }

    [Fact]
    public async Task NoRamAndNoMemorySize_ShouldFailWithPtBrError()
    {
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(_ => []));

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhuma memória física (hrStorageTable/hrMemorySize) encontrada no servidor.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingSnmp()
    {
        var snmp = new FakeSnmpClient(_ => Table(Row(1, "1", "1024", "1024", "512")));
        var provider = new SnmpServerMemoryProvider(snmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        snmp.AskedOids.Should().BeEmpty();
    }

    [Fact]
    public async Task SnmpThrows_ShouldFailWithPtBrWrappedError()
    {
        var provider = new SnmpServerMemoryProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
    }

    [Fact]
    public async Task ShouldWalkStorageTable()
    {
        var snmp = new FakeSnmpClient(_ => Table(Row(1, "1", "1024", "1024", "512")));
        var provider = new SnmpServerMemoryProvider(snmp);

        await provider.CheckAsync("192.0.2.21");

        snmp.AskedOids.Should().Contain(StorageTableOid,
            "a fonte é a hrStorageTable do HOST-RESOURCES-MIB (RFC 2790)");
    }

    [Fact]
    public async Task PercentRounding_ShouldUseAwayFromZero()
    {
        // 200/300 = 66,67% → 67% (AwayFromZero).
        var provider = new SnmpServerMemoryProvider(new FakeSnmpClient(_ =>
            Table(Row(1, "1", "1024", "300", "200", "Physical memory"))));

        var result = await provider.CheckAsync("192.0.2.21");

        result.Succeeded.Should().BeTrue();
        result.Values!["usedPercent"].Should().Be("67");
    }
}
