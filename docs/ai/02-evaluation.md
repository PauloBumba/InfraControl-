# 02 — AI Evaluation (harness de avaliação §45)

- **Sprint**: S9 — FAI-T010 (`docs/35` §11) · blueprint `docs/34` §45
- **Status**: foundation entregue (2026-10-09); execução contra modelo
  real pendente de ambiente (sem Docker/Ollama no workstation — `docs/35` §12)

## O que é

O `EvaluationHarness` (`src/InfraControl.Application/AI/Evaluation/`)
roda uma lista de `EvaluationCase` contra um `IEvaluationScenario`
injetado e aplica todos os `IEvaluationCheck` registrados. Um caso só
aprova quando **todos** os checks passam; o `EvaluationRunResult`
guarda o motivo de cada check reprovado (evidência auditável — §46/§63).

```text
EvaluationCase (pergunta + expectativas)
        ↓
IEvaluationScenario.RunAsync  ← cenário: fake nos testes HOJE;
        ↓                        fluxo real (Copilot) amanhã — sem
EvaluationOutcome              mudar o harness
(ferramentas, evidências, citações, denied)
        ↓
checks (tool-selection · authorization · grounding)
        ↓
EvaluationRunResult (passed/failed por caso + motivo por check)
```

## Casos (`EvaluationCase`)

| Campo | Significado (§45) |
|---|---|
| `Question` | a pergunta do dataset |
| `ExpectedTools` | ferramentas que DEVEM executar (`tool selection`) |
| `ForbiddenTools` | ferramentas que NÃO podem executar |
| `ExpectDenied` | o cenário deve negar por autorização (`authorization`) |
| `MinEvidenceCount` | mínimo de evidências (`grounding`) |
| `RequiredCitations` | citações obrigatórias (`grounding`/`citations`) |

## Checks entregues (não dependem de LLM)

1. **tool-selection** — `ExpectedTools ⊆ executadas` e
   `executadas ∩ ForbiddenTools = ∅`;
2. **authorization** — `ExpectDenied` ⟺ `outcome.Denied` (com o motivo
   da negação reprovando chamada legítima negada);
3. **grounding** — `EvidenceCount ≥ MinEvidenceCount` e todas as
   `RequiredCitations` presentes.

## O que fica pendente (ambiente + Fase 1)

- **Cenário real**: `IEvaluationScenario` ligado ao fluxo Copilot com
  Ollama de verdade (hoje: fakes nos testes unitários) —
  hallucination/structured output/confidence/timeout/prompt injection
  (§45) exigem modelo real;
- **Dataset versionado** de casos por capacidade (começar com
  Incidents + Knowledge);
- **Rerun no CI** quando houver runner com Docker (gate Fase 0 → 1).

## Como usar

```csharp
var harness = new EvaluationHarness(
    [new ToolSelectionCheck(), new AuthorizationCheck(), new GroundingCheck()]);

var run = await harness.RunAsync(cases, scenario, ct);
// run.AllPassed, run.PassedCount, run.Cases[i].Checks[j].Reason
```

Via DI: `EvaluationHarness` + os três `IEvaluationCheck` já estão
registrados em `AddInfrastructure` (FAI-T010); o `IEvaluationScenario`
é adicionado quando o fluxo real existir.
