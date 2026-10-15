using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;
using Moq;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T001 (docs/42 §C L166-177): SnmpHealthMonitoringProvider — saúde de
/// switch (CPU/memória/temperatura) escolhendo os OIDs por <c>Device.Vendor</c>,
/// via a MESMA seam <see cref="ISnmpClient"/> dos demais providers (decisão
/// registrada: não criar <c>ISnmpHealthProvider</c> — a ficha 42 é a única das
/// 8 da F30 que propõe uma seam nova).
/// REGRA: só afirma o que o device respondeu — métrica ausente = chave
/// ausente, nunca 0; vendor desconhecido = falha honesta.
/// </summary>
public sealed class SnmpHealthProviderTests
{
    // OIDs docs/42 §C (L170-174) — UniFi
    private const string UnifiCpuOid = "1.3.6.1.4.1.10002.1.1.1.4.2.1.3.1";
    private const string UnifiMemoryTotalOid = "1.3.6.1.4.1.10002.1.1.1.1.1.0";
    private const string UnifiMemoryFreeOid = "1.3.6.1.4.1.10002.1.1.1.1.2.0";
    private const string UnifiTemperatureOid = "1.3.6.1.4.1.4413.1.1.43.1.8.1.5.1.0";

    // OIDs docs/42 §C (L172-174) — Intelbras (2 linhas: OLT e Série 3000)
    private const string IntelbrasCpuOid = "1.3.6.1.4.1.13464.1.14.2.1.3.1.0";
    private const string IntelbrasCpuSerie3000Oid = "1.3.6.1.4.1.26138.3.600.9.1.1.3.33.1";
    private const string IntelbrasMemoryOid = "1.3.6.1.4.1.13464.1.14.2.1.3.2.0";
    private const string IntelbrasMemorySerie3000Oid = "1.3.6.1.4.1.26138.3.600.10.1.1.10.33";
    private const string IntelbrasTemperatureOid = "1.3.6.1.4.1.13464.1.14.2.1.3.3.0";

    // OIDs docs/42 §C (L172-174) — HP (HPE iLO/switch gerenciável)
    private const string HpCpuOid = "1.3.6.1.4.1.11.2.14.11.5.1.9.6.1.0";
    private const string HpMemoryOid = "1.3.6.1.4.1.11.2.14.11.5.1.1.2.1.1.1.6.1";
    private const string HpTemperatureOid = "1.3.6.1.4.1.11.2.14.11.1.2.8.1.1.9.0";

    // OIDs docs/42 §C (L172-174) — Aruba (ARUBAWIRED-SYSTEMINFO-MIB)
    private const string ArubaCpuInstantOid = "1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.3";
    private const string ArubaCpu1mOid = "1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.10";
    private const string ArubaMemoryOid = "1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.4";

    // OIDs docs/42 §C (L172-174) — TP-Link (TPLINK-SYSMONITOR)
    private const string TpLinkCpu5sOid = "1.3.6.1.4.1.11863.6.4.1.1.1.1.2";
    private const string TpLinkCpu1mOid = "1.3.6.1.4.1.11863.6.4.1.1.1.1.3";
    private const string TpLinkMemoryOid = "1.3.6.1.4.1.11863.6.4.1.2.1.1.2";
    private const string TpLinkTemperatureOid = "1.3.6.1.4.1.11863.6.4.1.3";

    private const string Target = "192.0.2.10";

    [Fact]
    public void Provider_NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpHealthMonitoringProvider(
            new FakeSnmpClient(_ => []), RepositoryFor(DeviceWith("Ubiquiti")));

        provider.Name.Should().Be("SnmpHealth");
        provider.CanHandle("SnmpHealth").Should().BeTrue();
        provider.CanHandle("snmphealth").Should().BeTrue();
        provider.CanHandle("SnmpSystem").Should().BeFalse();
    }

    [Fact]
    public async Task Unifi_ShouldReadCpuMemoryAndTemperature()
    {
        var client = new FakeSnmpClient(oid => oid switch
        {
            UnifiCpuOid => [new SnmpVariable(UnifiCpuOid, "12")],
            UnifiMemoryTotalOid => [new SnmpVariable(UnifiMemoryTotalOid, "1000")],
            UnifiMemoryFreeOid => [new SnmpVariable(UnifiMemoryFreeOid, "355")],
            UnifiTemperatureOid => [new SnmpVariable(UnifiTemperatureOid, "41")],
            _ => []
        });
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("Ubiquiti")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();

        var values = result.Values!;
        values["cpuPercent"].Should().Be("12");
        // 1000 total - 355 livre => 64.5% em uso (invariante: nunca "64,5")
        values["memoryPercent"].Should().Be("64.5");
        values["temperatureCelsius"].Should().Be("41");
    }

    [Fact]
    public async Task Unifi_ShouldUseUnifiOidsOnly()
    {
        var client = new FakeSnmpClient(oid => oid == UnifiCpuOid
            ? [new SnmpVariable(UnifiCpuOid, "12")]
            : []);
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("UniFi")));

        await provider.CheckAsync(Target);

        client.WalkedOids.Should().Contain(UnifiCpuOid);
        client.WalkedOids.Should().NotContain(IntelbrasCpuOid);
    }

    [Fact]
    public async Task Intelbras_ShouldUseIntelbrasOids_NotTheUnifiOnes()
    {
        var client = new FakeSnmpClient(oid => oid switch
        {
            IntelbrasCpuOid => [new SnmpVariable(IntelbrasCpuOid, "33")],
            IntelbrasMemoryOid => [new SnmpVariable(IntelbrasMemoryOid, "25")],
            IntelbrasTemperatureOid => [new SnmpVariable(IntelbrasTemperatureOid, "47")],
            _ => []
        });
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("Intelbras")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values["cpuPercent"].Should().Be("33");
        values["memoryPercent"].Should().Be("25");
        values["temperatureCelsius"].Should().Be("47");

        client.WalkedOids.Should().NotContain(UnifiCpuOid);
        client.WalkedOids.Should().NotContain(UnifiMemoryTotalOid);
    }

    [Fact]
    public async Task Intelbras_Serie3000_ShouldBeUsedWhenOltOidDoesNotAnswer()
    {
        var client = new FakeSnmpClient(oid => oid switch
        {
            IntelbrasCpuSerie3000Oid => [new SnmpVariable(IntelbrasCpuSerie3000Oid, "18")],
            IntelbrasMemorySerie3000Oid => [new SnmpVariable(IntelbrasMemorySerie3000Oid, "51")],
            _ => []
        });
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("intelbras")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values["cpuPercent"].Should().Be("18");
        values["memoryPercent"].Should().Be("51");
    }

    [Fact]
    public async Task MissingTemperature_ShouldOmitTheKey_NeverZero()
    {
        var client = new FakeSnmpClient(oid => oid switch
        {
            UnifiCpuOid => [new SnmpVariable(UnifiCpuOid, "12")],
            UnifiMemoryTotalOid => [new SnmpVariable(UnifiMemoryTotalOid, "1000")],
            UnifiMemoryFreeOid => [new SnmpVariable(UnifiMemoryFreeOid, "400")],
            _ => []
        });
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("Ubiquiti")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values.Should().ContainKey("cpuPercent");
        values["memoryPercent"].Should().Be("60");
        values.Should().NotContainKey(
            "temperatureCelsius", "o device não respondeu o sensor — nunca 0");
    }

    [Fact]
    public async Task PartialMemoryPair_ShouldOmitMemory_NeverZero()
    {
        var client = new FakeSnmpClient(oid => oid == UnifiCpuOid
            ? [new SnmpVariable(UnifiCpuOid, "12")]
            : []);
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("Ubiquiti")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values.Should().ContainKey("cpuPercent");
        values.Should().NotContainKey("memoryPercent", "sem total não há % — nunca 0");
        values.Should().NotContainKey("temperatureCelsius");
    }

    [Fact]
    public async Task UnknownVendor_ShouldFailWithoutValues()
    {
        var client = new FakeSnmpClient(_ => [new SnmpVariable(UnifiCpuOid, "12")]);
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("QualquerMarca")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeFalse();
        result.Values.Should().BeNull("nada foi lido, então nada pode ser reportado");
        result.Error.Should().Contain("QualquerMarca");
        client.WalkedOids.Should().BeEmpty("sem vendor conhecido não há OIDs para consultar");
    }

    [Fact]
    public async Task DeviceWithoutVendor_ShouldFailWithClearError()
    {
        var provider = new SnmpHealthMonitoringProvider(
            new FakeSnmpClient(_ => []), RepositoryFor(DeviceWith(null)));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeFalse();
        result.Values.Should().BeNull();
        result.Error.Should().Contain("fabricante");
    }

    [Fact]
    public async Task TargetNotRegistered_ShouldFailWithClearError()
    {
        var provider = new SnmpHealthMonitoringProvider(
            new FakeSnmpClient(_ => []), RepositoryFor(null));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeFalse();
        result.Values.Should().BeNull();
        result.Error.Should().Contain(Target);
        result.Error.Should().Contain("cadastrado");
    }

    [Fact]
    public async Task RepositoryFailure_ShouldNotClaimNotRegistered()
    {
        var repository = new Mock<IDeviceRepository>();
        repository
            .Setup(r => r.GetByIpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("banco fora do ar"));
        var provider = new SnmpHealthMonitoringProvider(
            new FakeSnmpClient(_ => []), repository.Object);

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeFalse();
        result.Values.Should().BeNull();
        // Distingue "não existe no cadastro" de "não consegui consultar".
        result.Error.Should().Contain("banco fora do ar");
        result.Error.Should().NotContain("cadastrado");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutTouchingTheRepository()
    {
        var repository = new Mock<IDeviceRepository>();
        var provider = new SnmpHealthMonitoringProvider(
            new FakeSnmpClient(_ => []), repository.Object);

        var result = await provider.CheckAsync("   ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("obrigatório");
        repository.Verify(
            r => r.GetByIpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AllOidsSilent_ShouldFailHonest()
    {
        var provider = new SnmpHealthMonitoringProvider(
            new FakeSnmpClient(_ => []), RepositoryFor(DeviceWith("Ubiquiti")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeFalse();
        result.Values.Should().BeNull();
        result.Error.Should().Contain("nenhuma");
    }

    [Fact]
    public async Task Hp_ShouldUseHpOids_AndNotTheOtherVendors()
    {
        var client = new FakeSnmpClient(oid => oid switch
        {
            HpCpuOid => [new SnmpVariable(HpCpuOid, "7")],
            HpMemoryOid => [new SnmpVariable(HpMemoryOid, "88")],
            HpTemperatureOid => [new SnmpVariable(HpTemperatureOid, "34")],
            _ => []
        });
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("HP")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values["cpuPercent"].Should().Be("7");
        values["memoryPercent"].Should().Be("88");
        values["temperatureCelsius"].Should().Be("34");

        client.WalkedOids.Should().Contain(HpCpuOid);
        client.WalkedOids.Should().NotContain(UnifiCpuOid);
        client.WalkedOids.Should().NotContain(IntelbrasCpuOid);
    }

    [Theory]
    [InlineData("HP")]
    [InlineData("HPE")]
    [InlineData("Hewlett-Packard")]
    [InlineData("ProCurve 2920-48G")]
    public async Task HpNamesInTheRealWorld_ShouldAllResolveToHpOids(string vendor)
    {
        var client = new FakeSnmpClient(oid => oid == HpCpuOid
            ? [new SnmpVariable(HpCpuOid, "7")]
            : []);
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith(vendor)));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue(
            "'{0}' é o mesmo fabricante e precisa cair nos OIDs de HP", vendor);
        result.Values!["cpuPercent"].Should().Be("7");
        client.WalkedOids.Should().Contain(HpCpuOid);
    }

    [Fact]
    public async Task Aruba_ShouldUseArubaOids_AndReportNoTemperature()
    {
        var client = new FakeSnmpClient(oid => oid switch
        {
            ArubaCpuInstantOid => [new SnmpVariable(ArubaCpuInstantOid, "23")],
            ArubaMemoryOid => [new SnmpVariable(ArubaMemoryOid, "20")],
            _ => []
        });
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("Aruba")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values["cpuPercent"].Should().Be("23");
        values["memoryPercent"].Should().Be("20");
        // docs/42 L174: Aruba não tem OID de temperatura na tabela
        // ("via MIBs de hardware") — a chave não existe, nunca 0.
        values.Should().NotContainKey("temperatureCelsius");
    }

    [Fact]
    public async Task Aruba_ShouldFallBackToTheAveragedCpu_WhenInstantIsSilent()
    {
        var client = new FakeSnmpClient(oid => oid == ArubaCpu1mOid
            ? [new SnmpVariable(ArubaCpu1mOid, "26")]
            : []);
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("Aruba Networks")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        result.Values!["cpuPercent"].Should().Be("26");
        client.WalkedOids.Should().Contain(ArubaCpuInstantOid, "primeiro candidato, silencioso");
        client.WalkedOids.Should().Contain(ArubaCpu1mOid, "segundo candidato responde");
    }

    [Fact]
    public async Task TpLink_ShouldUseTpLinkOids_AndRejectOctetStringTemperature()
    {
        // docs/42 L174: a temperatura TP-Link vem como OctetString — sem
        // decodificação documentada não se inventa número.
        var client = new FakeSnmpClient(oid => oid switch
        {
            TpLinkCpu5sOid => [new SnmpVariable(TpLinkCpu5sOid, "15")],
            TpLinkMemoryOid => [new SnmpVariable(TpLinkMemoryOid, "40")],
            TpLinkTemperatureOid => [new SnmpVariable(TpLinkTemperatureOid, "\u0000\u0019\u0000")],
            _ => []
        });
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith("TP-Link")));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values["cpuPercent"].Should().Be("15");
        values["memoryPercent"].Should().Be("40");
        values.Should().NotContainKey(
            "temperatureCelsius", "OctetString não é número — omitir em vez de inventar");
    }

    [Theory]
    [InlineData("Cisco")]
    [InlineData("Dell")]
    [InlineData("MikroTik")]
    [InlineData("Zyxel")]
    public async Task UnsupportedVendor_ShouldNotGuessOidsAndShouldNameTheSupportedOnes(string vendor)
    {
        var client = new FakeSnmpClient(_ => [new SnmpVariable(HpCpuOid, "7")]);
        var provider = new SnmpHealthMonitoringProvider(client, RepositoryFor(DeviceWith(vendor)));

        var result = await provider.CheckAsync(Target);

        result.Succeeded.Should().BeFalse();
        result.Values.Should().BeNull();
        client.WalkedOids.Should().BeEmpty("sem vendor conhecido não há OIDs para consultar");
        result.Error.Should().Contain(vendor, "a mensagem precisa dizer qual vendor recusou");
        result.Error.Should().Contain("Aruba", "e listar os que são suportados");
    }

    private static Device DeviceWith(string? vendor)
        => Device.Create(Guid.NewGuid(), "Switch-Corredor", DeviceType.Switch,
            ip: Target, vendor: vendor);

    private static IDeviceRepository RepositoryFor(Device? device)
    {
        var repository = new Mock<IDeviceRepository>();
        repository
            .Setup(r => r.GetByIpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        return repository.Object;
    }

    private sealed class FakeSnmpClient(
        Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public List<string> WalkedOids { get; } = [];

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            WalkedOids.Add(oid);
            return Task.FromResult(walkFactory(oid));
        }
    }
}
