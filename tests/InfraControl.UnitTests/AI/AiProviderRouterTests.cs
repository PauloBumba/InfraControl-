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
        var configuration = new AiRoutingOptionsConfiguration(
            Copilot: new("modelo-copilot"),
            IncidentAnalysis: new("modelo-incident"),
            KnowledgeGeneration: new("modelo-knowledge"),
            Reporting: new("modelo-reporting"),
            Automation: new("modelo-automation"));

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
        var configuration = new AiRoutingOptionsConfiguration(
            Copilot: new("modelo-copilot", "modelo-copilot-fallback"),
            IncidentAnalysis: new("modelo-incident"),
            KnowledgeGeneration: new("modelo-knowledge"),
            Reporting: new("modelo-reporting"),
            Automation: new("modelo-automation"));

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
        var configuration = new AiRoutingOptionsConfiguration(
            Copilot: new("modelo-copilot"),
            IncidentAnalysis: new("modelo-incident"),
            KnowledgeGeneration: new("modelo-knowledge"),
            Reporting: new("modelo-reporting"),
            Automation: new("modelo-automation"));

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var act = () => router.Resolve((AiCapability)999);

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Capacidade '999' não está configurada.");
    }

    [Fact]
    public void Resolve_modelo_default_vazio_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration(
            Copilot: new(""),
            IncidentAnalysis: new("modelo-incident"),
            KnowledgeGeneration: new("modelo-knowledge"),
            Reporting: new("modelo-reporting"),
            Automation: new("modelo-automation"));

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var act = () => router.Resolve(AiCapability.Copilot);

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Modelo default não configurado para a capacidade 'Copilot'.");
    }

    [Fact]
    public void Resolve_modelo_default_nulo_lanca_AiProviderNotConfiguredException()
    {
        var configuration = new AiRoutingOptionsConfiguration(
            Copilot: new((string)null!),
            IncidentAnalysis: new("modelo-incident"),
            KnowledgeGeneration: new("modelo-knowledge"),
            Reporting: new("modelo-reporting"),
            Automation: new("modelo-automation"));

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        var act = () => router.Resolve(AiCapability.Copilot);

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Modelo default não configurado para a capacidade 'Copilot'.");
    }

    [Fact]
    public void Resolve_todas_as_capacidades_mapeadas_devem_retornar_selecao_valida()
    {
        var configuration = new AiRoutingOptionsConfiguration(
            Copilot: new("modelo-copilot"),
            IncidentAnalysis: new("modelo-incident"),
            KnowledgeGeneration: new("modelo-knowledge"),
            Reporting: new("modelo-reporting"),
            Automation: new("modelo-automation"));

        var router = new AiProviderRouter(_mockRegistry.Object, configuration);

        foreach (var capability in Enum.GetValues<AiCapability>())
        {
            var selection = router.Resolve(capability);
            selection.Provider.Should().Be("ollama");
            selection.Model.Should().NotBeNullOrEmpty();
        }
    }
}
