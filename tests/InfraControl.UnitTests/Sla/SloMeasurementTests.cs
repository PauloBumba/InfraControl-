using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T006: SloMeasurement — validações de valor medido (0–100) e SLO.
/// </summary>
public sealed class SloMeasurementTests
{
    private static readonly Guid SloId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldPersistValorESlo()
    {
        var measuredAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var measurement = SloMeasurement.Create(SloId, measuredAt, 99.87m);

        measurement.SloId.Should().Be(SloId);
        measurement.MeasuredAt.Should().Be(measuredAt);
        measurement.ValuePercent.Should().Be(99.87m);
    }

    [Fact]
    public void Create_EmptySloId_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => SloMeasurement.Create(
            Guid.Empty, DateTime.UtcNow, 99.9m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do SLO é obrigatório.*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Create_ValueOutOfRange_ShouldThrowPtBr(decimal value)
    {
        var act = () => SloMeasurement.Create(SloId, DateTime.UtcNow, value);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O valor medido deve estar entre 0 e 100.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_ValueOnBoundaries_ShouldSucceed(decimal value)
    {
        var measurement = SloMeasurement.Create(SloId, DateTime.UtcNow, value);

        measurement.ValuePercent.Should().Be(value);
    }
}
