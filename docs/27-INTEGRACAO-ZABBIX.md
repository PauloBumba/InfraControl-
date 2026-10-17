# 27 — Integração com Zabbix (webhook de entrada)

- **Task**: F16-T001 (Fase 16 — Integrações) · **ADRs**: [ADR-023](adr/ADR-023-ZABBIX-INBOUND-WEBHOOK.md)
  (endpoint/token) + [ADR-027](adr/ADR-027-MONITORING-EVENT-INGESTION.md)
  (pipeline `MonitoringEvent → Alert → Incident`, dedup por `eventId`)
- **Fluxo**: Zabbix **avisa** (problem/OK) → InfraControl **orquestra**
  (`MonitoringEvent` → `Alert` → `Incident`, incidente no ciclo F6/F7:
  leitura, tarefa, conhecimento). Notificação fanout concluída na F16-T002
  (`IncidentCreatedDomainEvent` → outbox → consumer, destinatários Operator
  + Admin — cobre webhook, manual e auto-abertura).

## 1. Endpoint

```
POST /api/v1/integrations/zabbix/problems
Content-Type: application/json
X-Zabbix-Token: <token>
```

É o **primeiro endpoint público** da API (`[AllowAnonymous]` + entrada na
allowlist do arch test `Public_Endpoints_Should_Be_The_Explicit_Allowlist`).
A autenticação é **só** o header `X-Zabbix-Token` comparado (ordinal) com a
configuração `Integrations:Zabbix:Token`.

### Habilitar

`appsettings.json` da Api (default vazio = **desligado**):

```json
"Integrations": {
  "Zabbix": {
    "Token": "cole-um-token-forte-aqui"
  }
}
```

Em Docker: variável de ambiente `Integrations__Zabbix__Token`. Token vazio
também é o comportamento correto em dev — a rota responde 404.

## 2. Contrato

```json
{
  "eventId": "10451",
  "recoveryEventId": "10499",
  "host": "srv-web-01",
  "ip": "192.0.2.71",
  "name": "srv-web-01: Carga de CPU alta",
  "severity": 4,
  "status": "PROBLEM",
  "occurredAt": 1790865600
}
```

| Campo | Obrigatório | Regra |
|-------|-------------|-------|
| `eventId` | **sim** | `{EVENT.ID}` — no PROBLEM é o eventid do problem; no OK é o eventid **do problem original** (chave de dedup e correlação) |
| `recoveryEventId` | não | `{EVENT.RECOVERY.ID}` — id do evento de recovery (só no OK moderno; ausente → transição in-place, ver §3) |
| `host` | não | `{HOST.NAME1}` — fallback de match do device |
| `ip` | não | `{HOST.IP}` — match primário do device |
| `name` | **sim** | `{TRIGGER.NAME}` — vazio → 400 pt-BR |
| `severity` | **sim** | inteiro 0..5 (id de severidade do Zabbix) |
| `status` | **sim** | `PROBLEM` ou `OK` (case-insensitive) |
| `occurredAt` | não | `{EVENT.TIMESTAMP}` (unix seconds) — ausente/absurdo → relógio da plataforma |

### Resposta

Sempre no envelope `{isSuccess, data, errors}`:

| Situação | HTTP | `data` / `errors` |
|----------|------|-------------------|
| Sucesso | 200 | `data: { incidentId, outcome }` |
| Token de configuração vazio (desligado) | 404 | `errors: ["A integração com Zabbix está desativada."]` |
| Header ausente/divergente | 401 | `errors: ["Token do Zabbix inválido."]` |
| Validação do payload | 400 | `errors: ["O nome do problema é obrigatório. …"]`, `"O eventId é obrigatório …"` etc. (pt-BR, `GlobalExceptionHandler`) |

`outcome` (token de contrato, não traduzir):

- **`created`** — PROBLEM gerou `MonitoringEvent` + `Alert` + `Incident` novos;
- **`duplicate`** — já existe evento para esse `eventId`
  (`(source, external_event_id)` único — idempotente, não cria outro);
- **`resolved`** — OK recuperou o problem: alerta → `Resolved` e incidente
  vinculado (qualquer estado ≠ `Resolved`) → `Resolved`;
- **`none`** — OK sem `MonitoringEvent`/`Alert` correspondente (órfão: a
  evidência é gravada e nada mais é feito).

## 3. Comportamento

Pipeline (ADR-027): **`MonitoringEvent` → `Alert` → `Incident`**, num único
`SaveChanges` (evento + alerta + incidente + vínculo são atômicos).

1. **Match do device**: tenta por `ip` (`GetByIpAsync`), senão por hostname
   (`GetByNameAsync`). Sem match → evento/alerta/incidente nascem **sem
   device** com `host`/`ip`/`eventId` na descrição.
2. **PROBLEM** → `MonitoringEvent.CreateProblem` + `Alert.CreateExternal`
   (severidade 0..2 `Warning`, 3..5 `Critical`) + `Incident.Create` (sempre —
   1 incidente por alert/problema, decisão ADR-027) e vínculo N:N.
   Dedup por **`(source, external_event_id)`**: reenvio do mesmo `eventId`
   responde `duplicate` com o incidente já vinculado. Mesmo título em hosts
   diferentes **não** colide (título não é chave desde o ADR-027).
3. **OK** → correlação pelo **problem original** (`eventId`):
   - com `recoveryEventId` (moderno): cria a linha própria do recovery
     (`external_event_id = recoveryEventId`) e faz **backfill** da linha do
     problem (`status → Recovered`, `external_recovery_id → recoveryEventId`);
   - sem `recoveryEventId` (ação legada): **transição in-place** da linha do
     problem para `Recovered` (linha nova colidiria a unicidade);
   - em ambos: alerta → `Resolved` e todos os incidentes vinculados com
     `status != Resolved` (Open/Investigating/Identified/Monitoring) →
     `Resolved`. Replay do OK é idempotente.
   - **Sem linha de problem** (órfão, ex.: ação pré-upgrade): grava a linha do
     recovery, loga Warning e responde `none`.
4. **Severidade Zabbix → InfraControl** (6 → 4 níveis da UI no incidente):

   | Zabbix | `IncidentSeverity` | `AlertSeverity` |
   |--------|--------------------|-----------------|
   | 0 Not classified | Low | Warning |
   | 1 Information | Low | Warning |
   | 2 Warning | Medium | Warning |
   | 3 Average | High | Critical |
   | 4 High | Critical | Critical |
   | 5 Disaster | Critical | Critical |

5. Migração EF: `AddMonitoringEventsAndAlertExternalSource` (tabela
   `monitoring_events` + colunas `source`/`monitoring_event_id`/
   `external_problem_id` em `alerts`; linhas antigas ganham
   `source = 'Native'`).
6. Rate limit padrão (120 req/min) vale para a rota.

## 4. Runbook — action no Zabbix (8.x, via API — implementado 2026-10-03)

O Zabbix não envia esse JSON pronto: **o script da media type monta o
payload**. A configuração abaixo foi criada via JSON-RPC e validada de
ponta-a-ponta (PROBLEM → alert → incidente; OK → alert `Resolved`).

> **Gotchas do Zabbix 8.0** (aprendidos na implementação):
> - Não existe condição `value = PROBLEM/OK` na action: **uma action só**,
>   com `operations` (PROBLEM) + `recovery_operations` (OK).
> - `conditiontype` de trigger action: 0=host group, 1=host, 2=trigger,
>   3=event name, 4=severity, 6=time period, 13=template, 16=suppressed,
>   25/26=tags — sem "event value".
> - `opmessage.default_msg = 1` **falha** com `No message defined for media
>   type`: em `default_msg = 0` é obrigatório `subject` + `message`.
> - `user.medias.get` não existe: usar `user.get` + `selectMedias: "extend"`.
> - `user.medias.severity` é **bitmask**: `0` = nenhuma severidade (nunca
>   envia); usar `63` (todas). `active = 0` significa habilitado.
> - `action.create` não aceita `value`; `esc_period` aceita apenas
>   `60..604800` (segundos).
> - Auth: header `Authorization: Bearer <token>` — campo `auth` no corpo é
>   rejeitado (`unexpected parameter "auth"`).

### 4.1 Media types (2 — uma por status)

| Campo | Valor |
|---|---|
| `type` | `4` (webhook) |
| Nomes | `InfraControl — Problem` (id **105**) · `InfraControl — Recovery` (id **106**) |
| `status` | `0` (habilitada) |

Parâmetros das duas (`{name, value}`), diferindo só em `status`:

| name | value | Observação |
|------|-------|------------|
| `url` | `http://api:8080/api/v1/integrations/zabbix/problems` | rede interna do compose |
| `token` | `${ZABBIX_TOKEN}` (dev: `infracontrol-zabbix-dev-2026`) | header `X-Zabbix-Token` |
| `eventId` | `{EVENT.ID}` | **obrigatório** — no OK o macro resolve para o problem original |
| `recoveryEventId` | `{EVENT.RECOVERY.ID}` | só tem valor no recovery; vazio é ignorado |
| `host` | `{HOST.NAME1}` | |
| `ip` | `{HOST.IP}` | |
| `name` | `{TRIGGER.NAME}` | |
| `nseverity` | `{EVENT.NSEVERITY}` | numérico **0..5** (`{EVENT.SEVERITY}` é texto) |
| `status` | `PROBLEM` (105) / `OK` (106) | fixo por media type |

Script (JS do runtime do Zabbix) — monta o JSON do contrato v2 e faz POST:

```javascript
const p = JSON.parse(value);
if (!p.url) { throw 'InfraControl: URL não definida'; }
const request = new HttpRequest();
request.addHeader('Content-Type: application/json');
request.addHeader('X-Zabbix-Token: ' + p.token);
const body = { eventId: p.eventId, host: p.host, ip: p.ip, name: p.name,
               severity: parseInt(p.nseverity, 10), status: p.status };
if (p.recoveryEventId && p.recoveryEventId !== '') { body.recoveryEventId = p.recoveryEventId; }
const response = request.post(p.url, JSON.stringify(body));
Zabbix.log(4, 'InfraControl webhook [' + p.status + '] response: ' + response);
return 'OK';
```

> `occurredAt` ficou de fora de propósito: `{EVENT.TIMESTAMP}` é timestamp
> textual; sem ele o handler usa o horário atual.

### 4.2 Action única (id **7** — "InfraControl — triggers")

- `eventsource: 0` (trigger), `esc_period: 60`, `filter.conditions: []` (todos).
- `operations` → `operationtype: 0` (send message), grupo `usrgrpid 7`
  (Zabbix administrators), `opmessage.mediatypeid: 105`,
  `default_msg: 0` + `subject`/`message` explícitos com `{TRIGGER.NAME}`,
  `{HOST.NAME}`, `{EVENT.SEVERITY}`, `{EVENT.ID}`.
- `recovery_operations` → idem com `mediatypeid: 106` + `{EVENT.RECOVERY.ID}`.
- Usuário `Admin` (userid 1) com **mídias 105 e 106** habilitadas
  (`active: 0`, `severity: 63`, `period: 1-7,00:00-24:00`).

### 4.3 Gatilho de teste

- Host `Zabbix server` (hostid 10084) · item trapper `test.value`
  (itemid 72011, `type: 2`) · trigger `last(/Zabbix server/test.value)=1`
  (triggerid 33178, severidade 4/High).

```bash
# PROBLEM (1) e OK (0) — disparado de dentro do container do server:
docker exec infracontrol-zabbix-server zabbix_sender -z 127.0.0.1 \
  -s "Zabbix server" -k test.value -o 1   # depois -o 0 para resolver
```

Smoke test manual por curl (equivalente ao que o script envia):

```bash
curl -X POST http://localhost:8086/api/v1/integrations/zabbix/problems \
  -H "Content-Type: application/json" \
  -H "X-Zabbix-Token: <token>" \
  -d '{"eventId":"1","host":"srv-teste","ip":"192.0.2.10","name":"Teste webhook","severity":3,"status":"PROBLEM"}'
```

Evidência do ciclo real (2026-10-03): eventos Zabbix `8` (Problem) e `9`
(Recovered, `external_problem_id=8`) → alerta externo `severity=Critical`
→ incidente próprio `Resolved`.

## 5. Limitações conhecidas

- A action **legada sem `recoveryEventId`** ainda funciona (transição
  in-place), mas sem a linha própria de recovery não há par
  problem↔recovery auditável — adicione os dois macros quando puder (§4).
- Incidentes de Zabbix criados **antes do ADR-027** não têm
  `MonitoringEvent`/`Alert` vinculados: o OK correspondente responde `none` e
  o incidente legado fecha manualmente (nota do ADR-027).
- Dois hosts **fora do inventário** continuam podendo dividir o device
  (device é opcional), mas a chave de dedup é o `eventId`, não o título —
  sem colisão de nome. Se importar, cadastre os devices.
- Direção B (sair consumindo a API JSON-RPC do Zabbix) ficou como alternativa
  descartada no ADR-023 — possível num futuro se precisarmos de inventário/acks.

- F16-T002 (notificação): unit `IncidentTests` (+3, evento levantado em
  `Incident.Create`) e `IncidentCreatedNotificationTests` (3 — roles
  Operator+Admin, deliveries para usuários dessas roles, idempotência);
  integration `IncidentIntegrationTests.ManualCreate_ShouldWrite
  IncidentCreatedEventToOutbox`.

## 7. Infraestrutura Docker Compose (Zabbix 8.0 — F18-T001)

A infraestrutura do Zabbix 8.0 é executada via Docker Compose sob o perfil dedicado `zabbix` (`profiles: ["zabbix"]`).

### Imagens Oficiais & Serviços

| Serviço | Container | Imagem Oficial | Porta Host / Container | Volume / Persistência |
|:---|:---|:---|:---|:---|
| **`zabbix-db`** | `infracontrol-zabbix-db` | `postgres:17-alpine` | `5434:5432` (`ZABBIX_DB_PORT`) | `zabbix_db_data` |
| **`zabbix-server`** | `infracontrol-zabbix-server` | `zabbix/zabbix-server-pgsql:alpine-trunk` | `10052:10051` (`ZABBIX_SERVER_PORT`) | `zabbix_server_snmptraps`, `zabbix_server_mibs` |
| **`zabbix-web`** | `infracontrol-zabbix-web` | `zabbix/zabbix-web-nginx-pgsql:alpine-trunk` | `8086:8080` (`ZABBIX_WEB_PORT`) | *(Web em memória + conexão DB)* |
| **`zabbix-agent`** | `infracontrol-zabbix-agent` | `zabbix/zabbix-agent2:alpine-trunk` | `10053:10050` (`ZABBIX_AGENT_PORT`) | *(Métricas locais de teste)* |

> **Nota F18-T001 (decisão 2026-10-03):** imagens `alpine-trunk` mantidas
> **conforme orientação do usuário** ("deixa o zabbix conforme" — a API
> responde `8.0.0`); o upgrade para tags 8.0 oficiais fica para quando o
> registro sair de *devel* (pendência registrada no TASKS F18-T001). Portas
> vindas do `.env` — **8086** para o web porque `8085` é do `HTTP_PORT` do
> frontend; token de integração em `ZABBIX_TOKEN` (`.env`).

### Credenciais e Acesso de Desenvolvimento

- **Zabbix Web UI:** `http://localhost:8086` (Usuário: `Admin`, Senha padrão: `zabbix`).
- **PostgreSQL Dedicado:** Host `zabbix-db` (porta interna `5432`, publicada no host como `5434`), BD `zabbix`, Usuário `zabbix`, Senha `${ZABBIX_DB_PASSWORD:-zabbix_dev_password_2026}` (`ZABBIX_DB_PORT`).

### Comandos de Operação

```bash
# Subir apenas o ambiente infra + Zabbix
docker compose --profile zabbix up -d

# Subir a aplicação InfraControl completa + Zabbix
docker compose --profile app --profile zabbix up -d

# Verificar logs dos serviços Zabbix
docker compose logs -f zabbix-server zabbix-web zabbix-db zabbix-agent

# Parar os serviços Zabbix sem apagar dados
docker compose --profile zabbix stop

# Resetar completamente os dados do Zabbix (destrói volumes)
docker compose --profile zabbix down -v

# Atualizar imagens oficiais Zabbix 8.0
docker compose pull zabbix-server zabbix-web zabbix-agent zabbix-db
```

## 8. Frota de switchs no Zabbix (operação 2026-10-16)

Cadastro via API JSON-RPC (`Admin`/`zabbix`, Bearer token) dos 22 switchs do
ambiente. Divisão de responsabilidades mantida: **InfraControl** é System of
Record (device, local, marca, ficha) e faz checks SNMP sob demanda (F29/F30);
**Zabbix** faz a coleta contínua/histórico/triggers → webhook F16 →
`MonitoringEvent` → `Alert` → `Incident`. Nada duplicado dos dois lados.

### Host groups e templates

| Group | Hosts | Template | Interface |
|---|---|---|---|
| **Switches HP** (22) | 10 × HP A5120/V1910 (`.1`, `.231`–`.234`, `.240`, `.242`, `.250`–`.252`) | *HP Comware HH3C by SNMP* (`10227`) | SNMP `161`, v2c, community `public` |
| **Switches Intelbras** (23) | 5 × SG 2404 PoE / MR / PoE L2+ (`.220`, `.235`, `.243`, `.244`, `.245`) | *Network Generic Device by SNMP* (`10226`) | SNMP `161`, v2c, community `public` |
| **Switches de Mesa** (24) | 4 × TP-Link TL-SG108E (`.110`, `.120`, `.140`, `192.168.50.101`) | *ICMP Ping* (`10564`) | Agent `10050` (dummy — simple checks) |

Host técnico = `sw-<ip-com-hífens>`; nome visível = `<sysName> (<ip>)>`;
tags `vendor`/`location`; descrição com modelo e modo de monitoramento.

### Gotchas da API do Zabbix 8.0 (host.create)

- Params SNMP da interface vão **aninhados em `details`**
  (`details.version=2`, `details.community="public"`, `details.bulk=1`) —
  campos planos (`version`, `community` direto no objeto da interface)
  dão `-32602 Incorrect arguments passed to function`.
- `hostgroup.create` devolve `{groupids:[...]}` (sem `name`) — mapear a ordem
  das criações.
- Template SNMP exige interface tipo 2 senão falha com
  `Cannot inherit item "snmptrap.fallback" ... host interface of type "SNMP" is required`.

### Verificação (executada)

- 19 hosts criados (20 total com o `Zabbix server` pré-existente).
- Itens **verdes** (`state=0`, sem erro, fila vazia) nos representantes
  HP (`.231` — sysDescr/sysName/sysLocation/serial/firmware + ifTable),
  Intelbras (`.235`, `.244`, `.245` — inclui counter de tráfego) e
  TP-Link (`.110` — `icmpping=1`, latência ~9 ms).

### Pendências da frota

- `.241`, `.130`, `.150` **offline**: quando ligarem → identificar marca
  (web/SNMP), ativar SNMP se suportado e cadastrar nos groups acima.
- TL-SG108E **não tem SNMP no firmware** (auditado via `menuList.js`) —
  teto é ICMP ping; ver ficha `docs/42` §"Frota Real".
- Procedimentos de ativação de SNMP por modelo (web EXCU_SHELL / telnet
  CLI `snmp-server community public read-only`) documentados em
  `docs/42` §"Limitações e achados operacionais".

