# 27 — Integração com Zabbix (webhook de entrada)

- **Task**: F16-T001 (Fase 16 — Integrações) · **ADR**: [ADR-023](adr/ADR-023-ZABBIX-INBOUND-WEBHOOK.md)
- **Fluxo**: Zabbix **avisa** (problem/OK) → InfraControl **orquestra** (incidente
  no ciclo F6/F7: leitura, tarefa, conhecimento). Notificação fanout fica na
  F16-T002 (hoje incidente nenhum notifica — só eventos de identidade).

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
  "host": "srv-web-01",
  "ip": "192.0.2.71",
  "name": "srv-web-01: Carga de CPU alta",
  "severity": 4,
  "status": "PROBLEM"
}
```

| Campo | Obrigatório | Regra |
|-------|-------------|-------|
| `eventId` | não | `{EVENT.ID}` — só para log/descrição |
| `host` | não | `{HOST.NAME1}` — fallback de match do device |
| `ip` | não | `{HOST.IP}` — match primário do device |
| `name` | **sim** | `{TRIGGER.NAME}` — vazio → 400 pt-BR |
| `severity` | **sim** | inteiro 0..5 (id de severidade do Zabbix) |
| `status` | **sim** | `PROBLEM` ou `OK` (case-insensitive) |

### Resposta

Sempre no envelope `{isSuccess, data, errors}`:

| Situação | HTTP | `data` / `errors` |
|----------|------|-------------------|
| Sucesso | 200 | `data: { incidentId, outcome }` |
| Token de configuração vazio (desligado) | 404 | `errors: ["A integração com Zabbix está desativada."]` |
| Header ausente/divergente | 401 | `errors: ["Token do Zabbix inválido."]` |
| Validação do payload | 400 | `errors: ["O nome do problema é obrigatório. …"]` etc. (pt-BR, `GlobalExceptionHandler`) |

`outcome` (token de contrato, não traduzir):

- **`created`** — PROBLEM gerou incidente novo;
- **`duplicate`** — já havia incidente não-resolvido com mesmo título+device
  (idempotente, não cria outro);
- **`resolved`** — OK encontrou o incidente aberto e o resolveu;
- **`none`** — OK sem incidente aberto correspondente (nada a fazer).

## 3. Comportamento

1. **Match do device**: tenta por `ip` (`GetByIpAsync`), senão por hostname
   (`GetByNameAsync`). Sem match → incidente é criado **sem device**
   (`deviceId` nulo) com `host`/`ip`/`eventId` na descrição.
2. **PROBLEM** → `Incident.Create(título, severidade, device?, descrição)` e
   dedup anti-storm por **título + device** (null incluso): se já existe
   incidente não-`Resolved` com a mesma chave, responde `duplicate`.
   Mesmo título em devices diferentes não colide — o device faz parte da chave.
3. **OK** → resolve (→ `Resolved`, `ResolvedAt` estampado) o incidente da
   mesma chave; nenhum aberto → `none`.
4. **Severidade Zabbix → `IncidentSeverity`** (6 → 4 níveis da UI):

   | Zabbix | InfraControl |
   |--------|--------------|
   | 0 Not classified | Low |
   | 1 Information | Low |
   | 2 Warning | Medium |
   | 3 Average | High |
   | 4 High | Critical |
   | 5 Disaster | Critical |

5. Sem migration nova: só cria/resolve linhas em `incidents`.
6. Rate limit padrão (120 req/min) vale para a rota.

## 4. Runbook — action no Zabbix (6.x/7.x)

O Zabbix não envia esse JSON pronto: **o action monta o payload**. Passos:

1. **Configurações → Canais de notificação (Media types) → Criar**
   - Tipo: `Webhook` · Nome: `InfraControl`
   - **URL**: `https://<seu-host>/api/v1/integrations/zabbix/problems`
   - **Headers** (HTTP headers): `X-Zabbix-Token: <mesmo token do appsettings>`
   - **Parâmetros** (viram o corpo JSON):

     | Parâmetro | Valor |
     |-----------|-------|
     | `eventId` | `{EVENT.ID}` |
     | `host` | `{HOST.NAME1}` |
     | `ip` | `{HOST.IP}` |
     | `name` | `{TRIGGER.NAME}` |
     | `severity` | `{EVENT.SEVERITY}` |
     | `status` | `PROBLEM` *(fixo nesta action)* |

2. **Ações → Criar action de problema** (ex.: "InfraControl — problem"):
   - Condição: valor do gatilho = **PROBLEM** (e o escopo que quiser: grupos,
     severidade mínima…)
   - Operação → **Enviar para**: usuário/grupecanal `InfraControl`.
3. **Ação de recuperação** (action separada): mesma mídia, mas com
   `status` = `OK` — `{EVENT.VALUE}` dá 0/1, não `OK`, por isso o valor fixo
   em cada action.
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

- Dedup é **heurística** (título+device), a mesma filosofia anti-storm do F6:
  dois hosts **fora do inventário** com o mesmo nome de problema dividem a
  chave e o segundo vira `duplicate`. Se importar, cadastre os devices.
- Sem notificação ainda — crie a F16-T002 (`IncidentCreatedDomainEvent` +
  outbox + consumer, destinatários Operator e Admin) para o fanout.
- Sem correlation de eventos (reenvio do mesmo `eventId` não é detectado por
  id — a chave é título+device).
- Direção B (sair consumindo a API JSON-RPC do Zabbix) ficou como alternativa
  descartada no ADR-023 — possível num futuro se precisarmos de inventário/acks.

## 6. Evidências (F16-T001)

- Unit: `tests/InfraControl.UnitTests/Integrations/ZabbixIngestTests.cs` (19)
  + `ZabbixIntegrationControllerTests.cs` (5) — criação, sem device, fallback
  por host, dedup, same-title/devices diferentes, OK resolve/nada, reabertura,
  tabela de severidade, 400 pt-BR, 404 desligado, 401 token, 200 envelope,
  case-sensitivity do token.
- Integration: `tests/InfraControl.IntegrationTests/Integrations/
  ZabbixIngestIntegrationTests.cs` (3 — ponta-a-ponta pelo MediatR; exige
  Docker para executar).
- Arch: allowlist de endpoints públicos com `ZabbixIntegrationController.
  Problems`.
