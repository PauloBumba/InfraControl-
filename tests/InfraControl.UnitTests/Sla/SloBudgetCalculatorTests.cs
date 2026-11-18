using FluentAssertions;
using InfraControl.Application.Sla.Slos.Bindings;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// ADR-069: consumo linear do error budget — indisponibilidade
/// observada ÷ permitida pelo alvo, limitado a 100.
/// </summary>
public sealed class SloBudgetCalculatorTests
{
    [Fact]
    public void ComputeConsumedPercent_OnTarget_ShouldReturnZero()
    {
        // 100% de disponibilidade → zero consumo, qualquer alvo.
        SloBudgetCalculator.ComputeConsumedPercent(100m, 99.9m).Should().Be(0m);
    }

    [Fact]
    public void ComputeConsumedPercent_HalfBudget_ShouldReturn50()
    {
        // Alvo 99.9 (budget 0.1%), disponibilidade 99.95 (falha 0.05%)
        // → consumiu metade do budget.
        SloBudgetCalculator.ComputeConsumedPercent(99.95m, 99.9m).Should().Be(50m);
    }

    [Fact]
    public void ComputeConsumedPercent_BudgetExceeded_ShouldCapAt100()
    {
        // Smoke real: disponibilidade 48.769 vs alvo 99.5 → ~10246% → 100.
        SloBudgetCalculator.ComputeConsumedPercent(48.769m, 99.5m).Should().Be(100m);
    }

    [Fact]
    public void ComputeConsumedPercent_Target100_ShouldConsumeAllOnAnyFailure()
    {
        // Alvo 100% = budget zero: qualquer falha esgota; 100% avail = 0.
        SloBudgetCalculator.ComputeConsumedPercent(99.999m, 100m).Should().Be(100m);
        SloBudgetCalculator.ComputeConsumedPercent(100m, 100m).Should().Be(0m);
    }

    [Fact]
    public void ComputeConsumedPercent_BelowTarget_ShouldStayUnder100()
    {
        // 99.0% vs alvo 99.5 → falha 1.0 / budget 0.5 = 200% → cap 100.
        SloBudgetCalculator.ComputeConsumedPercent(99.0m, 99.5m).Should().Be(100m);
        // 99.8% vs alvo 99.5 → falha 0.2 / budget 0.5 = 40%.
        SloBudgetCalculator.ComputeConsumedPercent(99.8m, 99.5m).Should().Be(40m);
    }
}
