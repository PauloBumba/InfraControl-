# Domínio e Dados

## Entidades principais

```text
Location
Rack
Device
NetworkInterface
MacAddress
IpAddress
Vlan
SwitchPort
SwitchPortVlan
Connection
DiscoveryRun
DiscoveryResult
MonitoringCheck
MonitoringResult
Alert
Incident
Service
Task
SlaPolicy
TaskSla
TaskSlaPause
Slo
SloMeasurement
SloBudget
KnowledgeCategory
KnowledgeArticle
Notification
NotificationRecipient
NotificationDelivery
AuditLog
OutboxMessage
ProcessedMessage
RetentionPolicy
Invitation
InvitationRole
ExternalIdentity
PasswordResetToken
UserSession
```

## Device

Um Device representa equipamentos heterogêneos.

Tipos:

- Server
- Workstation
- Switch
- Router
- Firewall
- AccessPoint
- Printer
- Camera
- Phone
- UPS
- Other

## Relacionamentos

```text
Location 1:N Rack
Rack 1:N Device
Device 1:N NetworkInterface
Switch(Device) 1:N SwitchPort
SwitchPort N:N Vlan
NetworkInterface 1:N IpAddress
Interface N:N Interface via Connection
```

## Connection

Conexão entre duas interfaces — `SourceInterfaceId` → `TargetInterfaceId`
(F3-T005, decisões do usuário):

- Par **dirigido**: `(a,b)` é diferente de `(b,a)`; duplicado do mesmo
  par → 409.
- Única restrição: self-loop (mesma interface nas duas pontas) → 400.
  Duas interfaces do mesmo device são permitidas.
- FKs Restrict: excluir interface com conexão → 409 no handler.
- Ingestão automática a partir de achados Neighbor (LLDP/CDP):
  concluída em F3-T005b — gatilho no fim do run de discovery, casamento
  por NOME via atributo `local_if_name` (detalhes em docs/09).

PostgreSQL é a fonte da verdade.

## Alert

Condição detectada (glossário) — F6-T001:

- `DeviceId` + `Kind` (CheckKind) + `Severity` (Warning | Critical) +
  `Status` (Open | Suppressed | Resolved) + `Message` (pt-BR) +
  `ResolvedAt?`.
- Nasce **Open** na avaliação pós-check e **resolve automático** quando o
  próximo check mostra a condição normal (auto-resolve).
- **Suppressed** (F6-T004 — supressão topológica): com upstream
  alcançável via `Connection` em Incident Open, o alerta de qualquer
  kind nasce/vira `Suppressed` e é vinculado ao incidente raiz
  (`incident_alerts`) — sem incidente próprio; upstream normalizado →
  `Reopen` (Open de novo; `Resolved` nunca reabre). Busca de status
  aceita `open|suppressed|resolved`.
- Unicidade lógica: **1 alerta Open por (device, check)** — anti
  alert-storm; histórico completo é preservado (índice sem unicidade).
- Índice `(device_id, kind)`; FK cascade (device excluído → alertas
  excluídos). Criação é automática (pós-check) — não há POST público;
  leitura via `GET /monitoring/alerts`.
- Thresholds **fixos em código** (`AlertThresholds`): toner < 20%,
  drum < 10%; down do Ping exige **streak de 3 falhas consecutivas**
  lido do histórico (F6-T002 — detalhes em docs/09).

## Incident

F6-T003 — agrupa N `Alert` (N:N via `incident_alerts`):

- `Title` (obrigatório) + `Description?` + `Severity`
  (Low | Medium | High | Critical) + `Status` (5 estados da UI: Open |
  Investigating | Identified | Monitoring | Resolved) + `DeviceId?` +
  `ResolvedAt?`.
- **Auto-abertura**: um Alert **Critical** (Ping down com streak) cria 1
  Incident Open por device e vincula o alerta; Warning nunca abre.
  Alert órfão (criado antes do T003) ganha vínculo na próxima avaliação.
- Transições livres dentro do enum (≠ atual → 400); Resolved estampa
  `ResolvedAt` e sair dele limpa (reopen). `Acknowledge` = → Investigating.
- Índices em `status` e `device_id`; FKs cascade (device/alert/exclusão).
- **Vínculo upstream** (F6-T004): alerts `Suppressed` de devices atrás
  do root ficam ligados ao mesmo incidente pela N:N — 1 raiz, N
  relacionados.
