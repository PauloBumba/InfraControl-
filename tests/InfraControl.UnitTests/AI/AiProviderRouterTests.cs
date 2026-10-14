using FluentAssertions;
using InfraControl.Application.AI;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T005/S4 (ADR-037 §15): testes do AiProviderRouter
/// — roteamento de capacidade em (provider, modelo) primário + fallback.
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

    [Fact]
    public void Resolve_capacidade_valida_retorna_selecao_com_modelo_default()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var selection = router.Resolve(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.Model.Should().Be("modelo-copilot");
        selection.FallbackProvider.Should().BeNull();
        selection.FallbackModel.Should().BeNull();
    }

    [Fact]
    public void Resolve_capacidade_com_fallback_retorna_selecao_com_fallback()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot", FallbackModel = "modelo-copilot-fallback" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var selection = router.Resolve(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.Model.Should().Be("modelo-copilot");
        selection.FallbackProvider.Should().Be("ollama");
        selection.FallbackModel.Should().Be("modelo-copilot-fallback");
    }

    [Fact]
    public void Resolve_capacidade_inexistente_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var act = () => router.Resolve((AiCapability)999);

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Capacidade '999' não está configurada.");
    }

    [Fact]
    public void Resolve_modelo_default_vazio_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var act = () => router.Resolve(AiCapability.Copilot);

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Modelo default não configurado para a capacidade 'Copilot'.");
    }

    [Fact]
    public void Resolve_modelo_default_nulo_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = null! },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var act = () => router.Resolve(AiCapability.Copilot);

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Modelo default não configurado para a capacidade 'Copilot'.");
    }

    [Fact]
    public void Resolve_todas_as_capacidades_mapeadas_devem_retornar_selecao_valida()
    {
        var configuration = new AiRoutingOptionsConfiguration
        {
            Copilot = new() { DefaultModel = "modelo-copilot" },
            IncidentAnalysis = new() { DefaultModel = "modelo-incident" },
            KnowledgeGeneration = new() { DefaultModel = "modelo-knowledge" },
            Reporting = new() { DefaultModel = "modelo-reporting" },
            Automation = new() { DefaultModel = "modelo-automation" }
        };

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        foreach (var capability in Enum.GetValues<AiCapability>())
        {
            var selection = router.Resolve(capability);
            selection.Provider.Should().Be("ollama");
            selection.Model.Should().NotBeNullOrEmpty();
        }
    }

    // ── ADR-050 §2: provider por capacidade ─────────────────────────────

    [Fact]
    public void Resolve_com_provider_configurado_usa_o_provider_da_capacidade()
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

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        router.Resolve(AiCapability.Copilot).Provider.Should().Be("ollama");
        router.Resolve(AiCapability.IncidentAnalysis).Provider.Should().Be("openai");
    }

    [Fact]
    public void Resolve_com_fallback_provider_distinto_retorna_o_fallback_configurado()
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

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var selection = router.Resolve(AiCapability.Copilot);

        selection.Provider.Should().Be("ollama");
        selection.FallbackProvider.Should().Be("gemini");
        selection.FallbackModel.Should().Be("gemini-flash");
    }

    [Fact]
    public void Resolve_provider_inexistente_lanca_AiProviderNotConfiguredException()
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

        var router = new AiProviderRouter(registry, configuration);

        var act = () => router.Resolve(AiCapability.Copilot);

        act.Should().Throw<AiProviderNotConfiguredException>();
    }

    [Fact]
    public void Resolve_fallback_provider_inexistente_lanca_AiProviderNotConfiguredException()
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

        var router = new AiProviderRouter(registry, configuration);

        var act = () => router.Resolve(AiCapability.Copilot);

        act.Should().Throw<AiProviderNotConfiguredException>();
    }
}
