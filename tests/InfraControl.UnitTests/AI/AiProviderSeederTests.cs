using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.AI;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// ADR-057 (FAI-T011a §2): seed idempotente — Ollama default + rotas de
/// capacidade espelhando a config; cadastro do operador nunca é
/// sobrescrito; provider externo sem cadastro não gera rota (D3-A).
/// </summary>
public sealed class AiProviderSeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AiProviderSeederTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private static AiRoutingOptionsConfiguration FullRouting(
        string? copilotProvider = null) => new()
    {
        Copilot = new() { Provider = copilotProvider, DefaultModel = "llama3.2" },
        IncidentAnalysis = new() { DefaultModel = "llama3.2-incident" },
        KnowledgeGeneration = new() { DefaultModel = "llama3.2-knowledge" },
        Reporting = new() { DefaultModel = "llama3.2-reporting" },
        Automation = new() { DefaultModel = "llama3.2-automation" }
    };

    private async Task<(AppDbContext Context, AiProviderSeeder Seeder)> CreateSeederAsync(
        AiRoutingOptionsConfiguration routing)
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var repo = new AiProviderRepository(context);
        var seeder = new AiProviderSeeder(
            repo,
            context,
            Options.Create(new AiOllamaOptions { BaseUrl = "http://ollama:11434" }),
            routing);
        return (context, seeder);
    }

    [Fact]
    public async Task Seed_Cria_Ollama_E_Rotas_Da_Config()
    {
        var (context, seeder) = await CreateSeederAsync(FullRouting());

        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        var ollama = await context.AiProviders.SingleAsync(p => p.Name == "ollama");
        ollama.BaseUrl.Should().Be("http://ollama:11434");

        var routes = await context.AiProviderCapabilities.ToListAsync();
        routes.Should().HaveCount(5);
        routes.Select(r => r.Capability).Should().BeEquivalentTo(
        [
            "Copilot", "IncidentAnalysis", "KnowledgeGeneration",
            "Reporting", "Automation"
        ]);
        routes.Should().OnlyContain(r => r.ProviderId == ollama.Id);
    }

    [Fact]
    public async Task Seed_Eh_Idempotente()
    {
        var (context, seeder) = await CreateSeederAsync(FullRouting());

        await seeder.SeedAsync();
        await context.SaveChangesAsync();
        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        (await context.AiProviders.CountAsync()).Should().Be(1);
        (await context.AiProviderCapabilities.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Seed_Nao_Sobrescreve_Rota_Do_Operador()
    {
        var (context, seeder) = await CreateSeederAsync(FullRouting());
        var ollama = AiProvider.Create("ollama", "http://ja-cadastrada:11434");
        var rotaEditada = AiProviderCapability.Create("Copilot", ollama.Id, "modelo-editado");
        context.AiProviders.Add(ollama);
        context.AiProviderCapabilities.Add(rotaEditada);
        await context.SaveChangesAsync();

        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        var copilot = await context.AiProviderCapabilities
            .SingleAsync(r => r.Capability == "Copilot");
        copilot.DefaultModel.Should().Be("modelo-editado", "cadastro é autoridade");
        copilot.Id.Should().Be(rotaEditada.Id);

        var ollamaDb = await context.AiProviders.SingleAsync(p => p.Name == "ollama");
        ollamaDb.BaseUrl.Should().Be("http://ja-cadastrada:11434");
    }

    [Fact]
    public async Task Seed_Sem_Capacidade_Na_Config_So_Cria_Ollama()
    {
        var (context, seeder) = await CreateSeederAsync(new AiRoutingOptionsConfiguration());

        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        (await context.AiProviders.CountAsync()).Should().Be(1);
        (await context.AiProviderCapabilities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Seed_Provider_Externo_Ausente_Nao_Gera_Rota()
    {
        // Copilot aponta "openai-compatible" — ainda não cadastrado: a
        // capacidade fica sem rota e resolve pela config (D3-A).
        var (context, seeder) = await CreateSeederAsync(FullRouting(copilotProvider: "openai-compatible"));

        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        (await context.AiProviders.CountAsync()).Should().Be(1);
        var routes = await context.AiProviderCapabilities.ToListAsync();
        routes.Should().HaveCount(4);
        routes.Should().NotContain(r => r.Capability == "Copilot");
    }
}
