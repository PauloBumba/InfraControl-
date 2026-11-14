using FluentAssertions;
using InfraControl.Application.Monitoring.Results;
using InfraControl.Application.Monitoring.Results.Ports;
using Moq;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F54-T001: série de disponibilidade dos checks internos — células de
/// 5 min do repositório fundidas nos buckets pedidos, com buckets vazios
/// preenchidos com zero (continuidade do gráfico).
/// </summary>
public sealed class MonitoringAvailabilityQueryTests
{
    private static readonly DateTime From = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddHours(1);

    [Fact]
    public async Task MergesCellsIntoRequestedBuckets()
    {
        // 2 células de 5 min dentro do 1º bucket de 30 min; resto vazio.
        var cells = new[]
        {
            Cell(From, total: 4, succeeded: 3),
            Cell(From.AddMinutes(5), total: 4, succeeded: 4),
        };

        var result = await Handle(cells, From, To, bucketMinutes: 30);

        result.BucketMinutes.Should().Be(30);
        result.Points.Should().HaveCount(2);
        result.Points[0].Total.Should().Be(8);
        result.Points[0].Succeeded.Should().Be(7);
        result.Points[0].Failed.Should().Be(1);
        result.Points[1].Total.Should().Be(0);
        result.Points[0].BucketStartUtc.Should().Be(From);
        result.Points[1].BucketStartUtc.Should().Be(From.AddMinutes(30));
    }

    [Fact]
    public async Task AutoBucket_TargetsAbout80Points()
    {
        // 24h → bucket ~18min → arredonda p/ 20.
        var result = await Handle([], To.AddHours(-24), To, bucketMinutes: null);

        result.BucketMinutes.Should().Be(20);
        result.Points.Should().HaveCount(72); // 1440/20
    }

    [Fact]
    public async Task InvalidBucket_Throws()
    {
        var act = async () => await Handle([], From, To, bucketMinutes: 7);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TooManyPoints_Throws()
    {
        // 7 dias com bucket de 5 min = 2016 pontos > 2000.
        var act = async () => await Handle(
            [], To.AddDays(-7), To, bucketMinutes: 5);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RangeOver92Days_Throws()
    {
        var act = async () => await Handle(
            [], To.AddDays(-100), To, bucketMinutes: 1440);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InvertedRange_Throws()
    {
        var act = async () => await Handle([], To, From, bucketMinutes: 30);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static async Task<MonitoringAvailabilityResultDto> Handle(
        IReadOnlyList<MonitoringAvailabilityCell> cells,
        DateTime fromUtc,
        DateTime toUtc,
        int? bucketMinutes)
    {
        var repository = new Mock<IMonitoringResultRepository>();
        repository.Setup(r => r.GetAvailabilityCellsAsync(
                fromUtc, toUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cells);

        var handler = new GetMonitoringAvailabilityQueryHandler(repository.Object);
        return await handler.Handle(
            new GetMonitoringAvailabilityQuery(fromUtc, toUtc, bucketMinutes),
            CancellationToken.None);
    }

    private static MonitoringAvailabilityCell Cell(
        DateTime start, int total, int succeeded)
        => new(start.Year, start.Month, start.Day, start.Hour,
            start.Minute / 5, total, succeeded);
}
