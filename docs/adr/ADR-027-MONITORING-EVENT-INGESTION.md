# ADR-027 — Ingestão de eventos externos: MonitoringEvent → Alert → Incident

- **Status**: aceito
- **Data**: 2026-10-02
- **Contexto**: Fase 1 do roadmap de integrações (após auditoria 2026-10-01).
  Amenda o [ADR-023](ADR-023-ZABBIX-INBOUND-WEBHOOK.md) (endpoint/token
  continuam; dedup por título e "Alert fora do fluxo" são substituídos aqui).

## Problema

O webhook do ADR-023 entregava `Incident` direto, sem evidência do evento que
o originou:

1. **Sem linha de evento bruto.** Não havia como saber qual `eventid` do
   Zabbix abriu/fechou um incidente, nem auditar o payload original — o
   título do trigger era a única correlação.
2. **Dedup por título+device era heurística.** Dois hosts sem device com o
   mesmo nome de problema dividiam a chave; replay por id não existia.
3. **O `Alert` não participava.** Ele exige `DeviceId` + `CheckKind` (nasce
   do avaliador F6), mas o plan tinha decidido manter o alerta fora do fluxo
   do Zabbix — a tela de alertas não mostrava problemas do Zabbix.
4. **Dois bugs no ciclo de vida** (reproduzidos na auditoria):
   - `EnsureIncidentAsync` buscava incidente apenas `Open` — com o
     incidente em `Investigating` e o check ainda falhando, **cada ciclo de
     polling abria um novo incidente** para o mesmo device;
   - `CloseRecoveredIncidentsAsync` só fechava incidentes `Open` — um
     incidente em `Investigating/Identified/Monitoring` **nunca fechava**
     pelo recovery.

## Decisão

### 1. `MonitoringEvent` — evidência bruta de fonte externa

Nova entidade (`monitoring_events`) com **uma linha por evento recebido**:

- `Source` (`Native`|`Zabbix`, string — estensível sem migração de enum);
- `ExternalEventId` — o `eventid` recebido; **única** com `Source` (âncora de
  dedup);
- `ExternalProblemId` — lifecycle do problema (no Zabbix, problem e recovery
  são dois `eventid`s distintos e só o payload traz a correlação:
  `{EVENT.ID}` = problem original, `{EVENT.RECOVERY.ID}` = recovery);
- `ExternalRecoveryId` — espelho do `r_eventid` do Zabbix (problem →
  recovery), preenchido por backfill quando o recovery chega;
- `Severity` (int, escala da fonte), `Status` (`Problem`|`Recovered`),
  `OccurredAt`/`RecoveredAt`, `Message`, `RawPayload` (JSON reconstruído).

Correlação validada na documentação oficial do Zabbix: o objeto Problem tem
`r_eventid` = *"ID of the recovery event"* (o apontador vai do problem para o
recovery; o recovery **não** tem campo de volta); `objectid`/`triggerid` não
servem de chave (repetem-se por trigger). O par sai das macros
`{EVENT.ID}`/`{EVENT.RECOVERY.ID}` suportadas em *recovery operations*.

### 2. `Alert` ganha origem externa (DeviceId/Kind nullable)

- Novos campos: `Source`, `MonitoringEventId` (FK → `monitoring_events`,
  `SetNull` na exclusão), `ExternalProblemId`;
- `DeviceId` e `Kind` passam a ser **nullable**: host do Zabbix fora do
  inventário não tem device e trigger não tem `CheckKind` nativo. Fábrica
  nova `Alert.CreateExternal(...)` (fonte ≠ `Native` validada); `Alert.Create`
  nativo continua exigindo device+kind;
- Índice `(source, external_problem_id)` para o lookup de recuperação;
- `AlertDto`/front: `deviceId`/`deviceName`/`check` nullable + campo `source`.

### 3. Pipeline no `MonitoringEventIngestor`

O handler do webhook passa a **validar/normalizar** (name → severity →
status → eventId, mensagens pt-BR) e delegar ao ingestor
(`Application/Monitoring/Events`):

- **PROBLEM**: dedup por `(Source, ExternalEventId)` → cria evento + alerta +
  incidente + vínculo num **único `SaveChanges`** (atômico). Corrida
  simultânea estoura o índice único e o retry do Zabbix cai no caminho
  `duplicate` (o Zabbix processa notificações do mesmo trigger
  sequencialmente — doc do webhook);
- **OK moderno** (`recoveryEventId`): linha própria do recovery + backfill do
  problem; **OK legado** (sem o campo): transição **in-place** da linha do
  problem (linha nova colidiria a unicidade);
- **OK órfão** (sem linha de problem, ex. ação pré-upgrade): grava a linha do
  recovery, Warning no log, `none` — nunca derruba o webhook;
- Incidente externo é **1 por alert/problema** (não reutiliza o
  `GetOpenByDeviceAsync` por device como o nativo) e **todo PROBLEM gera
  incidente** (preserva a semântica "todo PROBLEM vira incidente" do
  ADR-023);
- Dedup/recovery **por título banido** do fluxo
  (`GetOpenByTitleAndDeviceAsync` removido).

### 4. Correções no avaliador nativo (mesma leva)

- `EnsureIncidentAsync` → `GetUnresolvedByDeviceAsync` (`Status != Resolved`):
  não abre 2º incidente enquanto o atual está em investigação;
- `CloseRecoveredIncidentsAsync` → `GetUnresolvedIdsLinkedToAlertAsync` +
  `GetUnresolvedByDeviceAsync`: o recovery fecha qualquer estado ≠ `Resolved`.

### 5. Lifecycle do Incidente: inalterado (sem `Acknowledged`)

O ciclo `Open → Investigating → Identified → Monitoring → Resolved` com
transições livres já cobre o ack (a operação "Acknowledge" mapeia
`Open → Investigating`). Um estado `Acknowledged` adicional seria
redundante — **o plano original que previa esse estado foi corrigido aqui**.

### 6. Notificação: domain events (amend F19-T007)

Nesta fase só `IncidentCreatedDomainEvent` era levantado (1 notificação por
incidente, F16-T002). **Amend (F19-T007, HANDOFF da sessão paralela)**:
`AlertResolvedDomainEvent` (`Alert.Resolve`) e `IncidentResolvedDomainEvent`
(`Incident.ChangeStatus → Resolved`) passaram a ser levantados — o
`NotificationEventConsumer` notifica as roles `Operator` e `Admin`
(mesmo padrão do `IncidentCreated`). `AlertCreated` continua fora (alerta
sem resolução não gera notificação).

## Consequências

- **Migração** `AddMonitoringEventsAndAlertExternalSource`: cria
  `monitoring_events` + colunas novas em `alerts`; linhas antigas ganham
  `source = 'Native'` (default na migração).
- **Contrato do webhook v2** (docs/27): `eventId` passou a ser **obrigatório**
  nos dois status — ações antigas precisam enviar `{EVENT.ID}` (o runbook
  atualiza a tabela de parâmetros); `recoveryEventId`/`occurredAt` são
  opcionais.
- Incidentes de Zabbix **pré-upgrade** não ganham linha de evento/alerta
  retroativos: o OK correspondente responde `none` e esses incidentes fecham
  manualmente (migração de dados fora de escopo).
- `monitoring_events` **ganhou retenção** no `CleanupService`
  (`RetentionDataType.MonitoringEvents`, default 90 dias sobre
  `occurred_at`, policy sobrescreve via Operations → Retention; índice
  `IX_monitoring_events_occurred_at` na migração
  `20261004005530_AddMonitoringEventsOccurredAtIndex`; alerta vinculado
  sobrevive com `monitoring_event_id` em NULL — `SetNull`).
- Alertas externos sem device entram no search com `deviceName` null; a UI
  mostra "Sem device · Zabbix" (fallbacks no front).
- Escritas concorrentes do mesmo `eventid` podem gerar 500 pontual (índice
  único) seguido de 200 `duplicate` no retry — aceito, sem catch de
  violação nesta etapa.

## Alternativas descartadas

- **Reusar `Incident.Title` como chave** (status quo): colisão entre hosts e
  payload sem id; mantido só o título como texto de exibição.
- **Estado `Acknowledged` no Incidente**: redundante com
  `Investigating` (ver §5).
- **Criar incidente externo só em severidade alta**: quebraria o contrato
  "todo PROBLEM vira incidente" do ADR-023 — padronizado como 1 por problema.
- **Notificar `AlertCreated/Resolved` nesta fase**: duplicaria notificação
  (incidente já avisa) antes de existir política de dedup — etapa própria.
- **Fallback de dedup por título quando `eventId` faltar**: contrato novo
  exige `eventId` (400 pt-BR); dois caminhos de dedup manteriam a
  heurística banida.
