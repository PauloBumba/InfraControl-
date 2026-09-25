using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T006: invariantes da entidade IpAddress — NetworkInterface 1:N IpAddress
/// (docs/03), IP único global, normalização canônica pt-BR. ValueObject órfão
/// Domain/ValueObjects/IpAddress.cs permanece fora de escopo.
/// </summary>
public sealed class IpAddressTests
{
    private static readonly Guid SomeInterfaceId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetInterfaceAndCanonicalIp()
    {
        var entity = IpAddress.Create(SomeInterfaceId, "  192.0.2.10  ");

        entity.InterfaceId.Should().Be(SomeInterfaceId);
        entity.Ip.Should().Be("192.0.2.10");
    }

    [Fact]
    public void Create_WithUppercaseIpv6_ShouldCanonicalizeToLowercase()
    {
        var entity = IpAddress.Create(SomeInterfaceId, "2001:0DB8:0000::1");

        entity.Ip.Should().Be("2001:db8::1");
    }

    [Fact]
    public void Create_ShouldGenerateIdAndTimestamps()
    {
        var entity = IpAddress.Create(SomeInterfaceId, "192.0.2.10");

        entity.Id.Should().NotBe(Guid.Empty);
        entity.CreateDateTime.Should().NotBe(default(DateTime));
        entity.UpdateDateTime.Should().NotBe(default(DateTime));
    }

    [Fact]
    public void Create_WithoutInterfaceId_ShouldThrowPtBrMessage()
    {
        var action = () => IpAddress.Create(Guid.Empty, "192.0.2.10");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da interface é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutIp_ShouldThrowPtBrMessage()
    {
        var action = () => IpAddress.Create(SomeInterfaceId, "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O IP é obrigatório.*");
    }

    [Fact]
    public void Create_WithInvalidIp_ShouldThrowPtBrMessage()
    {
        var action = () => IpAddress.Create(SomeInterfaceId, "banana");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O IP informado é inválido.*");
    }

    [Fact]
    public void Update_ShouldApplyCanonicalIp()
    {
        var entity = IpAddress.Create(SomeInterfaceId, "192.0.2.10");

        entity.Update("198.51.100.7");

        entity.Ip.Should().Be("198.51.100.7");
    }

    [Fact]
    public void Update_WithInvalidIp_ShouldThrowAndKeepState()
    {
        var entity = IpAddress.Create(SomeInterfaceId, "192.0.2.10");

        var action = () => entity.Update("not-an-ip");

        action.Should().Throw<ArgumentException>();
        entity.Ip.Should().Be("192.0.2.10", "falha de validação não pode alterar o agregado");
    }

    [Fact]
    public void Create_WithGarbageNumber_ShouldThrowPtBrMessage()
    {
        // IPAddress.TryParse aceita "1" como 0.0.0.1 — o domínio exige família v4/v6
        // textual completa? Não: aceita qualquer forma que o .NET canonicize em
        // InterNetwork/InterNetworkV6. "banana" cobre o caso rejeitado.
        var action = () => IpAddress.Create(SomeInterfaceId, "999.999.999.999");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O IP informado é inválido.*");
    }
}
