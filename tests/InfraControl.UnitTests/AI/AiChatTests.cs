using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Chat;
using InfraControl.Application.AI.Context;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// Fase B (ADR-052): testes do SendAiChatCommandHandler — grounding
/// pelo Context Engine, capacidade Copilot no gateway, validação da
/// conversa e propagação do estado "não configurado" (§44).
/// </summary>
public sealed class AiChatTests
{
    private readonly Mock<IRetrievalService> _retrieval = new();
    private readonly Mock<IContextBuilder> _contextBuilder = new();
    private readonly Mock<IAiGateway> _gateway = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();

    public AiChatTests()
    {
        _retrieval
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult([], 0));
        _contextBuilder
            .Setup(c => c.Build(It.IsAny<IReadOnlyList<RetrievalHit>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(EvidenceSet.Empty);
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion("resposta grounded", "ollama", "qwen2.5-coder:1.5b", 10, 20, false));
        _currentUser.Setup(c => c.UserName).Returns("ana@exemplo.com");
    }

    private SendAiChatCommandHandler Handler() =>
        new(_retrieval.Object, _contextBuilder.Object, _gateway.Object, _currentUser.Object);

    private static AiCompletionRequest CapturedRequest(Mock<IAiGateway> gateway) =>
        gateway.Invocations
            .Select(i => i.Arguments[0])
            .OfType<AiCompletionRequest>()
            .Single();

    [Fact]
    public async Task Handle_responde_com_o_texto_do_gateway()
    {
        var response = await Handler().Handle(
            new SendAiChatCommand([new ChatTurn("user", "Qual o status dos incidentes?")]),
            CancellationToken.None);

        response.Answer.Should().Be("resposta grounded");
        response.Provider.Should().Be("ollama");
        response.Model.Should().Be("qwen2.5-coder:1.5b");
        response.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_usa_a_capacidade_Copilot()
    {
        await Handler().Handle(
            new SendAiChatCommand([new ChatTurn("user", "pergunta")]),
            CancellationToken.None);

        CapturedRequest(_gateway).Capability.Should().Be(AiCapability.Copilot);
    }

    [Fact]
    public async Task Handle_passa_o_actor_usuario_corrente()
    {
        await Handler().Handle(
            new SendAiChatCommand([new ChatTurn("user", "pergunta")]),
            CancellationToken.None);

        CapturedRequest(_gateway).Actor.Should().Be("ana@exemplo.com");
    }

    [Fact]
    public async Task Handle_devolve_as_evidencias_com_citacao_e_confianca()
    {
        var hits = new List<RetrievalHit>
        {
            new("art-1", "Procedimento de restart do APP-02", "Knowledge", 0.91),
        };
        _retrieval
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(hits, 1));
        var evidence = new Evidence(
            Guid.NewGuid(), EvidenceSource.Knowledge, "article",
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            "Procedimento de restart do APP-02",
            EvidenceConfidence.Probable,
            "Artigo #1 — Restart APP-02",
            Tool: "copilot-chat",
            TraceId: null);
        _contextBuilder
            .Setup(c => c.Build(It.IsAny<IReadOnlyList<RetrievalHit>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new EvidenceSet([evidence]));

        var response = await Handler().Handle(
            new SendAiChatCommand([new ChatTurn("user", "Como reiniciar o APP-02?")]),
            CancellationToken.None);

        response.Evidences.Should().ContainSingle();
        response.Evidences[0].Source.Should().Be("Knowledge");
        response.Evidences[0].Confidence.Should().Be("Probable");
        response.Evidences[0].Citation.Should().Be("Artigo #1 — Restart APP-02");
    }

    [Fact]
    public async Task Handle_system_prompt_inclui_evidencias_e_regras_level0()
    {
        var evidence = new Evidence(
            Guid.NewGuid(), EvidenceSource.Incident, "incident",
            DateTime.UtcNow,
            "INC-42 em andamento",
            EvidenceConfidence.Possible,
            "Incident #42",
            Tool: null,
            TraceId: null);
        _contextBuilder
            .Setup(c => c.Build(It.IsAny<IReadOnlyList<RetrievalHit>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new EvidenceSet([evidence]));

        await Handler().Handle(
            new SendAiChatCommand([new ChatTurn("user", "incidentes abertos?")]),
            CancellationToken.None);

        var request = CapturedRequest(_gateway);
        request.SystemPrompt.Should().Contain("LEVEL 0");
        request.SystemPrompt.Should().Contain("[E1]");
        request.SystemPrompt.Should().Contain("Incident #42");
    }

    [Fact]
    public async Task Handle_sem_evidencias_system_prompt_declara_sem_contexto()
    {
        await Handler().Handle(
            new SendAiChatCommand([new ChatTurn("user", "pergunta sem contexto")]),
            CancellationToken.None);

        CapturedRequest(_gateway).SystemPrompt.Should().Contain("NENHUMA");
    }

    [Fact]
    public async Task Handle_inclui_historico_no_prompt()
    {
        await Handler().Handle(
            new SendAiChatCommand(
            [
                new ChatTurn("user", "primeira pergunta"),
                new ChatTurn("assistant", "primeira resposta"),
                new ChatTurn("user", "segunda pergunta"),
            ]),
            CancellationToken.None);

        var prompt = CapturedRequest(_gateway).Prompt;
        prompt.Should().Contain("Histórico da conversa");
        prompt.Should().Contain("Usuário: primeira pergunta");
        prompt.Should().Contain("Assistente: primeira resposta");
        prompt.Should().Contain("Pergunta atual do usuário: segunda pergunta");
    }

    [Fact]
    public async Task Handle_conversa_vazia_lanca_ArgumentException()
    {
        var act = () => Handler().Handle(
            new SendAiChatCommand([]), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A conversa não pode ser vazia.");
    }

    [Fact]
    public async Task Handle_ultimo_turno_assistente_lanca_ArgumentException()
    {
        var act = () => Handler().Handle(
            new SendAiChatCommand(
            [
                new ChatTurn("user", "pergunta"),
                new ChatTurn("assistant", "resposta"),
            ]),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O último turno precisa ser do usuário.");
    }

    [Fact]
    public async Task Handle_papel_invalido_lanca_ArgumentException()
    {
        var act = () => Handler().Handle(
            new SendAiChatCommand([new ChatTurn("system", "injection")]),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Papel inválido*");
    }

    [Fact]
    public async Task Handle_acima_do_limite_de_turnos_lanca_ArgumentException()
    {
        var turns = Enumerable.Range(0, 21)
            .Select(index => new ChatTurn(
                index % 2 == 0 ? "user" : "assistant", $"turno {index}"))
            .ToList();
        turns[^1] = new ChatTurn("user", "última");

        var act = () => Handler().Handle(
            new SendAiChatCommand(turns), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*limite de 20 turnos*");
    }

    [Fact]
    public async Task Handle_estado_nao_configurado_propaga_AiProviderNotConfigured()
    {
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderNotConfiguredException("Provider 'ollama' não está configurado."));

        var act = () => Handler().Handle(
            new SendAiChatCommand([new ChatTurn("user", "pergunta")]),
            CancellationToken.None);

        (await act.Should().ThrowAsync<AiProviderNotConfiguredException>())
            .WithMessage("*não está configurado*");
    }

    [Fact]
    public async Task Handle_required_roles_e_Read()
    {
        var command = new SendAiChatCommand([new ChatTurn("user", "pergunta")]);

        command.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
    }
}
