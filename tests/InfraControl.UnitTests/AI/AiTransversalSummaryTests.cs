using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Reporting;
using InfraControl.Application.Reports.Transversal;
using InfraControl.Application.Reports.Transversal.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// Fase E (docs/34 §37): resumo IA do relatório transversal — capacidade
/// Reporting, grounding nos números do read-model (ADR-049) e LEVEL 0.
/// </summary>
public sealed class AiTransversalSummaryTests
{
    private static readonly TransversalReportDto SampleReport = new(
        From: new DateOnly(2026, 1, 1),
        To: new DateOnly(2026, 6, 30),
        Pdi: new PdiReportBlock(12, new Dictionary<string, int> { ["Completed"] = 5, ["Active"] = 7 }, 5),
        Tasks: new TaskReportBlock(40, new Dictionary<string, int> { ["Closed"] = 20, ["Open"] = 20 }, 20, 3),
        Knowledge: new KnowledgeReportBlock(10, 8, 2),
        Projects: new ProjectReportBlock(4, new Dictionary<string, int> { ["InProgress"] = 2, ["Completed"] = 2 }),
        Incidents: new IncidentReportBlock(9, 6, 2.5));

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IAiGateway> _gateway = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();

    public AiTransversalSummaryTests()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetTransversalReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleReport);
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                "## Leitura geral\nOperação estável.", "ollama", "qwen2.5-coder:1.5b", 10, 120, false));
        _currentUser.Setup(c => c.UserName).Returns("ana@exemplo.com");
    }

    private SummarizeTransversalReportCommandHandler Handler()
        => new(_mediator.Object, _gateway.Object, _currentUser.Object);

    private static AiCompletionRequest CapturedRequest(Mock<IAiGateway> gateway)
        => gateway.Invocations
            .Select(i => i.Arguments[0])
            .OfType<AiCompletionRequest>()
            .Single();

    [Fact]
    public async Task Handle_devolve_o_resumo_do_gateway()
    {
        var response = await Handler().Handle(
            new SummarizeTransversalReportCommand(null, null), CancellationToken.None);

        response.Summary.Should().Be("## Leitura geral\nOperação estável.");
        response.Provider.Should().Be("ollama");
        response.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_usa_a_capacidade_Reporting()
    {
        await Handler().Handle(
            new SummarizeTransversalReportCommand(null, null), CancellationToken.None);

        CapturedRequest(_gateway).Capability.Should().Be(AiCapability.Reporting);
    }

    [Fact]
    public async Task Handle_prompt_carrega_o_periodo_e_os_numeros_do_read_model()
    {
        await Handler().Handle(
            new SummarizeTransversalReportCommand(
                new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            CancellationToken.None);

        var request = CapturedRequest(_gateway);
        request.Prompt.Should().Contain("2026-01-01").And.Contain("2026-06-30");
        request.Prompt.Should().Contain("overdue").And.Contain("3");
        request.Prompt.Should().Contain("averageResolutionHours").And.Contain("2.5");
        _mediator.Verify(m => m.Send(
            It.Is<GetTransversalReportQuery>(q =>
                q.From == new DateOnly(2026, 1, 1) && q.To == new DateOnly(2026, 6, 30)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_system_prompt_e_level0_com_numeros_somente()
    {
        await Handler().Handle(
            new SummarizeTransversalReportCommand(null, null), CancellationToken.None);

        var system = CapturedRequest(_gateway).SystemPrompt;
        system.Should().Contain("LEVEL 0");
        system.Should().Contain("SOMENTE os números");
        system.Should().Contain("NÃO recomende nem execute");
        system.Should().Contain("Não invente causas");
    }

    [Fact]
    public void RequiredRoles_e_Operate()
    {
        new SummarizeTransversalReportCommand(null, null).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }
}
