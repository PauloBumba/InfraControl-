namespace InfraControl.Application.AI.Evaluation;

/// <summary>
/// Caso de avaliação (FAI-T010/S9 · docs/34 §45 — AI Evaluation Tests):
/// pergunta → contexto/evidências esperadas → ferramentas esperadas →
/// resultado esperado (inclusive autorização). Dados puros, sem LLM —
/// o cenário sob teste é injetado pelo chamador.
/// </summary>
public sealed record EvaluationCase(
    string Id,
    string Question,
    IReadOnlyList<string>? ExpectedTools = null,
    IReadOnlyList<string>? ForbiddenTools = null,
    bool ExpectDenied = false,
    int MinEvidenceCount = 0,
    IReadOnlyList<string>? RequiredCitations = null,
    string? Description = null);

/// <summary>
/// O que o sistema sob teste produziu para um caso: ferramentas que
/// executou, quantidade/citações de evidências, se a autorização negou
/// e a resposta (quando houver).
/// </summary>
public sealed record EvaluationOutcome(
    IReadOnlyList<string>? ToolsExecuted = null,
    int EvidenceCount = 0,
    IReadOnlyList<string>? Citations = null,
    bool Denied = false,
    string? DeniedReason = null,
    string? Answer = null);

/// <summary>Resultado de 1 check dentro de um caso.</summary>
public sealed record EvalCheckResult(string Check, bool Passed, string? Reason = null);

/// <summary>Resultado de um caso: todos os checks precisam passar.</summary>
public sealed record EvaluationCaseResult(EvaluationCase Case, IReadOnlyList<EvalCheckResult> Checks)
{
    public bool Passed => Checks.All(check => check.Passed);
}

/// <summary>Resultado de um run de avaliação (§45).</summary>
public sealed record EvaluationRunResult(IReadOnlyList<EvaluationCaseResult> Cases)
{
    public int PassedCount => Cases.Count(result => result.Passed);

    public bool AllPassed => Cases.All(result => result.Passed);
}
