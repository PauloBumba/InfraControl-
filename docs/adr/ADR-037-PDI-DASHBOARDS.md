# ADR-037 — Dashboards do PDI (Colaborador, Gestor, Administrativo)

- **Status**: aceito
- **Data**: 2026-10-07
- **Contexto**: Fase 7 — Gestão (`docs/30-PLANO-MODULO-PDI.md` §18, §19, §20, §25). As Fases 1–6 (ADR-029, ADR-032, ADR-033, ADR-034, ADR-035, ADR-036) entregaram o ciclo de vida, a estrutura, o acompanhamento, as evidências, a governança e a avaliação; os §18–§20 descrevem os três painéis de síntese que faltavam para o módulo ser gerenciável sem varrer plano por plano.

---

## Problema

Os dados existem, mas só se acessam em granularidade de plano (estrutura, acompanhamento, avaliação):

- o **colaborador** (§18) não vê num só lugar o progresso, as fases, o próximo prazo e as pendências do próprio plano;
- o **gestor** (§19) precisa abrir um a um dos planos que gerencia para descobrir quem está atrasado, quem precisa de atenção e o que vence em breve — o §19 pede "acesso direto aos casos que precisam de atenção";
- o **administrador** (§20) não tem as contagens globais (criados/concluídos/em andamento/replanejados + taxa de conclusão + agregados de fases/check-ins/evidências/impedimentos).

## Decisão

### 1. Três queries de leitura, zero migração

Dashboards são **projeções** dos dados já existentes — nenhuma tabela, coluna nova ou migração de schema/dados. Três queries MediatR em `PdiQueries.cs` com DTOs próprios (`PdiDto.cs`):

| Query | Rota | Guarda |
|---|---|---|
| `GetPdiDashboardQuery(UserId)` | `GET /pdi/{userId}/dashboard` | policy `Operate` + `PdiAccess.EnsureCanView` (Admin/dono/gestor) — 404 sem plano, 403 estranho |
| `GetPdiManagerDashboardQuery()` | `GET /pdi/manager/dashboard` | policy `Operate` — planos em que `ManagerId = eu`; vazio → zeros |
| `GetPdiAdminDashboardQuery()` | `GET /pdi/admin/dashboard` | policy **`Administer`** + checagem `Admin` no handler (duas camadas, ADR-030) |

Somente leitura → **sem** `IAuditableRequest` (auditoria cobre mutações; os 32 comandos PDI da Fase 5/6 seguem intactos).

### 2. §18 — Dashboard do colaborador (`PdiDashboardDto`)

- `ProgressPercent` = média arredondada dos `Percent` das fases do plano (0 sem fases);
- `Phases` = título + status de cada fase (o front desenha ✓ ● ◐ ○ com os rótulos/cores já existentes);
- `NextDueDate` = menor `EndDate` entre fases **não concluídas** (sem fase pendente → `null`, o front mostra "—");
- `OpenImpediments` (**Pendências**) · `TotalEvidences` · `TotalCheckIns` — agregados de toda a árvore (uma query por coleção via `phaseIds`, mesma técnica do acompanhamento);
- `Status` do plano para o cabeçalho.

### 3. §19 — Dashboard do gestor (`PdiManagerDashboardDto`)

Classificação por plano (uma árvore + impedimentos + evidências por plano gerenciado — `ListByManagerAsync` novo no `IPdiRepository`):

- 🔴 **Atrasado**: `TargetDate < hoje` e status não `Completed`/`Cancelled`;
- 🟡 **Atenção**: não atrasado, não finalizado, e com *algum caso de ação*: impedimento `Open`, evidência `Pending` ou fase `PendingValidation`;
- 🟢 **Dentro do prazo**: o resto — inclusive planos concluídos/cancelados (nada a fazer);
- `Overdue + NeedsAttention + OnTrack = TotalPlans` (partição exata — os 3 rótulos do §19 cobrem todos os casos);
- `PhasesDueSoon` = fases não concluídas com `EndDate` em `[hoje, hoje+7d]`;
- `OpenImpediments` / `PendingEvidences` = somatório nos planos gerenciados;
- `Plans[]` (dono, status, prazo, flags `Overdue`/`NeedsAttention`, **nome resolvido**) = o "acesso direto aos casos" do §19.

### 4. §20 — Dashboard administrativo (`PdiAdminDashboardDto`)

Sobre **todos** os planos (`ListWithOwnersAsync(null)` + árvores):

- `TotalPlans`, `CompletedPlans`, `ReplannedPlans`, `CancelledPlans` e `InProgressPlans` = **complementar** dos demais (partição exata: `Total = Concluídos + Em andamento + Replanejados + Cancelados` — "em andamento" absorve `Draft/Planned/AwaitingAcceptance/Active/InProgress/UnderEvaluation`);
- `CompletionRatePercent` = `round(100 × concluídos / total)` (0 sem planos);
- `CompletedPhases`, `TotalCheckIns`, `TotalEvidences`, `TotalImpediments` globais.
- O rótulo de período do mock ("1º SEMESTRE 2027") **não** foi implementado: filtro por período é escopo de filtros/relatórios da Fase 8 (§21/§22) — não inventamos parâmetro.

### 5. Frontend — três painéis (`PdiDashboardPanels.tsx`)

- **§18**: faixa dentro do card "Meu plano" — barra de progresso, chips das fases (glifo + título) e a linha `Próximo prazo · Pendências · Evidências · Check-ins`;
- **§19**: card "Meus PDIs — N colaboradores" com 🟢/🟡/🔴, agregados e a lista dos casos 🟡/🔴 com botão **"Abrir"** → diálogo de avaliação do plano (leitura permitida ao gestor; a **estrutura** continua restrita a dono/Admin pelo guard do `GetPdiStructureQuery`, ADR-032 — documentado aqui);
- **§20**: card "PDI — visão administrativa" com as duas faixas de `Stat` (contagens/taxa; fases/check-ins/evidências/impedimentos);
- `pdi.ts` ganha os 3 tipos + `getPdiDashboard/getPdiManagerDashboard/getPdiAdminDashboard`;
- os painéis são **opcionais**: erro em um deles não derruba a página (estado `null`); carregamento único pós-`load` do plano.

## Consequências

- nenhum custo de escrita: dashboards recomputam a cada GET (escala adequada ao formato de plano único/colaborador); se crescer, o próximo passo é cache agregado — fora do escopo atual;
- a classificação 🟢/🟡/🔴 é regra de negócio documentada aqui (§19 traz só os rótulos) — mudar o janela de "atenção" (7 dias para fases, flags de ação) é uma decisão reversível nesta ADR;
- "acesso direto" = diálogo de avaliação; ver estrutura/acompanhamento de outro plano segue exigindo dono/Admin (guard pré-existente — nenhuma quebra);
- Admin que **também** é gestor vê os dois painéis (§19 dos seus planos + §20 globais);
- retenção de evidências segue aberta.

## Validação (2026-10-07)

- `PdiDashboardTests` **7/7** (§18 resumo completo + 403 estranho + 404 sem plano; §19 partição atrasado/atenção/prazo com agregados + zeros sem planos; §20 contagens/taxa + 403 não-admin);
- suítes: unit **1628/1628** · arch **117/117** · PDI **173/173** · front `tsc`/lint/build verdes;
- containers api+frontend reconstruídos (healthy), **sem migração nova**;
- E2E `/tmp/opencode/pdi_fase7_e2e.py` **32/32** (§18 progresso/prazo/totais + 403/404, §19 classificação + flags + agregados + zeros, §20 contagens/taxa + 403 não-admin, purga de resíduo);
- regressão E2E Fase 6 **31/31** e Fase 5 **63/63**.
