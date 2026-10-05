# ADR-030 — Matriz CRUD + autorização por role em todo o projeto

## Status

Accepted (2026-10-04 — aprovado pelo usuário com "documenta e começar a
executar"; execução por fases com checkpoint, conforme AGENTS.md).

## Contexto

O CRUD e a autorização por role existem, mas com cobertura desigual: 14
entidades com CRUD completo, 24 parciais, 7 só-leitura e 7 sem API; 12
controllers sem policy de role (3 deles com escrita); 6 entidades expostas
sem `GET /{id}`. Este ADR fecha o padrão para **todo** o projeto, estendendo
o ADR-016 (roles como fonte única) sem criar um terceiro modelo.

## Decisão

### 1. Escopo de CRUD por grupo de entidade

| Grupo | Regra |
|---|---|
| **Operacionais** (inventory, network, tasks, incidents, knowledge, SLA, monitoring checks, discovery, remote access) | CRUD completo exposto: list + **get by id** + create + update + delete (ou ciclo por estado, quando a entidade não deleta) |
| **Parciais operacionais** | Completar a operação que faltar (ex.: `PUT` em `Connection`) dentro da própria vertical slice |
| **Append-only / telemetria** | Somente leitura permanece: `Alert`, `AuditEntry`, `MonitoringResult`, `MonitoringEvent`, `DiscoveryResult`, `TaskHistory`, `Notification` (feed), `SloMeasurement`/`SloBudget` salvo decisão contrária registrada na task |
| **Self-service** | PDI, preferências de notificação, sessões próprias, convite (`me`/accept/reject) — escrita sobre o próprio registro |
| **Sem API (não muda)** | Infra de mensageria/integração: `OutboxMessage`, `ProcessedMessage`, `NotificationDelivery`, `IncidentAlert`, `InvitationRole`, `ExternalDeviceReference` |

### 2. `GET /{id}` obrigatório

Toda entidade exposta na API tem **também** endpoint `GET /{id}`
(recuperação pontual por identificador), na mesma política de leitura da
listagem. Lacunas fechadas por esta decisão (6):

| Endpoint | Nível |
|---|---|
| `GET /api/v1/admin/users/{userId}` | Operate (classe) |
| `GET /api/v1/admin/invitations/{invitationId}` | Administer |
| `GET /api/v1/notifications/{notificationId}` | Read + visibilidade (dono **ou** role, mesma regra do feed) |
| `GET /api/v1/network/switch-port-vlans/{id}` | Read |
| `GET /api/v1/inventory/devices/{deviceId}/remote/sessions/{sessionId}` | Read |
| `GET /api/v1/auth/sessions/{sessionId}` | Read self (404 para sessão alheia — mesma resposta do revoke) |

Consulta pontual = `IQuery<TDto>` + `IAuthorizedRequest.RequiredRoles`
(`RoleAccess.Read` por padrão) + `KeyNotFoundException` com mensagem pt-BR →
`GlobalExceptionHandler` devolve 404 no envelope `ApiResponse`.

### 3. Autorização em duas camadas, sem exceção

- **1ª camada (HTTP)**: `[Authorize(Policy = AuthorizationPolicies.Operate|Administer)]`
  em toda ação **mutante**; leitura usa `[Authorize]` simples. Nunca
  literal de role (guarda: `RoleAuthorizationStandardizationTests`).
- **2ª camada (MediatR)**: todo command/query mutante declara
  `IAuthorizedRequest.RequiredRoles` (guarda:
  `BuildingBlocksStandardizationTests`). As duas camadas declaram o **mesmo**
  nível — divergência entre elas é bug (ex.: `RunCheckCommand` com
  `RoleAccess.Read`).
- Escritas críticas/auditoráveis adicionam `IAuditableRequest.AuditResource`.

### 4. Matriz por entidade (Admin = tudo em todas; Operator = como abaixo)

| Nível | Entidades |
|---|---|
| **Administer** | User, UserRoleAssignment, gestão de Invitation, AuditEntry, RetentionPolicy, DeviceCredential, preferências de outrem |
| **Operate — CRUD operacional** | Device, IpAddress, MacAddress, NetworkInterface, Location, Rack, Vlan, SwitchPort, SwitchPortVlan, Connection, TaskItem, TaskComment, TaskAssignee, TaskDevice, TaskIncident, TaskSla/TaskSlaPause, Incident (sem DELETE — ciclo por estado), MonitoringCheck, DiscoveryRun (sem DELETE — histórico), KnowledgeArticle, KnowledgeCategory, KnowledgeArticle{Device,Incident,Service}, Service, SlaPolicy, Slo, SloBudget, SloMeasurement, RemoteSession, Integrações (sync UniFi) |
| **Read** | Alert, MonitoringResult, MonitoringEvent, DiscoveryResult, Notification (feed), TaskHistory, agregados (topology, switches, clients, global-search) |
| **Self-service** | PDI (Operator edita só o próprio; Admin todos), preferências próprias, sessões próprias, convites próprios |

### 5. Eventos de domínio + outbox nas operacionais

Todo agregado **operacional** com CRUD dispara, via `RaiseDomainEvent`,
`{Entity}CreatedDomainEvent`, `{Entity}UpdatedDomainEvent` e
`{Entity}DeletedDomainEvent` (namespace **`InfraControl.Domain.Events`** —
constraint do `OutboxDispatcherWorker.ResolveEventType`; construtor 1:1 com
as propriedades). O caminho é ADR-005: mesma transação →
`OutboxDispatchHandler<TEvent>` → `OutboxMessage` →
`OutboxDispatcherWorker` (5 s) → RabbitMQ → consumers registrados no único
`AddMassTransit` (via `configureBus` do `AddInfrastructure`).
`IEventBus` **não** é chamado direto (F16-T002). Consumidor de notificação
só onde há destinatário claro (decisão por evento na task); demais eventos
ficam publicados no bus aguardando consumidores futuros.

## Consequências

- `GET /{id}` vira guarda de arquitetura (teste varrendo controllers ×
  entidades expostas — evita regressão do gap de 6).
- Frontend passa a espelhar a matriz em `ROUTE_ACCESS`/`<Can>` (UI só
  esconde, quem autoriza é o backend).
- Entidades append-only ganham apenas endpoints de leitura; mutations de
  telemetria continuam sendo ingestão interna/webhook, não CRUD.
- Work split: Fase 1 gaps de autorização → Fase 2 CRUD parcial → Fase 3
  eventos/outbox → Fase 4 consumers → Fase 5 frontend → Fase 6 docs.

## Referências

- ADR-016 (roles fonte única), ADR-005 (outbox), ADR-006 (CQRS),
  ADR-007 (vertical slice), ADR-012 (notificações), ADR-028 (integrações).
- `docs/29-OPERATIONS-UX-BACKLOG.md` (entregas 1–4 concluídas).
