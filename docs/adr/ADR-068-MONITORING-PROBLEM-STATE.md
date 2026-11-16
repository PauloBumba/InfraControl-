# ADR-068 — Estado de problema de monitoramento (notificação só em transição)

- **Status**: **aceito pelo usuário em 2026-11-12; implementação em andamento**
- **Data**: 2026-11-12
- **Relacionado**: ADR-023, ADR-027, ADR-059, ADR-062, ADR-063; F55; docs/07, docs/27

## Contexto

O pipeline Zabbix → `MonitoringEvent` → `Alert` → `Incident` → domain events →
`NotificationEventConsumer` deduplica apenas por `(Source, ExternalEventId)`.
Cada `PROBLEM` novo do Zabbix traz um `eventid` novo, então updates de problema
e flaps que não passam por `OK` geram `Alert`/`Incident`/notificações novos a
cada repetição — observado em produção como milhares de mensagens de
"Incidente aberto"/"resolvido" no grupo de WhatsApp (risco de bloqueio do
número pela Meta).

O usuário definiu a regra: **notificar exclusivamente em mudança de estado** —
Normal→Incidente notifica; Incidente→continua incidente não; Incidente→
Resolvido notifica; Resolvido→novo incidente notifica de novo; Alerta→
Incidente notifica a mudança. Sem debounce, janela de supressão ou digest.

## Decisão

Nova entidade de domínio **`MonitoringProblemState`**: uma linha por
*problema lógico* (não por evento), com chave de correlação estável entre
flaps e ciclo de vida persistido.

### Chave de correlação

`MonitoringCorrelationKey.Resolve(source, sourceContextJson, deviceId, host,
ip, name)`:

- **primária**: `triggerId` do `ZabbixEventContextDto` (já consultado na
  ingestão via `IZabbixEventContextReader`, ADR-063) → `{Source}:trigger:{id}`;
- **fallback** (API Zabbix indisponível ou evento sem contexto): hash
  `{Source}:name:{deviceId|host|ip}:{sha256(nome normalizado)[..16]}`.

O `triggerid` é estável enquanto o trigger existir — é a identidade do
"mesmo problema" entre flaps, updates e reaberturas.

### Estados persistidos

`ProblemStatus`: `Alerta` (problem aberto sem incidente próprio — Warning ou
alerta suprimido por upstream), `Incidente` (problem aberto com incidente
próprio), `Resolvido`. `Normal` é a ausência de linha — não há custo de
manter estado para problemas que nunca abriram.

Campos: `Source`, `CorrelationKey` (único por source), `DeviceId`, `Severity`
(última da fonte), `Status`, `AlertId`/`IncidentId` do ciclo atual,
`LastProblemEventId`, `OpenedAt`, `ResolvedAt`, `OpenCount` (ciclos abertos),
`RepeatedCount` (repes suprimidas no ciclo atual — observabilidade de flap).

### Transições (em `MonitoringEventIngestor`)

- **PROBLEM + estado Alerta/Incidente** → grava o `MonitoringEvent` (histórico
  bruto preservado), incrementa `RepeatedCount` e **não chama o avaliador**:
  sem `Alert`/`Incident`/domain events novos = zero notificações. Resposta
  `suppressed` (novo token de outcome, aditivo ao contrato docs/27).
- **PROBLEM + estado Alerta + severidade Critical** → escalonamento
  Alerta→Incidente: avaliador roda, estado vira `Incidente` e o alerta
  anterior (Warning) é resolvido — a mudança é notificada.
- **PROBLEM + estado Resolvido/ausente** → fluxo atual (avaliador) + estado
  criado/atualizado (`OpenCount++` na reabertura).
- **OK** → caminho de resolução atual inalterado (idempotente por
  `Recovered`) + estado → `Resolvido`. Correlação via `AlertId`: todo
  problema externo gera exatamente um `Alert`, então o OK encontra o estado
  por `GetByAlertIdAsync(alert.Id)` — sem coluna nova em `monitoring_events`.

### O que não muda

- Idempotência por `eventId` (`Duplicate`) e recovery (`Recovered`) intactas.
- Supressão topológica BFS e "1 alerta por problem" do avaliador intactos.
- Consumer de notificações, regras por evento, canais e deliveries: **zero
  alteração** — eles passam a receber eventos só em transições reais.
- Pipeline nativo (checks ICMP/SNMP): já tem anti-storm por
  (device, check); fora do escopo.

## Consequências

- Flap/update de trigger para de gerar `Alert`/`Incident`/notificações
  duplicados **na fonte** — o banco também deixa de acumular linhas-lixo.
- Cada ciclo open→resolve notifica 2 vezes (aberto + resolvido), conforme a
  regra aprovada; `RepeatedCount`/`OpenCount` permitem medir flap depois.
- Corrida de dois PROBLEM simultâneos com a mesma chave: unicidade
  `(Source, CorrelationKey)` protege; o perdedor recebe erro de constraint e
  o retry do Zabbix cai no caminho já resolvido — mesmo comportamento do
  dedup por `eventId` atual.
- Estados de problemas abertos **antes** do deploy não existem: o primeiro
  evento pós-deploy cria a linha; incidentes legados continuam resolvendo
  pelo caminho atual (OK resolve alert/incident independentemente do estado).
