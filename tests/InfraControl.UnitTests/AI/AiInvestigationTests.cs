using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Context;
using InfraControl.Application.AI.Investigation;
using InfraControl.Application.AI.Tools;
using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// Fase C (ADR-053): testes da investigação de incidente — grounding,
/// capacidade IncidentAnalysis, LEVEL 0 e tool get_incident.
/// </summary>
public sealed class AiInvestigationTests
{
    private static readonly IncidentDto SampleIncident = new(
        Id: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Title: "Link de falha no APP-02",
        Description: "Perda de pacotes no link principal desde as 08:00.",
        Severity: "Critical",
        Status: "Investigating",
        DeviceId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
        OpenedAt: new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc),
        ResolvedAt: null,
        AlertCount: 3,
        AlertIds: []);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IRetrievalService> _retrieval = new();
    private readonly Mock<IContextBuilder> _contextBuilder = new();
    private readonly Mock<IAiGateway> _gateway = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();

    public AiInvestigationTests()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetIncidentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleIncident);
        _retrieval
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult([], 0));
        _contextBuilder
            .Setup(c => c.Build(It.IsAny<IReadOnlyList<RetrievalHit>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(EvidenceSet.Empty);
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion("análise grounded", "ollama", "qwen2.5-coder:1.5b", 10, 30, false));
        _currentUser.Setup(c => c.UserName).Returns("ana@exemplo.com");
    }

    private InvestigateIncidentCommandHandler Handler() =>
        new(_mediator.Object, _retrieval.Object, _contextBuilder.Object, _gateway.Object, _currentUser.Object);

    private static AiCompletionRequest CapturedRequest(Mock<IAiGateway> gateway) =>
        gateway.Invocations
            .Select(i => i.Arguments[0])
            .OfType<AiCompletionRequest>()
            .Single();

    [Fact]
    public async Task Handle_devolve_a_analise_do_gateway()
    {
        var response = await Handler().Handle(
            new InvestigateIncidentCommand(SampleIncident.Id), CancellationToken.None);

        response.Analysis.Should().Be("análise grounded");
        response.Provider.Should().Be("ollama");
        response.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_usa_a_capacidade_IncidentAnalysis()
    {
        await Handler().Handle(
            new InvestigateIncidentCommand(SampleIncident.Id), CancellationToken.None);

        CapturedRequest(_gateway).Capability.Should().Be(AiCapability.IncidentAnalysis);
    }

    [Fact]
    public async Task Handle_prompt_inclui_os_dados_do_incidente()
    {
        await Handler().Handle(
            new InvestigateIncidentCommand(SampleIncident.Id), CancellationToken.None);

        var request = CapturedRequest(_gateway);
        request.Prompt.Should().Contain("Link de falha no APP-02");
        request.Prompt.Should().Contain("Critical");
        request.Prompt.Should().Contain("Perda de pacotes");
        request.Prompt.Should().Contain("Alerts vinculados: 3");
    }

    [Fact]
    public async Task Handle_system_prompt_e_level0_com_hipoteses_e_sem_acao()
    {
        await Handler().Handle(
            new InvestigateIncidentCommand(SampleIncident.Id), CancellationToken.None);

        var system = CapturedRequest(_gateway).SystemPrompt;
        system.Should().Contain("LEVEL 0");
        system.Should().Contain("Probable");
        system.Should().Contain("NUNCA invente");
        system.Should().Contain("NÃO recomende");
    }

    [Fact]
    public async Task Handle_recupera_contexto_do_titulo_e_descricao()
    {
        await Handler().Handle(
            new InvestigateIncidentCommand(SampleIncident.Id), CancellationToken.None);

        _retrieval.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalRequest>(req =>
                req.Term.Contains("Link de falha no APP-02")
                && req.Term.Contains("Perda de pacotes")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_devolve_evidencias_mapeadas()
    {
        var evidence = new Evidence(
            Guid.NewGuid(), EvidenceSource.Knowledge, "article",
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            "Procedimento de link redundante",
            EvidenceConfidence.Possible,
            "Artigo #7 — Link principal",
            Tool: null,
            TraceId: null);
        _contextBuilder
            .Setup(c => c.Build(It.IsAny<IReadOnlyList<RetrievalHit>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new EvidenceSet([evidence]));

        var response = await Handler().Handle(
            new InvestigateIncidentCommand(SampleIncident.Id), CancellationToken.None);

        response.Evidences.Should().ContainSingle();
        response.Evidences[0].Citation.Should().Be("Artigo #7 — Link principal");
        response.Evidences[0].Confidence.Should().Be("Possible");
    }

    [Fact]
    public async Task Handle_incidente_inexistente_propaga_KeyNotFound()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetIncidentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Incidente não encontrado."));

        var act = () => Handler().Handle(
            new InvestigateIncidentCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void RequiredRoles_e_Operate()
    {
        var command = new InvestigateIncidentCommand(Guid.NewGuid());

        command.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
    }

    // ── Tool get_incident (ADR-053 §4) ──────────────────────────────────

    [Fact]
    public async Task Tool_get_incident_devolve_o_incidente_como_finding()
    {
        var tool = new GetIncidentTool(_mediator.Object);

        var result = await tool.ExecuteAsync(
            AiToolArguments.From(("id", SampleIncident.Id.ToString())),
            CancellationToken.None);

        result.ToolName.Should().Be("get_incident");
        result.Total.Should().Be(1);
        var finding = result.Findings.Single();
        finding.Title.Should().Be("Link de falha no APP-02");
        finding.Status.Should().Be("Investigating");
        finding.Detail.Should().Contain("Critical");
    }

    [Fact]
    public async Task Tool_get_incident_sem_id_lanca_AiToolArgumentException()
    {
        var tool = new GetIncidentTool(_mediator.Object);

        var act = () => tool.ExecuteAsync(AiToolArguments.Empty, CancellationToken.None);

        (await act.Should().ThrowAsync<AiToolArgumentException>())
            .WithMessage("*'id'*");
    }

    [Fact]
    public async Task Tool_get_incident_id_invalido_lanca_AiToolArgumentException()
    {
        var tool = new GetIncidentTool(_mediator.Object);

        var act = () => tool.ExecuteAsync(
            AiToolArguments.From(("id", "nao-e-um-guid")), CancellationToken.None);

        await act.Should().ThrowAsync<AiToolArgumentException>();
    }
}
