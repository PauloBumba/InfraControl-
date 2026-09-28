using FluentAssertions;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.Application.Sla.Slos;
using InfraControl.Application.Sla.Slos.Measurements;
using InfraControl.Application.Sla.Slos.Measurements.Commands;
using InfraControl.Application.Sla.Slos.Measurements.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T006: medições de SLO — CRUD, 400 (valor), 404 (medição/SLO),
/// exclusão de SLO com medições (409).
/// </summary>
public sealed class SloMeasurementCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SloMeasurementCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static RecordSloMeasurementCommandHandler RecordHandler(AppDbContext context)
        => new(
            new SloMeasurementRepository(context),
            new SloRepository(context),
            NullLogger<RecordSloMeasurementCommandHandler>.Instance);

    private static DeleteSloCommandHandler DeleteSloHandler(AppDbContext context)
        => new(
            new SloRepository(context),
            new SloMeasurementRepository(context),
            new SloBudgetRepository(context),
            NullLogger<DeleteSloCommandHandler>.Instance);

    private static async Task<Guid> SeedSloAsync(AppDbContext context)
    {
        var service = Service.Create("ERP");
        context.Services.Add(service);
        await context.SaveChangesAsync();

        var slo = Slo.Create("Disponibilidade ERP", 99.95m, 30, service.Id);
        context.Slos.Add(slo);
        await context.SaveChangesAsync();
        return slo.Id;
    }

    [Fact]
    public async Task Record_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);
        var measuredAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        var result = await RecordHandler(context).Handle(
            new RecordSloMeasurementCommand(sloId, measuredAt, 99.87m),
            CancellationToken.None);

        result.SloId.Should().Be(sloId);
        result.MeasuredAt.Should().Be(measuredAt);
        result.ValuePercent.Should().Be(99.87m);
        context.SloMeasurements.Should().ContainSingle(m => m.Id == result.Id);
    }

    [Fact]
    public async Task Record_UnknownSlo_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => RecordHandler(context).Handle(
            new RecordSloMeasurementCommand(
                Guid.NewGuid(), DateTime.UtcNow, 99.9m),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("SLO '*' não encontrado.");
    }

    [Fact]
    public async Task Record_ValueOver100_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);

        var action = () => RecordHandler(context).Handle(
            new RecordSloMeasurementCommand(sloId, DateTime.UtcNow, 100.5m),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O valor medido deve estar entre 0 e 100.*");
    }

    [Fact]
    public async Task Search_ShouldListBySloChronologically()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);
        await RecordHandler(context).Handle(
            new RecordSloMeasurementCommand(
                sloId, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), 99.9m),
            CancellationToken.None);
        await RecordHandler(context).Handle(
            new RecordSloMeasurementCommand(
                sloId, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 99.8m),
            CancellationToken.None);

        var page = await new SearchSloMeasurementsQueryHandler(
                new SloMeasurementRepository(context))
            .Handle(new SearchSloMeasurementsQuery(sloId), CancellationToken.None);

        page.Items.Should().HaveCount(2);
        // Padrão do repositório: medições mais recentes primeiro.
        page.Items.Select(m => m.MeasuredAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Get_UnknownMeasurement_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => new GetSloMeasurementQueryHandler(
                new SloMeasurementRepository(context))
            .Handle(new GetSloMeasurementQuery(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Medição de SLO '*' não encontrada.");
    }

    [Fact]
    public async Task DeleteSlo_WithMeasurements_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);
        await RecordHandler(context).Handle(
            new RecordSloMeasurementCommand(sloId, DateTime.UtcNow, 99.9m),
            CancellationToken.None);

        var action = () => DeleteSloHandler(context).Handle(
            new DeleteSloCommand(sloId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o SLO: existem medições vinculadas.");
    }
}
