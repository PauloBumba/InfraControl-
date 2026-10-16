using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Domain.Entities;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T005/S4 (ADR-037 §15) + ADR-057 (FAI-T011a): testes do
/// AiProviderRouter — roteamento de capacidade em (provider, modelo)
/// primário + fallback; cadastro primeiro (D3), config como fallback
/// (D3-A), provider inativo/ausente = §44.
/// </summary>
public sealed class AiProviderRouterTests
{
    private readonly Mock<IAiProviderRegistry> _mockRegistry;
    private readonly Mock<IAiProvider> _mockProvider;

    public AiProviderRouterTests()
    {
        _mockRegistry = new Mock<IAiProviderRegistry>();
        _mockProvider = new Mock<IAiProvider>();
        _mockProvider.Setup(p => p.Name).Returns("ollama");
        _mockRegistry.Setup(r => r.Get("ollama")).Returns(_mockProvider.Object);
    }

    private static Mock<IAiProviderRepository> EmptyRepository()
    {
        var mock = new Mock<IAiProviderRepository>();
        mock.Setup(repository => repository.GetCapabilityRouteAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiProviderCapability?)null);
        return mock;
    }

    [Fact]
    public async Task Resolve_capacidade_valida_retorna_selecao_com_modelo_default()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        var selection = await router.ResolveAsync(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.Model.Should().Be("modelo-copilot");
        selection.FallbackProvider.Should().BeNull();
        selection.FallbackModel.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_capacidade_com_fallback_retorna_selecao_com_fallback()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot", FallbackModel = "modelo-copilot-fallback" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        var selection = await router.ResolveAsync(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.Model.Should().Be("modelo-copilot");
        selection.FallbackProvider.Should().Be("ollama");
        selection.FallbackModel.Should().Be("modelo-copilot-fallback");
    }

    [Fact]
    public async Task Resolve_capacidade_inexistente_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        var act = () => router.ResolveAsync((AiCapability)999);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("Capacidade '999' não está configurada.");
    }

    [Fact]
    public async Task Resolve_modelo_default_vazio_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        var act = () => router.ResolveAsync(AiCapability.Copilot);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("Modelo default não configurado para a capacidade 'Copilot'.");
    }

    [Fact]
    public async Task Resolve_modelo_default_nulo_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = null! },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        var act = () => router.ResolveAsync(AiCapability.Copilot);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("Modelo default não configurado para a capacidade 'Copilot'.");
    }

    [Fact]
    public async Task Resolve_todas_as_capacidades_mapeadas_devem_retornar_selecao_valida()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        foreach (var capability in Enum.GetValues<AiCapability>())
        {
            var selection = await router.ResolveAsync(capability);
            selection.Provider.Should().Be("ollama");
            selection.Model.Should().NotBeNullOrEmpty();
        }
    }

    // ── ADR-050 §2: provider por capacidade ─────────────────────────────

    [Fact]
    public async Task Resolve_com_provider_configurado_usa_o_provider_da_capacidade()
    {
        var mockOpenAi = new Mock<IAiProvider>();
        mockOpenAi.Setup(p => p.Name).Returns("openai");
        _mockRegistry.Setup(r => r.Get("openai")).Returns(mockOpenAi.Object);

        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { Provider = "ollama", DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { Provider = "openai", DefaultModel = "gpt-4o-mini" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        (await router.ResolveAsync(AiCapability.Copilot)).Provider.Should().Be("ollama");
        (await router.ResolveAsync(AiCapability.IncidentAnalysis)).Provider.Should().Be("openai");
    }

    [Fact]
    public async Task Resolve_com_fallback_provider_distinto_retorna_o_fallback_configurado()
    {
        var mockGemini = new Mock<IAiProvider>();
        mockGemini.Setup(p => p.Name).Returns("gemini");
        _mockRegistry.Setup(r => r.Get("gemini")).Returns(mockGemini.Object);

        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new()
            {
                Provider = "ollama",
                DefaultModel = "modelo-copilot",
                FallbackProvider = "gemini",
                FallbackModel = "gemini-flash"
            },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, EmptyRepository().Object);

        var selection = await router.ResolveAsync(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.FallbackProvider.Should().Be("gemini");
        selection.FallbackModel.Should().Be("gemini-flash");
    }

    [Fact]
    public async Task Resolve_provider_inexistente_lanca_AiProviderNotConfiguredException()
    {
        // FakeAiRegistry (registro real de nome→provider): "grok" não existe.
        var registry = new FakeAiRegistry(_mockProvider.Object);
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { Provider = "grok", DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            registry, configuration, EmptyRepository().Object);

        var act = () => router.ResolveAsync(AiCapability.Copilot);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>();
    }

    [Fact]
    public async Task Resolve_fallback_provider_inexistente_lanca_AiProviderNotConfiguredException()
    {
        var registry = new FakeAiRegistry(_mockProvider.Object);
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new()
            {
                Provider = "ollama",
                DefaultModel = "modelo-copilot",
                FallbackProvider = "grok",
                FallbackModel = "grok-mini"
            },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            registry, configuration, EmptyRepository().Object);

        var act = () => router.ResolveAsync(AiCapability.Copilot);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>();
    }

    // ── ADR-057 (FAI-T011a): cadastro primeiro (D3) ─────────────────────

    [Fact]
    public async Task Resolve_rota_no_cadastro_vence_a_configuracao()
    {
        var ollama = AiProvider.Create("ollama", "http://localhost:11434");
        var route = AiProviderCapability.Create("Copilot", ollama.Id, "modelo-do-cadastro");
        var repository = EmptyRepository();
        repository.Setup(r => r.GetCapabilityRouteAsync("Copilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        repository.Setup(r => r.GetByIdAsync(ollama.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ollama);

        // Config aponta outro modelo — o cadastro tem prioridade.
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-da-config" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, repository.Object);

        var selection = await router.ResolveAsync(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.Model.Should().Be("modelo-do-cadastro");
    }

    [Fact]
    public async Task Resolve_rota_com_fallback_no_cadastro_retorna_fallback()
    {
        var ollama = AiProvider.Create("ollama", "http://localhost:11434");
        var backup = AiProvider.Create("backup", "http://backup:11434");
        var mockBackup = new Mock<IAiProvider>();
        mockBackup.Setup(p => p.Name).Returns("backup");
        _mockRegistry.Setup(r => r.Get("backup")).Returns(mockBackup.Object);

        var route = AiProviderCapability.Create(
            "Copilot", ollama.Id, "modelo-primario", backup.Id, "modelo-fallback");
        var repository = EmptyRepository();
        repository.Setup(r => r.GetCapabilityRouteAsync("Copilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        repository.Setup(r => r.GetByIdAsync(ollama.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ollama);
        repository.Setup(r => r.GetByIdAsync(backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);

        var router = new AiProviderRouter(
            _mockRegistry.Object, new AiRoutingOptionsConfiguration(), repository.Object);

        var selection = await router.ResolveAsync(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.Model.Should().Be("modelo-primario");
        selection.FallbackProvider.Should().Be("backup");
        selection.FallbackModel.Should().Be("modelo-fallback");
    }

    [Fact]
    public async Task Resolve_rota_com_provider_inativo_no_cadastro_eh_nao_configurado()
    {
        var ollama = AiProvider.Create("ollama", "http://localhost:11434");
        ollama.Deactivate();
        var route = AiProviderCapability.Create("Copilot", ollama.Id, "modelo-copilot");
        var repository = EmptyRepository();
        repository.Setup(r => r.GetCapabilityRouteAsync("Copilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        repository.Setup(r => r.GetByIdAsync(ollama.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ollama);

        // Config continuaria resolvendo — mas a desativação não pode ser
        // burlada (ADR-057 D3-A: config é fallback SÓ sem linha).
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(
            _mockRegistry.Object, configuration, repository.Object);

        var act = () => router.ResolveAsync(AiCapability.Copilot);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*inativo*");
    }

    [Fact]
    public async Task Resolve_rota_com_provider_sem_adaptador_eh_nao_configurado()
    {
        // Cadastro aponta "grok" — nenhum adaptador registrado (ADR-050 §1).
        var grok = AiProvider.Create("grok", "http://grok:8080");
        var route = AiProviderCapability.Create("Copilot", grok.Id, "grok-mini");
        var repository = EmptyRepository();
        repository.Setup(r => r.GetCapabilityRouteAsync("Copilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        repository.Setup(r => r.GetByIdAsync(grok.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grok);

        // FakeAiRegistry (registro real de nome→provider): "grok" não existe.
        var registry = new FakeAiRegistry(_mockProvider.Object);
        var router = new AiProviderRouter(
            registry, new AiRoutingOptionsConfiguration(), repository.Object);

        var act = () => router.ResolveAsync(AiCapability.Copilot);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>();
    }

    [Fact]
    public async Task Resolve_rota_com_fallback_inativo_no_cadastro_eh_nao_configurado()
    {
        var ollama = AiProvider.Create("ollama", "http://localhost:11434");
        var backup = AiProvider.Create("backup", "http://backup:11434");
        backup.Deactivate();
        var route = AiProviderCapability.Create(
            "Copilot", ollama.Id, "modelo-primario", backup.Id, "modelo-fallback");
        var repository = EmptyRepository();
        repository.Setup(r => r.GetCapabilityRouteAsync("Copilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        repository.Setup(r => r.GetByIdAsync(ollama.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ollama);
        repository.Setup(r => r.GetByIdAsync(backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);

        var router = new AiProviderRouter(
            _mockRegistry.Object, new AiRoutingOptionsConfiguration(), repository.Object);

        var act = () => router.ResolveAsync(AiCapability.Copilot);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*inativo*");
    }
}
