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
| §5 tipos (14) | **ausente** — não há campo `Type` |
| §6 identificação: descrição, tags, autor, responsável | **parcial** — só `Title`/`Content`/`CategoryId`; autor só na auditoria |
| §6 conteúdo estruturado (objetivo, contexto, pré-requisitos, procedimento, observações, problemas, soluções, limitações, referências) | **ausente** — `Content` único (20k) |
| §6 governança: versão, próxima revisão, responsável pela revisão, aprovador | **ausente** |
| §7 status: RASCUNHO → EM REVISÃO → APROVADA → PUBLICADA → EM REVISÃO → ATUALIZADA + Arquivada/Obsoleta/Rejeitada | **diferente** — enum de 3 estados (`Draft/Published/Archived`), ciclo Draft→Published→Archived→Draft |
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
  K1 tipos + identificação; K2 workflow dos 9 status; K3 versionamento;
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
