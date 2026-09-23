using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T001: invariantes da entidade Location — mensagens nascem em pt-BR
/// na camada de domínio (regra confirmada na F0-T023).
/// </summary>
public sealed class LocationTests
{
    [Fact]
    public void Create_ShouldTrimNameAndDescription()
    {
        var location = Location.Create("  Data Center A  ", "  Principal  ");

        location.Name.Should().Be("Data Center A");
        location.Description.Should().Be("Principal");
    }

    [Fact]
    public void Create_WithBlankDescription_ShouldStoreNull()
    {
        var location = Location.Create("Sala 1", "   ");

        location.Description.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldGenerateIdAndTimestamps()
    {
        var location = Location.Create("Sala 1");

        location.Id.Should().NotBe(Guid.Empty);
        location.CreateDateTime.Should().NotBe(default(DateTime));
        location.UpdateDateTime.Should().NotBe(default(DateTime));
    }

    [Fact]
    public void Create_WithoutName_ShouldThrowPtBrMessage()
    {
        var action = () => Location.Create("   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do local é obrigatório.*");
    }

    [Fact]
    public void Create_WithNullName_ShouldThrowPtBrMessage()
    {
        var action = () => Location.Create(null!);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do local é obrigatório.*");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowPtBrMessage()
    {
        var action = () => Location.Create(new string('a', Location.NameMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do local pode ter no máximo 120 caracteres.*");
    }

    [Fact]
    public void Create_WithTooLongDescription_ShouldThrowPtBrMessage()
    {
        var action = () => Location.Create("Sala", new string('a', Location.DescriptionMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do local pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Update_ShouldApplyNewNameAndDescription()
    {
        var location = Location.Create("Sala 1");

        location.Update("  Sala 2  ", "  Reformulada  ");

        location.Name.Should().Be("Sala 2");
        location.Description.Should().Be("Reformulada");
    }

    [Fact]
    public void Update_WithoutName_ShouldThrowAndKeepState()
    {
        var location = Location.Create("Sala 1");

        var action = () => location.Update("  ", null);

        action.Should().Throw<ArgumentException>();
        location.Name.Should().Be("Sala 1", "falha de validação não pode alterar o agregado");
    }
}
