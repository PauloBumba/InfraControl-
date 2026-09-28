using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T007: SloBudget — validações de percentual (0–100), SLO e período.
/// </summary>
public sealed class SloBudgetTests
{
    private static readonly Guid SloId = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldPersistPeriodoConsumoESlo()
    {
        var budget = SloBudget.Create(SloId, Start, Start.AddDays(30), 42.5m);

        budget.SloId.Should().Be(SloId);
        budget.PeriodStart.Should().Be(Start);
        budget.PeriodEnd.Should().Be(Start.AddDays(30));
        budget.ConsumedPercent.Should().Be(42.5m);
    }

    [Fact]
    public void Create_NullPeriodEnd_ShouldMeanOpenPeriod()
    {
        var budget = SloBudget.Create(SloId, Start, null, 10m);

        budget.PeriodEnd.Should().BeNull();
    }

    [Fact]
    public void Create_EmptySloId_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => SloBudget.Create(
            Guid.Empty, Start, null, 10m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do SLO é obrigatório.*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Create_ConsumedOutOfRange_ShouldThrowPtBr(decimal value)
    {
        var act = () => SloBudget.Create(SloId, Start, null, value);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O percentual consumido deve estar entre 0 e 100.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_ConsumedOnBoundaries_ShouldSucceed(decimal value)
    {
        var budget = SloBudget.Create(SloId, Start, null, value);

        budget.ConsumedPercent.Should().Be(value);
    }

    [Fact]
    public void Create_PeriodEndNotAfterStart_ShouldThrowPtBr()
    {
        var act = () => SloBudget.Create(SloId, Start, Start, 10m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O fim do período deve ser posterior ao início.*");
    }
}
