using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T001: Service — criação/validação em pt-BR e limites de tamanho
/// (padrão Location).
/// </summary>
public sealed class ServiceTests
{
    [Fact]
    public void Create_ShouldTrimNameAndPersistDescription()
    {
        var service = Service.Create("  ERP Corporativo  ", "  Sistema crítico  ");

        service.Name.Should().Be("ERP Corporativo");
        service.Description.Should().Be("Sistema crítico");
        service.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithoutDescription_ShouldBeNull()
    {
        var service = Service.Create("Portal do Colaborador");

        service.Description.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => Service.Create("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome do serviço é obrigatório.*");
    }

    [Fact]
    public void Create_NameOver120_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => Service.Create(new string('x', 121));

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome do serviço pode ter no máximo 120 caracteres.*");
    }

    [Fact]
    public void Update_DescriptionOver500_ShouldThrowArgumentExceptionPtBr()
    {
        var service = Service.Create("Serviço válido");

        var act = () => service.Update("Serviço válido", new string('y', 501));

        act.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do serviço pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Update_ShouldReplaceNameAndClearBlankDescription()
    {
        var service = Service.Create("Nome antigo", "Descrição");

        service.Update("Nome novo", "   ");

        service.Name.Should().Be("Nome novo");
        service.Description.Should().BeNull();
    }
}
