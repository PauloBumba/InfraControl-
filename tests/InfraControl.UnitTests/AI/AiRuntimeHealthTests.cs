using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Health;
using InfraControl.Infrastructure.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T006/S5 (docs/34 §44 · ADR-036): runtime health da camada de IA
/// — capacidade sem mapeamento = "não configurado" DEGRADADO, teste de
/// conexão real por provider (§13), e o IHealthCheck do host devolve
/// sempre Healthy/Degraded (NUNCA Unhealthy por falta de ambiente — o
/// sistema sobe sem Ollama). Sem Docker: fakes.
/// </summary>
public sealed class AiRuntimeHealthTests
{
    private static readonly AiProviderSelection Selection =
        new("ollama", "modelo-primario", "backup", "modelo-backup");

    [Fact]
    public async Task CheckAsync_tudo_mapeado_e_provider_ok_Healthy()
    {
        var provider = new FakeAiProvider("ollama");
        var backup = new FakeAiProvider("backup");
        var service = new AiRuntimeHealthService(
            new FakeAiRouter(Selection),
            new FakeAiRegistry(provider, backup));

        var report = await service.CheckAsync();

        report.IsHealthy.Should().BeTrue();
        report.Components.Should().HaveCount(5 + 2); // 5 capacidades + 2 providers
        report.Components.Should().OnlyContain(component =>
            component.Status == AiComponentStatus.Healthy);
        report.Components.Should().Contain(component =>
            component.Component == "provider:ollama" &&
            component.Detail == "conexão OK (§13)");
    }

    [Fact]
    public async Task CheckAsync_capacidade_sem_mapeamento_vira_Degraded_nao_configurado()
    {
        var service = new AiRuntimeHealthService(
            new FakeAiRouter(selection: null), // nenhuma capacidade mapeada (§44)
            new FakeAiRegistry(new FakeAiProvider("ollama")));

        var report = await service.CheckAsync();

        report.IsHealthy.Should().BeFalse();
        var capabilityLines = report.Components
            .Where(component => component.Component.StartsWith("capability:"))
            .ToList();
        capabilityLines.Should().HaveCount(5);
        capabilityLines.Should().OnlyContain(component =>
            component.Status == AiComponentStatus.Degraded &&
            component.Detail!.Contains("não está configurada"));
        report.Components.Should().NotContain(component =>
            component.Component.StartsWith("provider:"));
    }

    [Fact]
    public async Task CheckAsync_provider_inalcancavel_vira_Degraded()
    {
        var provider = new FakeAiProvider("ollama") { ConnectionOk = false };
        var service = new AiRuntimeHealthService(
            new FakeAiRouter(Selection),
            new FakeAiRegistry(provider));

        var report = await service.CheckAsync();

        report.IsHealthy.Should().BeFalse();
        report.Components.Should().Contain(component =>
            component.Component == "provider:ollama" &&
            component.Status == AiComponentStatus.Degraded &&
            component.Detail == "endpoint inalcançável (§13)");
        // as capacidades seguem mapeadas (só o provider degradou)
        report.Components.Should().Contain(component =>
            component.Component == "capability:Copilot" &&
            component.Status == AiComponentStatus.Healthy);
    }

    [Fact]
    public async Task CheckAsync_falha_inesperada_na_conexao_degrada_sem_derrubar()
    {
        var provider = new FakeAiProvider("ollama")
        {
            ConnectionFault = new InvalidOperationException("dns falhou"),
        };
        var service = new AiRuntimeHealthService(
            new FakeAiRouter(Selection),
            new FakeAiRegistry(provider));

        var report = await service.CheckAsync();

        report.IsHealthy.Should().BeFalse();
        report.Components.Should().Contain(component =>
            component.Component == "provider:ollama" &&
            component.Status == AiComponentStatus.Degraded &&
            component.Detail!.Contains("dns falhou"));
    }

    [Fact]
    public async Task HealthCheck_sem_ambiente_devolve_Degraded_e_payload_por_componente()
    {
        var service = new AiRuntimeHealthService(
            new FakeAiRouter(selection: null),
            new FakeAiRegistry());
        var check = new AiRuntimeHealthCheck(service);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data.Should().ContainKey("capability:Copilot");
        result.Description.Should().Contain("não configurado");
    }

    [Fact]
    public async Task HealthCheck_com_ambiente_configurado_devolve_Healthy()
    {
        var service = new AiRuntimeHealthService(
            new FakeAiRouter(Selection),
            new FakeAiRegistry(new FakeAiProvider("ollama"), new FakeAiProvider("backup")));
        var check = new AiRuntimeHealthCheck(service);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("provider:ollama");
    }
}
