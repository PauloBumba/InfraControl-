namespace InfraControl.Application.AI.Evaluation;

/// <summary>
/// Cenário sob teste (§45): executa um <see cref="EvaluationCase"/>
/// contra o fluxo real de IA e devolve o que aconteceu. O harness do
/// foundation NÃO conhece o fluxo — hoje os testes injetam um fake;
/// quando o Copilot/Fase 1 existir, o cenário real passa por aqui
/// sem mudar o harness (documentado em docs/ai/02).
/// </summary>
public interface IEvaluationScenario
{
    Task<EvaluationOutcome> RunAsync(
        EvaluationCase evaluationCase,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Harness de avaliação (FAI-T010/S9 · docs/34 §45): roda cada caso
/// pelo cenário e aplica TODOS os checks registrados — um caso só
/// aprova quando todos os checks passam. Devolve o run completo com
/// motivo por check reprovado (evidência auditável, §46/§63).
/// </summary>
public sealed class EvaluationHarness(IEnumerable<IEvaluationCheck> checks)
{
    private readonly IReadOnlyList<IEvaluationCheck> _checks = [.. checks];

    public Task<EvaluationRunResult> RunAsync(
        IReadOnlyList<EvaluationCase> cases,
        IEvaluationScenario scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(scenario);

        return RunCoreAsync(cases, scenario, cancellationToken);

        async Task<EvaluationRunResult> RunCoreAsync(
            IReadOnlyList<EvaluationCase> caseList,
            IEvaluationScenario evaluationScenario,
            CancellationToken token)
        {
            var results = new List<EvaluationCaseResult>();
            foreach (var evaluationCase in caseList)
            {
                token.ThrowIfCancellationRequested();

                var outcome = await evaluationScenario.RunAsync(evaluationCase, token);
                var checkResults = _checks
                    .Select(check => check.Evaluate(evaluationCase, outcome))
                    .ToList();

                results.Add(new EvaluationCaseResult(evaluationCase, checkResults));
            }

            return new EvaluationRunResult(results);
        }
    }
}
