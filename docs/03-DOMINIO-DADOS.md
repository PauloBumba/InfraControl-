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
- VirtualMachine <!-- F31-T001 (docs/53 D2): VM no inventário; perfil default = o de Server (D3 — "VM entra na categoria de servidores") -->
- VCenter <!-- F31-T001 (docs/53 D2): gerenciador vCenter; perfil default = o de Server (D3) -->

### ExternalDeviceReference (F19-T006)

Identidade externa estável do device (`external_device_references`):
`1 Device → N referências`, unicidade física em `(source, external_id)`
— a ponte **"MAC/Device ID → Device"** das integrações (UniFi na F20).
`source` ∈ `UniFi | Zabbix`; o identificador é normalizado (trim +
minúsculas) no domínio e no lookup. **A resolução do Zabbix por
IP/hostname não muda** (regra 6 da revisão F19/F20/F21): esta tabela só
é consultada quando a fonte manda um identificador estável.

## Relacionamentos

```text
Location 1:N Rack
Rack 1:N Device
Device 1:N NetworkInterface
Switch(Device) 1:N SwitchPort
SwitchPort N:N Vlan
NetworkInterface 1:N IpAddress
Interface N:N Interface via Connection
VirtualMachine(Device) N:1 hypervisor: Device   (hypervisor_device_id, FK self — F31-T002)
VirtualMachine(Device) N:1 vCenter: Device      (vcenter_device_id, FK self, type=VCenter — F31-T002)
```

## Connection

Conexão entre duas interfaces — `SourceInterfaceId` → `TargetInterfaceId`
(F3-T005, decisões do usuário):

- Par **dirigido**: `(a,b)` é diferente de `(b,a)`; duplicado do mesmo
  par → 409.
- Única restrição: self-loop (mesma interface nas duas pontas) → 400.
  Duas interfaces do mesmo device são permitidas.
- FKs Restrict: excluir interface com conexão → 409 no handler.
- Origem/confiança da informação (F53-T001, spec 11.7): colunas `source`
  (`manual | lldp | cdp | unifi | unknown`) e `confidence`
  (`confirmed | estimated | unknown`), gravadas em **minúsculas** no banco
  e no DTO (casing igual ao UniFi live — `lldp`/`confirmed`). CRUD manual
  grava `manual`/`confirmed`; o PUT (reapontar) volta para
  `manual`/`confirmed` (spec 11.3 — ajuste manual é configuração manual);
  ingestão LLDP/CDP grava a origem do achado com `confirmed`; linhas
  legadas (migração `AddConnectionOrigin`) → `unknown`/`unknown`;
  `estimated` está reservado para inferências futuras. O UniFi **não**
  persiste origem — fica só como indicação live (REGRA 8/ADR-028).
- Ingestão automática a partir de achados Neighbor (LLDP/CDP):
  concluída em F3-T005b — gatilho no fim do run de discovery, casamento
  por NOME via atributo `local_if_name` (detalhes em docs/09).
- Grafo de topologia (`GetTopology`, F53-T002): cada aresta expõe as
  pontas enriquecidas por SwitchPort casada por **nome** (device + nome em
  minúsculas): nome da interface, speed/duplex e VLANs da porta
  (`SwitchPortVlan`); ponta sem porta casada → speed/duplex/VLANs `null`
  (porta casada sem VLAN cadastrada → lista vazia). Aresta fantasma
  (desenhada sem Connection persistida) não tem origem/confiança (`null`).
  O `layer` de cada node é calculado por BFS multi-raiz a partir de
  devices Router/Firewall sobre as Connections não-fantasma (1 = raiz);
  device sem caminho até uma raiz → `layer` `null` (fora da hierarquia).

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
- **Recovery** (F6-T006): quando um alert resolve (condição normalizada),
  incidentes **Open** vinculados a ele são verificados — se **todos** os
  alerts vinculados estão `Resolved`, o incidente fecha automático
  (→ Resolved com `ResolvedAt`). Incidente **sem vínculos** (manual)
  nunca fecha por recovery — só por comando. Um vínculo ainda ativo
  (ex.: downstream `Suppressed` sem resposta) mantém o incidente aberto.
- **`RootCause`** (F6-T005, string? 500): causa provável derivada por
  heurística fixa — (1) upstream alcançável com Incident Open → falha
  nele; (2) vizinho com alerta de Ping Open **mais antigo** que o
  incidente → enlace/switch do vizinho; (3) senão → falha local. A
  regra de tempo evita culpar vítimas (que caem depois). Recalculada a
  cada avaliação (cobre a corrida do T004); incidente manual sem device
  → null; expondo na API (IncidentDto).
