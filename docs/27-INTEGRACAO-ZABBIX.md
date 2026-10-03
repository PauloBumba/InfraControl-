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

## 4. Runbook — action no Zabbix (6.x/7.x)

O Zabbix não envia esse JSON pronto: **o action monta o payload**. Passos:

1. **Configurações → Canais de notificação (Media types) → Criar**
   - Tipo: `Webhook` · Nome: `InfraControl`
   - **URL**: `https://<seu-host>/api/v1/integrations/zabbix/problems`
   - **Headers** (HTTP headers): `X-Zabbix-Token: <mesmo token do appsettings>`
   - **Parâmetros** (viram o corpo JSON):

     | Parâmetro | Valor | Observação |
     |-----------|-------|------------|
     | `eventId` | `{EVENT.ID}` | **obrigatório** — no OK o macro já resolve para o problem original |
     | `recoveryEventId` | `{EVENT.RECOVERY.ID}` | opcional — preenche só na action de OK (macro de recovery operation); no PROBLEM vem vazio e é ignorado |
     | `host` | `{HOST.NAME1}` | |
     | `ip` | `{HOST.IP}` | |
     | `name` | `{TRIGGER.NAME}` | |
     | `severity` | `{EVENT.SEVERITY}` | |
     | `occurredAt` | `{EVENT.TIMESTAMP}` | opcional (unix seconds) |
     | `status` | `PROBLEM` *(fixo nesta action)* | |

2. **Ações → Criar action de problema** (ex.: "InfraControl — problem"):
   - Condição: valor do gatilho = **PROBLEM** (e o escopo que quiser: grupos,
     severidade mínima…)
   - Operação → **Enviar para**: usuário/grupecanal `InfraControl`.
3. **Ação de recuperação** (action separada): mesma mídia, mas com
   `status` = `OK` — `{EVENT.VALUE}` dá 0/1, não `OK`, por isso o valor fixo
   em cada action. Nesta action o `eventId` = `{EVENT.ID}` entrega o problem
   original e o `recoveryEventId` = `{EVENT.RECOVERY.ID}` o id do evento de
   recovery (os dois juntos permitem a linha própria + backfill do §3.3).
4. **Testar**: botão "Testar" da media type; em produção basta disparar um
   trigger de teste e conferir `GET /api/v1/incidents` (incidente `Open` →
   depois `Resolved` quando o problema some).

Smoke test manual por curl:

```bash
curl -X POST https://<seu-host>/api/v1/integrations/zabbix/problems \
  -H "Content-Type: application/json" \
  -H "X-Zabbix-Token: <token>" \
  -d '{"eventId":"1","host":"srv-teste","ip":"192.0.2.10","name":"Teste webhook","severity":3,"status":"PROBLEM"}'
```

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
| **`zabbix-db`** | `infracontrol-zabbix-db` | `postgres:17-alpine` | `5433:5432` | `zabbix_db_data` |
| **`zabbix-server`** | `infracontrol-zabbix-server` | `zabbix/zabbix-server-pgsql:alpine-8.0-latest` | `10051:10051` | `zabbix_server_snmptraps`, `zabbix_server_mibs` |
| **`zabbix-web`** | `infracontrol-zabbix-web` | `zabbix/zabbix-web-nginx-pgsql:alpine-8.0-latest` | `8085:8080` | *(Web em memória + conexão DB)* |
| **`zabbix-agent`** | `infracontrol-zabbix-agent` | `zabbix/zabbix-agent2:alpine-8.0-latest` | `10050:10050` | *(Métricas locais de teste)* |

### Credenciais e Acesso de Desenvolvimento

- **Zabbix Web UI:** `http://localhost:8085` (Usuário: `Admin`, Senha padrão: `zabbix`).
- **PostgreSQL Dedicado:** Host `zabbix-db` (porta interna `5432`, publicada no host como `5433`), BD `zabbix`, Usuário `zabbix`, Senha `${ZABBIX_DB_PASSWORD:-zabbix_dev_password_2026}`.

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

