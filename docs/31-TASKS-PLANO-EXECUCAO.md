# Tasks — Plano de Execução (gap analysis e evolução do módulo)

> **Origem:** especificação "TASKS — Plano de Execução" colada pelo usuário no
> chat em 2026-10-06 (o arquivo original **não** está no repositório). Este
> documento guarda a análise de lacunas (gap analysis) da spec contra o código
> real do módulo Tasks (Fase 7) e o registro de execução das fases aprovadas.
> Cada fase vira uma task em `TASKS.md` (**Fase 23**), uma por checkpoint.

## 1. O que já existe no módulo (Fase 7 + SLA)

Base comparada, toda implementada e testada (~144 testes só do módulo):

- `TaskItem` com `TaskKind` (11 tipos) e `TaskState` (7 estados);
- workflow de transições no domínio (`TaskStateWorkflow`, F7-T007) —
  transição inválida → 409 pt-BR;
- histórico append-only `TaskHistory` (F7-T006) + auditoria global
  (`AuditEntries`, pipeline MediatR);
- comentários (`TaskComment`), vínculos N:N (`TaskAssignee`, `TaskDevice`,
  `TaskIncident`);
- SLA por task (`TaskSla` + pausas, F8-T003/T004);
- UI: kanban por estado (`TasksPage`) com filtros e transição inline.

## 2. Lacunas da spec × código (§ → código)

| Tema da spec | Status no código |
|---|---|
| §3 catálogo de tipos | **parcial** — `TaskKind` existe; os "tipos de timeline" do doc não |
| §5 planejamento e resultado | **concluído (P3)** — prioridade/prazo/responsável (P1), planejamento/resultado (P3) |
| §6 ciclo/estados | **existe** — workflow implementado; qualquer mudança de ciclo exige ADR |
| §7 fases | **ausente** — não há fases dentro da task |
| §8 tipos de timeline | **ausentes** — as 22 ações do `TaskHistoryAction` não são os "tipos de timeline" da spec |
| §10 impedimentos estruturados | **ausente** — não há registro de impedimento |
| §11 evidências | **ausente** — não há anexo/evidência de conclusão |
| §12–§13 critérios e replanejamento | **concluído (P3)** — `task_criteria` + CRUD/marcação; `ReplanTaskCommand` com motivo e histórico |
| §14 Chamado (integração) | **módulo não existe** no repo |
| §15 PDI (integração) | PDI existe (Fases 1–2), **integração Task↔PDI não** |
| §16 Knowledge (integração) | KB existe (F10), **integração Task↔KB não** |
| §17 auditoria | **existe** — `TaskHistory` + `AuditEntries` |
| UI de detalhe `/tasks/:id` | **concluído (P2)** — rota + 10 cards |
| subtarefas, progresso | **ausentes** |

## 3. Priorização proposta (P1–P5)

Sugestão entregue na análise; cada uma só executa com aprovação do usuário
(uma fase por checkpoint, AGENTS.md):

| Fase | Escopo |
|---|---|
| **P1** | Identificação básica: prioridade, prazo (due date) e responsável — domínio, commands/queries, DTO, API, migração, testes e front (form + card) |
| **P2** | Tela de detalhe `/tasks/:id` ligando comentários/histórico/vínculos já existentes |
| **P3** | Planejamento/resultado e critérios/replanejamento (§5, §12–§13) |
| **P4** | Impedimentos, evidências, subtarefas, progresso, fases e tipos de timeline (§7, §8, §10, §11) |
| **P5** | Integrações Chamado/PDI/Knowledge (§14–§16) — requer módulo/decisão de arquitetura |

## 4. Execução — P1 (F23-T001, 2026-10-06)

Pedido do usuário: "executame fase pode ser e documenta isso"; fase escolhida
pelo usuário = **P1**.

### Decisões de design (seguindo padrões existentes)

- `TaskPriority` = `Low/Medium/High/Critical` (espelha `IncidentSeverity`),
  persistida como `string(20)`; default **Medium**.
- `DueDate` = `DateOnly?` (coluna `date`, precedente `target_date` do PDI);
  prazo no passado é **permitido** (task atrasada ≠ erro de domínio).
- `ResponsibleId` = `Guid?` **sem FK** (precedente `manager_id`, ADR-029 —
  usuário excluído não quebra a task). Nome resolvido em leitura via
  `DomainUsers.DisplayName`.
- Responsável **não** exige vínculo de assignee: assignees = participantes
  (N:N), responsável = dono da identificação.
- Endpoint novo `PUT /api/v1/tasks/{id}/identification` (policy `Operate`) =
  comando de **substituição**: `priority` obrigatória (400 pt-BR quando
  vazia/inválida), `dueDate`/`responsibleId` nulos **limpam** o valor;
  responsável desconhecido → 404 pt-BR.
- Histórico: ações novas `PriorityChanged`, `DueDateChanged`,
  `ResponsibleChanged` — uma por campo alterado, detalhe no padrão
  "de → para" (`Medium → High`,
  `sem prazo → 2026-10-20`, `sem responsável → {id}` / `{id} → removido`);
  regravação idêntica = no-op (sem save nem histórico).
- Migração `20261006181221_AddTaskIdentification` gerada **sem `--no-build`**
  (regra do repo) com **backfill hand-edit** `defaultValue: "Medium"` (o EF
  gerava `""`, que quebraria o ValueConverter na leitura de tasks antigas —
  mesmo precedente de `alerts.source`).
- Colunas seguem o naming da tabela `tasks` (PascalCase, sem `HasColumnName`,
  como `Title`/`Kind`/`State`).

### Arquivos alterados

**Backend**
- `src/InfraControl.Domain/Enums/TaskPriority.cs` (novo)
- `src/InfraControl.Domain/Enums/TaskHistoryAction.cs` (+3 ações)
- `src/InfraControl.Domain/Entities/TaskItem.cs` (+`Priority`/`DueDate`/
  `ResponsibleId`, setters `SetPriority`/`SetDueDate`/`SetResponsible`,
  `Create` estendido)
- `src/InfraControl.Application/Tasks/TaskDto.cs` (+`priority`, `dueDate`,
  `responsibleId`, `responsibleName`)
- `src/InfraControl.Application/Tasks/Commands/TaskCommands.cs`
  (`CreateTaskCommand` + campos, `ParsePriority`, `ResolveResponsibleNameAsync`)
- `src/InfraControl.Application/Tasks/Commands/TaskIdentificationCommands.cs`
  (novo slice `SetTaskIdentificationCommand`)
- `src/InfraControl.Application/Tasks/Ports/ITaskAssigneeRepository.cs`
  (+`GetUserNamesAsync`)
- `src/InfraControl.Application/Tasks/Queries/TaskQueries.cs`
  (`responsibleName` no Search em 1 consulta e no Get)
- `src/InfraControl.Infrastructure/Persistence/Configurations/TaskItemConfiguration.cs`
- `src/InfraControl.Infrastructure/Repositories/TaskAssigneeRepository.cs`
- `src/InfraControl.Infrastructure/Persistence/Migrations/20261006181221_AddTaskIdentification.cs`
  (+Designer + snapshot)
- `src/InfraControl.Api/ViewModels/Tasks/CreateTaskViewModel.cs`
- `src/InfraControl.Api/ViewModels/Tasks/SetTaskIdentificationViewModel.cs` (novo)
- `src/InfraControl.Api/Controllers/Tasks/TasksController.cs` (+PUT identification)

**Testes**
- `tests/InfraControl.UnitTests/Tasks/TaskItemTests.cs` (+5 — domínio)
- `tests/InfraControl.UnitTests/Tasks/TaskIdentificationCommandTests.cs`
  (novo, 10 — substituição, guards 400/404, histórico, create com
  identificação)
- `tests/InfraControl.UnitTests/Tasks/TaskCommandTests.cs` /
  `TaskHistoryCommandTests.cs` (ajuste do construtor do handler)

**Frontend**
- `frontend/InfraControl.Web/src/services/api/tasks.ts` (campos no `TaskItem`,
  `createTask` estendido, `setTaskIdentification`)
- `frontend/InfraControl.Web/src/pages/TasksPage.tsx` (form com
  prioridade/prazo/responsável em criação **e** edição, botão "Identificação"
  no card, chip de prioridade colorido, prazo com vencido destacado,
  responsável exibido; `listUsers()` com fallback silencioso para Viewer)

**Docs** — este arquivo, `docs/00`, `docs/04`, `TASKS.md` (Fase 23),
`PROJECT_STATE.md`.

### Validação

| Check | Resultado |
|---|---|
| `dotnet build -c Release` | **0 erros** (9 avisos pré-existentes em arquivos não tocados) |
| Unit tests | **1599/1599** ✅ |
| Architecture tests | **115/115** ✅ |
| Filtro Tasks (unit) | **144/144** ✅ |
| Migração (`dotnet ef migrations add`, sem `--no-build`) | gerada; `dotnet ef migrations script` validado: `ADD "Priority" … DEFAULT 'Medium'`, `ADD "DueDate" date`, `ADD "ResponsibleId" uuid`, `IX_tasks_Priority` |
| Front `npm run lint` | **exit 0** ✅ |
| Front `npm run build` (tsc -b + vite) | **exit 0** ✅ |
| Testes de integração | **NÃO executados** — Docker indisponível no ambiente (decisão do usuário: "não roda o docker") |
| Migração aplicada em banco | **NÃO** — sem Docker não há PostgreSQL dev |
| Smoke E2E / commit | **NÃO** — ver pendências |

### Pendências da P1

1. **Integração**: rodar `dotnet test tests/InfraControl.IntegrationTests`
   (inclui `TaskIdentificationIntegrationTests` a ser criado se necessário)
   quando houver Docker.
2. **Migração**: aplicar no dev com `dotnet ef database update` (sem Docker).
3. **Commit**: ambiente sem `git` (instalação bloqueada — CDN de assets do
   GitHub inacessível da rede); DoD "commit realizado" fica **pendente**.
4. Editar título/descrição de task continua sem UI (escopo da P2+) —
   **resolvido na P2** (§5).

## 5. Execução — P2 (F23-T002, 2026-10-06)

Pedido do usuário: "segue" (continuidade após a P1). Fase escolhida na
priorização = **P2** — **front puro**: todos os endpoints (F7 CRUD/comentários/
histórico/vínculos + F8 SLA) já existiam; o que faltava era a rota de detalhe.

### Decisões de design

- **Rota `/tasks/:id`** = nível `read` (leitura para qualquer role
  autenticada, mesmo padrão de `/devices/:id`); toda escrita fica atrás de
  `Can operate` — o backend continua sendo a autoridade (403 no envelope).
- **Catálogo compartilhado** `components/tasks/taskCatalogs.ts`: estados,
  kinds, prioridade, prazo, rótulos do histórico (`historyActionLabel`) e
  `messageFor` extraídos da TasksPage — fonte única para kanban e detalhe,
  sem página importando página (evita o lint `react-refresh/
  only-export-components`).
- **SLA ausente é fluxo normal**: `GET /tasks/{id}/sla` com 404 vira "sem
  SLA aplicado" (`ApiError.status`), não banner de erro; aplicação de política
  só aparece nesse caso.
- **Listas de apoio são best-effort**: `listUsers`/`listDevices`/
  `listIncidents`/`listSlaPolicies` falhando (403 de Viewer em users, rede)
  silenciam e a UI cai para ids curtos (`shortId`) — a leitura da task nunca
  quebra por causa de vocabulário.
- Nomes de participantes/dispositivos/incidentes resolvidos **no front**
  (o backend devolve só ids nas listas de vínculo); responsável continua
  vindo `responsibleName` do backend.
- Exclusão (tarefa e comentário) exige `ConfirmDialog`; edições (dados e
  identificação) abrem `Dialog` com os mesmos limites do domínio
  (título 120, descrição/comentário 500).

### Arquivos alterados

**Frontend**
- `frontend/InfraControl.Web/src/components/tasks/taskCatalogs.ts` (novo)
- `frontend/InfraControl.Web/src/pages/TaskDetailPage.tsx` (novo — seções:
  descrição editável, comentários, histórico, estado, identificação,
  participantes, vínculos, SLA)
- `frontend/InfraControl.Web/src/pages/TasksPage.tsx` (importa o catálogo;
  título do card virou `Link` para o detalhe)
- `frontend/InfraControl.Web/src/services/api/tasks.ts` (+`TaskComment`,
  `TaskHistoryEntry`, `TaskSla`, `TaskSlaPause` e ~20 funções: get/update/
  delete task, comments CRUD, history, assignees, devices, incidents,
  sla/apply/pauses/pause/resume)
- `frontend/InfraControl.Web/src/app/router/index.tsx` (lazy + rota +
  rótulo ativo "Detalhe da Tarefa")

**Docs** — este arquivo, `TASKS.md` (Fase 23), `PROJECT_STATE.md`.

**Backend** — intocado (todos os endpoints já existiam).

### Validação

| Check | Resultado |
|---|---|
| Front `npm run lint` | **exit 0** ✅ |
| Front `npm run build` (tsc -b + vite) | **exit 0** ✅ (chunk `TaskDetailPage` 17.65 kB) |
| Testes front | não existe suíte de testes no front (lint/build = gate) |
| Backend | intocado — suíte unit/arch da P1 (**1599/1599 · 115/115**) intacta |
| Smoke E2E manual | **NÃO** — sem ambiente (Docker/git, ver pendências da P1) |

### Pendências da P2

1. Smoke manual da rota em ambiente com backend + banco (mesma trava da
   P1: Docker para integração/banco, git para commit).
2. Editar comentários (`PUT /tasks/comments/{id}`) não ganhou UI — exclusão
   e criação cobrem o fluxo; reavaliar se a spec pedir.

## 6. Execução — P3 (F23-T003, 2026-10-06)

Pedido do usuário: "proxima fase". A spec original "TASKS — Plano de
Execução" não está no repositório e o texto das seções não foi colado,
então o design partiu do que a gap analysis registrou (§5 = planejamento
e resultado; §12 = critérios de aceite; §13 = ciclo de replanejamento) e
dos **precedentes do repo**: CRUD de comentários (F7-T005) para os
critérios e replanejamento do PDI (`docs/30` §14: plano falhou → novo
plano + justificativa) para o ciclo. **Sujeito a revisão** quando a
spec original estiver disponível.

### Decisões de design

- **`TaskItem.PlannedStart`/`PlannedEnd`** (`DateOnly?`) = datas previstas
  de início/fim do plano; o **prazo de compromisso continua no `DueDate`**
  (P1) — campos distintos, sem duplicação de semântica.
- **`TaskItem.Result`** (`string?`, máx. 2000) = resultado da execução;
  `null`/vazio limpa. Setters silenciosos no domínio (mesma arquitetura
  da identificação P1) — o handler captura antes/depois e grava histórico.
- **`TaskCriterion`** (tabela nova `task_criteria`): `Description`
  (300), `IsDone`, `DoneAt`; FK `task_id` **cascade** e evento de domínio
  CRUD — mesmo molde exato dos comentários.
- **Comandos no molde da P1**: `UpdateTaskPlanningCommand` (substituição,
  nulls limpam, fim < início → 400), `UpdateTaskResultCommand` (no-op sem
  registro) — histórico "de → para" por campo alterado
  (`PlanningChanged`/`ResultChanged`), textos truncados para caber no
  `TaskHistory.DetailMaxLength` = 200.
- **Replanejo (`ReplanTaskCommand`, §13)**: início/fim/prazo + **motivo
  obrigatório (≤80 chars — garante o detail ≤200)** em um único
  comando; sem mudança real → 400 "Nada a replanejar"; **UMA** entrada
  `Replanned` com `campos alterados — motivo: …`.
- **Critérios (§12)**: `GET/POST {id}/criteria` + `PUT/DELETE criteria/
  {id}` (mesmas rotas/verbos dos comentários, leitura `Authorize`,
  escrita `Operate`); um registro `CriterionAdded/Updated/Removed` por
  mudança, com descrição truncada (190). Marcar/desmarcar carimba
  `DoneAt` (`DateTime.UtcNow` → null ao desmarcar).
- **+6 ações** no `TaskHistoryAction` (16 → 22) e rótulos no catálogo do
  front. DTO `TaskDto` ganhou `plannedStart`/`plannedEnd`/`result`.
- **Front**: card *Planejamento* (Editar + Replanejar) na coluna
  direita; cards *Resultado* (editável) e *Critérios* (checklist com
  progresso `x/y`, adicionar/marcar/excluir) na coluna principal;
  diálogo de replanejo com aviso de que o ciclo fica no histórico.

### Arquivos alterados

**Domínio**
- `Domain/Entities/TaskItem.cs` (`+PlannedStart/PlannedEnd/Result`,
  `ResultMaxLength`, `SetPlanning`/`SetResult`)
- `Domain/Entities/TaskCriterion.cs` (novo)
- `Domain/Enums/TaskHistoryAction.cs` (+6)
- `Domain/Events/TasksDomainEvents.cs` (+3 eventos de critério)

**Application**
- `Tasks/TaskDto.cs` (+3 campos), `Tasks/TaskCriterionDto.cs` (novo)
- `Tasks/Ports/ITaskCriterionRepository.cs` (novo)
- `Tasks/Commands/TaskPlanningCommands.cs` (novo — 3 comandos + formatos
  pt-BR + projeção `TaskDto` compartilhada)
- `Tasks/Commands/TaskCriterionCommands.cs` (novo — add/update/delete)
- `Tasks/Queries/TaskCriterionQueries.cs` (novo)

**Infra**
- `Persistence/Configurations/TaskCriterionConfiguration.cs` (novo)
- `Persistence/AppDbContext.cs` (+`DbSet<TaskCriterion>`)
- `Repositories/TaskCriterionRepository.cs` (novo)
- `Persistence/Migrations/20261006201131_AddTaskPlanningAndCriteria.cs`
  (novo — 3 colunas em `tasks` + tabela `task_criteria`; **gerada, não
  aplicada**)

**API**
- `ViewModels/Tasks/` — `UpdateTaskPlanning`, `UpdateTaskResult`,
  `ReplanTask` (novos) + `AddTaskCriterionViewModel.cs` (Add/Update)
- `Controllers/Tasks/TasksController.cs` (+7 endpoints)

**Frontend**
- `services/api/tasks.ts` (`+TaskCriterion` no `TaskItem`, `+7 funções`)
- `components/tasks/taskCatalogs.ts` (+6 rótulos de histórico)
- `pages/TaskDetailPage.tsx` (+3 cards, +3 diálogos, confirm de critério)

**Testes**
- `UnitTests/Tasks/TaskCriterionTests.cs` (novo — 9 de domínio)
- `UnitTests/Tasks/TaskPlanningCommandTests.cs` (novo — 12)
- `UnitTests/Tasks/TaskCriterionCommandTests.cs` (novo — 14)
- `IntegrationTests/Common/BaseIntegrationTest.cs` (+registro do repo)

**Docs** — este arquivo, `TASKS.md` (Fase 23), `PROJECT_STATE.md`,
`docs/04`.

### Validação

| Check | Resultado |
|---|---|
| `dotnet build -c Release` | **0 erros** ✅ (16 avisos pré-existentes, nenhum em arquivo tocado) |
| `dotnet test tests/InfraControl.UnitTests` (filtro Tasks) | **156/156** ✅ (35 novos) |
| `dotnet test tests/InfraControl.UnitTests` (total) | **1634/1634** ✅ (P1 tinha 1599) |
| `dotnet test tests/InfraControl.ArchitectureTests` | **115/115** ✅ |
| `dotnet ef migrations add` (sem `--no-build`) | gerada ✅ |
| `dotnet ef migrations script` | `ALTER TABLE tasks ADD "PlannedEnd"/"PlannedStart" date, "Result" text` + `CREATE TABLE task_criteria` (FK cascade + índice), colunas nullable → **sem backfill** ✅ |
| Front `npm run lint` | **exit 0** ✅ |
| Front `npm run build` | **exit 0** ✅ (chunk `TaskDetailPage` 24.66 kB) |
| Migração em banco / integração / smoke | **NÃO** — sem Docker (mesma trava das fases anteriores) |

### Pendências da P3

1. Aplicar `20261006201131_AddTaskPlanningAndCriteria` quando houver
   banco (`dotnet ef database update`).
2. Smoke manual (planejar → replanejar → marcar critério → ver
   histórico) em ambiente com backend + banco.
3. Edição da **descrição** de critério não ganhou UI (`PUT` existe e é
   testado; adicionar/marcar/excluir cobrem o fluxo) — mesmo corte do
   comentário na P2.
4. Confrontar este design com o texto original da spec (§5, §12, §13)
   quando ela estiver disponível e ajustar divergências.

## 7. Execução — P4 (F23-T004): split e P4a (2026-10-07)

A P4 original juntava 6 features (subtarefas, progresso, impedimentos,
evidências, fases e tipos de timeline) — grande demais para uma task
(AGENTS: "dividir se estiver grande"), então foi dividida:

- **P4a** (executada) — subtarefas + progresso;
- **P4b** — impedimentos estruturados (§10);
- **P4c** — evidências (§11) — requer decisão de armazenamento de
  arquivo antes (possível ADR);
- **P4d** — fases (§7) + tipos de timeline (§8) — dependem do texto da
  spec; validar quando colado.

### P4a — subtarefas e progresso (executada)

**Escopo**: auto-relação `TaskItem.ParentTaskId`, filtros de hierarquia
no Search, `PUT /tasks/{id}/parent`, progresso derivado e card na tela
de detalhe.

**Decisões** (sujeitas a revisão quando a spec estiver disponível):

- Subtarefa É task: mesma tabela `tasks`, FK auto-referente com
  `ON DELETE SET NULL` — pai excluído → filho sobrevive e vira raiz.
- Profundidade ≤ 2: "Subtarefas não podem ter subtarefas" (400 pt-BR) —
  elimina ciclos por construção; auto-relação → 400 no domínio.
- Progresso DERIVADO (sem campo manual): concluídas (Resolved/Closed) ÷
  não canceladas × 100 (`MidpointRounding.AwayFromZero`); sem subtarefas
  não canceladas → `progress` null. Cancelled fora do denominador
  (trabalho abandonado não é "falta").
- `Search` ganhou `parentId` (filhos de X) e `rootOnly` (só raízes) —
  mútuos → 400 "filtros incompatíveis". O kanban (`TasksPage`) passou a
  usar `rootOnly=true`: subtarefas vivem dentro do detalhe do pai.
- No-op (`PUT /parent` com o mesmo pai) → 200 sem histórico.
- Histórico em 3 frentes por movimentação: no filho `ParentChanged`
  ("de → para" com títulos truncados em 95 → detail ≤193) e em cada pai
  afetado `SubtaskAdded`/`SubtaskRemoved` (título do filho); criação com
  pai grava `SubtaskAdded` no pai. `TaskHistoryAction` 22 → **25**.
- `TaskDto` +4 campos (`parentTaskId`, `parentTitle`, `subtaskCount`,
  `progress`) — stats e títulos em 2 consultas por página, no padrão
  dos group joins já existentes (sem N+1).

**Arquivos-chave**: `TaskItem.cs` (+`ParentTaskId`/`SetParent`),
`TaskHistoryAction.cs` (+3), `TaskDto.cs` (+4 campos + `From`
estendido), novo `TaskSubtaskCommands.cs` (`SetTaskParentCommand`;
`TaskPlanningDtos` → `TaskDtoProjection`), `TaskCommands.cs`
(`CreateTaskCommand.ParentTaskId`), `TaskQueries.cs` (filtros + stats),
`ITaskItemRepository`/`TaskItemRepository` (+`GetSubtaskStatsAsync`,
`GetTitlesAsync`), `TaskItemConfiguration.cs` (FK SetNull + índice),
`TasksController.cs` (`PUT {id}/parent` + filtros), `tasks.ts`/
`TaskDetailPage.tsx` (card Subtarefas com barra de progresso, breadcrumb
"Subtarefa de …", criar em diálogo herdando o tipo do pai, desvincular
com confirm)/`TasksPage.tsx` (rootOnly + barra no card)/`taskCatalogs.ts`
(+3 rótulos), `TaskSubtaskCommandTests.cs` (16 testes).

**Migração**: `20261007145430_AddTaskSubtasks` gerada; script validado
(coluna + índice + FK `SET NULL`); **não aplicada** (sem Docker).

**Validação**: build 0 erros · unit **1650/1650** · arch **115/115** ·
front lint/build exit 0 (chunk `TaskDetailPage` 28.05 kB).

**Pendências da P4a**: migração pendente de ambiente; design derivado
sem a spec (revisar quando § subtarefas/progresso estiver disponível).

## 8. Execução — P4b (F23-T004): impedimentos (2026-10-07)

**Escopo** (§10): registro estruturado do bloqueio de uma task, com
resolução carimbada — o "porquê está travado" auditável.

**Decisões** (sujeitas a revisão quando a spec estiver disponível):

- Nova entidade `TaskImpediment` (`task_impediments`: `description` ≤300,
  `resolved_at timestamp?`, `detected_by`/`resolved_by` ausentes de
  propósito — quem detectou/resolveu vive no histórico com actor; FK da
  task `Cascade`, índice em `task_id`) — molde exato dos critérios, com
  eventos CRUD ADR-030 (`TaskImpedimentCreated/Updated/DeletedDomainEvent`).
- **O estado do workflow não muda**: registrar impedimento NÃO move a
  task para `Blocked` (mudança de ciclo de estados exigiria ADR). Um
  `Blocked` sem impedimento estruturado e vice-versa são válidos.
- Múltiplos impedimentos por task; `resolved_at` null = em aberto.
- `TaskHistoryAction` 25 → **30** (`ImpedimentAdded`, `ImpedimentUpdated`,
  `ImpedimentResolved`, `ImpedimentReopened`, `ImpedimentRemoved`) —
  um registro por aspecto alterado no PUT, no-op (nada mudou) sem
  registro, descrição truncada em 190/70+70/185 conforme o formato.

**Arquivos-chave**: `TaskImpediment.cs` + 3 eventos (`TasksDomainEvents`),
`TaskHistoryAction.cs` (+5), novo `TaskImpedimentCommands.cs`
(Add/Update/Delete) + `TaskImpedimentQueries.cs` + `TaskImpedimentDto.cs`
+ `ITaskImpedimentRepository`, config `task_impediments` + `DbSet` +
`TaskImpedimentRepository` (DI automática por `RepositoryRegistrar`),
`TasksController` (4 endpoints: `{id}/impediments` GET/POST,
`impediments/{id}` PUT/DELETE — leitura `Authorize`, escrita `Operate`),
`AddTaskImpedimentViewModel`/`UpdateTaskImpedimentViewModel`, front
(`tasks.ts` +interface +4 funções, `taskCatalogs` +5 rótulos,
`TaskDetailPage` card "Impedimentos (abertos/total)" com chips
Aberto/Resolvido, resolver/reabrir e excluir com confirm, diálogo de
registro ≤300), testes `TaskImpedimentTests` (9) +
`TaskImpedimentCommandTests` (15).

**Migração**: `20261007154956_AddTaskImpediments` gerada; script
validado (tabela + FK cascade + índice); **não aplicada** (sem Docker).

**Validação**: build 0 erros · unit **1674/1674** · arch **115/115** ·
front lint/build exit 0.

**Pendências da P4b**: migração pendente de ambiente; design derivado
sem a spec §10 (revisar quando disponível).

## 9. Execução — P4c (F23-T004): evidências (2026-10-07)

**Escopo** (§11): anexo de **arquivo de evidência** na task — primeiro
upload do produto. **Decisão de armazenamento pedida ao usuário no
checkpoint**: escolhida "Disco local + ADR-033" (detalhes e
consequências em `docs/adr/ADR-033-LOCAL-EVIDENCE-FILE-STORAGE.md`).

**Decisões** (sujeitas à revisão da spec, dentro do ADR-033):

- Nova entidade `TaskEvidence` (`task_evidences`: `FileName` ≤200
  normalizado para o último segmento, `StoredName` = **GUID +
  extensão** gerado no domínio, `ContentType` ≤100 default
  `application/octet-stream`, `FileSize` long, `Note?` ≤300; FK da task
  `Cascade`, índice em `task_id`) — o nome original é **só metadado**,
  nunca caminho (travessia fora por construção; a porta revalida).
- Limites no domínio (400 pt-BR): **10 MB**, allowlist de extensões
  (`png jpg jpeg gif webp pdf txt log csv json xml zip`), arquivo
  vazio, nome/nota/tipo acima dos limites.
- Porta `IEvidenceFileStore` (Application) + `LocalEvidenceFileStore`
  (Infrastructure, `Storage:EvidencePath` default `evidence-files`,
  guard de `storedName`, erro de IO logado e engolido na exclusão —
  banco é fonte de verdade).
- `TaskHistoryAction` 30 → **32** (`EvidenceAdded`, `EvidenceRemoved`
  com formato `"nome.ext (kB)"` truncado em 150 + sufixo ≤200);
  exclusão grava histórico **antes** do Remove e apaga o arquivo
  **depois** do banco.
- **Client HTTP do front** (`client.ts`): extensão mínima e
  deliberada — `FormData` não recebe `Content-Type` fixo (o fetch monta
  o boundary) e nova opção `raw` devolve `blob` para download; lógica
  de sessão/antiforgery intacta (401 → `signedOut`, `X-XSRF-TOKEN` em
  toda escrita, inclusive multipart).
- Sem `GET /{id}` de evidência (molde das listas de
  comentários/critérios): `GET /tasks/{id}/evidences`,
  `GET /tasks/evidences/{id}/download` (leitura `Authorize`,
  `Content-Disposition` com o nome original), `POST
  /tasks/{id}/evidences` multipart (Operate) e `DELETE
  /tasks/evidences/{id}` (Operate → arquivo + registro).

**Arquivos-chave**: `TaskEvidence.cs`, eventos `TaskEvidenceCreated/
DeletedDomainEvent`, `TaskHistoryAction.cs` (+2), novos
`TaskEvidenceCommands.cs`/`TaskEvidenceQueries.cs`/`TaskEvidenceDto.cs`
/`ITaskEvidenceRepository`/`IEvidenceFileStore`, `StorageOptions.cs` +
`LocalEvidenceFileStore.cs` + `TaskEvidenceConfiguration.cs` +
`TaskEvidenceRepository.cs` (DI automática) + `DependencyInjection.cs`
(storage manual — interface sem par repositório), `appsettings.json`
(`Storage`), `TasksController` (+4 endpoints), front (`client.ts`
FormData/raw, `tasks.ts` +interface +4 funções incl. download por blob,
`taskCatalogs` +2 rótulos + `formatFileSize` + `EVIDENCE_ACCEPT`,
`TaskDetailPage` card "Evidências" com upload (arquivo + nota),
download por clique e exclusão com confirm), testes `TaskEvidenceTests`
(13) + `TaskEvidenceCommandTests` (14, com store em diretório
temporário — sem Docker).

**Migração**: `20261007181534_AddTaskEvidences` gerada; script
validado (tabela + FK cascade + índice); **não aplicada** (sem Docker).

**Validação**: build 0 erros · unit **1701/1701** · arch **115/115** ·
front lint/build exit 0.

**Pendências da P4c**: migração pendente de ambiente; **backup do banco
não leva os arquivos em disco** e exclusão em massa de task deixa órfão
o arquivo (ambas registradas no ADR-033 como consequências aceitas);
aceites de tipo espelhados entre domínio e front (revisar com a spec).

## 10. Execução — P4d (F23-T004): fases e timeline (2026-10-07)

**Escopo** (§7 fases + §8 tipos de timeline): as duas lacunas que a
gap analysis marcava como dependentes do texto da spec — **o texto não
está no repositório**, então as duas decisões de arquitetura foram
**perguntadas ao usuário no checkpoint** e ele escolheu:

1. **§7 — "Etapas nomeadas por task"**: nova tabela `task_phases`
   (nenhum conjunto fixo imposto pelo domínio);
2. **§8 — "Timeline unificada tipada"**: composição em **leitura**,
   sem tabela nova.

**Decisões** (escolhidas pelo usuário; sujeitas à revisão da spec):

- Entidade `TaskPhase` (`task_phases`: `Name` ≤120 required, `Position`
  int, `IsCurrent` bool, `CompletedAt?` timestamp; FK da task `Cascade`,
  índice `(task_id, position)`). Fases são **ortogonais ao workflow**
  (§6): estado = situação, fase = etapa de execução; registrar fase não
  muda estado.
- Posição com **gaps aceitos**: exclusão no meio deixa buraco (a
  leitura ordena por `Position`; novas fases entram em `max+1`).
- **Ponteiro único** de "fase atual": o `PUT` com `current: true` limpa
  os irmãos no mesmo save; a **primeira fase já nasce atual** (sem
  histórico extra — é consequência do cadastro); excluir a fase atual
  **não promove** ninguém (sem mágica).
- Conclusão carimbada (`CompletedAt`), reabertura limpa; um histórico
  **por aspecto alterado** no PUT (nome/ponteiro/conclusão — até 3 no
  mesmo comando), no-op = 200 sem registro; nome vazio/>120 → 400
  pt-BR; 404 pt-BR para task/fase inexistente.
- `TaskHistoryAction` 32 → **38** (`PhaseAdded`, `PhaseUpdated`,
  `PhaseCurrentChanged`, `PhaseCompleted`, `PhaseReopened`,
  `PhaseRemoved` — nome truncado no detail de 190).
- Endpoints: `GET/POST /tasks/{id}/phases`, `PUT /tasks/phases/{id}`
  (Operate), `DELETE /tasks/phases/{id}` (Operate); leitura `Authorize`.
- **Timeline**: `GET /tasks/{id}/timeline?types=&limit=` — funde
  **histórico + comentários** (impedimentos/evidências/SLA/fases entram
  pelo histórico — mesmas fontes, **sem duplicata**; a ação `Commented`
  do histórico é descartada e o comentário vem da fonte rica com texto
  integral), **13 tipos** (`criacao, edicao, estado, atribuicao,
  vinculo, planejamento, criterio, subtarefa, bloqueio, anexo, sla,
  fase, comentario` — mapeados em `TaskTimelineTypes`), `types`
  comma-separated (desconhecido → 400 pt-BR), `limit` clamp 1..500
  default 100, mais recente primeiro, ator já resolvido (portas de
  histórico/comentário já devolvem nome).
- Front: card **"Fases (x/y)"** (checkbox concluir/reabrir, chip
  "Atual", botão tornar atual, excluir com confirm, cadastro inline;
  rename sem UI — PUT testado, como nos critérios) e o card
  **"Histórico" virou "Linha do tempo"** com chips de tipo (filtros
  locais sobre a resposta, `limit=500`).

**Arquivos-chave**: `TaskPhase.cs` + eventos `TaskPhaseCreated/
Updated/DeletedDomainEvent` + enum (+6), `TaskPhaseDto.cs`,
`ITaskPhaseRepository`, `TaskPhaseCommands.cs` (Add/Update/Delete),
`TaskPhaseQueries.cs`, `TaskTimelineTypes.cs`,
`TaskTimelineEntryDto.cs`, `TaskTimelineQueries.cs`, config +
`TaskPhaseRepository` + `DbSet` (DI automática), `appsettings` (sem
mudança), `TasksController` (+5 endpoints), front (`tasks.ts`
+interface `TaskPhase`/`TaskTimelineEntry` +8 funções, `taskCatalogs`
+6 rótulos → 38 + `timelineTypes`/`timelineTypeVisual`,
`TaskDetailPage` card Fases + Linha do tempo), testes `TaskPhaseTests`
(9) + `TaskPhaseCommandTests` (13) + `TaskTimelineQueryTests` (10).

**Migração**: `20261007191602_AddTaskPhases` gerada; script validado
(tabela + FK cascade + índice); **não aplicada** (sem Docker).

**Validação**: build 0 erros · unit **1733/1733** · arch **115/115** ·
migração gerada + script validado · front lint/build exit 0.

**Pendências da P4d**: migração pendente de ambiente; designs de §7/§8
foram escolhidos pelo usuário **sem o texto original da spec**
(revisar quando o texto aparecer); rename de fase sem UI (PUT testado);
timeline carrega no máx. 500 entradas (teto do clamp).

## 11. Próximas fases

P1–P3, P4a–P4d, **P5a**, **P5b** e **P5c** concluídas — **F23-T004
cobre subtarefas, progresso, impedimentos, evidências, fases e
timeline**; **F23-T005 fechou** com os vínculos Task↔Knowledge (§13),
Task↔PDI (§14) e a conferência §14 Chamado = Incident (§15 — sem sobra
de código). Restam os itens da spec não cobertos pelo plano (§6 ciclo de
estados — qualquer mudança exige ADR).

## 12. Specs recebidas — Knowledge (docs/32) e Indicadores (docs/33)

Em 2026-10-07 o usuário colou as specs **"KNOWLEDGE — Plano de Base de
Conhecimento"** (docs/32) e **"INDICADORES E RELATÓRIOS — Plano"**
(docs/33); ambas arquivadas integralmente no repo. Gap × código:

| Spec Knowledge (docs/32) | Código (F10) |
|---|---|
| §5 tipos (14) | **concluído (K1/F24-T001)** — campo `type` (14 tipos, conjunto fechado, 400 pt-BR) |
| §6 identificação: descrição, tags, autor, responsável | **concluído (K1/F24-T001)** — `description`, `tags` (tabela `knowledge_article_tags`), `author_id` (automático/imutável), `responsible_id` (404) + nomes nas queries; conteúdo estruturado continua ausente |
| §6 conteúdo estruturado (objetivo, contexto, pré-requisitos, procedimento, observações, problemas, soluções, limitações, referências) | **ausente** — `Content` único (20k) |
| §6 governança: versão, próxima revisão, responsável pela revisão, aprovador | **ausente** |
| §7 status: RASCUNHO → EM REVISÃO → APROVADA → PUBLICADA → EM REVISÃO → ATUALIZADA + Arquivada/Obsoleta/Rejeitada | **concluído (K2/F24-T002, ADR-039)** — 8 estados com matriz de transições, 409 em rótulos pt-BR e estados não publicados ocultos de leitores |
| §8 versionamento (v1.0→, alteração/autor/data/motivo/aprovador) | **ausente** |
| §9 rastreabilidade de origem (Task/PDI/Chamado/…) | **ausente** |
| §10 relação com Tasks (criada a partir de, utilizada por, atualizada por) | **ausente** — vínculos só de device/incident/service |
| §11 relação com chamados ("Knowledge utilizada na resolução") | **existe** — `knowledge_article_incidents` (Chamado = Incident, decisão do usuário) |
| §12 revisão periódica (180 dias) | **ausente** |
| §13 busca (título, conteúdo, categoria, tags, autor, sistema, setor, tipo, keywords) | **parcial** — `term` em Title/Content |
| §2–§4 criação automática Task/PDI → rascunho | **ausente** (sem infraestrutura de IA no repo) |
| §15 indicadores | **ausente** — docs/33, módulo inteiro novo |

**Spec Indicadores (docs/33)**: camada inteira ausente (indicadores de
Tasks/PDI/Knowledge, dashboards colaborador/gestor/admin, relatórios,
histórico mensal, exportação PDF/Excel/CSV, IA de interpretação). Só
existe o dashboard inicial genérico (F14 com mock).

### Trilhas de execução proposta (dividir para conquistar)

- **Trilha P5 (Tasks §14–§16)** — ✅ **Fechada**: P5a Task↔Knowledge
  (**§13**), P5b Task↔PDI (**§14**) e P5c conferência Chamado = Incident
  (**§15**, sem sobra) — F23-T005 concluída.
- **Trilha K (Knowledge)** → **Fase 24 do TASKS.md (F24-T001–T007)** —
  K1 tipos + identificação; K2 workflow dos 8 status (concluída); K3 versionamento;
  K4 origem/rastreabilidade (pode absorver o vínculo do P5a); K5 revisão
  periódica; K6 geração automática (definir IA vs rascunho montado dos
  registros); K7 busca avançada.
- **Trilha I (Indicadores)** → **Fase 25 do TASKS.md (F25-T001–T006)** —
  módulo novo (indicadores → dashboards → relatórios →
  histórico/exportação → automáticos/IA), escopo de cada task confirmado
  na execução.

IA (docs/32 §14, docs/33 §16): **não há infraestrutura de IA no repo**
— definir orçamento/arquitetura antes de qualquer geração automática.

## 13. Execução — P5a (F23-T005, 2026-10-07)

Vínculo **Task ↔ Knowledge** (spec Tasks §16 × docs/32 §10), decidido
pelo usuário no question tool: executar "P5a: vínculo Task↔Knowledge"
e alvo do P5b = "Task ↔ PDI (plano do usuário)"; §14 Chamado = módulo
`Incident` existente (decisão anterior).

### Decisões de design

- Espelho do vínculo F7-T004 (`task_incidents`): tabela
  `task_knowledge_articles`, PK composta `(task_id, article_id)`, FKs
  **Cascade/Cascade** — task e artigo têm exclusão (ADR-030 §5) e
  apagar qualquer um limpa o vínculo (mesmo comportamento de
  `knowledge_article_incidents`).
- Nome por camada: entidade `TaskKnowledgeArticle` (coluna
  `article_id`), commands `Link/UnlinkTaskArticleCommand`, rota
  `POST/DELETE /tasks/{id}/articles/{articleId}`, DTO
  `articleCount`/`articleIds`, histórico `KnowledgeLinked`/
  `KnowledgeUnlinked` (38→**40**, tipo de timeline `vinculo`).
- 404 pt-BR (task/artigo/vínculo), 409 em duplicidade; escrita
  `Operate`, leitura `Read`.
- DTOs de mutação (estado/identificação/planejamento/subtarefas) **não
  enriquecem** `articleIds` — mesmo desenho parcial já aceito (o front
  recarrega via GET após toda mutação); Get/Search enriquecem.

### Arquivos alterados

- Domínio: `TaskKnowledgeArticle.cs` (novo), `TaskHistoryAction` +2
  (40), `TasksDomainEvents` +2 eventos.
- Application: `ITaskKnowledgeArticleRepository` (novo),
  `TaskKnowledgeCommands.cs` (novo), `TaskDto` +2 campos,
  `TaskQueries` (2 handlers + group join), `TaskTimelineTypes`
  (ações novas → `vinculo`; doc 40).
- Infra: `TaskKnowledgeArticleConfiguration` (novo),
  `TaskKnowledgeArticleRepository` (novo), `DbSet`, migração
  `20261007225543_AddTaskKnowledgeArticles` (script validado: tabela +
  2 FKs Cascade + índice; **não aplicada**).
- Api: `TasksController` +2 endpoints (`204`).
- Front: `tasks.ts` (+`articleCount`/`articleIds`, +
  `linkTaskArticle`/`unlinkTaskArticle`), `taskCatalogs` +2 rótulos →
  40, `TaskDetailPage` card **"Knowledge"** (select de vínculo, título
  resolvido com fallback para id curto).
- Testes: `TaskKnowledgeArticleTests` (3) + `TaskArticleCommandTests`
  (10 — inclui gravação do histórico e tipo da timeline); 6 pontos de
  construção dos handlers de query atualizados; `BaseIntegrationTest`
  +registro. Integration tests da P5a **não criados** (mesmo critério
  das fases P4 — suíte parada no ambiente).

### Validação

- build 0 erros · unit **1746/1746** · arch **115/115** · script da
  migração OK · front lint exit 0 / build ✓.

### Pendências da P5a

- Migração não aplicada + testes de integração não executados (Docker).
- ~~P5b e P5c seguem abertos~~ — P5b concluída em 2026-10-07 (§14);
  resta P5c.

## 14. Execução — P5b (F23-T005, 2026-10-07)

Vínculo **Task ↔ PDI** (spec Tasks §15), alvo decidido pelo usuário no
question tool: "Task ↔ PDI (plano do usuário)".

### Decisões de design

- **Chave por `user_id`**: a API do PDI identifica o plano pelo dono
  (`PUT /pdi/{userId}`, 1 plano vigente por usuário) e o DTO do front
  (`Pdi`) não expõe o PK — a junção usa o dono, não `plan.id`.
- Tabela `task_pdis`, PK composta `(task_id, user_id)`; FK da task
  **Cascade**; **`user_id` sem FK** (critério do ADR-029 para refs de
  usuário — `UserId`/`ManagerId` do próprio plano também não têm FK).
  A existência do plano é validada no handler via
  `IPdiRepository.GetByUserIdAsync` (linkar exige PDI criado → 404).
- Nome por camada: entidade `TaskPdi`, commands
  `Link/UnlinkTaskPdiCommand`, rota `POST/DELETE
  /tasks/{id}/pdi/{userId}` (segmento singular, molde do módulo
  `/pdi/{userId}`), DTO `pdiCount`/`pdiUserIds`, histórico
  `PdiLinked`/`PdiUnlinked` (40→**42**, tipo de timeline `vinculo`).
- 404 pt-BR (task/PDI/vínculo), 409 em duplicidade; escrita `Operate`.
- Card **"PDI"** no detalhe: nomes dos donos resolvidos por
  `listUsers` (a leitura do card **não** depende da policy `Operate`
  do `/pdi` — Viewer vê os vinculados); select de vínculo usa
  `listPdis` (403 de Viewer silencia → lista vazia, padrão já
  estabelecido).

### Arquivos alterados

- Domínio: `TaskPdi.cs` (novo), `TaskHistoryAction` +2 (42),
  `TasksDomainEvents` +2 eventos.
- Application: `ITaskPdiRepository` (novo), `TaskPdiCommands.cs`
  (novo), `TaskDto` +2 campos, `TaskQueries` (2 handlers + group
  join), `TaskTimelineTypes` (ações → `vinculo`; doc 42).
- Infra: `TaskPdiConfiguration` (novo), `TaskPdiRepository` (novo),
  `DbSet`, migração `20261007232706_AddTaskPdis` (script validado:
  tabela + FK Cascade única — `user_id` sem FK, por decisão; **não
  aplicada**).
- Api: `TasksController` +2 endpoints (`204`).
- Front: `tasks.ts` (+`pdiCount`/`pdiUserIds`, +`linkTaskPdi`/
  `unlinkTaskPdi`), `taskCatalogs` +2 rótulos → **42**,
  `TaskDetailPage` card **"PDI"**.
- Testes: `TaskPdiTests` (3) + `TaskPdiCommandTests` (10 — inclui
  gravação do histórico e tipo da timeline; seed do plano via
  `IndividualDevelopmentPlan.Create` + `PdiChangeActor`); ajustes nos 6
  arquivos que constroem os handlers de query (13 construções — 6º
  parâmetro); `BaseIntegrationTest` +registro. Integration tests da P5b
  **não criados** (mesmo critério das fases P4/P5a).

### Validação

- build 0 erros · unit **1759/1759** · arch **115/115** · script da
  migração OK · front lint exit 0 / build ✓.

### Pendências da P5b

- Migração não aplicada + testes de integração não executados (Docker).
- Resta **P5c** (Chamado = Incident — conferir sobra dos vínculos já
  existentes) para fechar F23-T005 → conferência em §15.

## 15. Conferência — P5c (F23-T005, 2026-10-07)

Última fatia de P5: §14 da spec Tasks (**Chamado, integração**) com a
decisão já registrada — **Chamado = módulo `Incident` existente**, sem
módulo novo. Conferência do que os vínculos existentes cobrem:

| Verificação | Situação |
|---|---|
| Task ↔ Chamado (lado da task) | **existe** (F7-T004) — tabela `task_incidents` (PK composta, FKs Cascade), endpoints `POST/DELETE /tasks/{id}/incidents/{incidentId}` (404/409 pt-BR, `Operate`), `TaskDto.incidentIds`, card "Incidentes" no detalhe, histórico `IncidentLinked/IncidentUnlinked` (tipo `vinculo`), testes (`TaskIncidentCommandTests`) |
| Knowledge utilizada na resolução do Chamado (spec Knowledge §11) | **existe** — `knowledge_article_incidents`, commands `Link/UnlinkKnowledgeArticleIncidentCommand` no módulo Knowledge (artigo é o dono do vínculo) |
| Chamado → lista de Tasks vinculadas (lado do incidente) | **não existe** — e é o precedente do repo: nenhum vínculo é exposto pelo lado reverso (a página de Incidentes é lista, sem detalhe com links; knowledge↔incident só pelo artigo). Não é gap da §14, que pede integração **do módulo Tasks** com o Chamado — coberta acima. |
| Task originada em Chamado / origem da Knowledge vinda de Chamado | fora da §14 — a segunda é escopo de **K4** (rastreabilidade de origem, spec Knowledge §9) na Fase 24 |

**Resultado: nenhuma sobra de código.** F23-T005 fecha com P5a (§13),
P5b (§14) e esta conferência; a única ligação restante do §14
(task↔chamado reversa) só entraria por pedido explícito do usuário
(não inventar requisitos).

## 16. Execução — K1 / F24-T001 (Fase 24, 2026-10-07)

**Tipos e identificação** do artigo (spec Knowledge `docs/32` §5–§6) —
primeira task da Fase 24, na autorização "F23 (P5c) + Fase 24 inteira".

### Decisões de design

- **Tipo**: enum `KnowledgeArticleType` com os 14 valores da §5,
  guardado como **string** (coluna `type` 32) e exposto no DTO como
  **nome do enum em string** (`"Procedure"`, …) — contrato explícito
  apesar do MVC não registrar `JsonStringEnumConverter` (o `status`
  numérico legado continua como está). **Opcional**: nulo = sem tipo
  (a spec não define default para os 10 artigos do seed).
- **Descrição**: `string?` de **500** (campo de identificação, não
  conteúdo — o limite é decisão da task; spec não número).
- **Tags**: **coleção primitiva do EF** (`IReadOnlyList<string>` +
  backing field explícito) → coluna única `text[]` no Postgres/JSON no
  Sqlite, em vez de tabela N:N — menos peças e o filtro do K7 usa
  `Contains` (traduzido para `= ANY`/`json_each`). Normalização no
  domínio (`KnowledgeArticle.NormalizeTags`): trim + lowercase + dedup
  na ordem; ≤50 por tag e ≤20 no total (pt-BR → 400). Linhas antigas =
  `[]`/NULL com getter que nunca devolve null.
- **Autor × responsável**: `AuthorId` vem do **servidor**
  (`ICurrentUserContext.UserId`) no `POST` e é **imutável**
  (`SetIdentification` não toca); `ResponsibleId` vem do cliente com
  validação 404 `Usuário '{id}' não encontrado.` (molde do
  responsável de Task). As duas FKs para `DomainUsers` são **SetNull**
  — mesma decisão da categoria do artigo; `Restrict` do `TaskComment`
  bloquearia a exclusão dura de usuário que o produto tem.
- **Nomes no DTO** (`authorName`/`responsponsibleName`): resolvidos no
  backend por `IKnowledgeArticleRepository.GetUserNamesAsync` (join,
  DisplayName com fallback e-mail) — leitura sem depender da policy do
  `/admin/users`; a busca resolve a página inteira num round-trip.
- **DTO por init-props + factory `From/FromAsync`**: os campos novos
  não quebram nenhuma construção existente e centralizam o mapeamento
  dos 4 pontos (Create, ChangeStatus, Search, Get).
- Commands `Create`/`Update` estendidos com parâmetros **opcionais**
  no fim — os 14 call sites antigos (unidade + integração) seguem
  compilando; `Update` não mexe no `Status` nem no autor.

### Arquivos alterados

- Domínio: `Enums/KnowledgeArticleType.cs` (novo, 14 valores),
  `KnowledgeArticle` (+Type/Description/AuthorId/ResponsibleId/Tags,
  `SetIdentification`, `NormalizeTags`, constantes 500/50/20).
- Application: `KnowledgeArticleDto` (+7 init-props, `From`/`FromAsync`
  /`NameIds`), `IKnowledgeArticleRepository` (+`UserExistsAsync`,
  `GetUserNamesAsync`), `KnowledgeArticleCommands` (Create/Update
  estendidos, `ParseType`, `EnsureResponsibleExistsAsync`),
  `KnowledgeArticleQueries` (mapeamento novo com batch de nomes).
- Infra: `KnowledgeArticleRepository` (+2 métodos),
  `KnowledgeArticleConfiguration` (+5 colunas, 2 FKs SetNull, `tags`
  com backing field), migração
  `20261008001827_AddKnowledgeArticleIdentification` (script validado:
  `type` 32, `description` 500, FKs SetNull, `tags text[]` — **não
  aplicada**).
- Api: ViewModels +`Type`/`Description`/`Tags`/`ResponsibleId`,
  controller repassa nos 2 endpoints.
- Front: `knowledge.ts` (+7 campos no tipo, +4 no input de criação),
  `knowledgeCatalogs.ts` (**novo** — 14 rótulos pt-BR), `KnowledgePage`
  (form com tipo/responsável/descrição/tags + card com chip de tipo,
  descrição, chips de tags e rodapé "Autor · Responsável"; select de
  responsável carrega `listUsers`, falha → select vazio).
- Testes: `KnowledgeArticleIdentificationTests` (**novo**, 7) +
  `KnowledgeArticleCommandTests` +5 (criação com identificação,
  responsável 404, tipo 400, update mantém autor, busca com nomes) —
  `CreateContextAsync` agora semeia o usuário corrente (FK SetNull) e
  `FakeCurrentUser` aceita id. Integration tests da K1 **não criados**
  (critério das fases sem Docker).

### Validação

- build 0 erros · unit **1771/1771** (+12) · arch **115/115** · script
  da migração OK · front lint exit 0 / build ✓.

### Pendências da K1

- Migração não aplicada + testes de integração não executados (Docker).
  Os 10 artigos do seed ficam sem tipo/descrição (nulo = "sem tipo").

## 17. Execução — K2 / F24-T002 (Fase 24, 2026-10-07)

**Workflow dos status** (spec `docs/32` §7) — transição de status é
regra de domínio, então **ADR antes do código** (`AGENTS.md`):
**ADR-034** (`docs/adr/ADR-034-KNOWLEDGE-STATUS-WORKFLOW.md`).

### Decisões do ADR-034

- **8 estados** (a §7 repete "EM REVISÃO" — é um único `InReview`):
  `Draft/InReview/Approved/Published/Updated/Archived/Obsolete/
  Rejected`; rótulos pt-BR **verbatim da §7** ("Publicada"/"Arquivada"
  — muda o gênero do legado).
- **Números legados estáveis**: `Draft=0/Published=1/Archived=2`
  preservados (contrato do front), novos 3–7; armazenamento segue
  string → **nenhuma migração de dados** (o seed usa os 3 nomes
  antigos, continua válido).
- **Matriz de 16 pares válidos**: cadeia `Draft→InReview→Approved→
  Published`; revisão do publicado `Published→InReview→Updated`;
  `Published/Updated→Archived/Obsolete`; saídas `Archived/Obsolete/
  Rejected→Draft`. `InReview` aceita os dois fechamentos (o estado não
  guarda origem — registrado como consequência). Fora da matriz ou
  repetir estado = 409 (mecanismo inalterado).
- **Visibilidade generalizada** (F10-T003 → ADR-034 §4): `includeDrafts`
  virou **`includeUnpublished`**; a fonte única do conjunto é
  `KnowledgeArticleStatusRules.UnpublishedStatuses` (Domínio — a
  coleção entra na expressão LINQ e vira `IN`, traduzível nos dois
  providers; `IsUnpublished()` extensão para o `GET/{id}` = 404).
  Nunca-publicados = `Draft/InReview/Approved/Rejected`.
- 400 com as 8 opções; rota/policy/DTO **inalterados** (superfície
  continua `POST {id}/status`, `Operate`).

### Arquivos alterados

- Domínio: `KnowledgeArticleStatus` (8 valores, doc por estado),
  `KnowledgeArticleStatusRules` (**novo**), `KnowledgeArticle.
  TransitionTo` (matriz nova).
- Application: mensagem de 400 (8 opções), port/repo
  `includeDrafts→includeUnpublished`, `CanSeeDrafts→CanSeeUnpublished`,
  `GET/{id}` por `IsUnpublished()`, cabeçalho das queries.
- Infra: filtro do `SearchAsync` com o conjunto do domínio (`IN`).
- Front: `knowledge.ts` +5 constantes (3–7), `KnowledgePage.
  statusConfig` com 8 chips na ordem da §7 (rótulos femininos).
- Testes: `KnowledgeArticleTests` transições reescritas (8 → cadeia,
  revisão, rejeição, 4 conflitos + regra de visibilidade) ;
  `KnowledgeArticleStatusCommandTests` reescrito (12 — seed por
  caminho válido `PathTo`, cadeia via handler, revisão→Updated, 409×2,
  404, 400 com 8 opções, busca oculta/mostra os 4+4, GET InReview 404);
  `CrudDomainEventLote3Tests` (cadeia até Published);
  integração `KnowledgeArticleIntegrationTests` (3 testes na cadeia
  nova — compilam, não executam sem Docker).
- Docs: **ADR-034** (novo), docs/04 §Publicação reescrita.

### Validação

- build 0 erros · unit **1775/1775** (+4 líquidos) · arch **115/115** ·
  front lint exit 0 / build ✓.

### Pendências da K2

- Testes de integração não executados (Docker); migração **nenhuma**
  nova (enum em string).

## 18. Execução — K3 / F24-T003 (Fase 24, 2026-10-07)

**Versionamento** (spec `docs/32` §8 — "uma Knowledge nunca perde
versões anteriores"). Sem precedente de snapshot no repo (o PDI tem
timeline de entradas, não cópia de conteúdo); o molde foi o próprio
`PdiHistoryEntry` para os metadados com ator resolvido.

### Modelo (docs/04 §Versionamento)

- Tabela **`knowledge_article_versions`**: snapshot completo
  (title/content/description/type/tags/category — a "alteração" da §8)
  + `reason` (motivo, obrigatório ≤500), `author_id` (autor),
  `CreateDateTime` (data), `approved_by_id`/`approved_at` (aprovador,
  carimbado na aprovação do K2). Chave única `(article_id, version)`;
  FK artigo **Cascade** (apagar apaga o histórico), autor/aprovador
  **SetNull** (molde K1); `tags` = coleção primitiva (JSON/`text[]`).
- **Numeração no domínio** (`KnowledgeArticle.BumpVersion` /
  `MarkVersionApproved`): criação **v1.0**; cada edição abre minor
  (1.1, 1.2…); aprovar põe o piso → próxima edição abre major novo
  (2.0, 3.0…) — cadeia da §8 (v1.0 → v1.1 → v2.0) sem renumerar linha
  já gravada. `version` + `version_major_floor` viram colunas no
  artigo (defaults `1.0`/`1` preenchem as linhas antigas).
- **Snapshot `KnowledgeArticleVersion.SnapshotOf`** valida motivo em
  pt-BR (vazio/`>500` = 400/ArgumentException) e **copia** as tags (o
  artigo segue vivo). `Approve` carimba só o **primeiro** aprovador.

### Pontos de disparo (handlers)

- `Create` → linha v1.0 (motivo "Criação do artigo");
- `Update` → `BumpVersion()` + snapshot (command ganhou **`Reason`
  obrigatório** — posição após `CategoryId`, antes dos opcionais da
  K1; VM `[Required]` "O motivo da alteração é obrigatório.");
- `ChangeStatus` → se `Approved`: `MarkVersionApproved()` no artigo +
  `Approve(currentUserId)` na versão corrente (handlers ganharam
  `IKnowledgeArticleVersionRepository` + `ICurrentUserContext`).
- DI automática (RepositoryRegistrar — `IXxxRepository`/`XxxRepository`
  casam o padrão); DTO do artigo ganhou `version` (init-prop, molde K1)
  + `knowledge.ts` ganhou o campo no contrato.

### Endpoints (Read, mesma visibilidade ADR-034 §4)

- `GET {id}/versions` → `ListKnowledgeArticleVersionsQuery` — ordenado
  por **número** (major, minor) desc (o timestamp empataria no mesmo
  tick e "1.10" não ordena com "1.2"), `isCurrent` = linha == versão
  corrente, autor/aprovador nomeados num round-trip;
- `GET {id}/versions/compare?from=&to` →
  `CompareKnowledgeArticleVersionsQuery` — dois snapshots lado a lado
  (o diff é de quem consome); lado inexistente = 404
  `Versão 'x' não encontrada.`; estado nunca-publicado sem escrita =
  404 (mesmo check do `GET/{id}`).

### Migração `20261008004732_AddKnowledgeArticleVersioning`

- `knowledge_articles` + `version` (`DEFAULT '1.0'`) +
  `version_major_floor` (`DEFAULT 1`); tabela + 3 índices (único no
  par artigo/versão); **backfill idempotente** (`INSERT…SELECT` com
  `NOT EXISTS`): artigos existentes viram a v1.0 do próprio histórico
  (motivo "Criação do artigo", `tags` com `COALESCE` para `{}`).
- Script validado (`dotnet ef migrations script`): `ALTER TABLE … ADD
  version … DEFAULT '1.0'` backfilla as linhas antigas; `gen_random_uuid()`
  é core do PG13+. **Não aplicada** (Docker).

### Arquivos alterados/criados

- Domínio: `KnowledgeArticle` (Version, `_versionMajorFloor`,
  `BumpVersion`, `MarkVersionApproved`), **`KnowledgeArticleVersion`**
  (novo).
- Application: port **`IKnowledgeArticleVersionRepository`** (novo) +
  `KnowledgeArticleVersionWithActors`; DTOs
  **`KnowledgeArticleVersionDto`/`Snapshot`/`Compare`** (novo);
  **`KnowledgeArticleVersionQueries`** (novo — list/compare);
  commands (Reason + snapshot + carimbo); `KnowledgeArticleDto.Version`.
- Infra: **`KnowledgeArticleVersionRepository`** (novo) +
  `KnowledgeArticleVersionConfiguration` (novo); `KnowledgeArticle`
  config (+2 colunas); `AppDbContext` (+DbSet); migração.
- API: VM `Reason` `[Required]`; controller +2 GET (rota
  `versions/compare` literal).
- Front: `knowledge.ts` +`version` no contrato (**sem UI de histórico —
  backlog**, como já é a edição).
- Testes: **`KnowledgeArticleVersionTests`** (8 — regra de numeração,
  snapshot imutável, motivos 400 pt-BR, primeiro aprovador) +
  **`KnowledgeArticleVersionCommandTests`** (8 — v1.0 no create,
  minor com motivo, motivo vazio não grava, aprovação carimba + abre
  major, 404 de artigo/lista/compare, visibilidade 404, compare com os
  dois snapshots); call sites de `Update`/handlers ajustados (Reason +
  ctor) — integração só compila (Docker).
- Docs: docs/04 §Versionamento, docs/31 §18, TASKS, PROJECT_STATE.

### Validação

- build 0 erros · unit **1791/1791** (+16) · arch **115/115** · script
  da migração OK (backfill incluso) · front lint exit 0 / build ✓.

### Pendências da K3

- Migração `AddKnowledgeArticleVersioning` não aplicada + testes de
  integração não executados (Docker); histórico no **front** (listar /
  comparar versões) = backlog junto com a UI de edição.

## 19. Execução — K4 / F24-T004 (Fase 24, 2026-10-07)

**Rastreabilidade de origem** (spec `docs/32` §9 — "a Knowledge mantém
sua origem", múltiplas origens: Tasks/PDIs/Chamados/Projetos/documentos/
treinamentos).

### Design (decisões da task)

- **Tabela nova `knowledge_article_origins`** em vez de estender o
  vínculo P5a (`task_knowledge_articles`): aquele é task-centro e de
  USO (§10 "ser utilizada por uma Task"); origem (§9) é artigo-centro
  e polimórfica (Task/PDI/Chamado) — modelos diferentes, sem sobrepor.
  Projetos/documentos/treinamentos **não existem** como entidade no
  repo → fora do enum (AGENTS: não inventar; se nascerem, +1 valor).
- **Polimórfico sem FK** em `origin_id` (alvo de outro agregado —
  precedente das refs sem FK do ADR-033); a **existência é validada
  no handler por tipo** (`ITaskItemRepository`/`IPdiRepository`/
  `IIncidentRepository`) com 404 pt-BR por tipo. `article_id` FK
  Cascade; chave única `(article_id, origin_type, origin_id)`;
  `origin_type` string (enum `KnowledgeOriginType`).
- `KnowledgeArticleOrigin` = `AggregateRoot` com eventos
  Created/Deleted (molde `KnowledgeArticleDevice` — ADR-030 §5).
- **Endpoints** no molde F10-T004: `POST/DELETE
  {id}/origins/{type}/{originId}` (`Operate`); 400 `Origem inválida:
  'x'. Use 'task', 'pdi' ou 'incident'.` / 404 (artigo, alvo por
  tipo, vínculo) / 409 `A origem já está vinculada ao artigo.`
- **DTO**: `KnowledgeArticleOriginDto(Type, Id)` (enum como string —
  decisão K1); `origins` no artigo como init-prop vindo **em lote na
  busca** (round-trip único + group, mesmo critério do batch de nomes
  do K1 — sem N+1) **e** no detalhe. Diferença dos vínculos F10-T004
  (só detalhe): §9 pede a origem visível no card.
- Front: `knowledge.ts` +`origins`; chips "Origem: Task/PDI/Chamado"
  no card (rótulo Incident→"Chamado" conforme §9).

### Migração `20261008010616_AddKnowledgeArticleOrigins`

- Tabela + FK Cascade + índice único do par (artigo, tipo, id);
  **script validado** (`dotnet ef migrations script`), **não
  aplicada** (Docker) — pendências de migração agora **11**.

### Arquivos alterados/criados

- Domínio: `KnowledgeOriginType` (novo), `KnowledgeArticleOrigin`
  (novo), `KnowledgeDomainEvents` +2 eventos.
- Application: port `IKnowledgeArticleOriginRepository` (novo),
  `KnowledgeArticleOriginDto` (novo), `KnowledgeArticleOriginCommands`
  (novo — Link/Unlink com ParseType compartilhado), `KnowledgeArticleDto
  .Origins`, Search/Get handlers (+repositório de origem e anexação).
- Infra: `KnowledgeArticleOriginConfiguration` +
  `KnowledgeArticleOriginRepository` (novos), `AppDbContext` +DbSet,
  migração.
- API: controller +POST/DELETE origins.
- Front: contrato + chips no card.
- Testes: **`KnowledgeArticleOriginCommandTests`** (8 — origem no
  detalhe e na busca, múltiplas origens, 400/404×3/409, unlink);
  helpers `SearchHandler`/`GetHandler` de 3 arquivos de teste ganharam
  o repositório de origem.
- Docs: docs/04 §Origem, docs/31 §19, TASKS, PROJECT_STATE.

### Validação

- build 0 erros · unit **1799/1799** (+8) · arch **115/115** · script
  da migração OK · front lint exit 0 / build ✓.

### Pendências da K4

- Migração `AddKnowledgeArticleOrigins` não aplicada + testes de
  integração não executados (Docker); nome/título do alvo de origem no
  card (hoje só tipo+id, como desenha a §9 "Task #0042") = backlog;
  origem automática ao "criar artigo a partir de uma task" = backlog
  com a UI de edição.

## 20. Execução — K5 / F24-T005 (Fase 24, 2026-10-07)

**Revisão periódica** (spec `docs/32` §6 governança + §12 — "Revisar
Knowledge a cada 180 dias" → "Knowledge necessita de revisão" →
manter/atualizar/substituir/arquivar). Sem precedente de agenda no
repo; o molde dos endpoints/validação foi o K1 (`EnsureResponsible-
ExistsAsync` para o responsável) e o do 409, os vínculos.

### Design (decisões da task)

- **Campos de governança da §6** no domínio: `NextReviewAt`,
  `ReviewIntervalDays` (ex.: 180) e `ReviewResponsibleId` (§6
  distingue "responsável" do artigo = K1 de "responsável pela
  revisão" = K5). Invariante: `nextReviewAt` não-nulo ⟺ `interval`
  não-nulo. Intervalo **1..3650** (`Min/MaxReviewIntervalDays` no
  domínio; fora = 400 `O intervalo de revisão deve estar entre 1 e
  3650 dias.` — VM `[Range]` + domínio).
- **Agenda à parte do PUT do artigo** (`PUT {id}/review-schedule`):
  o PUT do artigo é full-state com opcionais — mandar `null` não
  distinguiria "limpar" de "não enviou" e clientes antigos apagariam
  a agenda (mesma reflexão do ADR-033 sobre presença do campo).
- **Ações da §12 mapeadas sem inventar endpoint novo**: manter =
  `POST {id}/review/confirm` (`ConfirmReview` → 409 sem agenda);
  atualizar = `PUT` do artigo (`PostponeReview()` no handler —
  reagenda sozinho, no-op sem agenda); substituir = reescrever o
  conteúdo (`PUT`) ou arquivar + origem no substituto (K4); arquivar =
  `POST {id}/status` (ADR-034).
- **Alerta = filtro + flag** (escopo da task: "filtro pendentes de
  revisão"): `SearchKnowledgeArticlesQuery(+PendingReview)` →
  repositório `NextReviewAt != null && <= now` (`now` capturado fora —
  EF não traduz `AppTimeProvider`); DTO ganhou `pendingReview`
  (comparação no `From`) + `nextReviewAt/reviewIntervalDays/
  reviewResponsibleId/reviewResponsibleName` (revisor entrou no
  `NameIds` do batch K1 — round-trip único, sem N+1).
- **Front**: `knowledge.ts` +5 campos + `ArticleFilters.pendingReview`;
  KnowledgePage: botão **"Revisões"** (toggle amarelo ao lado de
  "Filtrar"; "Limpar" reseta) e chip âmbar **"Revisar"** no card quando
  `pendingReview`.
- **Testes sem relógio falso global**: `AppTimeProvider.Current` é
  estático e os testes rodam em paralelo — envelhecer a linha é feito
  com SQL direto (`UPDATE ... SET "NextReviewAt" = datetime('now',
  '-1 day')`), não trocando o relógio compartilhado.

### Migração `20261008012115_AddKnowledgeArticleReview`

- 3 colunas nullable em `knowledge_articles` (`"NextReviewAt"`,
  `"ReviewIntervalDays"`, `"ReviewResponsibleId"`) — sem backfill
  (agenda nova por artigo); **script validado**, **não aplicada**
  (Docker) — pendências de migração agora **12**.

### Arquivos alterados/criados

- Domínio: `KnowledgeArticle` (+3 campos, `SetReviewSchedule` /
  `ConfirmReview` / `PostponeReview`, `Min/MaxReviewIntervalDays`).
- Application: **`KnowledgeArticleReviewCommands`** (novo — set
  schedule + confirm), `SearchKnowledgeArticlesQuery(+PendingReview)`,
  port `SearchAsync(+pendingReview)`, DTO (+5 campos, `NameIds` com
  revisor, `PendingReview` no `From`).
- Infra: `KnowledgeArticleRepository.SearchAsync` +filtro; migração.
- API: VM `SetKnowledgeArticleReviewScheduleViewModel` (`[Range]`) +
  controller +`PUT {id}/review-schedule` e `POST {id}/review/confirm`.
- Front: contrato + toggle "Revisões" + chip "Revisar".
- Testes: **`KnowledgeArticleReviewTests`** (9 — agenda calculada +
  responsável nomeado, 400 de intervalo ×2, 404 de responsável,
  limpeza, confirm reagenda, 409 sem agenda, edição reagenda, filtro
  pendentes).
- Docs: docs/04 §Revisão periódica, docs/31 §20, TASKS, PROJECT_STATE.

### Validação

- build 0 erros · unit **1808/1808** (+9) · arch **115/115** · script
  da migração OK · front lint exit 0 / build ✓.

### Pendências da K5

- Migração `AddKnowledgeArticleReview` não aplicada + testes de
  integração não executados (Docker); ação **substituir** por outro
  artigo com destaque/redirect = backlog (hoje = conteúdo novo via
  PUT + origem K4); notificação push/e-mail do alerta (o §12 só diz
  "alerta") = backlog.

## 21. Execução — K6 / F24-T006 (Fase 24, 2026-10-07)

**Geração automática de rascunho** (spec docs/32 §2–§4 — "Gerar
Knowledge" ao concluir Task/PDI"). A task bloqueava por arquitetura ("sem
infra de IA no repo — definir antes, possível ADR"): decisão apresentada
ao usuário e **aprovada em 2026-10-07** → montagem **determinística** dos
registros existentes, **sem LLM** (a §3 exige "conteúdo baseado nos
registros existentes / não inventar"; o repo não tem provedor, chave ou
orçamento de IA — docs/31 §12). Registrada em **ADR-035**.

### Design (decisões da task)

- **Endpoints explícitos** (nada roda sozinho na conclusão — a §2 diz
  "o sistema poderá disponibilizar"):
  - `POST /api/v1/tasks/{taskId}/generate-knowledge` (Operate) — só
    `Resolved`/`Closed` ("ao concluir"); 409
    `A task precisa estar concluída para gerar Knowledge.`;
  - `POST /api/v1/pdi/{userId}/generate-knowledge` (Operate) — dono
    ou Admin (mesma checagem de dono dos endpoints de PDI: 403
    `Você só pode gerar Knowledge do seu próprio PDI.`), só
    `Completed`; 404 `PDI do usuário '{id}' não encontrado.`
- **Nasce Draft** (§2 "Knowledge sugerida → Revisão" — nenhum caminho
  pula o ADR-034), **versão v1.0** com motivo de proveniência
  "Geração automática a partir de …" (K3, §8) e **origem** Task/Pdi
  (K4, §9) — nos dois casos reaproveitando os repositórios/rotas
  existentes, **zero migração nova**.
- **`KnowledgeDraftComposer`** (função pura em Application, testável
  sem I/O): seção só entra se houver registro — seção vazia nunca é
  escrita (é assim que "não inventar" vira código). Task: Objetivo
  (descrição/prazo), Resultado, Critérios de aceite (x/ ),
  Fases (posição + estado), Subtarefas (estatísticas do
  `GetSubtaskStatsAsync` — não há tabela de subtarefas legível),
  Dificuldades e impedimentos (resolvido/aberto), Evidências
  (arquivo/nota), Comentários (data + autor) e Linha do tempo (últimas
  50, ordenadas por data, com ator e rótulo `TaskTimelineTypes`). PDI:
  Plano (estratégia + prazo), Objetivos (só campos preenchidos,
  competência resolvida no catálogo) e Linha do tempo (tipo/detalhe/
  motivo). Título: task.Title / primeiro objetivo (§4 — ex.: o PDI
  "Conhecer funcionamento do sistema de Tomografia" vira artigo com
  esse título, sem reescrita mágica); banner `> Rascunho gerado
  automaticamente…` no topo; cortes nos limites do domínio (200/20k,
  sem surrogate solto).
- **Front**: `tasks.ts`/`pdi.ts` +`generateTaskKnowledge`/
  `generatePdiKnowledge` (retornam o artigo; throw em resposta vazia,
  molde `upsertPdi`); TaskDetailPage: botão **"Gerar Knowledge"** no
  header, renderizado só quando `state` é Resolvida/Fechada, dentro de
  `<Can operate>`, via `run()` (aviso pt-BR + recarga); PdiPage: botão
  na linha da tabela (Admin) e no card do plano próprio, só
  `status === "Completed"`, com `NoticeBanner` novo (estado `notice`).
- **Testes** (8, `KnowledgeGenerationTests`): task concluída monta
  todas as seções + v1.0 + origem Task; task aberta = 409; task
  inexistente = 404; task sem registros omite seções vazias (§3);
  PDI concluído monta do objetivo + origem Pdi; PDI em Draft = 409;
  PDI inexistente = 404 (Admin); PDI de outro usuário = 403.

### Arquivos alterados/criados

- **`docs/adr/ADR-035-KNOWLEDGE-POR-MONTAGEM-DETERMINISTICA.md`** (novo).
- **`docs/34-PLANO-AI-ENGINEERING-BLUEPRINT.md`** (novo — blueprint de
  IA recebida do usuário em 2026-10-07, arquivada integralmente;
  investigação/ plano da Fase 0 sai no checkpoint da Fase 24 — usuário
  aprovou Fase 0 como próxima fase; ADRs de IA continuarão de ADR-036).
- Application: **`KnowledgeDraftComposer`** (novo) +
  **`KnowledgeGenerationCommands`** (novo — 2 commands + handlers com
  12 ports no total).
- API: `TasksController` +`POST {taskId}/generate-knowledge`;
  `PdiController` +`POST {userId}/generate-knowledge`.
- Front: `tasks.ts`, `pdi.ts`, `TaskDetailPage.tsx`, `PdiPage.tsx`.
- Testes: **`KnowledgeGenerationTests`** (8).
- Docs: docs/04 §Geração automática, docs/31 §21, TASKS, PROJECT_STATE.

### Validação

- build 0 erros · unit **1816/1816** (+8) · arch **115/115** · front
  lint exit 0 / build ✓ · **sem migração** (pendências continuam 12).

### Pendências da K6

- Sem Docker: Ollama/health checks da blueprint ficam para a Fase 0 com
  ambiente pendente (igual às migrações); evidência de execução do
  endpoint via Integração (Playwright/API) depende do ambiente de
  teste; "sistema/setor" e seções sem registro (pesquisas/decisões
  literais) ficam de fora até o modelo existir (K7 usa o mesmo
  critério).

## 22. Execução — K7 / F24-T007 (Fase 24, 2026-10-07)

**Busca avançada** (spec docs/32 §13) com o escopo aprovado — "só o
que existe no modelo". No caminho a task pegou um bug de wiring do K5.

### Design (decisões da task)

- **Filtros estruturados AND** somados ao `SearchAsync` (port →
  repositório → query → controller): `categoryId` (FK exata),
  `authorId` (autoria — FK SetNull), `type` (nome do enum; o binder do
  MVC aceita nome mesmo sem `JsonStringEnumConverter` — inválido vira
  400 automático do `[ApiController]`) e `tag` (a normalização
  trim+lowercase do domínio é repetida no filtro — "Switch" acha
  "switch", coleção primitiva do EF traduz para `ANY`). `term` passa
  a casar também na **descrição**.
- **Bug corrigido**: `KnowledgeArticlesController.Search` **não
  repassava o `PendingReview` do K5** — a query string era ignorada e
  o filtro nunca saía do endpoint (os testes do K5 cobrem o handler,
  não a action; sem integração por falta de Docker). A action agora
  repassa todos os parâmetros. Registrado no comentário do parâmetro
  para não virar regressão silenciosa.
- **Ausentes por modelo**: "sistema"/"setor" da §13 não existem em
  nenhuma entidade (`Setor`/`Sector` não aparecem no domínio) — sem
  filtro e sem chip, documentado como ausente em docs/04 e no
  comentário da task (mesmo critério da K6).
- **Front** (`knowledge.ts` + `KnowledgePage`): barra de filtros com
  select de categoria (categorias já carregadas), select de autor
  (opções derivadas do próprio DTO — id + nome vêm do backend, sem
  `/admin/users`; autor selecionado sem artigo na página vira opção
  sintética), chips dos 14 tipos (toggle: clicar de novo limpa) e chip
  da tag ativa; **tag e "Autor:" dos cards viraram botões de filtro**;
  o "Limpar" do header zera tudo (term, revisões e novos); qualquer
  mudança de filtro volta para a página 1. `ArticleFilters` +=
  categoryId/authorId/type/tag.

### Arquivos alterados

- Port `IKnowledgeArticleRepository` + `KnowledgeArticleRepository`
  (4 filtros + descrição no termo).
- `KnowledgeArticleQueries` (record += 4 props; handler normaliza a
  tag antes de chamar o repo).
- `KnowledgeArticlesController.Search` (+= 5 parâmetros — inclui o fix
  do pendingReview).
- Front: `knowledge.ts` (ArticleFilters + query string),
  `KnowledgePage.tsx` (estado, barra, cards clicáveis, Limpar geral).
- Testes: **`KnowledgeArticleSearchFilterTests`** (6 — categoria,
  autor, tipo [exclui artigo sem tipo], tag [caixa], termo na
  descrição, combinação AND — todos pelo handler).
- Docs: docs/04 §Busca avançada, docs/31 §22, TASKS, PROJECT_STATE.

### Validação

- build 0 erros · unit **1822/1822** (+6) · arch **115/115** · front
  lint exit 0 / build ✓ · **sem migração** (pendências seguem 12).

### Pendências da K7

- Cobertura do fix do `pendingReview` é por leitura de código — teste
  de HTTP/exigência da query string depende de Integração (Docker);
  índices dedicados dos novos filtros = backlog (FKs já têm índice;
  tag vive em coluna JSON/text[]);
  "sistema/setor" entra só se o modelo ganhar os campos (requisito
  novo → task/ADR nova).

## 23. Entrega — Investigação Sprint 0 da AI Blueprint (2026-10-07)

Fim do mandato da Fase 24: blueprint arquivada em **docs/34** (§66 —
"só investigação e planejamento, NÃO implementar") e investigação
entregue em **docs/35-PLANO-AI-FOUNDATION-SPRINT0.md**: arquitetura
encontrada, componentes reutilizáveis/inexistentes, Docker existente,
dependências, gaps e conflitos (Docker indisponível → §47/§63 sem
evidência; ADRs de IA de **ADR-036**; `profiles: [ai]` × ADR-022;
ADR-035 × §33), riscos, **ADRs 036–041**, plano Docker preparado
(não executado), ordem S1–S9 e critérios de aceite do §63/§67
separando executável sem Docker × pendente de ambiente. **Zero código
de IA escrito nesta etapa.** Fase 0 — AI Foundation = próxima fase,
aguardando autorização para abrir tasks.
