# ADR-035 — Governança do PDI (Motivo Obrigatório, Aprovação de Fase, Auditoria e Timeline)

- **Status**: aceito
- **Data**: 2026-10-07
- **Contexto**: Fase 5 — Governança (`docs/30-PLANO-MODULO-PDI.md` §14, §15, §17, §25). As Fases 1–4 (ADR-029, ADR-032, ADR-033, ADR-034) entregaram o ciclo de vida do plano, a árvore de desenvolvimento, o acompanhamento bilateral e as evidências; o plano de produto exige agora o **replanejamento auditável** (§14), as **aprovações** (§15) e o **histórico completo** (§17), além da auditoria de escrita que as Fases 2–4 adiaram deliberadamente.

---

## Problema

O §14 determina que *toda* alteração de campo registre **valor anterior; novo valor; motivo; quem alterou; quando alterou** — nada de editar e perder histórico. O §15 define aprovação em pontos específicos:

1. gestor cria PDI → colaborador aceita → PDI começa;
2. colaborador conclui fase → gestor valida → próxima fase.

E o §17 pede timeline completa (aceite, fase iniciada, impedimento, prazo alterado, validação). Hoje:

- o `Update` do plano aceita mudança de campo **sem motivo** e o histórico `Updated` não guarda valor anterior → novo;
- o `PdiPhaseStatus` termina em `Completed` direto pelo PUT genérico — **qualquer dono** concluía/reabria a própria fase, sem o par concluir → validar do §15.2;
- check-ins e impedimentos (Fase 3) não gravavam eventos no timeline;
- nenhum comando PDI emitia registro de auditoria (Fases 2–4 marcaram "auditoria = Fase 5").

## Decisão

### 1. §14 — motivo obrigatório e valor anterior → novo no `Update`

- `IndividualDevelopmentPlan.Update` (comparação campo a campo sobre `Strategy`, `TargetDate`, `ManagerId`, `PeriodStart/End`) exige `reason` **quando algum campo mudou** → `ArgumentException("O motivo da alteração é obrigatório.")` (400 pt-BR);
- mudança **apenas de status** mantém o motivo opcional (o §15.1 exige aceite **sem** motivo; transições continuam governadas por `PdiTransitions` — ADR-029);
- o evento `Updated` ganha `Detail` no formato `estratégia: "old" → "novo"; prazo: 2026-10-07 → 2026-11-07; gestor: …; período: …` (null → `sem gestor`/`sem período`; estratégia trunca em 80 chars) além do `Reason` já existente;
- ordem de validação em `Update`: `EnsureCanMove` → `RequireStrategy` → `EnsureValid` → motivo (preserva a precedência de erro dos testes de transição).

### 2. §15.2 — aprovação de fase (`PendingValidation` + comandos dedicados)

| | |
|---|---|
| Status novo | `PdiPhaseStatus.PendingValidation = 4` (enum→string, **sem migração**) |
| Comandos | `SubmitPdiPhaseCommand(Id)` — dono/Admin (`EnsureCanReport`) leva `NotStarted/InProgress` → `PendingValidation` (fora de execução → 409 "…em execução…"); `ValidatePdiPhaseCommand(Id, Validated/Rejected, Reason?)` — só Admin/gestor (`EnsureCanValidatePhase`, novo em `PdiAccess`) |
| Rejeição | `Reason` obrigatório (nulo/em branco → 400) → volta a `InProgress` com o motivo no histórico `PhaseRejected` |
| Validação | `Completed` + **auto-start** da próxima fase `NotStarted` → `InProgress` (novo `ListPhasesAsync`) |
| Guard do PUT genérico | não-Admin não altera status **para nem desde** `PendingValidation/Completed` (no-op do status vigente segue permitido) → 400 "A conclusão e a reabertura de fase exigem o fluxo de aprovação." — fecha o bypass de Fase 2 |
| Estado errado | 409 `InvalidOperationException` (ex.: revalidar fase `Completed`, concluir fase `NotStarted`) |

Rotas: `POST /api/v1/pdi/phases/{id}/submit` e `PUT /api/v1/pdi/phases/{id}/validation` (`PdiStructureController`, `ValidatePdiPhaseViewModel` com `JsonStringEnumConverter`).

§15.1 (aceite) já existia: `AwaitingAcceptance → Active` pelo dono sem motivo (Fase 1) — **sem alteração**, coberta pelo E2E.

### 3. §17 — eventos de acompanhamento no timeline

`PdiHistoryType` + `PhaseStarted=6, PhaseSubmitted=7, PhaseValidated=8, PhaseRejected=9, CheckInCreated=10, ImpedimentCreated=11, ImpedimentResolved=12` (sem migração — conversion string) com factories `ForPhaseEvent`/`ForCheckInCreated`/`ForImpediment`; gravados pelos handlers (submit/validate, create de check-in, create/resolve de impedimento) no padrão `plans.AddHistoryRange(plan.DrainPendingHistory())`.

### 4. Auditoria de todos os comandos PDI

Os **28 comandos** existentes + os 2 novos de aprovação implementam `IAuditableRequest` com `AuditResource => "PDI"` — escritas PDI passam a alimentar `AuditEntries` (action = nome do request, ator/correlação/resultado herdados do pipeline global).

### 5. Frontend

- `pdi.ts`: `PendingValidation`, 7 tipos novos com rótulos, `submitPdiPhase`/`validatePdiPhase`;
- `PdiStructureDialog`: botões **Concluir/Validar/Rejeitar** por papel (`canSubmitPhase`/`canValidatePhase`), diálogo de rejeição com motivo, opções de status do PUT restritas por papel (não-Admin não vê `PendingValidation/Completed`);
- `PdiPage`: motivo obrigatório quando qualquer campo do plano difere do original (`fieldsChanged`), payload com `reason`, label dinâmico "Salvar alterações" / "Salvar com motivo".

## Consequências

- escrita de fase e de plano agora **não podem** ser concluída/reaberta por engano via PUT — o fluxo de aprovação é a única porta (o guard é por status, não por rota, então é resiliente a novos endpoints);
- Admin valida qualquer plano (inclusive o próprio) — mesmo privilégio já concedido em evidências (ADR-034);
- sem migração nesta fase (enum→string + detalhe em texto livre);
- os PUTs do E2E devem reenviar gestor/período do Admin (omissão **limpa** os campos — semântica pré-existente de Admin = estado completo), senão o motivo passa a ser exigido por causa da limpeza;
- retention de evidências (comentada na Fase 4) **não** entrou aqui — continua aberta.

## Validação (2026-10-07)

- `PdiGovernanceTests` **11/11** (motivo 400 + detalhe anterior→novo, aceite sem motivo, submit/validar/rejeitar c/ 409s, auto-start, guard do PUT, permissão de validação, timeline de check-in/impedimento, auditoria); `PdiCommandTests` adaptado deliberadamente (3 testes agora enviam `Reason:` e assere o detalhe novo);
- suítes: unit **1605/1605** · arch **117/117** · PDI **150/150** · front `tsc`/lint/build verdes;
- containers api+frontend reconstruídos (healthy);
- E2E `/tmp/opencode/pdi_fase5_e2e.py` **63/63** (§14 400/200/histórico, aceite sem motivo, ciclo completo de aprovação com rejeição, 409s, guard do PUT 400 × no-op 204, 403s de papel, auto-start nos dois planos, timeline de check-in/impedimento, auditoria `AuditEntries` por SQL, purga de resíduo);
- regressão E2E Fase 4 **50/50**.
