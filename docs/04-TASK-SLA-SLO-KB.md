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

API (`api/v1/tasks`): `Search` (term/kind/state, policies `TasksRead`),
`Get` (`TasksRead`), `Create`/`Update`/`Delete` (`TasksManage`).

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

## Knowledge Base

Artigos:

- Draft
- Published
- Archived

Relacionamentos possíveis com Device, Incident e Service.
