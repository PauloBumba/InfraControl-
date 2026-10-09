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
| **Append-only / telemetria** | Somente leitura permanece: `Alert`, `AuditEntry`, `MonitoringResult`, `MonitoringEvent`, `DiscoveryResult`, `TaskHistory`, `Notification` (feed), `SloMeasurement`/`SloBudget` — **decisão Fase 2 (2026-10-05): sem PUT**, registros imutáveis (create + delete para correção); `Slo` mantém CRUD completo |
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
  - [x] Fase 1 concluída (2026-10-04): RunCheck/UniFi sync/Notification
    prefs → policy `Operate`.
  - [x] Fase 2 concluída (2026-10-05): `Connection` PUT, `MonitoringCheck`
    CRUD (lista migrou para `devices/{id}/checks`), `TaskComment` PUT/DELETE,
    `Invitation` PUT (roles pendentes), `Incident` sem DELETE confirmado,
    `TaskItem` update já existia, `SloMeasurement`/`SloBudget` sem PUT.
    Ajuste estrutural: `Invitation.RoleAssignments` expõe a `List` real
    (não `AsReadOnly()`) — o fixup do EF precisa escrever na coleção.
  - [x] Fase 3 concluída (2026-10-06): eventos Created/Updated/Deleted em
    todas as operacionais (Inventory, Network, Tasks, Monitoring, Incident,
    RemoteAccess, Knowledge, SLA/Service/Slo, Invitation — lotes 1–3),
    `{var}.Delete()` nos delete handlers (evento na mesma transação),
    `Incident.ChangeStatus` → `IncidentUpdatedDomainEvent`,
    `Invitation.SetRoles`/`Cancel` → `InvitationUpdatedDomainEvent`,
    `Create` que delega a `Update` limpa o Updated transitório
    (`ClearDomainEvents`) antes do Created. Suítes: unit 1594,
    arch 117, integration 193.
  - [x] Fase 4 concluída (2026-10-06): contrato outbox validado por
    teste — todo evento em `InfraControl.Domain.Events` resolve no
    constraint do `OutboxDispatcherWorker.ResolveEventType` e o payload
    round-trippa com `OutboxJson.Options` preservando `EventId`
    (fix: `DomainEvent.EventId`/`OccurredAt` viraram `{ get; init; }` —
    com `{ get; }` puro o STJ descartava o GUID e a idempotência do
    inbox EventId+Consumer quebrava); CRUD Location ponta-a-ponta na
    outbox (`CrudOutboxIntegrationTests`). **Decisão de consumers**:
    nenhum consumer novo — os eventos CRUD (Created/Updated/Delete das
    operacionais) não têm destinatário claro (ADR-012); ficam
    publicados no bus aguardando consumidores futuros. O
    `NotificationEventConsumer` mantém o escopo atual (User, Invitation,
    Incident, Alert, Pdi).
  - [x] Fase 5 concluída (2026-10-08): frontend espelha a matriz com
    componente de visão reutilizável + React Query. Infra nova:
    `components/ui/EntityView.tsx` (`EntityView<T>` com visões
    tabela/grid/kanban, `ViewSwitcher` de 3 ícones e
    `useEntityViewMode(entityKey, fallback)` persistindo a preferência por
    entidade em `localStorage entityView:<key>` com sync entre abas) e
    `hooks/queries.ts` (`usePagedQuery`/`useListQuery` + mutações
    `useCreate/Update/DeleteMutation` que invalidam por prefixo de
    queryKey, `mutationErrorMessage`, `asApiError`). 10 páginas migradas
    de `useState+useEffect+load()` para React Query com CRUD completo
    espelhando a matriz: Locations/Racks, Devices, Switches, Network
    (VLANs), Tasks (kanban por `TaskState`, identificação, exclusão),
    Incidents (kanban por status + `PUT {id}/status` + acknowledge, sem
    DELETE), Knowledge (abas artigos/categorias com edit/delete/status),
    SLA (abas serviços/políticas/SLOs + Error Budget), Invitations
    (roles/cancelar atrás de `Can administer`) e Remote Sessions
    (abrir/encerrar SSH-RDP); escrita sempre atrás de
    `<Can level="operate|administer">` (exibição — backend continua
    autorizando). Validação: `tsc -b`/`eslint`/`vite build` verdes;
    backend intocado — unit 1852, arch 119, integration 194, 0 falhas.
  - [x] Fase 6 concluída (2026-10-08): docs — este ADR com as Fases 1–6
    marcadas, entrada no `PROJECT_STATE.md` e `docs/28` atualizado com os
    componentes novos (`EntityView`/`ViewSwitcher`/`useEntityViewMode`,
    camada React Query) e o estado das páginas migradas.

## Referências

- ADR-016 (roles fonte única), ADR-005 (outbox), ADR-006 (CQRS),
  ADR-007 (vertical slice), ADR-012 (notificações), ADR-028 (integrações).
- `docs/29-OPERATIONS-UX-BACKLOG.md` (entregas 1–4 concluídas).
