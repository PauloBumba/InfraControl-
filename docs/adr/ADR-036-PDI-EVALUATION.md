# ADR-036 — Avaliação Final do PDI (Autoavaliação, Gestor, Resultado)

- **Status**: aceito
- **Data**: 2026-10-07
- **Contexto**: Fase 6 — Avaliação (`docs/30-PLANO-MODULO-PDI.md` §15.3, §16, §17, §23, §24, §25). As Fases 1–5 (ADR-029, ADR-032, ADR-033, ADR-034, ADR-035) entregaram o ciclo de vida, a estrutura, o acompanhamento, as evidências e a governança; o plano de produto fecha agora o fluxo oficial da §24 — `… VALIDAÇÃO → AVALIAÇÃO FINAL → RESULTADO` — com as duas avaliações do §15.3 e a comparação final do §16.

---

## Problema

O §15.3 define a ordem: **colaborador faz autoavaliação → gestor faz avaliação → resultado final**. O §16 define o resultado como um de quatro estados — *atingido · parcialmente atingido · não atingido · replanejado* — com avaliação qualitativa opcional, e o §23 nomeia explicitamente as entidades `PDIAssessment` e `PDIResult`. O modelo de ciclo de vida (§3/ADR-029) já tinha os estados `Em acompanhamento → Em avaliação → Concluído`, mas:

- não existe onde a avaliação mora (uma por lado, com autor);
- não há gate: o resultado podia ser "registrado" (não havia onde) sem as avaliações;
- `UnderEvaluation → Completed` era a única saída — o resultado **"replanejado"** do §16 não tinha seta para devolver o plano ao ciclo;
- a timeline (§17) não pedia eventos de avaliação.

## Decisão

### 1. Entidades `PdiAssessment` (2 linhas) e `PdiResult` (1 linha) sob o plano

| | |
|---|---|
| `PdiAssessment` / `pdi_assessments` | FK `Restrict` → `individual_development_plans`, `Side` (`Self` = autoavaliação / `Manager` = avaliação do gestor), `AuthorId` (user ref **sem FK** — critério ADR-029), `Content` ≤4000, índices: PK + **único (PlanId, Side)** — upsert pela própria linha |
| `PdiResult` / `pdi_results` | FK `Restrict` → plano, **único (PlanId)**, `Outcome` (`PdiResultOutcome` 4 valores do §16 + `ToDisplayLabel` pt-BR), `Notes?` ≤4000 (qualitativo opcional), `FinalizedById/FinalizedAt` |
| Migração | `20261007233505_PdiEvaluation` (enum→string, **sem** mudança de dados) |

Regras de fluxo (erros pt-BR: `ArgumentException` 400 · `InvalidOperationException` 409 · `UnauthorizedAccessException` 403):

1. **Lado é determinado pelo ator no servidor** — dono grava `Self`, gestor definido ou Admin grava `Manager`, resto → 403;
2. autoavaliação: plano `InProgress`/`UnderEvaluation`; salvar a **primeira** enquanto `InProgress` leva o plano a **`UnderEvaluation`** (efeito colateral com `StatusChanged` no timeline — status-only, sem motivo, mesma regra do aceite);
3. avaliação do gestor: **exige a autoavaliação** (400) e plano `UnderEvaluation` (409);
4. resultado: exige avaliação do gestor (400), plano `UnderEvaluation` (409), outcome válido (400) e é **único** (2ª → 409);
5. plano `Completed`/`Cancelled` **congela** a avaliação (409).

### 2. Transição nova: `UnderEvaluation → Replanned`

O resultado **"Replanejado"** (§16) conclui o ciclo devolvendo o plano à fila (`Replanned → Active` já existia — ADR-029). Adicionada a seta em `PdiTransitions` (documentada no próprio mapa e em `PdiTransitionsTests`) — refinamento da matriz da §3 justificado pelo §16; os demais 3 resultados → `Completed`.

### 3. Comandos/query e API

- `SavePdiAssessmentCommand(UserId, Content)` — upsert por lado (`PUT /pdi/{userId}/assessment`);
- `FinalizePdiResultCommand(UserId, Outcome, Notes?)` — `PUT /pdi/{userId}/result` (Admin/gestor via novo `PdiAccess.EnsureCanEvaluate`);
- `GetPdiEvaluationQuery(UserId)` → `GET /pdi/{userId}/evaluation` — snapshot do plano + as duas avaliações + resultado (base da comparação inicial/durante/final), leitura `EnsureCanView` (Admin/dono/gestor);
- os 2 comandos implementam `IAuditableRequest` (`AuditResource = "PDI"`) — a auditoria da Fase 5 cobre agora os 32 comandos PDI;
- histórico: `PdiHistoryType.AssessmentCreated=13` / `ResultFinalized=14` (sem migração) via `plan.RecordAssessmentCreated/RecordResultFinalized`.

### 4. Frontend — `PdiEvaluationDialog`

Botão **"Avaliar"** (linha de ações do Admin + card do operador) abre o diálogo com 4 blocos: snapshot do plano (estratégia/prazo/gestor/período/status — "inicial · durante"), autoavaliação (textarea do dono), avaliação do gestor (textarea do gestor/Admin, habilitada depois da autoavaliação) e resultado (select dos 4 estados + nota, gestor/Admin) — tudo somente leitura depois de finalizado. `pdi.ts` ganha tipos, `PDI_RESULT_OUTCOME_*`, `getPdiEvaluation/savePdiAssessment/finalizePdiResult`, os 2 rótulos de timeline e `UnderEvaluation: ["Completed", "Replanned"]` espelhando a transição nova.

## Consequências

- a ordem do §15.3 é **imposta pelo backend** (não é só UI): sem autoavaliação não há avaliação; sem avaliação não há resultado;
- Admin no **próprio** plano grava só o lado `Self` (o lado é derivado do ator) — um plano sem `managerId` só recebe resultado de Admin *diferente do dono*; sem gestor e só com o dono Admin, o ciclo não fecha (mesma lacuna documentada da validação de fase — Fase 5);
- 1 resultado por plano, imutável (correção = novo ciclo via `Replanejado`, que é justamente o mecanismo do §16);
- a comparação do §16 é **apresentacional**: o snapshot do plano + as avaliações + o resultado no GET único; não há campos "Inicial/Durante/Final" armazenados (não inventamos requisito);
- retenção de evidências segue aberta (não entrou na Fase 5 nem na 6).

## Validação (2026-10-07)

- `PdiEvaluationTests` **15/15** (transição automática + timeline, upsert por linha, guards 400/409/403, resultado `Atingido`→`Completed` e `Replanejado`→`Replanned`, resultado único, outcome inválido, auditoria) + `PdiTransitionsTests` atualizado com a seta nova;
- suítes: unit **1621/1621** · arch **117/117** · PDI **166/166** · front `tsc`/lint/build verdes;
- containers api+frontend reconstruídos (healthy);
- E2E `/tmp/opencode/pdi_fase6_e2e.py` **31/31** (guards 400 na ordem do fluxo, transição + timeline, upsert, congelamento 409, ciclo `Replanejado → Replanned → Active`, auditoria SQL, 2 colaboradores convidados, purga de resíduo);
- regressão E2E Fase 5 **63/63**.
