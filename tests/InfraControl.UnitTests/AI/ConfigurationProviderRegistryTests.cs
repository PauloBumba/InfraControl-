using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Exceptions;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T005/S4 (ADR-037 §2): testes do ConfigurationProviderRegistry
/// — registro v1 de providers por configuração/env.
/// </summary>
public sealed class ConfigurationProviderRegistryTests
{
    [Fact]
    public void Get_provider_ollama_existente_retorna_provider()
    {
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.Setup(p => p.Name).Returns("ollama");

        var registry = new ConfigurationProviderRegistry(mockProvider.Object);

        var provider = registry.Get("ollama");

        provider.Should().BeSameAs(mockProvider.Object);
    }

    [Fact]
    public void Get_provider_ollama_case_insensitive_retorna_provider()
    {
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.Setup(p => p.Name).Returns("ollama");

        var registry = new ConfigurationProviderRegistry(mockProvider.Object);

        var provider = registry.Get("OLLAMA");

        provider.Should().BeSameAs(mockProvider.Object);
    }

    [Fact]
    public void Get_provider_inexistente_lanca_AiProviderNotConfiguredException()
    {
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.Setup(p => p.Name).Returns("ollama");

        var registry = new ConfigurationProviderRegistry(mockProvider.Object);

        var act = () => registry.Get("openai");

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Provider 'openai' não está configurado.");
    }

    [Fact]
    public void Get_provider_null_lanca_AiProviderNotConfiguredException()
    {
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.Setup(p => p.Name).Returns("ollama");

        var registry = new ConfigurationProviderRegistry(mockProvider.Object);

        var act = () => registry.Get((string)null!);

        act.Should().Throw<AiProviderNotConfiguredException>();
    }
}
