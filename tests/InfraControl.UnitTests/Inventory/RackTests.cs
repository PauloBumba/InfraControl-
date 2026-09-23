using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T002: invariantes da entidade Rack — mensagens nascem em pt-BR no domínio.
/// </summary>
public sealed class RackTests
{
    private static readonly Guid SomeLocationId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetLocationAndTrimNameAndDescription()
    {
        var rack = Rack.Create(SomeLocationId, "  R01  ", "  Coluna frontal  ");

        rack.LocationId.Should().Be(SomeLocationId);
        rack.Name.Should().Be("R01");
        rack.Description.Should().Be("Coluna frontal");
    }

    [Fact]
    public void Create_WithBlankDescription_ShouldStoreNull()
    {
        var rack = Rack.Create(SomeLocationId, "R01", "   ");

        rack.Description.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldGenerateIdAndTimestamps()
    {
        var rack = Rack.Create(SomeLocationId, "R01");

        rack.Id.Should().NotBe(Guid.Empty);
        rack.CreateDateTime.Should().NotBe(default(DateTime));
        rack.UpdateDateTime.Should().NotBe(default(DateTime));
    }

    [Fact]
    public void Create_WithoutLocationId_ShouldThrowPtBrMessage()
    {
        var action = () => Rack.Create(Guid.Empty, "R01");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do local é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutName_ShouldThrowPtBrMessage()
    {
        var action = () => Rack.Create(SomeLocationId, "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do rack é obrigatório.*");
    }

    [Fact]
    public void Create_WithNullName_ShouldThrowPtBrMessage()
    {
        var action = () => Rack.Create(SomeLocationId, null!);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do rack é obrigatório.*");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowPtBrMessage()
    {
        var action = () => Rack.Create(SomeLocationId, new string('a', Rack.NameMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do rack pode ter no máximo 60 caracteres.*");
    }

    [Fact]
    public void Create_WithTooLongDescription_ShouldThrowPtBrMessage()
    {
        var action = () => Rack.Create(SomeLocationId, "R01", new string('a', Rack.DescriptionMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do rack pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Update_ShouldApplyNewNameAndDescription()
    {
        var rack = Rack.Create(SomeLocationId, "R01");

        rack.Update("  R02  ", "  Nova  ");

        rack.Name.Should().Be("R02");
        rack.Description.Should().Be("Nova");
    }

    [Fact]
    public void Update_WithoutName_ShouldThrowAndKeepState()
    {
        var rack = Rack.Create(SomeLocationId, "R01");

        var action = () => rack.Update("  ", null);

        action.Should().Throw<ArgumentException>();
        rack.Name.Should().Be("R01", "falha de validação não pode alterar o agregado");
    }

    [Fact]
    public void Update_ShouldNotChangeLocation()
    {
        var rack = Rack.Create(SomeLocationId, "R01");

        rack.Update("R02", null);

        rack.LocationId.Should().Be(SomeLocationId, "mudança de local não é requisito da F2-T002");
    }
}
