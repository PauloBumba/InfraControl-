using FluentAssertions;
using InfraControl.Application.AI;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T005/S4 (ADR-037 §2) + ADR-050 §1: testes do
/// ConfigurationProviderRegistry — registro v1 multi-provider por
/// configuração/env, resolução por Name case-insensitive.
/// </summary>
public sealed class ConfigurationProviderRegistryTests
{
    private static IAiProvider Provider(string name)
    {
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.Setup(p => p.Name).Returns(name);
        return mockProvider.Object;
    }

    [Fact]
    public void Get_provider_ollama_existente_retorna_provider()
    {
        var registry = new ConfigurationProviderRegistry(
            [Provider("ollama"), Provider("openai"), Provider("gemini")]);

        var provider = registry.Get("ollama");

        provider.Name.Should().Be("ollama");
    }

    [Fact]
    public void Get_provider_externo_retorna_o_provider_correspondente()
    {
        var registry = new ConfigurationProviderRegistry(
            [Provider("ollama"), Provider("openai"), Provider("gemini")]);

        registry.Get("openai").Name.Should().Be("openai");
        registry.Get("gemini").Name.Should().Be("gemini");
    }

    [Fact]
    public void Get_provider_ollama_case_insensitive_retorna_provider()
    {
        var registry = new ConfigurationProviderRegistry(
            [Provider("ollama"), Provider("openai")]);

        var provider = registry.Get("OLLAMA");

        provider.Name.Should().Be("ollama");
    }

    [Fact]
    public void Get_provider_inexistente_lanca_AiProviderNotConfiguredException()
    {
        var registry = new ConfigurationProviderRegistry([Provider("ollama")]);

        var act = () => registry.Get("grok");

        act.Should().Throw<AiProviderNotConfiguredException>()
            .WithMessage("Provider 'grok' não está configurado.");
    }

    [Fact]
    public void Get_provider_null_lanca_AiProviderNotConfiguredException()
    {
        var registry = new ConfigurationProviderRegistry([Provider("ollama")]);

        var act = () => registry.Get((string)null!);

        act.Should().Throw<AiProviderNotConfiguredException>();
    }

    [Fact]
    public void Get_sem_providers_lanca_AiProviderNotConfiguredException()
    {
        var registry = new ConfigurationProviderRegistry([]);

        var act = () => registry.Get("ollama");

        act.Should().Throw<AiProviderNotConfiguredException>();
    }
}
