using FluentAssertions;
using InfraControl.Application.Sla.Services;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.Application.Sla.Services.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T001: handlers do slice Service com repositório real em Sqlite —
/// 400 (nome), 404 (id), 409 (duplicado case-insensitive) em pt-BR.
/// </summary>
public sealed class ServiceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ServiceCommandTests()
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

    private static CreateServiceCommandHandler CreateHandler(AppDbContext context)
        => new(new ServiceRepository(context), NullLogger<CreateServiceCommandHandler>.Instance);

    private static UpdateServiceCommandHandler UpdateHandler(AppDbContext context)
        => new(new ServiceRepository(context), NullLogger<UpdateServiceCommandHandler>.Instance);

    private static DeleteServiceCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new ServiceRepository(context),
            new SlaPolicyRepository(context),
            NullLogger<DeleteServiceCommandHandler>.Instance);

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();

        var result = await CreateHandler(context).Handle(
            new CreateServiceCommand("ERP Corporativo", "Núcleo de negócio"),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("ERP Corporativo");
        result.Description.Should().Be("Núcleo de negócio");
        context.Services.Should().ContainSingle(service => service.Id == result.Id);
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateServiceCommand("ERP Corporativo"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateServiceCommand("  erp corporativo  "), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um serviço com este nome.");
    }

    [Fact]
    public async Task Create_EmptyName_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateServiceCommand("   "), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome do serviço é obrigatório.*");
    }

    [Fact]
    public async Task Update_ExistingService_ShouldChangeNameAndDescription()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateServiceCommand("Antigo"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateServiceCommand(created.Id, "Novo", "Atualizado"),
            CancellationToken.None);

        var stored = await context.Services.SingleAsync(
            service => service.Id == created.Id);
        stored.Name.Should().Be("Novo");
        stored.Description.Should().Be("Atualizado");
    }

    [Fact]
    public async Task Update_DuplicateNameOfAnotherService_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateServiceCommand("Email"), CancellationToken.None);
        var other = await CreateHandler(context).Handle(
            new CreateServiceCommand("Intranet"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateServiceCommand(other.Id, "email"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um serviço com este nome.");
    }

    [Fact]
    public async Task Update_UnknownService_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => UpdateHandler(context).Handle(
            new UpdateServiceCommand(Guid.NewGuid(), "Novo"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Serviço '*' não encontrado.");
    }

    [Fact]
    public async Task Delete_ExistingService_ShouldRemove()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateServiceCommand("Descartável"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteServiceCommand(created.Id), CancellationToken.None);

        context.Services.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownService_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteServiceCommand(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Serviço '*' não encontrado.");
    }

    [Fact]
    public async Task Search_WithTerm_ShouldFilterByNameIgnoringCase()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateServiceCommand("ERP Corporativo"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateServiceCommand("Portal RH"), CancellationToken.None);

        var result = await new SearchServicesQueryHandler(new ServiceRepository(context))
            .Handle(new SearchServicesQuery(Term: "erp"), CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.Name.Should().Be("ERP Corporativo");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Get_UnknownService_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => new GetServiceQueryHandler(new ServiceRepository(context))
            .Handle(new GetServiceQuery(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Serviço '*' não encontrado.");
    }
}
