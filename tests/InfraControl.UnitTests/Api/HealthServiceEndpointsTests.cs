using FluentAssertions;
using InfraControl.Api.HealthChecks;
using Microsoft.Extensions.Configuration;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// Tela de status (F50): <c>Health:Services</c> aceita slots pré-declarados
/// no compose com valor vazio (serviços de profile opcional — docs/66 D7) —
/// entradas sem Name/Url são descartadas em vez de virarem probe morto.
/// </summary>
public sealed class HealthServiceEndpointsTests
{
    private static IReadOnlyList<HealthServiceEndpoint> Parse(
        params (string Key, string Value)[] entries)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                entries.ToDictionary(entry => entry.Key, entry => (string?)entry.Value))
            .Build();

        return HealthServiceEndpoints.FromConfiguration(configuration);
    }

    [Fact]
    public void FromConfiguration_ReturnsEmpty_WhenSectionMissing()
    {
        Parse().Should().BeEmpty();
    }

    [Fact]
    public void FromConfiguration_KeepsCompleteEntries()
    {
        var endpoints = Parse(
            ("Health:Services:0:Name", "frontend"),
            ("Health:Services:0:Url", "http://frontend/healthz"),
            ("Health:Services:0:Link", "https://localhost:8443"),
            ("Health:Services:1:Name", "rabbitmq"),
            ("Health:Services:1:Url", "http://rabbitmq:15672"));

        endpoints.Should().HaveCount(2);
        endpoints[0].Name.Should().Be("frontend");
        endpoints[0].Link.Should().Be("https://localhost:8443");
        endpoints[1].Link.Should().BeNull();
    }

    [Fact]
    public void FromConfiguration_SkipsBlankNameOrUrl()
    {
        // Slots do compose com ${VAR:-} vazio (profile obs desligado — D7).
        var endpoints = Parse(
            ("Health:Services:2:Name", ""),
            ("Health:Services:2:Url", ""),
            ("Health:Services:3:Name", "grafana"),
            ("Health:Services:3:Url", ""),
            ("Health:Services:4:Name", ""),
            ("Health:Services:4:Url", "http://prometheus:9090"),
            ("Health:Services:5:Name", "adminer"),
            ("Health:Services:5:Url", "http://adminer:8080"));

        endpoints.Should().ContainSingle();
        endpoints[0].Name.Should().Be("adminer");
    }

    [Fact]
    public void FromConfiguration_TrimsValues()
    {
        var endpoints = Parse(
            ("Health:Services:0:Name", "  seq  "),
            ("Health:Services:0:Url", " http://seq:80 "));

        endpoints.Should().ContainSingle();
        endpoints[0].Name.Should().Be("seq");
        endpoints[0].Url.Should().Be("http://seq:80");
    }
}
