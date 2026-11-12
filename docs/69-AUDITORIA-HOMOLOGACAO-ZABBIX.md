# 69 — Auditoria completa e homologação Zabbix (2026-11-10)

Auditoria técnica completa do ambiente Zabbix de produção (hospitais:
APs, impressoras, câmeras/NVR, switches, servidores, roteadores), incluindo
a cadeia de alertas até o grupo de WhatsApp via InfraControl.

Método: API JSON-RPC (Zabbix **8.0.0rc2** — atenção: schema novo,
`item_rtdata`/`trigger_rtdata`/`events`/`problem`), SQL direto no
PostgreSQL do Zabbix, leitura do código do InfraControl e testes
controlados. Coleta reproduzível: `scripts/zabbix/audit-collect.ps1` e
`scripts/zabbix/api-call.ps1`.

## 1. Estado geral verificado

| Item | Resultado |
|---|---|
| Hosts reais | 152 (sem IP/DNS/hostname duplicados) |
| Templates | 392 instalados; ~9 em uso efetivo |
| Problemas não resolvidos | ~92–97 (12 info, 35 warning, 35 average, 18 high, 0 disaster) |
| Itens | ~11,9k coletando; 26 unsupported (maioria esperada); ~199 sem histórico (72 = IEAS agente morto, 23 = features off do server, 9 = SUPTI666, 6 = OIDs ausentes do NVR, 30 = `snmptrap.fallback` normal) |
| Interfaces unavailable | 5 reais: `HSDS_SUPTI666`, `IEAS-Impressoes-Servidor` (agents mortos — pendência conhecida), `ap-192-168-10-237`, `nvr-192-168-26-108`, `prn-192-168-22-105` |
| Pinger | ~44% busy (era 100% antes da Sprint 1); queue=0–8 |
| Backup | diário 02:00 OK; dump de 10/11 em andamento; DB ≈ 11 GB (era 6,9 GB em 01/11 — observar crescimento) |
| Versão | **8.0.0rc2 alpine-trunk** (pendência de upgrade para estável) |
| Usuários | apenas Admin + guest (sem conta operacional) |
| Maintenances | nenhuma; 0 problemas reconhecidos |

## 2. Causa raiz do "muito alerta"

~4.700 eventos de trigger em 72h (~65/hora). Cadeia completa:

```
Trigger flapping (novo eventid a cada ciclo)
  → action 7 (sem filtro de severidade) → webhook media type 105/106
  → InfraControl ingest (dedup por eventId — NÃO deduplica flap)
  → Alert/Incident → NotificationEventConsumer → WhatsApp
```

Medições das últimas 24h no InfraControl:

- **1.070** `AlertResolvedDomainEvent` ("Alerta resolvido")
- **472** `AlertCreatedDomainEvent`
- **471** `IncidentCreatedDomainEvent` ("Incidente aberto")
- **454** `IncidentResolvedDomainEvent` ("Incidente resolvido")
- Entregas WhatsApp: ~2.160/dia, 0 erros — pipeline funciona, volume é o problema.

Como `AlertResolvedDomainEvent` usava o fanout default (que inclui
WhatsApp), **todo flap de warning virava mensagem**; incidentes
severity ≥3 geravam até 3 mensagens por ciclo (abre + resolve + alerta
resolve).

Principais fontes de flap (72h):

| Eventos | Trigger | Observação |
|---|---|---|
| ~3.250 | `Taxa de erros alta` Wi-Fi (APs) | bursts reais de erro oscilando (ex.: `wifi1ap5` do AP_Pronto_Socorro: 186/1111 amostras >20 err/s) |
| ~1.500 | `sem link` de portas + ICMP down | links físicos oscilando (ex.: porta 22 do sw-192-168-127-243: 42 problemas/72h, transições a cada 1–2 min) |
| ~400 | ICMP ping down/loss/latência | hosts instáveis + duplicidade em 3 câmeras |

## 3. Correções aplicadas (todas reversíveis)

Backup pré-mudança: export versionado `zabbix-config/export/*`
(atualizado 10/11) + dump `backups/zabbix/` (em execução).

### Sprint A — Zabbix

| # | Mudança | Motivo | Reversão |
|---|---|---|---|
| A1 | Protótipo `29743` (template Network Generic Device by SNMP): trigger "Taxa de erros alta" — funções `min/max` de `$,5m`→`$,10m`, recovery `*0.8`→`*0.6` | bursts reais de erro Wi-Fi oscilavam na deadband estreita; janela 10m + deadband 60% reduz ciclos sem esconder erro sustentado | reverter parâmetro das funções / recovery_expression |
| A2 | Protótipos `29744` (Network Generic) e `29755` (HP Comware): função de status do "sem link" `last($)`→`min($,5m)` | porta que oscila 1↔2 a cada 1–2 min gerava 40+ problemas/72h; agora exige 5 min seguidos down; queda real alerta com ~5 min de atraso | reverter função para `last` |
| A3 | Triggers `39611`, `39600`, `39589` (ICMP-down herdadas) desabilitadas em `cam-192-168-26-133/16/190` | cada câmera tinha 2 triggers idênticas (`max($,#3)=0` no mesmo item) → alerta duplo severity 4; mantida a trigger "ICMP Ping" standalone (padrão da frota de câmeras) | `trigger.update status=0` |
| A4 | LLD `printer.alerts.discovery` (item `83312` + 36 cópias): `lifetime` 1h→7d | índices de alerta de impressão são transitórios; 1h apagava/recriava itens habilitados → flapping de unsupported | `UPDATE items SET lifetime='1 hour'` |
| A5 | `prn-192-168-22-166`: reabilitados item `86069` (`printer.alert.severity[1.19]`) e trigger `40428` ("Alerta crítico: 3") | **achado real**: impressora reportava severidade 3 (crítico) e 4 ativamente, mas item+trigger estavam desabilitados → ponto cego. Última leitura 10/11 13:09 = 3 | desabilitar item/trigger novamente |

Notas:
- A1 propaga às ~316 instâncias LLD; A2 propaga às ~931 instâncias
  "sem link" no próximo ciclo do `net.if.discovery` (delay 1h).
- `HSDS_SUPTI666` (agente morto): triggers de serviço Windows
  `36025`/`39051` mantidas desabilitadas — problema residual é
  cosmético; serviço provavelmente desligado de propósito.
- Portas com link fisicamente instável (sw-127-243 portas 5/22,
  sw-127-234 Gi1/0/12) são **problema real de infraestrutura** —
  recomenda-se inspeção física (cabo/SFP/negociação), não silêncio
  no Zabbix.

### Sprint B — InfraControl

- `notification_event_rules`: nova regra `AlertResolvedDomainEvent`
  → canais `Email,SignalR` (sem WhatsApp). Efeito: elimina ~1.070
  msgs/dia; incidentes críticos continuam abrindo/resolvendo no grupo.
  Reversível pela UI de regras de notificação ou DELETE na tabela.

## 4. Verificação

- `smoke-test.ps1`: **6/6 OK** (login, /ping, action 7, media types
  105/106, queue=0, pinger ~44%).
- `triggerprototype.update` propaga funções/expressões às cópias de
  hosts; instâncias LLD atualizam no próximo run da regra.
- Regra de notificação lida por evento pelo consumer (sem cache).

## 5. Achado crítico: WhatsApp CAIU em 23/10

Durante a validação pós-mudanças, descoberto que **nenhuma mensagem
de WhatsApp está sendo entregue desde 23/10/2026**:

- Instâncias Evolution `Admin` e `bumba`: `connectionStatus=close`,
  `disconnectionReasonCode=401` ("Log out instance") desde
  23/10/2026 ~18:59.
- Deliveries recentes: `WhatsApp | Failed | HTTP 400 Bad Request`
  (247 falhas nas últimas 2h; grupo `120363431225988863@g.us` não
  resolve com a sessão encerrada — logs: "Cache request for group …
  not found").
- A regra `AlertResolvedDomainEvent → Email,SignalR` já está ativa
  (verificado: notificações de resolução pós-13:47 não geram delivery
  WhatsApp).
- O pipeline Zabbix→InfraControl→fila de delivery funciona; o gargalo
  é a sessão WhatsApp desconectada.

**Ação necessária (manual)**: reconectar a instância no Evolution API
(`POST /instance/connect/{instance}` gera QR) e escanear com o
WhatsApp do número monitorado. Sem isso, ETAPA 4 (WhatsApp) e os
testes de envio/recuperação não podem ser homologados.

## 6. Pendências (precisam de decisão/autorização)

1. **Comunidades SNMP** `public`/`hsds` (131 hosts) — rotação exige
   janela e credenciais por fabricante.
2. **Versão** 8.0.0rc2 alpine-trunk → upgrade para release estável.
3. **Agents mortos**: `HSDS_SUPTI666`, `IEAS-Impressoes-Servidor`.
4. **Conta operacional** Zabbix (hoje só Admin/guest).
5. **Crescimento do DB** 6,9→11 GB — revisar housekeeping/trends.
6. **Prn-192-168-22-166**: verificar fisicamente o alerta crítico da
  impressora (o alerta agora volta a notificar — esperado).
7. **Links instáveis** sw-127-243/234 — inspeção física.
8. **APs com bursts de erro Wi-Fi** (wifi1ap5 etc.) — avaliar RF/
   interferência; erros são reais, não falsos positivos.
9. ~40 triggers de câmera `absent`/OID — revisar se ruído residual.
10. **Reconexão da sessão WhatsApp** (QR code) — bloqueante para
    homologação do canal.

## 7. Status de homologação

**APROVADO COM RESSALVAS** — para homologação plena faltam:

- Observação de 24–48h do volume de notificações pós-mudanças
  (meta: <100 msgs/dia no WhatsApp **após reconexão da sessão**).
- Confirmação da propagação LLD de A1/A2 (próximo ciclo horário).
- Itens 1–4 e 7–9 das pendências (fora do escopo Zabbix-only).

O ambiente está **funcionalmente estável**: fila zerada, pinger
saudável, backups diários, alertas críticos com caminho único e
recuperação registrada. O ruído residual é dominado por problemas
físicos reais (links, RF, agents mortos) que o monitoramento está
corretamente reportando.

## 8. Validação ponta-a-ponta do fluxo Zabbix → InfraControl (10/11 ~16:05 UTC)

### Correção aplicada durante a validação

- **Media types 105/106**: `occurredAt` adicionado ao corpo do webhook
  com `Number(p.occurredAt)` — enviado como string causava **HTTP 400**
  (binding `long?`); corrigido e confirmado com eventos reais
  (evento 47652 → 200 às 16:00:43).
- Contrato confirmado: no status `OK`, `{EVENT.ID}` = **eventid do
  problema original** e `{EVENT.RECOVERY.ID}` = eventid do recovery —
  exatamente o que `MonitoringEventIngestor` espera
  (`problemId = input.EventId`).

### Teste controlado executado (payloads diretos no endpoint)

| Cenário | Resultado |
|---|---|
| PROBLEM severity 4 (Critical) | `created` — MonitoringEvent + Alert + **Incidente Open** |
| PROBLEM duplicado (mesmo eventId) | `duplicate` — mesmo incidentId, sem efeito colateral |
| OK com eventId=problem + recoveryEventId | `resolved` — problem→Recovered, recovery row criada, **alert Resolved + incidente Resolved** |
| OK órfão (sem problem correspondente) | `none` — recovery gravado sem fabricar alerta/incidente |

### Evidência em produção (eventos reais pós-correção)

- Recuperações reais resolvendo corretamente: problem 47652
  (`AP_SalaDeAula2 wifi1ap5`) → `MarkRecovered(47658)` + linha de
  recovery própria; alert e incidente fechados.
- `name` agora chega com prefixo do host
  (`{HOST.NAME1}: {TRIGGER.NAME}`) — ex.: `AP_SalaDeAula2:
  Network Generic Device: Interface wifi1ap5...`.
- `occurredAt` aceito (unix seconds → UTC no handler).

### Observações

- Host/IP trafegam no payload; device só é vinculado quando o IP
  existe no inventário InfraControl (~86 hosts Zabbix sem device —
  gap de inventário, não de fluxo).
- Dados de teste `TESTE-FLUXO*` permanecem nas tabelas, todos
  `Resolved` — rastreáveis para limpeza futura se desejado.
- WhatsApp segue fora do escopo desta validação (sessão `close`).

## 9. Política de notificação "alerta é alerta" (10/11 ~17:30 UTC)

Decisão do usuário: incidente notifica como incidente, alerta como
alerta, resolvido como resolvido — sem transformar alerta em incidente.

### Alterações

- `Alert.Create`/`CreateExternal`: `AlertCreatedDomainEvent` agora é
  levantado para **Warning** também (`HasIncident=false`); Critical
  segue com `HasIncident=true`. Substitui a decisão ADR-059 "só
  Critical notifica".
- `NotificationEventConsumer`: título dinâmico — "Alerta crítico
  criado" (Critical) / "Alerta criado" (Warning). Warning segue pelo
  fanout (Email+SignalR+WhatsApp quando ativo); Critical-com-incidente
  continua SignalR+Email para não duplicar "Incidente aberto".
- `notification_event_rules`: `AlertResolvedDomainEvent` →
  `Email,SignalR,WhatsApp` (antes Email,SignalR) — **efetivo já no
  build em produção** (regras lidas dinamicamente).
- Testes atualizados: `AlertTests`, `AlertEvaluatorTests`,
  `AlertCreatedNotificationTests`, `NotificationEventConsumerTests`.

### Resultado esperado (após deploy)

| Evento Zabbix | InfraControl | WhatsApp |
|---|---|---|
| Warning (wifi erro, toner, reboot, ping loss) | Alerta aberto + resolvido | ✅ sim |
| High/Critical | Incidente aberto + resolvido | ✅ sim |

### Bloqueio de deploy

O código novo (Warning→evento) precisa de rebuild, mas a árvore está
em **merge incompleto** (`MERGE_HEAD`): a feature Backup do remoto
trouxe `BackupSettingsRepository` sem a camada Application
(`IBackupSettingsRepository`, queries/commands) — nem em nenhuma
branch ela existe. Build quebra em
`src/InfraControl.Infrastructure/Repositories/BackupSettingsRepository.cs`.
Domínio compila isolado; Workers/API pendentes até o merge ser
resolvido (completar o slice Application/Backup ou remover os
arquivos staged).
