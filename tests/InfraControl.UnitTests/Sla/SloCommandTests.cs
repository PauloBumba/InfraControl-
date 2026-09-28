using FluentAssertions;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.Application.Sla.Slos;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.Application.Sla.Slos.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T005: handlers de Slo — CRUD, 400 (alvo/janela), 404 (SLO/serviço),
/// exclusão de Service com SLOs (409).
/// </summary>
public sealed class SloCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SloCommandTests()
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

    private static CreateSloCommandHandler CreateHandler(AppDbContext context)
        => new(
            new SloRepository(context),
            new ServiceRepository(context),
            NullLogger<CreateSloCommandHandler>.Instance);

    private static UpdateSloCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new SloRepository(context),
            new ServiceRepository(context),
            NullLogger<UpdateSloCommandHandler>.Instance);

    private static DeleteSloCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new SloRepository(context),
            new SloMeasurementRepository(context),
            new SloBudgetRepository(context),
            NullLogger<DeleteSloCommandHandler>.Instance);

    private static DeleteServiceCommandHandler DeleteServiceHandler(AppDbContext context)
        => new(
            new ServiceRepository(context),
            new SlaPolicyRepository(context),
            new SloRepository(context),
            NullLogger<DeleteServiceCommandHandler>.Instance);

    private static async Task<Guid> SeedServiceAsync(AppDbContext context, string name = "ERP")
    {
        var service = Service.Create(name);
        context.Services.Add(service);
        await context.SaveChangesAsync();
        return service.Id;
    }

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateSloCommand("Disponibilidade do ERP", 99.95m, 30, serviceId),
            CancellationToken.None);

        result.Name.Should().Be("Disponibilidade do ERP");
        result.TargetPercent.Should().Be(99.95m);
        result.WindowDays.Should().Be(30);
        result.ServiceId.Should().Be(serviceId);
        context.Slos.Should().ContainSingle(slo => slo.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownService_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateSloCommand("Órfão", 99.9m, 30, Guid.NewGuid()),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Serviço '*' não encontrado.");
    }

    [Fact]
    public async Task Create_TargetPercentOver100_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateSloCommand("Impossível", 100.5m, 30, serviceId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O alvo do SLO deve estar entre 0 (exclusivo) e 100.*");
    }

    [Fact]
    public async Task Update_ExistingSlo_ShouldChangeAlvoEJanela()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateSloCommand("Antes", 99.9m, 30, serviceId), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateSloCommand(created.Id, "Depois", 99.99m, 90, serviceId),
            CancellationToken.None);

        var stored = await context.Slos.SingleAsync(slo => slo.Id == created.Id);
        stored.Name.Should().Be("Depois");
        stored.TargetPercent.Should().Be(99.99m);
        stored.WindowDays.Should().Be(90);
    }

    [Fact]
    public async Task Update_UnknownSlo_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => UpdateHandler(context).Handle(
            new UpdateSloCommand(Guid.NewGuid(), "Novo", 99.9m, 30, Guid.NewGuid()),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("SLO '*' não encontrado.");
    }

    [Fact]
    public async Task Delete_ExistingSlo_ShouldRemove()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateSloCommand("Descartável", 99.9m, 30, serviceId),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteSloCommand(created.Id), CancellationToken.None);

        context.Slos.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_ShouldFilterByTermAndServiceId()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context, "ERP");
        var otherServiceId = await SeedServiceAsync(context, "Intranet");
        await CreateHandler(context).Handle(
            new CreateSloCommand("Disponibilidade ERP", 99.95m, 30, serviceId),
            CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateSloCommand("Latência Intranet", 99m, 7, otherServiceId),
            CancellationToken.None);

        var byTerm = await new SearchSlosQueryHandler(new SloRepository(context))
            .Handle(new SearchSlosQuery(Term: "disponib"), CancellationToken.None);
        byTerm.TotalCount.Should().Be(1);

        var byService = await new SearchSlosQueryHandler(new SloRepository(context))
            .Handle(new SearchSlosQuery(ServiceId: serviceId), CancellationToken.None);
        byService.TotalCount.Should().Be(1);
        byService.Items.Single().Name.Should().Be("Disponibilidade ERP");
    }

    [Fact]
    public async Task DeleteService_WithSlos_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context);
        await CreateHandler(context).Handle(
            new CreateSloCommand("Vinculado", 99.9m, 30, serviceId), CancellationToken.None);

        var action = () => DeleteServiceHandler(context).Handle(
            new DeleteServiceCommand(serviceId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o serviço: existem SLOs vinculados.");
    }
}
