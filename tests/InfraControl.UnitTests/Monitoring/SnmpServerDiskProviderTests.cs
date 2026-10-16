using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T003c: SnmpServerDiskProvider — partições via hrStorageTable
/// (HOST-RESOURCES-MIB RFC 2790), filtrando hrStorageType fixed(3)/
/// removable(4)/flash(8) e descartando RAM(1)/swap(2). O pior disco é o
/// alertável; a lista completa vai em Values["disks"] (JSON) para a UI.
/// </summary>
public sealed class SnmpServerDiskProviderTests
{
    private const string StorageTableOid = "1.3.6.1.2.1.25.2.3.1";

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
        yield return new($"{StorageTableOid}.{ColDescr}.{index}", descr ?? "disk-{index}");
        yield return new($"{StorageTableOid}.{ColAlloc}.{index}", alloc);
        yield return new($"{StorageTableOid}.{ColSize}.{index}", size);
        yield return new($"{StorageTableOid}.{ColUsed}.{index}", used);
    }

    private static IReadOnlyList<SnmpVariable> Table(params IEnumerable<SnmpVariable>[] rows)
        => [.. rows.SelectMany(row => row)];

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(Row(1, "3", "1024", "1024", "512"))));

        provider.Name.Should().Be("SnmpServerDisk");
        provider.CanHandle("SnmpServerDisk").Should().BeTrue();
        provider.CanHandle("snmpserverdisk").Should().BeTrue();
        provider.CanHandle("SnmpServerMemory").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportSinglePartition()
    {
        // 100 MiB com 50 MiB usados.
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(Row(1, "3", "1024", "102400", "51200", "/"))));

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["diskCount"].Should().Be("1");
        result.Values["worstDisk"].Should().Be("/");
        result.Values["worstPercent"].Should().Be("50");
        result.Values["disks"].Should().Contain("\"name\":\"/\"")
            .And.Contain("\"usedPercent\":50")
            .And.Contain("\"totalMB\":100")
            .And.Contain("\"usedMB\":50");
    }

    [Fact]
    public async Task MultiplePartitions_ShouldReportWorstAsAlertable()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "3", "1024", "102400", "5120", "/var"),   //  5%
                Row(2, "3", "1024", "51200", "46080", "/"),      // 90%
                Row(3, "3", "1024", "10240", "1024", "/tmp")))); // 10%

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeTrue();
        result.Values!["diskCount"].Should().Be("3");
        result.Values["worstDisk"].Should().Be("/");
        result.Values["worstPercent"].Should().Be("90");
    }

    [Fact]
    public async Task DisksJson_ShouldRoundTripWithEveryPartition()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "3", "1024", "102400", "5120", "/var"),
                Row(2, "3", "1024", "51200", "46080", "/"))));

        var result = await provider.CheckAsync("192.0.2.22");

        var disks = JsonSerializer.Deserialize<JsonElement>(
            result.Values!["disks"]).EnumerateArray().ToArray();
        disks.Should().HaveCount(2, "a UI renderiza uma barra por partição");
        disks.Select(disk => disk.GetProperty("name").GetString())
            .Should().BeEquivalentTo(["/var", "/"]);
    }

    [Fact]
    public async Task RamAndSwapRows_ShouldBeIgnored()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "1", "4096", "262144", "200000", "Physical memory"),
                Row(2, "2", "1024", "512", "512", "Swap space"),
                Row(3, "3", "1024", "102400", "51200", "/"))));

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeTrue();
        result.Values!["diskCount"].Should().Be("1", "RAM/swap não são discos");
        result.Values["worstDisk"].Should().Be("/");
    }

    [Fact]
    public async Task RemovableAndFlashTypes_ShouldCountAsDisks()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "4", "1024", "10240", "1024", "/media/usb"),
                Row(2, "8", "1024", "20480", "4096", "/ssd"))));

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeTrue();
        result.Values!["diskCount"].Should().Be("2");
    }

    [Fact]
    public async Task NetworkOrUnknownTypes_ShouldBeSkipped()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "9", "1024", "1048576", "1048576", "//nas/share"),
                Row(2, "99", "1024", "1024", "10", "weird"))));

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeFalse(
            "rede/não-classificado não é partição local");
        result.Error.Should().Be(
            "Nenhuma partição de disco válida encontrada no servidor.");
    }

    [Fact]
    public async Task ZeroSizeOrGarbageRows_ShouldBeSkipped()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(
                Row(1, "3", "1024", "0", "0", "/vazio"),
                Row(2, "3", "1024", "1024", "lixo", "/lixo"),
                Row(3, "3", "1024", "102400", "51200", "/"))));

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeTrue();
        result.Values!["diskCount"].Should().Be("1");
    }

    [Fact]
    public async Task OnlyRamRows_ShouldFailWithPtBrError()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ =>
            Table(Row(1, "1", "4096", "262144", "131072", "Physical memory"))));

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhuma partição de disco válida encontrada no servidor.");
    }

    [Fact]
    public async Task EmptyWalk_ShouldFailWithPtBrError()
    {
        var provider = new SnmpServerDiskProvider(new FakeSnmpClient(_ => []));

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhuma partição de disco válida encontrada no servidor.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingSnmp()
    {
        var snmp = new FakeSnmpClient(_ => Table(Row(1, "3", "1024", "1024", "512")));
        var provider = new SnmpServerDiskProvider(snmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        snmp.AskedOids.Should().BeEmpty();
    }

    [Fact]
    public async Task SnmpThrows_ShouldFailWithPtBrWrappedError()
    {
        var provider = new SnmpServerDiskProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.22");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
    }

    [Fact]
    public async Task ShouldWalkStorageTable()
    {
        var snmp = new FakeSnmpClient(_ => Table(Row(1, "3", "1024", "1024", "512")));
        var provider = new SnmpServerDiskProvider(snmp);

        await provider.CheckAsync("192.0.2.22");

        snmp.AskedOids.Should().Contain(StorageTableOid,
            "a fonte é a hrStorageTable do HOST-RESOURCES-MIB (RFC 2790)");
    }
}
