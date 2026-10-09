# ADR-038 — Analytics do PDI (Indicadores, Relatórios e Exportação)

- **Status**: aceito
- **Data**: 2026-10-07
- **Contexto**: Fase 8 — Analytics (`docs/30-PLANO-MODULO-PDI.md` §21, §22, §25). As Fases 1–7 (ADR-029, ADR-032, ADR-033, ADR-034, ADR-035, ADR-036, ADR-037) entregaram o ciclo de vida, a estrutura, o acompanhamento, as evidências, a governança, a avaliação e os três dashboards; o §21 pede indicadores consolidados e o §22 os três relatórios (individual, semestral, de dificuldades) com exportação PDF/Excel — é a etapa em que o PDI "começa a gerar informação estratégica".

---

## Problema

Os dados existem e os dashboards (Fase 7) mostram o estado atual, mas:

- não há **indicadores históricos/agregados** (taxa de conclusão, tempo médio de conclusão, evolução por competência, distribuição de impedimentos);
- não há **relatórios exportáveis** — o §22 descreve 3 documentos (individual, semestral, de dificuldades) que hoje só existem como mock;
- o dashboard administrativo (§20) ficou **sem filtro de período** — o rótulo "1º SEMESTRE 2027" do mock foi explicitamente deferido para a Fase 8 (ADR-037 §4).

## Decisão

### 1. Queries de leitura pura — zero migração, zero escrita

Tudo são **projeções** dos dados existentes: nenhuma tabela nova, coluna nova ou mutação. 4 queries MediatR em `PdiAnalyticsQueries.cs` + DTOs em `PdiAnalyticsDtos.cs`:

| Query | Rota | Guarda |
|---|---|---|
| `GetPdiIndicatorsQuery(From?, To?)` | `GET /pdi/indicators` | `Administer` + `EnsureAdmin` no handler |
| `GetPdiIndividualReportQuery(UserId)` | `GET /pdi/reports/individual/{userId}` | `Operate` + `EnsureCanView` (Admin/dono/gestor) — 404 sem plano, 403 estranho |
| `GetPdiSemesterReportQuery(From?, To?)` | `GET /pdi/reports/semester` | `Administer` + `EnsureAdmin` |
| `GetPdiDifficultiesReportQuery(From?, To?)` | `GET /pdi/reports/difficulties` | `Administer` + `EnsureAdmin` |

Somente leitura → **sem** `IAuditableRequest` (mesmo critério da Fase 7).

### 2. Escopo de período único (helper `PdiAnalytics`)

Classe estática interna com as regras compartilhadas:

- **escopo** = planos com `TargetDate` dentro de `[de..até]` (lados nulos = sem limite; sem filtro = tudo). O **relatório individual** é o único sem filtro — o período vem do próprio plano (`PeriodStart/End`);
- `EnsurePeriod`: `'de' > 'até'` → `ArgumentException` → **400** ("O período 'de' não pode ser posterior ao 'até'.");
- `Percent`: arredondamento `AwayFromZero`, 0 quando base = 0;
- `EnsureAdmin` + `EnsureCanView` reutilizados dos padrões das Fases 6/7;
- `AvgCompletionDaysAsync`: primeiro evento `Completed` do timeline (histórico) − criação do plano, em dias (`DayNumber` — `DateOnly - DateOnly` não existe em C#), só planos `Completed` com evento, `null` quando nada concluído;
- `AggregateImpediments`: distribuição por tipo (texto livre — ver "Escopos deliberados"), `%` por tipo, contagem de resolvidos e `AvgResolutionDays` = média de `ResolvedAt − OccurredAt` (`null` sem resoluções).

### 3. §21 — Indicadores (`GET /pdi/indicators`)

4 blocos no DTO `PdiIndicatorsDto`:

- **Execution**: `TotalPlans`, `CompletedPlans`, `CompletionRatePercent`, `OverduePlans` (meta vencida não finalizada — mesma regra do §19), `PhasesCompleted`, `PhasesOverdue`, `AvgCompletionDays`;
- **Development**: `CompetenciesWorked`, `ObjectivesTotal`, contagens dos 4 desfechos do §16 (`ResultsAtingido/Parcial/NaoAtingido/Replanejado`) e `EvolutionByCompetency` (objetivos + % médio das fases por competência — objetivo → estratégia → fase);
- **Accompaniment**: `CheckIns`, `CheckInsPerPlan` (1 casa), `Feedbacks`, `EvidencesValidated`;
- **Difficulties**: `TotalImpediments`, `ByType[]` (tipo, contagem, %), `AvgResolutionDays`.

### 4. §22 — Três relatórios + exportação

- **Individual**: compõe as 3 leituras já existentes (`GetPdiStructureQuery` + `GetPdiAccompanimentQuery` + `GetPdiEvaluationQuery` via builders extraídos `PdiStructureBuilder`/`PdiAccompanimentBuilder`/`PdiEvaluationBuilder` — os handlers originais mantêm os guards e delegam) + cabeçalho (dono, e-mail, gestor, período). `EnsureCanView` → 404 sem plano / 403 estranho.
- **Semestral**: sem filtro = **semestre corrente** (jan–jun / jul–dez do ano atual), rótulo `"Nº SEMESTRE YYYY"` (janela não semestral → intervalo `dd/MM/yyyy`); `Trends` compara com a janela anterior de mesmo tamanho (`prevTo = from−1`, `length = diff+1`); linhas de gestores (agrupadas por `ManagerId`, com contagem/concluídos) e de competências; `Difficulties` reusa `AggregateImpediments`.
- **Dificuldades**: distribuição `%` por tipo no período (ex. do §22: "38% das dificuldades relacionadas à capacitação") + resolvidos + média de resolução.

### 5. Exportação PDF/Excel (`Api/Reporting/`)

- `PdiExportFormat` (`readonly record struct`): `Pdf` (`application/pdf`) e `Xlsx` (spreadsheetml) com `Parse(string?)` — nulo/vazio = PDF, texto fora de `pdf|xlsx` → `ArgumentException` → **400**;
- `PdiReportRenderers` (static class, static ctor define `QuestPDF.Settings.License = LicenseType.Community`): 6 renderers `Render{Individual,Semester,Difficulties}{Pdf,Xlsx}` — **QuestPDF 2026.9.1** (só via lambdas — os tipos `Column`/`Table` são internos) e **ClosedXML 0.105.1** (sheets "Resumo"/"Estrutura"/"Avaliação" e "Dificuldades");
- 3 endpoints de export × 2 formatos = as 6 saídas (200 + `Content-Disposition` com nome `pdi-relatorio-*-{yyyyMMdd}.{ext}`); formato é **query string** (`?format=xlsx`), parse antes de executar a query (400 barra a consulta).

### 6. Filtro `from`/`to` no dashboard administrativo (deferido da Fase 7)

`GetPdiAdminDashboardQuery(From?, To?)` ganha os mesmos parâmetros — o `PdiController` delega o parse para `PdiReportsController.ParseDate` (`internal static`, AAAA-MM-DD invariante, inválida = 400). Mesmo escopo (`TargetDate` na janela).

### 7. Frontend — `PdiReportsDialog` (4 abas)

- `pdi.ts` ganha a seção "Analytics (Fase 8)": tipos (`PdiIndicators`, `PdiIndividualReport`, `PdiSemesterReport`, `PdiDifficultiesReport`, `PdiReportFormat`), `getPdiIndicators/getPdiIndividualReport/getPdiSemesterReport/getPdiDifficultiesReport` e `downloadPdiReport` (blob → objectURL — o path de download é o próprio endpoint de export, **em inglês**);
- `PdiReportsDialog.tsx`: 4 abas (Indicadores · Dificuldades · Individual · Semestral) com filtros De/Até (recarga automática), seletor de colaborador (admin) e botões PDF/Excel (nas abas de relatório); não-admin vê só a aba Individual (o próprio);
- `PdiPage`: botão **"Relatórios"** no header (admin) e **"Relatório"** no card próprio — monta o dialog com `plans`/`ownUserId`/`isAdmin`.

## Escopos deliberados (não inventamos requisito)

- **Lookup de tipo de impedimento não entrou**: o §21/§22 tratam "tipos" como **texto livre** (o domínio já grava `Type` string desde a Fase 3) — criar catálogo seria requisito novo sem amarração no mock; anotação "lookup = Fase 8" em docs/29/docs/30 atualizada para "pendente";
- **Setores omitidos**: §21 ("setores com maior ocorrência") e §22 ("setores" do relatório semestral) — o domínio PDI **não tem campo de setor** (o participante pertence a um setor no módulo Organization, fora do escopo do PDI); adicionar exigiria join/ADR própria;
- **"Atividades" do relatório individual** vêm junto na árvore `Structure` (já existente) — nada novo;
- exportação CSV não foi pedida (só PDF/Excel no §22);
- nenhum cache: indicadores recomputam a cada GET (mesma tolerância da Fase 7).

## Consequências

- leitura pura → custo por GET proporcional ao nº de planos no escopo (uma árvore por plano); se crescer, cache agregado é o próximo passo;
- a % por tipo arredonda individualmente (soma pode diferir de 100 em cenários de 3+ tipos) — coerente com o "38%" do §22, que é por fatia;
- o relatório individual é acessível a dono/gestor (`Operate` + `EnsureCanView`), os demais são Admin-only (`Administer` + `EnsureAdmin` — 2 camadas, ADR-030);
- pacotes novos no `Api.csproj`: **QuestPDF 2026.9.1** e **ClosedXML 0.105.1** (licença QuestPDF Community — uso de organização pequena);
- rótulo do período no dashboard continua ausente no front (o dialog de relatórios tem os filtros) — se o painel §20 quiser rótulo, é ajuste fino futuro.

## Validação (2026-10-07)

- `PdiAnalyticsTests` **16/16** (indicadores 4 blocos + período + 400 + 403 + vazio; individual dono/estranho/404; semestral rótulo/janela/400/403; dificuldades escopo/400/403; renderers `%PDF`/`PK` + `XLWorkbook` abre com sheets; `PdiExportFormat.Parse` 400; `AdminDashboard_PeriodFilter_ShouldScopePlans` do §20);
- suítes: unit **1644/1644** · arch **119/119** · PDI **189/189** · front `tsc`/lint/build verdes;
- containers api+frontend reconstruídos (healthy), **sem migração nova**;
- E2E `/tmp/opencode/pdi_fase8_e2e.py` **76/76** (§21 blocos/período/400/403, §22 individual 200/403/404 + exports PDF/XLSX/400, semestral rótulo/janela/403/exports, dificuldades escopo/403/exports, §20 dashboard com `from`/`to`, purga de resíduo);
- regressão E2E Fase 7 **32/32**, Fase 6 **31/31**, Fase 5 **63/63**, Fase 4 **50/50**.
