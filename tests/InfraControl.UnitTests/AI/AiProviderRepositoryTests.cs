using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// ADR-057 (FAI-T011a): repositório do cadastro de providers — busca
/// por nome, upsert de rota por capacidade (reusa o Id) e verificação
/// de existência. Harness Sqlite in-memory (molde dos testes de
/// Alerts/Discovery).
/// </summary>
public sealed class AiProviderRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AiProviderRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, AiProviderRepository Repo)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return (context, new AiProviderRepository(context));
    }

    [Fact]
    public async Task AddAsync_E_GetByName_Devolvem_Provider_Com_Modelos()
    {
        var (context, repo) = await CreateAsync();
        var provider = AiProvider.Create(
            "ollama", "http://localhost:11434", models: ["llama3.2", "qwen"]);

        await repo.AddAsync(provider);
        await context.SaveChangesAsync();

        var found = await repo.GetByNameAsync("ollama");

        found.Should().NotBeNull();
        found!.Id.Should().Be(provider.Id);
        found.Models.Should().Equal("llama3.2", "qwen");
    }

    [Fact]
    public async Task GetByName_Ausente_Devolve_Null()
    {
        var (_, repo) = await CreateAsync();

        (await repo.GetByNameAsync("inexistente")).Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_Ordena_Por_Nome()
    {
        var (context, repo) = await CreateAsync();
        await repo.AddAsync(AiProvider.Create("gemini", "https://a.example"));
        await repo.AddAsync(AiProvider.Create("ollama", "http://localhost:11434"));
        await context.SaveChangesAsync();

        var list = await repo.ListAsync();

        list.Select(provider => provider.Name).Should().Equal("gemini", "ollama");
    }

    [Fact]
    public async Task Upsert_Rota_Nova_Insere_E_Encontra_Por_Capacidade()
    {
        var (context, repo) = await CreateAsync();
        var provider = AiProvider.Create("ollama", "http://localhost:11434");
        await repo.AddAsync(provider);
        await context.SaveChangesAsync();

        var route = AiProviderCapability.Create("Copilot", provider.Id, "llama3.2");
        await repo.UpsertCapabilityRouteAsync(route);
        await context.SaveChangesAsync();

        var found = await repo.GetCapabilityRouteAsync("Copilot");
        found.Should().NotBeNull();
        found!.Id.Should().Be(route.Id);
        found.DefaultModel.Should().Be("llama3.2");
        (await repo.CapabilityRouteExistsAsync("Copilot")).Should().BeTrue();
        (await repo.CapabilityRouteExistsAsync("Reporting")).Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_Rota_Existente_Reusa_Id_E_Atualiza()
    {
        var (context, repo) = await CreateAsync();
        var provider = AiProvider.Create("ollama", "http://localhost:11434");
        await repo.AddAsync(provider);
        await context.SaveChangesAsync();

        var original = AiProviderCapability.Create("Copilot", provider.Id, "llama3.2");
        await repo.UpsertCapabilityRouteAsync(original);
        await context.SaveChangesAsync();

        var novaRota = AiProviderCapability.Create("Copilot", provider.Id, "qwen");
        await repo.UpsertCapabilityRouteAsync(novaRota);
        await context.SaveChangesAsync();

        var found = await repo.GetCapabilityRouteAsync("Copilot");
        found.Should().NotBeNull();
        found!.Id.Should().Be(original.Id, "o upsert reusa a linha existente");
        found.DefaultModel.Should().Be("qwen");

        var total = await context.AiProviderCapabilities.CountAsync();
        total.Should().Be(1, "não pode duplicar a capacidade");
    }
}
