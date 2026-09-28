using FluentAssertions;
using InfraControl.Application.Sla.Policies;
using InfraControl.Application.Sla.Policies.Commands;
using InfraControl.Application.Sla.Policies.Queries;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T002: handlers de SlaPolicy — CRUD, 400 (prazos/nome), 404
/// (política/serviço), pré-check de exclusão de Service com policies (409).
/// </summary>
public sealed class SlaPolicyCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SlaPolicyCommandTests()
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

    private static CreateSlaPolicyCommandHandler CreateHandler(AppDbContext context)
        => new(
            new SlaPolicyRepository(context),
            new ServiceRepository(context),
            NullLogger<CreateSlaPolicyCommandHandler>.Instance);

    private static UpdateSlaPolicyCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new SlaPolicyRepository(context),
            new ServiceRepository(context),
            NullLogger<UpdateSlaPolicyCommandHandler>.Instance);

    private static DeleteSlaPolicyCommandHandler DeleteHandler(AppDbContext context)
        => new(new SlaPolicyRepository(context), NullLogger<DeleteSlaPolicyCommandHandler>.Instance);

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
            new CreateSlaPolicyCommand("Padrão ERP", 30, 480, serviceId),
            CancellationToken.None);

        result.Name.Should().Be("Padrão ERP");
        result.ResponseMinutes.Should().Be(30);
        result.ResolutionMinutes.Should().Be(480);
        result.ServiceId.Should().Be(serviceId);
        context.SlaPolicies.Should().ContainSingle(policy => policy.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownService_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateSlaPolicyCommand("Órfã", 30, 480, Guid.NewGuid()),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Serviço '*' não encontrado.");
    }

    [Fact]
    public async Task Create_ZeroResponseMinutes_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateSlaPolicyCommand("Sem prazo", 0, 480),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O prazo de resposta deve ser maior que zero.*");
    }

    [Fact]
    public async Task Update_ExistingPolicy_ShouldChangePrazos()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateSlaPolicyCommand("Antes", 30, 480), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateSlaPolicyCommand(created.Id, "Depois", 60, 720),
            CancellationToken.None);

        var stored = await context.SlaPolicies.SingleAsync(policy => policy.Id == created.Id);
        stored.Name.Should().Be("Depois");
        stored.ResponseMinutes.Should().Be(60);
        stored.ResolutionMinutes.Should().Be(720);
    }

    [Fact]
    public async Task Update_UnknownPolicy_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => UpdateHandler(context).Handle(
            new UpdateSlaPolicyCommand(Guid.NewGuid(), "Nova", 30, 480),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Política de SLA '*' não encontrada.");
    }

    [Fact]
    public async Task Delete_ExistingPolicy_ShouldRemove()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateSlaPolicyCommand("Descartável", 30, 480), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteSlaPolicyCommand(created.Id), CancellationToken.None);

        context.SlaPolicies.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownPolicy_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteSlaPolicyCommand(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Política de SLA '*' não encontrada.");
    }

    [Fact]
    public async Task Search_ShouldFilterByTermAndServiceId()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context, "ERP");
        await CreateHandler(context).Handle(
            new CreateSlaPolicyCommand("Do ERP", 30, 480, serviceId), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateSlaPolicyCommand("Global", 60, 720), CancellationToken.None);

        var byTerm = await new SearchSlaPoliciesQueryHandler(new SlaPolicyRepository(context))
            .Handle(new SearchSlaPoliciesQuery(Term: "erp"), CancellationToken.None);
        byTerm.Items.Should().ContainSingle(policy => policy.Name == "Do ERP");

        var byService = await new SearchSlaPoliciesQueryHandler(new SlaPolicyRepository(context))
            .Handle(new SearchSlaPoliciesQuery(ServiceId: serviceId), CancellationToken.None);
        byService.Items.Should().ContainSingle(policy => policy.Name == "Do ERP");

        var all = await new SearchSlaPoliciesQueryHandler(new SlaPolicyRepository(context))
            .Handle(new SearchSlaPoliciesQuery(), CancellationToken.None);
        all.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task DeleteService_WithPolicies_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var serviceId = await SeedServiceAsync(context);
        await CreateHandler(context).Handle(
            new CreateSlaPolicyCommand("Vinculada", 30, 480, serviceId),
            CancellationToken.None);

        var action = () => new DeleteServiceCommandHandler(
                new ServiceRepository(context),
                new SlaPolicyRepository(context),
                NullLogger<DeleteServiceCommandHandler>.Instance)
            .Handle(new DeleteServiceCommand(serviceId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o serviço: existem políticas de SLA vinculadas.");
    }
}
