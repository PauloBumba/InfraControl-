using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F19-T006: ExternalDeviceReference — identidade externa estável do
/// device (MAC/Device ID), unicidade por (Source, ExternalId) e resolução
/// normalizada (trim + minúsculas). A resolução do Zabbix por IP/hostname
/// não é coberta aqui — permanece no fluxo de ingestão (regra 6).
/// </summary>
public sealed class ExternalDeviceReferenceTests
{
    [Fact]
    public void Create_ShouldMapSourceExternalIdAndDevice()
    {
        var deviceId = Guid.NewGuid();

        var reference = ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, "F4:92:BF:00:11:22", deviceId);

        reference.Source.Should().Be(ExternalDeviceSource.UniFi);
        reference.ExternalId.Should().Be("f4:92:bf:00:11:22");
        reference.DeviceId.Should().Be(deviceId);
        reference.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("  F4:92:BF:00:11:22  ", "f4:92:bf:00:11:22")]
    [InlineData("ABCD-1234", "abcd-1234")]
    [InlineData("001", "001")]
    public void Create_ShouldNormalizeExternalIdForLookup(
        string raw, string normalized)
    {
        var reference = ExternalDeviceReference.Create(
            ExternalDeviceSource.Zabbix, raw, Guid.NewGuid());

        reference.ExternalId.Should().Be(normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutExternalId_ShouldThrowPtBr(string? externalId)
    {
        var action = () => ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, externalId!, Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O identificador externo é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutDeviceId_ShouldThrowPtBr()
    {
        var action = () => ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, "aa:bb:cc:dd:ee:ff", Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do device é obrigatório.*");
    }

    [Fact]
    public void Create_WithTooLongExternalId_ShouldThrowPtBr()
    {
        var action = () => ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, new string('a', 129), Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O identificador externo pode ter no máximo 128 caracteres.*");
    }

    [Fact]
    public void NormalizeExternalId_ShouldBeIdempotent()
    {
        var once = ExternalDeviceReference.NormalizeExternalId("  F4:92:BF  ");
        var twice = ExternalDeviceReference.NormalizeExternalId(once);

        twice.Should().Be(once).And.Be("f4:92:bf");
    }
}
