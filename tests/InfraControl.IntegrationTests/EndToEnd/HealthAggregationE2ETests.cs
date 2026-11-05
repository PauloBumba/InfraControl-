using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InfraControl.IntegrationTests.EndToEnd;

/// <summary>
/// F46-T006 — agregado de saúde: o /health cobre banco, cache, IA, backlog
/// do outbox (saúde dos workers) e — quando configurados via Health:Services
/// — cada serviço irmão da stack com link de console para a HealthPage.
/// </summary>
public sealed class HealthAggregationE2ETests(E2EHost host) : IClassFixture<E2EHost>
{
    [Fact]
    public async Task Health_ShouldCoverCoreChecks_WithNoBacklog()
    {
        var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/health");
        // 503 é legítimo no host E2E: o check masstransit-bus exige o
        // RabbitMQ real (só existe no compose) — o agregado desce para
        // Unhealthy, mas os checks individuais continuam respondendo.
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = json.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .ToArray();

        checks.Should().Contain(["postgresql", "redis", "outbox-backlog"],
            "banco, cache e backlog do outbox (workers) precisam estar cobertos");

        // Sem Health:Services no host de teste, os checks de serviços irmãos
        // simplesmente não existem — o agregado continua válido.
        var outbox = json.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "outbox-backlog");
        outbox.GetProperty("status").GetString().Should().Be("Healthy",
            "host E2E não tem dispatcher rodando — o backlog deve estar zerado");
    }
}
