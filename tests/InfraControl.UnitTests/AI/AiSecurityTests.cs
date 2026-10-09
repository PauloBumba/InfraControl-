using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.Common.Interfaces;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T007/S6 (ADR-040 · docs/34 §41/§54/§55): limites do gateway —
/// prompt acima de <c>MaxPromptCharacters</c> é recusado ANTES de
/// chamar o provider e é auditado; <c>MaxCompletionTokens</c> do
/// gateway vira <c>Generation.MaxTokens</c> quando o chamador não
/// define geração (o provider mapeia para <c>num_predict</c>); o
/// motivo da troca (§54) fica em <c>FallbackReason</c> e o ator (§41)
/// propaga à auditoria. Sem Docker: fakes.
/// </summary>
public sealed class AiSecurityTests
{
    private static (AiGateway Gateway, FakeAiProvider Provider, FakeAuditSink Audit)
        BuildGateway(AiProviderSelection selection, AiGatewayOptions? options = null)
    {
        var provider = new FakeAiProvider(selection.Provider);
        var backup = new FakeAiProvider(selection.FallbackProvider ?? "backup");
        var audit = new FakeAuditSink();
        var gateway = new AiGateway(
            new FakeAiRouter(selection),
            new FakeAiRegistry(provider, backup),
            new FakeResilienceExecutor(),
            audit,
            options);
        return (gateway, provider, audit);
    }

    [Fact]
    public async Task CompleteAsync_prompt_acima_do_limite_e_recusado_antes_do_provider_e_auditado()
    {
        var (gateway, provider, audit) = BuildGateway(
            new AiProviderSelection("ollama", "modelo-primario"),
            new AiGatewayOptions(MaxPromptCharacters: 10));

        var act = async () => await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "este prompt tem bem mais de dez caracteres"));

        var failure = await act.Should().ThrowAsync<AiGatewayException>();
        failure.Which.Message.Should().Contain("excede o limite de 10 caracteres");
        provider.Calls.Should().BeEmpty("nenhum token deve ser gasto acima do limite §55");

        var entry = audit.Entries.Should().ContainSingle().Which;
        entry.Success.Should().BeFalse();
        entry.Error.Should().Contain("excede o limite");
    }

    [Fact]
    public async Task CompleteAsync_aplica_MaxCompletionTokens_do_gateway_quando_chamador_nao_define()
    {
        var (gateway, provider, _) = BuildGateway(
            new AiProviderSelection("ollama", "modelo-primario"),
            new AiGatewayOptions(MaxCompletionTokens: 256));

        await gateway.CompleteAsync(new AiCompletionRequest(AiCapability.Reporting, "relatório"));

        provider.Calls.Should().ContainSingle();
        provider.Calls[0].Generation.Should().NotBeNull();
        provider.Calls[0].Generation!.MaxTokens.Should().Be(256);
    }

    [Fact]
    public async Task CompleteAsync_respeita_Generation_do_chamador_sobre_o_cap_do_gateway()
    {
        var (gateway, provider, _) = BuildGateway(
            new AiProviderSelection("ollama", "modelo-primario"),
            new AiGatewayOptions(MaxCompletionTokens: 256));

        await gateway.CompleteAsync(new AiCompletionRequest(
            AiCapability.Copilot, "prompt",
            Generation: new AiGenerationOptions(MaxTokens: 999)));

        provider.Calls[0].Generation!.MaxTokens.Should().Be(999);
    }

    [Fact]
    public async Task CompleteAsync_fallback_registra_motivo_da_troca_54_e_actor_41()
    {
        var selection = new AiProviderSelection(
            "ollama", "modelo-primario", "backup", "modelo-backup");
        var (gateway, provider, audit) = BuildGateway(selection);
        provider.Fault = new TimeoutException("primário caiu no timeout");

        var completion = await gateway.CompleteAsync(new AiCompletionRequest(
            AiCapability.IncidentAnalysis, "analise", Actor: "maria", TraceId: "t-64"));

        completion.UsedFallback.Should().BeTrue();
        var entry = audit.Entries.Should().ContainSingle().Which;
        entry.UsedFallback.Should().BeTrue();
        entry.FallbackReason.Should().Contain("primário caiu no timeout");
        entry.Actor.Should().Be("maria");
        entry.TraceId.Should().Be("t-64");
        entry.Success.Should().BeTrue();
    }
}
