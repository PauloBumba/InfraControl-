using FluentAssertions;
using InfraControl.Application.Dashboard.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Dashboard;

/// <summary>
/// F54-T003 (docs/69): séries do dashboard — período inválido → 400 pt-BR,
/// buckets alinhados/zerados, agregação de polls/alertas/incidentes na
/// janela [from, to) excluindo linhas fora do período.
/// </summary>
public sealed class DashboardSeriesQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DashboardSeriesQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, GetDashboardSeriesQueryHandler Handler)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return (context, new GetDashboardSeriesQueryHandler(
            new MonitoringResultRepository(context),
            new AlertRepository(context),
            new IncidentRepository(context)));
    }

    [Fact]
    public async Task Series_WithInvalidPeriod_ShouldThrowArgumentErrorPtBr()
    {
        var (context, handler) = await CreateAsync();

        var action = () => handler.Handle(
            new GetDashboardSeriesQuery("1y"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Período inválido*1h*24h*7d*30d*");
    }

    [Fact]
    public async Task Series_24h_ShouldBucketInWindowAndExcludeOldRows()
    {
        var (context, handler) = await CreateAsync();
        var location = Location.Create("Sala Séries");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(
            location.Id, "sw-s", DeviceType.Switch, ip: "192.0.2.60");
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;

        // No período:2 polls (1 ok,1 falha),1 alerta aberto agora e1
        // alerto criado há48h mas RESOLVIDO agora (entra só em resolved).
        context.MonitoringResults.Add(
            MonitoringResult.Create(device.Id, CheckKind.Ping, true, 5));
        context.MonitoringResults.Add(
            MonitoringResult.Create(device.Id, CheckKind.Ping, false, 5));
        var oldPoll = MonitoringResult.Create(device.Id, CheckKind.Ping, true, 5);
        context.MonitoringResults.Add(oldPoll);

        context.Alerts.Add(Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down agora"));
        var oldResolved = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Warning, "velho");
        oldResolved.Resolve();
        context.Alerts.Add(oldResolved);

        var incident = Incident.Create("Série", IncidentSeverity.Low);
        var resolvedIncident = Incident.Create("Série encerrada", IncidentSeverity.Low);
        resolvedIncident.ChangeStatus(IncidentStatus.Resolved);
        context.Incidents.AddRange(incident, resolvedIncident);
        await context.SaveChangesAsync();

        // ApplyAuditFields do AppDbContext estampa CreateDateTime=now em
        // toda entidade Added — o retrocesso precisa vir DEPOIS do insert
        // (Modified não é re-estampado; só UpdateDateTime).
        oldPoll.CreateDateTime = now.AddHours(-48);
        oldResolved.CreateDateTime = now.AddHours(-48);
        await context.SaveChangesAsync();

        var series = await handler.Handle(
            new GetDashboardSeriesQuery("24h"), CancellationToken.None);

        series.BucketMinutes.Should().Be(60);
        series.Polls.Count.Should().BeInRange(24, 25);
        series.Polls.Should().HaveSameCount(series.PollFailures);
        series.Polls.Should().HaveSameCount(series.AlertsOpened);
        series.Polls.Should().HaveSameCount(series.AlertsResolved);
        series.Polls.Should().HaveSameCount(series.IncidentsOpened);
        series.Polls.Should().HaveSameCount(series.IncidentsResolved);

        series.Polls.Sum(point => point.Value).Should().Be(2,
            "o poll de48h está fora da janela de24h");
        series.PollFailures.Sum(point => point.Value).Should().Be(1);

        series.AlertsOpened.Sum(point => point.Value).Should().Be(1,
            "a criação de48h não conta como aberto no período");
        series.AlertsResolved.Sum(point => point.Value).Should().Be(1);
        series.IncidentsOpened.Sum(point => point.Value).Should().Be(2,
            "ambos nasceram na janela; o de48h não existe p/ incidentes");
        series.IncidentsResolved.Sum(point => point.Value).Should().Be(1);

        // Buckets contínuos: mesma grade de1h, crescentes e sem buracos.
        series.Polls.Zip(series.Polls.Skip(1), (a, b) => b.BucketUtc - a.BucketUtc)
            .Should().OnlyContain(gap => gap == TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task Series_1h_ShouldUseFiveMinuteBuckets()
    {
        var (context, handler) = await CreateAsync();
        var location = Location.Create("Sala Hora");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(
            location.Id, "sw-h", DeviceType.Switch, ip: "192.0.2.61");
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        context.MonitoringResults.Add(
            MonitoringResult.Create(device.Id, CheckKind.Ping, true, 5));
        await context.SaveChangesAsync();

        var series = await handler.Handle(
            new GetDashboardSeriesQuery("1h"), CancellationToken.None);

        series.BucketMinutes.Should().Be(5);
        series.Polls.Count.Should().BeInRange(12, 13);
        series.Polls.Sum(point => point.Value).Should().Be(1);
        series.Polls[^1].BucketUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-15));
    }
}
