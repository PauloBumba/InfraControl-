using FluentAssertions;
using InfraControl.Application.Sla.Slos;
using InfraControl.Application.Sla.Slos.Budgets;
using InfraControl.Application.Sla.Slos.Budgets.Commands;
using InfraControl.Application.Sla.Slos.Budgets.Queries;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T007: orçamento de SLO — CRUD, 400 (percentual/período), 404
/// (orçamento/SLO), exclusão de SLO com orçamentos (409).
/// </summary>
public sealed class SloBudgetCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SloBudgetCommandTests()
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

    private static RecordSloBudgetCommandHandler RecordHandler(AppDbContext context)
        => new(
            new SloBudgetRepository(context),
            new SloRepository(context),
            NullLogger<RecordSloBudgetCommandHandler>.Instance);

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

    private static readonly DateTime Start =
        new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SearchByService_ShouldFilterPaginatedAndOrderChronologically()
    {
        await using var context = await CreateContextAsync();

        var serviceA = Service.Create("ERP");
        context.Services.Add(serviceA);
        var serviceB = Service.Create("Portal");
        context.Services.Add(serviceB);
        await context.SaveChangesAsync();

        var sloA1 = Slo.Create("SLO A1", 99.9m, 30, serviceA.Id);
        var sloA2 = Slo.Create("SLO A2", 99.5m, 30, serviceA.Id);
        var sloB = Slo.Create("SLO B", 99.0m, 30, serviceB.Id);
        context.Slos.AddRange(sloA1, sloA2, sloB);
        await context.SaveChangesAsync();

        await RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloA1.Id, Start, null, 10m), CancellationToken.None);
        await RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloA2.Id, Start.AddDays(30), null, 20m), CancellationToken.None);
        // O budget do serviço B é o mais recente de todos — se o filtro
        // falhar, ele aparece na frente e o teste denuncia.
        await RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloB.Id, Start.AddDays(60), null, 30m), CancellationToken.None);

        var handler = new SearchSloBudgetsByServiceQueryHandler(
            new SloBudgetRepository(context));

        var servicoA = await handler.Handle(
            new SearchSloBudgetsByServiceQuery(serviceA.Id, PageSize: 1),
            CancellationToken.None);
        servicoA.TotalCount.Should().Be(2, "budgets do outro serviço ficam de fora");
        servicoA.Items.Should().ContainSingle();
        servicoA.Items.Single().SloId.Should().Be(
            sloA2.Id, "ordenado por PeriodStart desc dentro do serviço");

        var servicoB = await handler.Handle(
            new SearchSloBudgetsByServiceQuery(serviceB.Id), CancellationToken.None);
        servicoB.TotalCount.Should().Be(1);
        servicoB.Items.Single().SloId.Should().Be(sloB.Id);

        var todos = await handler.Handle(
            new SearchSloBudgetsByServiceQuery(), CancellationToken.None);
        todos.TotalCount.Should().Be(3, "sem serviceId lista todos os serviços");
        todos.Items.Select(b => b.PeriodStart).Should().BeInDescendingOrder();

        var clampado = await handler.Handle(
            new SearchSloBudgetsByServiceQuery(serviceA.Id, Page: 0, PageSize: 500),
            CancellationToken.None);
        clampado.Page.Should().Be(1);
        clampado.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Record_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);

        var result = await RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloId, Start, Start.AddDays(30), 42.5m),
            CancellationToken.None);

        result.SloId.Should().Be(sloId);
        result.PeriodStart.Should().Be(Start);
        result.PeriodEnd.Should().Be(Start.AddDays(30));
        result.ConsumedPercent.Should().Be(42.5m);
        context.SloBudgets.Should().ContainSingle(b => b.Id == result.Id);
    }

    [Fact]
    public async Task Record_UnknownSlo_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => RecordHandler(context).Handle(
            new RecordSloBudgetCommand(Guid.NewGuid(), Start, null, 10m),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("SLO '*' não encontrado.");
    }

    [Fact]
    public async Task Record_ConsumedOver100_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);

        var action = () => RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloId, Start, null, 100.5m),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O percentual consumido deve estar entre 0 e 100.*");
    }

    [Fact]
    public async Task Record_PeriodEndBeforeStart_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);

        var action = () => RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloId, Start, Start.AddDays(-1), 10m),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O fim do período deve ser posterior ao início.*");
    }

    [Fact]
    public async Task Search_ShouldListBySloChronologically()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);
        await RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloId, Start.AddDays(30), null, 30m),
            CancellationToken.None);
        await RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloId, Start, null, 20m),
            CancellationToken.None);

        var page = await new SearchSloBudgetsQueryHandler(
                new SloBudgetRepository(context))
            .Handle(new SearchSloBudgetsQuery(sloId), CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.Items.Select(b => b.PeriodStart).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Get_UnknownBudget_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => new GetSloBudgetQueryHandler(
                new SloBudgetRepository(context))
            .Handle(new GetSloBudgetQuery(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Orçamento de SLO '*' não encontrado.");
    }

    [Fact]
    public async Task DeleteSlo_WithBudgets_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var sloId = await SeedSloAsync(context);
        await RecordHandler(context).Handle(
            new RecordSloBudgetCommand(sloId, Start, null, 10m),
            CancellationToken.None);

        var action = () => DeleteSloHandler(context).Handle(
            new DeleteSloCommand(sloId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o SLO: existem orçamentos vinculados.");
    }
}
