using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T002: SlaPolicy — validações de nome e prazos em pt-BR
/// (compromisso de resposta/resolução — docs/04).
/// </summary>
public sealed class SlaPolicyTests
{
    [Fact]
    public void Create_ShouldPersistPrazosAndTrimName()
    {
        var policy = SlaPolicy.Create("  Padrão ERP  ", 30, 480);

        policy.Name.Should().Be("Padrão ERP");
        policy.ResponseMinutes.Should().Be(30);
        policy.ResolutionMinutes.Should().Be(480);
        policy.ServiceId.Should().BeNull("política global por padrão");
    }

    [Fact]
    public void Create_WithServiceId_ShouldBindToService()
    {
        var serviceId = Guid.NewGuid();

        var policy = SlaPolicy.Create("Do serviço", 15, 240, serviceId);

        policy.ServiceId.Should().Be(serviceId);
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => SlaPolicy.Create("   ", 30, 480);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome da política de SLA é obrigatório.*");
    }

    [Fact]
    public void Create_NameOver120_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => SlaPolicy.Create(new string('x', 121), 30, 480);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome da política de SLA pode ter no máximo 120 caracteres.*");
    }

    [Theory]
    [InlineData(0, 480)]
    [InlineData(-5, 480)]
    public void Create_ResponseMinutesNotPositive_ShouldThrowPtBr(
        int responseMinutes, int resolutionMinutes)
    {
        var act = () => SlaPolicy.Create("Política", responseMinutes, resolutionMinutes);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O prazo de resposta deve ser maior que zero.*");
    }

    [Theory]
    [InlineData(30, 0)]
    [InlineData(30, -1)]
    public void Create_ResolutionMinutesNotPositive_ShouldThrowPtBr(
        int responseMinutes, int resolutionMinutes)
    {
        var act = () => SlaPolicy.Create("Política", responseMinutes, resolutionMinutes);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O prazo de resolução deve ser maior que zero.*");
    }

    [Fact]
    public void Update_ShouldReplacePrazosAndService()
    {
        var policy = SlaPolicy.Create("Antes", 30, 480);
        var serviceId = Guid.NewGuid();

        policy.Update("Depois", 60, 720, serviceId);

        policy.Name.Should().Be("Depois");
        policy.ResponseMinutes.Should().Be(60);
        policy.ResolutionMinutes.Should().Be(720);
        policy.ServiceId.Should().Be(serviceId);
    }
}
