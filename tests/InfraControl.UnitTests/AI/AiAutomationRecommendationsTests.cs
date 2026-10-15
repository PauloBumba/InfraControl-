using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Automation;
using InfraControl.Application.AI.Context;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// Fase F (ADR-055 · blueprint §38–§40): Automation Agent LEVEL 1 —
/// recomenda com sinais determinísticos, não executa; sem sinais devolve
/// lista vazia SEM chamar o LLM; contrato JSON tolerante.
/// </summary>
public sealed class AiAutomationRecommendationsTests
{
    private static readonly IncidentDto ActiveIncident = new(
        Id: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Title: "Link de falha no APP-02",
        Description: null,
        Severity: "Critical",
        Status: "Open",
        DeviceId: null,
        OpenedAt: new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc),
        ResolvedAt: null,
        AlertCount: 2,
        AlertIds: []);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IRetrievalService> _retrieval = new();
    private readonly Mock<IContextBuilder> _contextBuilder = new();
    private readonly Mock<IAiGateway> _gateway = new();

    public AiAutomationRecommendationsTests()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SearchIncidentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<IncidentDto>([], 0, 1, 5));
        _retrieval
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult([], 0));
        _contextBuilder
            .Setup(c => c.Build(It.IsAny<IReadOnlyList<RetrievalHit>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(EvidenceSet.Empty);
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                """{"items": [{"title": "Conferir enlace do APP-02", "rationale": "Incidente Critical aberto desde 08:00. Verificar status do link antes de agir.", "priority": "alta"}]}""",
                "ollama", "qwen2.5-coder:1.5b", 10, 150, false));
    }

    private GenerateAutomationRecommendationsCommandHandler Handler()
        => new(_mediator.Object, _retrieval.Object, _contextBuilder.Object, _gateway.Object);

    private static AiCompletionRequest CapturedRequest(Mock<IAiGateway> gateway)
        => gateway.Invocations
            .Select(i => i.Arguments[0])
            .OfType<AiCompletionRequest>()
            .Single();

    private void WithActiveIncidents()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SearchIncidentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<IncidentDto>([ActiveIncident], 1, 1, 5));
    }

    [Fact]
    public async Task Sem_sinais_devolve_lista_vazia_sem_chamar_o_llm()
    {
        var response = await Handler().Handle(
            new GenerateAutomationRecommendationsCommand(), CancellationToken.None);

        response.Items.Should().BeEmpty();
        response.Provider.Should().BeEmpty();
        _gateway.Verify(
            g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _retrieval.Verify(
            r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Com_sinais_usa_a_capacidade_Automation_e_devolve_os_itens()
    {
        WithActiveIncidents();

        var response = await Handler().Handle(
            new GenerateAutomationRecommendationsCommand(), CancellationToken.None);

        response.Items.Should().ContainSingle();
        response.Items[0].Title.Should().Be("Conferir enlace do APP-02");
        response.Items[0].Priority.Should().Be("alta");
        response.Provider.Should().Be("ollama");
        CapturedRequest(_gateway).Capability.Should().Be(AiCapability.Automation);
    }

    [Fact]
    public async Task Prompt_carrega_os_sinais_dos_incidentes_ativos()
    {
        WithActiveIncidents();

        await Handler().Handle(
            new GenerateAutomationRecommendationsCommand(), CancellationToken.None);

        var request = CapturedRequest(_gateway);
        request.Prompt.Should().Contain("Link de falha no APP-02");
        request.Prompt.Should().Contain("Critical").And.Contain("Open");
    }

    [Fact]
    public async Task System_prompt_e_LEVEL1_sem_execucao_com_guardrails()
    {
        WithActiveIncidents();

        await Handler().Handle(
            new GenerateAutomationRecommendationsCommand(), CancellationToken.None);

        var system = CapturedRequest(_gateway).SystemPrompt;
        system.Should().Contain("LEVEL 1");
        system.Should().Contain("NÃO executa");
        system.Should().Contain("NÃO descreve comandos");
        system.Should().Contain("§40");
        system.Should().Contain("apagar dados");
        system.Should().Contain("{\"items\"");
    }

    [Fact]
    public async Task Resposta_malformada_vai_para_o_fallback_com_prioridade_media()
    {
        WithActiveIncidents();
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                "texto corrido sem JSON", "ollama", "qwen2.5-coder:1.5b", 10, 50, false));

        var response = await Handler().Handle(
            new GenerateAutomationRecommendationsCommand(), CancellationToken.None);

        response.Items.Should().ContainSingle();
        response.Items[0].Title.Should().Contain("texto do modelo");
        response.Items[0].Priority.Should().Be("média");
        response.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Prioridade_e_normalizada_para_pt_BR()
    {
        WithActiveIncidents();
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                """{"items": [{"title": "A", "rationale": "motivo a", "priority": "high"}, {"title": "B", "rationale": "motivo b", "priority": "qualquer"}, {"title": "C", "rationale": "motivo c", "priority": "baixa"}]}""",
                "ollama", "qwen2.5-coder:1.5b", 10, 80, false));

        var response = await Handler().Handle(
            new GenerateAutomationRecommendationsCommand(), CancellationToken.None);

        response.Items.Should().HaveCount(3);
        response.Items[0].Priority.Should().Be("alta");
        response.Items[1].Priority.Should().Be("média");
        response.Items[2].Priority.Should().Be("baixa");
    }

    [Fact]
    public async Task Titulo_e_clampado_no_limite_do_contrato()
    {
        WithActiveIncidents();
        var longTitle = new string('x', 300);
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                $"{{\"items\": [{{\"title\": \"{longTitle}\", \"rationale\": \"motivo\", \"priority\": \"alta\"}}]}}",
                "ollama", "qwen2.5-coder:1.5b", 10, 80, false));

        var response = await Handler().Handle(
            new GenerateAutomationRecommendationsCommand(), CancellationToken.None);

        response.Items[0].Title.Should().HaveLength(120);
    }

    [Fact]
    public void RequiredRoles_e_Operate()
    {
        new GenerateAutomationRecommendationsCommand().RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }
}
