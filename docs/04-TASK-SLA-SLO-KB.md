# Task, SLA, SLO e Knowledge Base

## Task

Task é uma unidade genérica de trabalho.

Tipos:

- Manual
- Incident
- Alert
- Monitoring
- Discovery
- Maintenance
- Change
- Preventive
- Corrective
- Project
- System

Pode relacionar-se a múltiplos dispositivos, usuários, incidentes e serviços.

### Implementação (F7-T001)

A entidade é `TaskItem` (não `Task` — evita a colisão com
`System.Threading.Tasks.Task` em todo o código assíncrono). Persistência:
tabela `tasks`, enums como `string(20)`.

- `TaskKind` = os 11 tipos acima; `TaskState` = os 7 estados acima.
- `Title` (120, obrigatório) e `Description` (500, opcional); nasce em `Open`.
- Transições de estado são `F7-T007` (Workflow); vínculos (assignees,
  devices, incidentes), comentários e histórico: `F7-T002`→`T006`.

### Assignees (F7-T002)

N:N Task ↔ Usuário na tabela `task_assignees` (PK composta, FKs
`Cascade` — apagar a task ou o usuário limpa os vínculos).

- `POST/DELETE api/v1/tasks/{taskId}/assignees/{userId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404.
- `TaskDto` expõe `assigneeCount` + `assigneeIds`.

### Devices vinculados (F7-T003)

N:N Task ↔ Device na tabela `task_devices` (PK composta; FK da task
`Cascade`, FK do device `Restrict` — excluir um device com tasks
vinculadas é bloqueado no `DeleteDeviceCommand`).

- `POST/DELETE api/v1/tasks/{taskId}/devices/{deviceId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404.
- `TaskDto` expõe `deviceCount` + `deviceIds`.

### Incidentes vinculados (F7-T004)

N:N Task ↔ Incident na tabela `task_incidents` (PK composta; FK da task
`Cascade`, FK do incidente `Restrict`).

- `POST/DELETE api/v1/tasks/{taskId}/incidents/{incidentId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404.
- `TaskDto` expõe `incidentCount` + `incidentIds`.

### Comentários (F7-T005)

Tabela `task_comments` (`task_id`, `author_id`, `content` ≤ 500; FK da
task `Cascade`, FK do autor `Restrict`). Sem edição — histórico de
alterações vem em `F7-T006`.

- `GET api/v1/tasks/{taskId}/comments` (policy `TasksRead`) — ordem
  cronológica, com `authorName` (DisplayName).
- `POST .../comments` (policy `TasksManage`) — autor = usuário logado.

### Histórico (F7-T006)

Tabela `task_histories` (`task_id`, `actor_id` nullable, `action`,
`detail` ≤ 200; FK da task `Cascade`, sem FK no ator). Todo comando grava
uma entrada (`Created`, `Updated`, `Assigned`, `Unassigned`,
`DeviceLinked`, `DeviceUnlinked`, `IncidentLinked`, `IncidentUnlinked`,
`Commented`).

- `GET api/v1/tasks/{taskId}/history` (policy `TasksRead`) — ordem
  cronológica, com `actorName` (DisplayName).

### Workflow (F7-T007)

Transições válidas (mapa `TaskStateWorkflow` no domínio); par fora do
mapa → `InvalidOperationException` → 409 pt-BR; mesmo estado é no-op
sem histórico:

```text
Open       → InProgress, Cancelled
InProgress → Waiting, Blocked, Resolved, Cancelled
Waiting    → InProgress, Blocked, Cancelled
Blocked    → InProgress, Cancelled
Resolved   → Closed, InProgress
Cancelled  → Open
Closed     → Open
```

- `PUT api/v1/tasks/{taskId}/state` (`TasksManage`) com body
  `{ "state": "..." }` — devolve o `TaskDto` atualizado; estado
  desconhecido → 400, task inexistente → 404.
- Cada transição grava `StateChanged` no histórico com o detalhe
  `"De → Para"`.

API (`api/v1/tasks`): `Search` (term/kind/state, policies `TasksRead`),
`Get` (`TasksRead`), `Create`/`Update`/`Delete` (`TasksManage`),
`ChangeState` (`TasksManage`).

## Estados

```text
Open
InProgress
Waiting
Blocked
Resolved
Cancelled
Closed
```

Transições entre estados: seção **Workflow (F7-T007)** acima.

## SLA

SLA controla compromisso de resposta e resolução.

Entidades:

- SlaPolicy
- TaskSla
- TaskSlaPause

O SLA aplicado deve preservar os prazos históricos.

## SLO

SLO mede objetivo de serviço.

Exemplo:

```text
Availability >= 99.95%
Window = 30 days
```

Entidades:

- Service
- Slo
- SloMeasurement
- SloBudget

### Service (F8-T001)

Serviço de TI prestando — raiz do módulo SLA/SLO (`SlaPolicy` F8-T002 e
`Slo` F8-T005 referenciam Service). Padrão Location: `Name` (120,
obrigatório, único case-insensitive — pré-check + índice
`ux_services_name ON services (LOWER(name))`) e `Description` (500,
opcional); tabela `services`.

- `GET api/v1/sla/services` (busca term/page/pageSize) e
  `GET .../{id}` (policy `SlaRead`); `POST`/`PUT`/`DELETE`
  (policy `SlaManage`); duplicidade → 409, id inexistente → 404.
- Permissões novas `SlaRead`/`SlaManage` cobrem todo o módulo Fase 8
  (SLA e SLO); Viewer ganha `SlaRead`, User ganha `SlaManage`.

### SlaPolicy (F8-T002)

Política de SLA — compromisso de **resposta** e **resolução** (docs/04).
Tabela `sla_policies`: `Name` (120, obrigatório), `ResponseMinutes` e
`ResolutionMinutes` (inteiros > 0 — 400 pt-BR caso contrário) e
`ServiceId` opcional (FK `Restrict` para `services` — política global
`null` ou vinculada a um Service).

- `GET api/v1/sla/policies` (filtros `term` e `serviceId`) e
  `GET .../{id}` (policy `SlaRead`); `POST`/`PUT`/`DELETE`
  (`SlaManage`); 404 para política/serviço inexistente.
- `DELETE /api/v1/sla/services/{id}` passa a bloquear com 409 quando
  houver políticas vinculadas (pré-check + FK `Restrict`).
- Os prazos são snapshotados no `TaskSla` (F8-T003) para preservar os
  prazos históricos (docs/04).

### TaskSla (F8-T003)

SLA aplicado a uma Task — 1:1 (PK `task_id`), tabela `task_slas`:
`SlaPolicyId` (FK `Restrict`), snapshot `ResponseMinutes`/`ResolutionMinutes`
e os vencimentos `ResponseDueAt`/`ResolutionDueAt` (= abertura da task +
minutos). Mudanças futuras na política não alteram o snapshot.

- `POST api/v1/tasks/{taskId}/sla` (policy `SlaManage`, body
  `{ "slaPolicyId": "..." }`) — 409 se já aplicado; grava `SlaApplied`
  no histórico com os prazos.
- `GET .../tasks/{taskId}/sla` (policy `SlaRead`) — 404 se não aplicado.
- `DELETE api/v1/sla/policies/{id}` bloqueia com 409 enquanto houver
  tasks com o SLA aplicado (pré-check + FK `Restrict`).
### TaskSlaPause (F8-T004)

Pausa do relógio de SLA — tabela `task_sla_pauses` (`task_id` FK
`Cascade` para `task_slas`: apagar a task/SLA limpa as pausas;
`start_at`, `end_at` nullable = pausa em aberto).

- `POST .../tasks/{taskId}/sla/pause` (`SlaManage`) — 404 sem SLA,
  409 se já há pausa em aberto; grava `SlaPaused`.
- `POST .../tasks/{taskId}/sla/resume` (`SlaManage`) — 409 se não há
  pausa em aberto; **soma a duração aos `*DueAt`** (snapshot de minutos
  intacto) e grava `SlaResumed`.
- `GET .../tasks/{taskId}/sla/pauses` (`SlaRead`) — cronológico;
  404 se o SLA não está aplicado.

### Slo (F8-T005)

Objetivo de serviço de um Service (docs/04: `Availability >= 99.95%`,
`Window = 30 days`). Tabela `slos`: `Name` (120), `TargetPercent`
(`numeric(6,3)`, 0 < alvo ≤ 100 — 400 pt-BR), `WindowDays` (> 0) e
`ServiceId` obrigatório (FK `Restrict` → `services`).

- `GET api/v1/sla/slos` (filtros `term` e `serviceId`) e `GET .../{id}`
  (policy `SlaRead`); `POST`/`PUT`/`DELETE` (`SlaManage`).
- `DELETE /api/v1/sla/services/{id}` passa a bloquear com 409 quando
  houver SLOs vinculados (pré-check + FK `Restrict`).
- Medição chega em `SloMeasurement` (abaixo) e orçamento em
  `SloBudget` (F8-T007).

### SloMeasurement (F8-T006)

Medição do SLO em um momento — valor observado comparável ao
`TargetPercent`. Tabela `slo_measurements`: `SloId` (FK `Restrict` →
`slos`), `MeasuredAt` (DateTime) e `ValuePercent` (`numeric(6,3)`,
0–100 — 400 pt-BR fora da faixa). Registro imutável (sem `PUT`).

- `GET api/v1/sla/slos/{sloId}/measurements` (`SlaRead`) — mais
  recentes primeiro; `GET api/v1/sla/slos/measurements/{id}` (404).
- `POST .../slos/{sloId}/measurements` (`SlaManage`) — 201; 404 para
  SLO inexistente.
- `DELETE .../slos/measurements/{id}` (`SlaManage`).
- `DELETE /api/v1/sla/slos/{id}` bloqueia com 409 enquanto houver
  medições (pré-check + FK `Restrict`).

### SloBudget (F8-T007)

Consumo do erro budget do SLO em uma janela. Tabela `slo_budgets`:
`SloId` (FK `Restrict` → `slos`), `PeriodStart`, `PeriodEnd` (null =
período aberto; se presente deve ser posterior ao início) e
`ConsumedPercent` (`numeric(6,3)`, 0–100 — 400 pt-BR fora da faixa).

- `GET api/v1/sla/slos/{sloId}/budgets` (`SlaRead`) — mais recentes
  primeiro; `GET api/v1/sla/slos/budgets/{id}` (404).
- `POST .../slos/{sloId}/budgets` (`SlaManage`) — 201; 404 para SLO
  inexistente.
- `DELETE .../slos/budgets/{id}` (`SlaManage`).
- `DELETE /api/v1/sla/slos/{id}` bloqueia com 409 também com orçamentos
  (pré-check + FK `Restrict`).

## Knowledge Base

Artigos:

- Draft
- Published
- Archived

Relacionamentos possíveis com Device, Incident e Service.

### Categorias (F10-T001)

`KnowledgeCategory` (tabela `knowledge_categories`): `name` (120, **único
case-insensitive** — pré-check no handler → 409 + índice
`ux_knowledge_categories_name ON (LOWER(name))` via SQL bruto,
precedente da Location) e `description` (500, opcional).

Rotas `api/v1/knowledge/categories` (`KnowledgeController`): `GET` (busca
com `term`, `RoleAccess.Read`) + `GET/{id}` (404), `POST` (201), `PUT`,
`DELETE` (`RoleAccess.Operate`; 404). Escrita 400 pt-BR via
`[Required]`/`[MaxLength]` no ViewModel. A FK dos artigos (F10-T002)
vem com a própria task.
