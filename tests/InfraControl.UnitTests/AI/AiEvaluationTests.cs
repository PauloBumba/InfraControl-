using FluentAssertions;
using InfraControl.Application.AI.Evaluation;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T010/S9 (docs/34 §45 — AI Evaluation Tests): harness de
/// avaliação com os três checks que não dependem de LLM (tool
/// selection, authorization, grounding/citations) — reprova com motivo
/// por check; a execução contra modelo real fica pendente de ambiente
/// (§12) e entra via <see cref="IEvaluationScenario"/> sem mudar o
/// harness. Sem Docker: cenário fake.
/// </summary>
public sealed class AiEvaluationTests
{
    private static EvaluationHarness Harness() =>
        new([new ToolSelectionCheck(), new AuthorizationCheck(), new GroundingCheck()]);

    private sealed class FakeScenario(EvaluationOutcome outcome) : IEvaluationScenario
    {
        public List<EvaluationCase> ExecutedCases { get; } = [];

        public Task<EvaluationOutcome> RunAsync(
            EvaluationCase evaluationCase,
            CancellationToken cancellationToken = default)
        {
            ExecutedCases.Add(evaluationCase);
            return Task.FromResult(outcome);
        }
    }

    private static EvaluationCase Question(
        string id = "c1",
        IReadOnlyList<string>? expectedTools = null,
        IReadOnlyList<string>? forbiddenTools = null,
        bool expectDenied = false,
        int minEvidence = 0,
        IReadOnlyList<string>? citations = null) =>
        new(
            id,
            "por que o link caiu?",
            ExpectedTools: expectedTools,
            ForbiddenTools: forbiddenTools,
            ExpectDenied: expectDenied,
            MinEvidenceCount: minEvidence,
            RequiredCitations: citations);

    [Fact]
    public async Task Run_caso_perfeito_aprova_todos_os_checks()
    {
        var scenario = new FakeScenario(new EvaluationOutcome(
            ToolsExecuted: ["get_zabbix_problems", "search_knowledge"],
            EvidenceCount: 3,
            Citations: ["Knowledge#a1"],
            Denied: false));

        var result = await Harness().RunAsync(
            [Question(
                expectedTools: ["get_zabbix_problems"],
                minEvidence: 2,
                citations: ["Knowledge#a1"])],
            scenario);

        result.AllPassed.Should().BeTrue();
        result.PassedCount.Should().Be(1);
    }

    [Fact]
    public async Task Run_ferramenta_errada_reprova_tool_selection_com_motivo()
    {
        var scenario = new FakeScenario(new EvaluationOutcome(
            ToolsExecuted: ["get_open_tasks"]));

        var result = await Harness().RunAsync(
            [Question(expectedTools: ["get_zabbix_problems"])],
            scenario);

        var caseResult = result.Cases.Should().ContainSingle().Which;
        caseResult.Passed.Should().BeFalse();
        var check = caseResult.Checks.Should()
            .ContainSingle(item => item.Check == "tool-selection").Which;
        check.Passed.Should().BeFalse();
        check.Reason.Should().Contain("get_zabbix_problems");
    }

    [Fact]
    public async Task Run_ferramenta_proibida_reprova()
    {
        var scenario = new FakeScenario(new EvaluationOutcome(
            ToolsExecuted: ["delete_incident"]));

        var result = await Harness().RunAsync(
            [Question(forbiddenTools: ["delete_incident"])],
            scenario);

        result.AllPassed.Should().BeFalse();
        result.Cases[0].Checks.Should().Contain(check =>
            check.Check == "tool-selection" && check.Reason!.Contains("proibidas"));
    }

    [Fact]
    public async Task Run_esperava_negacao_e_passou_reprova_authorization()
    {
        var scenario = new FakeScenario(new EvaluationOutcome(Denied: false));

        var result = await Harness().RunAsync(
            [Question(expectDenied: true)],
            scenario);

        result.Cases[0].Checks.Should().Contain(check =>
            check.Check == "authorization" && !check.Passed &&
            check.Reason!.Contains("esperava negação"));
    }

    [Fact]
    public async Task Run_negou_quando_nao_devia_reprova_com_motivo()
    {
        var scenario = new FakeScenario(new EvaluationOutcome(
            Denied: true, DeniedReason: "Roles ausentes: Admin"));

        var result = await Harness().RunAsync(
            [Question()],
            scenario);

        result.Cases[0].Checks.Should().Contain(check =>
            check.Check == "authorization" && !check.Passed &&
            check.Reason!.Contains("Roles ausentes: Admin"));
    }

    [Fact]
    public async Task Run_poucas_evidencias_ou_citacao_ausente_reprova_grounding()
    {
        var scenario = new FakeScenario(new EvaluationOutcome(
            EvidenceCount: 1, Citations: ["Knowledge#outro"]));

        var result = await Harness().RunAsync(
            [Question(minEvidence: 2, citations: ["Knowledge#a1"])],
            scenario);

        var grounding = result.Cases[0].Checks.Should()
            .ContainSingle(check => check.Check == "grounding").Which;
        grounding.Passed.Should().BeFalse();
        grounding.Reason.Should().Contain("evidências 1 < mínimo 2");
    }

    [Fact]
    public async Task Run_varios_casos_resultado_por_caso_e_consolidado()
    {
        var scenario = new FakeScenario(new EvaluationOutcome(ToolsExecuted: ["ok_tool"]));
        var cases = new[]
        {
            Question("ok", expectedTools: ["ok_tool"]),
            Question("ruim", expectedTools: ["outra_ferramenta"]),
        };

        var result = await Harness().RunAsync(cases, scenario);

        result.Cases.Should().HaveCount(2);
        result.PassedCount.Should().Be(1);
        result.AllPassed.Should().BeFalse();
        scenario.ExecutedCases.Select(item => item.Id).Should().Equal("ok", "ruim");
    }
}
