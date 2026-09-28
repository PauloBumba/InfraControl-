using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T005: Slo — validações de alvo percentual, janela e serviço em
/// pt-BR (docs/04: Availability &gt;= 99.95%, Window = 30 days).
/// </summary>
public sealed class SloTests
{
    private static readonly Guid ServiceId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldPersistAlvoJanelaEService()
    {
        var slo = Slo.Create("  Disponibilidade do ERP  ", 99.95m, 30, ServiceId);

        slo.Name.Should().Be("Disponibilidade do ERP");
        slo.TargetPercent.Should().Be(99.95m);
        slo.WindowDays.Should().Be(30);
        slo.ServiceId.Should().Be(ServiceId);
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => Slo.Create("   ", 99.9m, 30, ServiceId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome do SLO é obrigatório.*");
    }

    [Fact]
    public void Create_NameOver120_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => Slo.Create(new string('x', 121), 99.9m, 30, ServiceId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome do SLO pode ter no máximo 120 caracteres.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Create_TargetPercentOutOfRange_ShouldThrowPtBr(decimal target)
    {
        var act = () => Slo.Create("SLO", target, 30, ServiceId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O alvo do SLO deve estar entre 0 (exclusivo) e 100.*");
    }

    [Fact]
    public void Create_WindowDaysNotPositive_ShouldThrowPtBr()
    {
        var act = () => Slo.Create("SLO", 99.9m, 0, ServiceId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("A janela do SLO deve ser maior que zero.*");
    }

    [Fact]
    public void Create_EmptyServiceId_ShouldThrowPtBr()
    {
        var act = () => Slo.Create("SLO", 99.9m, 30, Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O serviço do SLO é obrigatório.*");
    }

    [Fact]
    public void Update_ShouldReplaceAllFields()
    {
        var slo = Slo.Create("Antes", 99.9m, 30, ServiceId);
        var newService = Guid.NewGuid();

        slo.Update("Depois", 99.99m, 90, newService);

        slo.Name.Should().Be("Depois");
        slo.TargetPercent.Should().Be(99.99m);
        slo.WindowDays.Should().Be(90);
        slo.ServiceId.Should().Be(newService);
    }
}
