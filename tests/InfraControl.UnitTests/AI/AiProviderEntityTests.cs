using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.AI;

public sealed class AiProviderEntityTests
{
    [Fact]
    public void Create_Should_Normalize_And_Defaults_Active()
    {
        var provider = AiProvider.Create(
            "  ollama ",
            "http://localhost:11434",
            apiKeyEncrypted: null,
            models: [" llama3.2 ", "llama3.2", " qwen "]);

        provider.Name.Should().Be("ollama");
        provider.BaseUrl.Should().Be("http://localhost:11434");
        provider.ApiKeyEncrypted.Should().BeNull();
        provider.IsActive.Should().BeTrue();
        provider.Models.Should().Equal("llama3.2", "qwen");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Reject_Blank_Name(string? name)
    {
        var act = () => AiProvider.Create(name, "http://localhost:11434");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("localhost:11434")]
    [InlineData("ftp://example.com")]
    [InlineData("not a url")]
    public void Create_Should_Reject_Non_Http_BaseUrl(string baseUrl)
    {
        var act = () => AiProvider.Create("ollama", baseUrl);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Should_Reject_Too_Long_Name()
    {
        var act = () => AiProvider.Create(new string('x', 65), "http://localhost:11434");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateApiKey_Null_Should_Keep_Existing_Key()
    {
        var provider = AiProvider.Create(
            "openai-compatible", "https://api.example.com", "v1:abc");

        provider.UpdateApiKey(apiKeyEncrypted: null);

        provider.ApiKeyEncrypted.Should().Be("v1:abc");
    }

    [Fact]
    public void UpdateApiKey_NewValue_Should_Replace()
    {
        var provider = AiProvider.Create(
            "openai-compatible", "https://api.example.com", "v1:old");

        provider.UpdateApiKey("v1:new");

        provider.ApiKeyEncrypted.Should().Be("v1:new");
    }

    [Fact]
    public void Deactivate_Then_Activate_Should_Toggle_IsActive()
    {
        var provider = AiProvider.Create("ollama", "http://localhost:11434");

        provider.Deactivate();
        provider.IsActive.Should().BeFalse();

        provider.Activate();
        provider.IsActive.Should().BeTrue();
        provider.Models.Should().BeEmpty();
    }
}

public sealed class AiProviderCapabilityEntityTests
{
    [Fact]
    public void Create_Should_Store_Route()
    {
        var providerId = Guid.NewGuid();
        var fallbackProviderId = Guid.NewGuid();

        var route = AiProviderCapability.Create(
            "copilot", providerId, "llama3.2", fallbackProviderId, "qwen");

        route.Capability.Should().Be("copilot");
        route.ProviderId.Should().Be(providerId);
        route.DefaultModel.Should().Be("llama3.2");
        route.FallbackProviderId.Should().Be(fallbackProviderId);
        route.FallbackModel.Should().Be("qwen");
    }

    [Fact]
    public void Create_Should_Reject_FallbackModel_Without_FallbackProvider()
    {
        var act = () => AiProviderCapability.Create(
            "copilot", Guid.NewGuid(), "llama3.2",
            fallbackProviderId: null, fallbackModel: "qwen");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Should_Reject_Empty_DefaultModel()
    {
        var act = () => AiProviderCapability.Create(
            "copilot", Guid.NewGuid(), "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Should_Reject_Empty_ProviderId()
    {
        var act = () => AiProviderCapability.Create(
            "copilot", Guid.Empty, "llama3.2");

        act.Should().Throw<ArgumentException>();
    }
}
