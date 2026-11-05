using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.AI;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// ADR-065 (modelo único): <c>Ai:Routing:Default</c> é a fonte do modelo
/// único; as entradas por capacidade viram override opcional e o cadastro
/// continua autoridade quando a linha existe (ADR-057 D3-A). Cascata de
/// resolução: cadastro → config por capacidade → Default → §44.
/// </summary>
public sealed class AiRoutingDefaultTests : IDisposable
{
    private readonly Mock<IAiProviderRegistry> _registry;
    private readonly Mock<IAiProvider> _provider;

    public AiRoutingDefaultTests()
    {
        _provider = new Mock<IAiProvider>();
        _provider.Setup(p => p.Name).Returns("ollama");
        _registry = new Mock<IAiProviderRegistry>();
        _registry.Setup(r => r.Get("ollama")).Returns(_provider.Object);
    }

    private static Mock<IAiProviderRepository> WithoutRegistryRoute()
    {
        var repository = new Mock<IAiProviderRepository>();
        repository.Setup(r => r.GetCapabilityRouteAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiProviderCapability?)null);
        return repository;
    }

    private AiProviderRouter Router(AiRoutingOptionsConfiguration configuration)
        => new(_registry.Object, configuration, WithoutRegistryRoute().Object);

    // ---------------- router: cascatada ----------------

    [Fact]
    public async Task Router_sem_config_de_capacidade_usa_modelo_unico_default()
    {
        var router = Router(new AiRoutingOptionsConfiguration
        {
            Default = new() { Provider = "ollama", DefaultModel = "modelo-unico" }
        });

        var selection = await router.ResolveAsync(AiCapability.Reporting);

        selection.Provider.Should().Be("ollama");
        selection.Model.Should().Be("modelo-unico");
        selection.FallbackModel.Should().BeNull();
    }

    [Fact]
    public async Task Router_override_de_capacidade_precede_o_modelo_unico()
    {
        var router = Router(new AiRoutingOptionsConfiguration
        {
            Default = new() { DefaultModel = "modelo-unico" },
            Reporting = new() { DefaultModel = "modelo-reporting-especial" }
        });

        var selection = await router.ResolveAsync(AiCapability.Reporting);
        var copilot = await router.ResolveAsync(AiCapability.Copilot);

        selection.Model.Should().Be("modelo-reporting-especial");
        copilot.Model.Should().Be("modelo-unico");
    }

    [Fact]
    public async Task Router_uma_capacidade_apenas_usa_override_as_outras_herdam_default()
    {
        var router = Router(new AiRoutingOptionsConfiguration
        {
            Default = new() { DefaultModel = "modelo-unico", FallbackModel = "fallback-unico" },
            Automation = new() { DefaultModel = "modelo-automation-especial" }
        });

        var automation = await router.ResolveAsync(AiCapability.Automation);
        var knowledge = await router.ResolveAsync(AiCapability.KnowledgeGeneration);

        automation.Model.Should().Be("modelo-automation-especial");
        automation.FallbackModel.Should().Be("fallback-unico");
        knowledge.Model.Should().Be("modelo-unico");
        knowledge.FallbackModel.Should().Be("fallback-unico");
    }

    [Fact]
    public async Task Router_default_com_provider_declarado_resolve_provider()
    {
        _registry.Setup(r => r.Get("gemini")).Returns(
            Mock.Of<IAiProvider>(p => p.Name == "gemini"));

        var router = Router(new AiRoutingOptionsConfiguration
        {
            Default = new() { Provider = "gemini", DefaultModel = "modelo-unico" }
        });

        var selection = await router.ResolveAsync(AiCapability.Copilot);

        selection.Provider.Should().Be("gemini");
        selection.Model.Should().Be("modelo-unico");
    }

    [Fact]
    public async Task Router_sem_nada_lanca_nao_configurado()
    {
        var router = Router(new AiRoutingOptionsConfiguration());

        var resolve = async () => await router.ResolveAsync(AiCapability.Copilot);

        await resolve.Should().ThrowAsync<AiProviderNotConfiguredException>();
    }

    [Fact]
    public async Task Router_capacidade_sem_modelo_mas_com_default_usa_default()
    {
        var router = Router(new AiRoutingOptionsConfiguration
        {
            Default = new() { DefaultModel = "modelo-unico" },
            Copilot = new()
        });

        var selection = await router.ResolveAsync(AiCapability.Copilot);

        selection.Model.Should().Be("modelo-unico");
    }

    // ---------------- seeder: uma fonte ----------------

    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");

    private DbContextOptions<AppDbContext>? _options;

    private void EnsureOpen()
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            _connection.Open();
        }

        _options ??= new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, AiProviderSeeder Seeder)> CreateSeederAsync(
        AiRoutingOptionsConfiguration routing)
    {
        EnsureOpen();
        var context = new AppDbContext(_options!);
        await context.Database.EnsureCreatedAsync();
        var repository = new AiProviderRepository(context);
        var seeder = new AiProviderSeeder(
            repository,
            context,
            Options.Create(new AiOllamaOptions { BaseUrl = "http://ollama:11434" }),
            routing);
        return (context, seeder);
    }

    [Fact]
    public async Task Seeder_semeia_modelo_unico_em_todas_capacidades()
    {
        var (context, seeder) = await CreateSeederAsync(
            new AiRoutingOptionsConfiguration
            {
                Default = new() { DefaultModel = "modelo-unico" }
            });

        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        var routes = await context.AiProviderCapabilities.ToListAsync();
        routes.Should().HaveCount(5);
        routes.Should().OnlyContain(r => r.DefaultModel == "modelo-unico");
    }

    [Fact]
    public async Task Seeder_respeita_override_de_capacidade_sobre_o_default()
    {
        var (context, seeder) = await CreateSeederAsync(
            new AiRoutingOptionsConfiguration
            {
                Default = new() { DefaultModel = "modelo-unico" },
                Reporting = new() { DefaultModel = "modelo-reporting-especial" }
            });

        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        var routes = await context.AiProviderCapabilities.ToDictionaryAsync(
            r => r.Capability);

        routes["Reporting"].DefaultModel.Should().Be("modelo-reporting-especial");
        routes["Copilot"].DefaultModel.Should().Be("modelo-unico");
        routes["Automation"].DefaultModel.Should().Be("modelo-unico");
    }

    [Fact]
    public async Task Seeder_com_estrutura_antiga_sem_default_continua_semeando()
    {
        var (context, seeder) = await CreateSeederAsync(
            new AiRoutingOptionsConfiguration
            {
                Copilot = new() { DefaultModel = "modelo-copilot" },
                IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
                KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
                Reporting = new() { DefaultModel = "modelo-reporting" },
                Automation = new() { DefaultModel = "modelo-automation" }
            });

        await seeder.SeedAsync();
        await context.SaveChangesAsync();

        var routes = await context.AiProviderCapabilities.ToDictionaryAsync(
            r => r.Capability);

        routes.Should().HaveCount(5);
        routes["Copilot"].DefaultModel.Should().Be("modelo-copilot");
        routes["Automation"].DefaultModel.Should().Be("modelo-automation");
    }
}
