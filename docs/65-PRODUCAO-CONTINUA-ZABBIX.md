# 65. Produção Contínua para Zabbix — Estudo e Plano

> **Objetivo:** estabelecer pipeline de produção contínua (Continuous Delivery/Deployment) para o Zabbix,
> garantindo upgrades seguros, versionamento de configuração, automação de validação e redução de risco
> operacional.
> **Base:** auditoria técnica de 2026-11-01 (docs/64) · ADR-023 (webhook) · docs/27 (integração)
> **Princípio:** mudanças em produção só via pipeline testado; intervenção manual é exceção documentada.

---

## 1. Estado Atual (baseline da auditoria docs/64)

|| Componente | Estado | Impedimento para CD |
| --- | --- | --- | --- |
| Versão | 8.0.0rc2 (alpine-trunk) | ❌ **instável** — trunk muda a cada build; upgrade necessário antes de CD |
| Deploy | Docker Compose manual | ❌ sem pipeline; alterações são manuais (`docker compose up`) |
| Configuração | via UI + API manual | ❌ sem versionamento; mudanças não rastreáveis |
| Backup | nenhum realizado | ❌ sem rollback possível |
| Segredos | `.env` local + comunidade `public` | ❌ sem secret manager; SNMP exposto |
| HA | single-node (sem proxy) | ❌ single point of failure |
| Monitoramento | Zabbix monitorando a si mesmo | ⚠️ parcial (pinger 100%, cache 73%) |
| Testes | nenhum automatizado | ❌ sem validação antes de deploy |

### Problemas críticos que bloqueiam CD (prioridade P0)

1. **Versão instável (trunk)** — pipeline não pode depender de builds que quebram API entre deploys
2. **Sem backup** — rollback impossível; CD exige rollback rápido
3. **Sem versionamento de configuração** — drift entre ambientes não detectável
4. **Comunidade SNMP `public`** — segurança inadequada para produção
5. **ICMP pinger 100%** — gargalo que pode piorar com scale horizontal

---

## 2. Estratégia de Produção Contínua

### 2.1. Arquitetura de Ambientes

```
┌─────────────────────────────────────────────────────────────────┐
│                    Pipeline de Produção Contínua                 │
└─────────────────────────────────────────────────────────────────┘

        dev                   staging                   production
        ↓                      ↓                         ↓
  docker-compose      docker-compose-overlay    docker-compose-overlay
  (trunk local)       (7.4/8.0 estável)         (7.4/8.0 estável)
        ↓                      ↓                         ↓
  testes manuais     testes automatizados    smoke test + monitoramento
        ↓                      ↓                         ↓
  ──→ validação  ←───    validação  ←────────    rollback automático
```

### 2.2. Pilares

| Pilar | Implementação | Ferramenta |
| --- | --- | --- |
| **Versionamento de config** | exportação via API → Git | Zabbix API + GitHub Actions |
| **CI/CD** | pipeline de build/test/deploy | GitHub Actions |
| **Backup/Recovery** | dump PostgreSQL + export config | scripts dedicados |
| **Secretos** | vault environment + secrets manager | .env + HashiCorp Vault / AWS Secrets |
| **Monitoramento do Zabbix** | dashboard dedicado + alertas | Zabbix self-monitoring |
| **Rollback** | versão anterior do compose + restore DB | docker compose + pg_restore |
| **Validação** | smoke test automatizado | scripts + pytest/k6 |
| **HA** | proxy + HA node (fase 2) | Zabbix Proxy |

---

## 3. Fase 1 — Estabilização (P0, bloqueia CD)

### 3.1. Upgrade para versão estável

**Problema:** `alpine-trunk` (8.0.0rc2) muda a cada build, quebrando contratos de API entre deploys.

**Decisão:** migrar para **Zabbix 7.4.15** (LTS estável) ou aguardar tag 8.0.0 oficial sair de *devel*.

**Plano de execução:**

1. Backup do banco atual (pg_dump antes de qualquer mudança)
2. Testar upgrade em staging (docker-compose com volume dedicado)
3. Validar:
   - API JSON-RPC (autenticação Bearer, endpoints)
   - Webhook action 7 (media types 105/106)
   - Templates de impressoras/switches
   - Dashboards
4. Se sucesso → deploy em produção com rollback planejado
5. Documentar gotchas da API 7.4.15 (já levantados em docs/27 §4)

**Script de backup (automatizar no pipeline):**

```bash
#!/bin/bash
# backup-zabbix.sh
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups/zabbix"
mkdir -p "$BACKUP_DIR"

# 1. Export configuration via API
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "configuration.export",
    "params": {
      "format": "json",
      "options": {
        "hosts": true,
        "items": true,
        "triggers": true,
        "graphs": true,
        "templates": true
      }
    },
    "auth": "$ZABBIX_API_TOKEN",
    "id": 1
  }' > "$BACKUP_DIR/config_$TIMESTAMP.json"

# 2. Dump PostgreSQL
docker exec infracontrol-zabbix-db pg_dump -U zabbix zabbix \
  > "$BACKUP_DIR/db_$TIMESTAMP.sql"

# 3. Retenção (últimos 7 dias)
find "$BACKUP_DIR" -name "*.sql" -mtime +7 -delete
find "$BACKUP_DIR" -name "*.json" -mtime +7 -delete
```

### 3.2. Versionamento de Configuração

**Problema:** mudanças via UI não são rastreáveis; drift entre ambientes.

**Solução:** GitOps — configuração declarativa exportada para Git.

**Estrutura do repositório de config:**

```
zabbix-config/
├── zabbix/
│   ├── hosts/              # host definitions (JSON via API)
│   ├── templates/          # custom templates (XML)
│   ├── actions/            # actions (JSON)
│   ├── media_types/        # webhooks (JSON)
│   └── dashboards/         # dashboards (JSON)
├── scripts/
│   ├── export-config.sh    # dump completo via API
│   ├── import-config.sh    # apply config via API
│   └── validate-config.sh  # smoke test de estrutura
└── tests/
    └── config_validation.py # validação de integridade
```

**Workflow:**

1. Desenvolvedor altera config via UI (dev)
2. `export-config.sh` → commit no branch `zabbix-config`
3. PR → revisão + validação automática
4. Merge → `import-config.sh` aplica em staging
5. Smoke test → aprovação → deploy produção

**Script de exportação (automatizado nightly):**

```bash
#!/bin/bash
# export-config.sh
AUTH_TOKEN=$(./get-auth-token.sh)
CONFIG_DIR="zabbix-config/zabbix"

# Export hosts
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"method\":\"host.get\",\"params\":{\"output\":\"extend\"},\"auth\":\"$AUTH_TOKEN\",\"id\":1}" \
  | jq '.result' > "$CONFIG_DIR/hosts/hosts.json"

# Export templates
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"method\":\"template.get\",\"params\":{\"output\":\"extend\"},\"auth\":\"$AUTH_TOKEN\",\"id\":1}" \
  | jq '.result' > "$CONFIG_DIR/templates/templates.json"

# Export actions (inclui action 7 - InfraControl)
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"method\":\"action.get\",\"params\":{\"output\":\"extend\"},\"auth\":\"$AUTH_TOKEN\",\"id\":1}" \
  | jq '.result' > "$CONFIG_DIR/actions/actions.json"

# Export media types (105/106)
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"method\":\"mediatype.get\",\"params\":{\"output\":\"extend\"},\"auth\":\"$AUTH_TOKEN\",\"id\":1}" \
  | jq '.result' > "$CONFIG_DIR/media_types/media_types.json"
```

### 3.3. Correção de Gargalos (pré-requisito para scale)

**ICMP pinger 100%** — bloqueia scale horizontal de hosts.

**Diagnóstico:** `StartPingers` padrão (1) insuficiente para 152 hosts.

**Correção (em `zabbix_server.conf`):**

```ini
# Aumentar pingers (baseado em: 1 pinger ~ 100 hosts)
StartPingers=5
StartPingersUnreachable=5
```

**Validação após mudança:**

1. Verificar `zabbix[queue,10m]` → deve ser 0
2. Verificar `zabbix[process,pinger]` → utilização < 75%
3. Smoke test de ping em 20 hosts aleatórios

**Config cache 73,9%** — monitorar antes de aumentar `CacheSize`.

```ini
# Se passar de 85%, aumentar:
CacheSize=64M  # padrão 8M
```

### 3.4. Segurança de Segredos

**Problema atual:**
- `.env` com senhas em texto
- Comunidade SNMP `public` em 131 interfaces
- Token Zabbix hardcoded

**Solução:**

1. **Variáveis de ambiente** (já via `.env` no compose):
   ```env
   ZABBIX_DB_PASSWORD=<vault-secret>
   ZABBIX_TOKEN=<vault-secret>
   SNMP_COMMUNITY=<vault-secret>  # substituir `public`
   ```

2. **Secret Manager (fase 2):**
   - HashiCorp Vault / AWS Secrets Manager
   - Pull no runtime pelo container

3. **SNMP read-only dedicada:**
   - Criar comunidade única por segmento de rede
   - Exemplo: `zabbix_read_hospital_2026` (rotacionável)

**Script de rotação (cron job mensal):**

```bash
#!/bin/bash
# rotate-snmp-community.sh
NEW_COMMUNITY="zabbix_read_$(date +%Y%m)"
OLD_COMMUNITY="zabbix_read_$(date -d '1 month ago' +%Y%m)"

# Atualizar em todos os hosts SNMP via API
for host in $(./list-snmp-hosts.sh); do
  curl -X POST http://localhost:8086/api_jsonrpc.php \
    -H "Content-Type: application/json" \
    -d "{
      \"jsonrpc\": \"2.0\",
      \"method\": \"host.update\",
      \"params\": {
        \"hostid\": \"$host\",
        \"interfaces\": [{
          \"type\": 2,
          \"details\": {
            \"community\": \"$NEW_COMMUNITY\"
          }
        }]
      },
      \"auth\": \"$ZABBIX_API_TOKEN\",
      \"id\": 1
    }"
done
```

---

## 4. Fase 2 — Pipeline CI/CD

### 4.1. Workflow GitHub Actions

```yaml
# .github/workflows/zabbix-cd.yml
name: Zabbix Continuous Delivery

on:
  push:
    branches: [main]
    paths: ['zabbix-config/**']
  pull_request:
    branches: [main]
    paths: ['zabbix-config/**']

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Validate Zabbix config
        run: |
          python tests/config_validation.py
          bash scripts/validate-config.sh

  deploy-staging:
    needs: validate
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: actions/checkout@v4
      - name: Deploy to staging
        run: |
          docker compose -f docker-compose.staging.yml up -d
          bash scripts/import-config.sh staging
      - name: Smoke test
        run: bash scripts/smoke-test.sh staging

  deploy-production:
    needs: deploy-staging
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/checkout@v4
      - name: Backup production
        run: bash scripts/backup-zabbix.sh production
      - name: Deploy to production
        run: |
          docker compose -f docker-compose.prod.yml up -d
          bash scripts/import-config.sh production
      - name: Smoke test
        run: bash scripts/smoke-test.sh production
      - name: Rollback on failure
        if: failure()
        run: bash scripts/rollback-zabbix.sh production
```

### 4.2. Smoke Test Automatizado

```python
# tests/smoke_test.py
import requests
import sys

ZABBIX_URL = "http://localhost:8086"
API_TOKEN = os.environ["ZABBIX_API_TOKEN"]

def test_api_auth():
    """Test authentication via Bearer token"""
    r = requests.post(
        f"{ZABBIX_URL}/api_jsonrpc.php",
        json={
            "jsonrpc": "2.0",
            "method": "user.login",
            "params": {
                "username": "Admin",
                "password": os.environ["ZABBIX_ADMIN_PASSWORD"]
            },
            "id": 1
        }
    )
    assert r.status_code == 200
    assert "result" in r.json()

def test_webhook_action():
    """Test action 7 (InfraControl) exists and is enabled"""
    r = requests.post(
        f"{ZABBIX_URL}/api_jsonrpc.php",
        headers={"Authorization": f"Bearer {API_TOKEN}"},
        json={
            "jsonrpc": "2.0",
            "method": "action.get",
            "params": {"filter": {"name": "InfraControl — triggers"}},
            "auth": API_TOKEN,
            "id": 1
        }
    )
    assert r.status_code == 200
    actions = r.json()["result"]
    assert len(actions) == 1
    assert actions[0]["status"] == "0"  # enabled

def test_media_types():
    """Test media types 105/106 exist"""
    r = requests.post(
        f"{ZABBIX_URL}/api_jsonrpc.php",
        headers={"Authorization": f"Bearer {API_TOKEN}"},
        json={
            "jsonrpc": "2.0",
            "method": "mediatype.get",
            "params": {"filter": {"name": ["InfraControl — Problem", "InfraControl — Recovery"]}},
            "auth": API_TOKEN,
            "id": 1
        }
    )
    assert r.status_code == 200
    media_types = r.json()["result"]
    assert len(media_types) == 2

def test_queue_health():
    """Test queue is not backing up"""
    r = requests.post(
        f"{ZABBIX_URL}/api_jsonrpc.php",
        headers={"Authorization": f"Bearer {API_TOKEN}"},
        json={
            "jsonrpc": "2.0",
            "method": "item.get",
            "params": {"filter": {"key_": "zabbix[queue,10m]"}},
            "auth": API_TOKEN,
            "id": 1
        }
    )
    assert r.status_code == 200
    items = r.json()["result"]
    queue_value = int(items[0]["lastvalue"])
    assert queue_value < 10, f"Queue too high: {queue_value}"

if __name__ == "__main__":
    test_api_auth()
    test_webhook_action()
    test_media_types()
    test_queue_health()
    print("✓ All smoke tests passed")
```

### 4.3. Rollback Automatizado

```bash
#!/bin/bash
# rollback-zabbix.sh
ENV=$1
BACKUP_DIR="/backups/zabbix"
LATEST_BACKUP=$(ls -t "$BACKUP_DIR/db_*.sql" | head -1)

echo "Rolling back $ENV to $LATEST_BACKUP"

# 1. Stop Zabbix server
docker compose -f docker-compose.$ENV.yml stop zabbix-server

# 2. Restore database
docker exec -i infracontrol-zabbix-db psql -U zabbix zabbix < "$LATEST_BACKUP"

# 3. Restore configuration
LATEST_CONFIG=$(ls -t "$BACKUP_DIR/config_*.json" | head -1)
./scripts/import-config.sh "$ENV" "$LATEST_CONFIG"

# 4. Restart Zabbix
docker compose -f docker-compose.$ENV.yml start zabbix-server

echo "Rollback complete"
```

---

## 5. Fase 3 — Alta Disponibilidade (opcional, fase 2)

### 5.1. Arquitetura com Proxy

```
┌─────────────────────────────────────────────────────────────┐
│                     Produção com HA                         │
└─────────────────────────────────────────────────────────────┘

   Zabbix Server HA          Zabbix Proxy 1          Zabbix Proxy 2
   (active/passive)          (HSDS)                  (HSSM)
         │                        │                       │
         └────────────────────────┴───────────────────────┘
                                    │
                            ┌───────┴───────┐
                            │ PostgreSQL HA │
                            │ (Patroni)     │
                            └───────────────┘
```

**Implementação:**

1. **Zabbix Server HA** (active/passive via `HANODENAME`)
2. **Zabbix Proxies** (por localidade — HSDS/HSSM)
3. **PostgreSQL HA** (Patroni + etcd)
4. **Load balancer** (nginx para web UI)

**Benefícios:**
- Elimina single point of failure
- Reduz latência de coleta (proxies locais)
- Isola falhas de rede

---

## 6. Fase 4 — Monitoramento do Zabbix

### 6.1. Dashboard Dedicado

**Widgets:**

- Health do Zabbix server (processes, cache, queue)
- Itens unsupported por grupo
- Problemas ativos por severidade
- ICMP pinger utilization
- Database size (trends)
- Webhook action 7 status (últimas entregas)
- Triggers desabilitadas com problemas ativos

### 6.2. Alertas Internos

| Alerta | Severidade | Ação |
| --- | --- | --- |
| Queue > 100 | Alta | Escalar pingers |
| Config cache > 85% | Média | Aumentar CacheSize |
| Webhook falha > 3 | Alta | Verificar API InfraControl |
| ICMP pinger > 75% | Alta | Escalar pingers |
| Database > 10 GB | Média | Revisar retenção |

---

## 7. Cronograma de Implementação

### Sprint 1 (2 semanas) — Estabilização P0

- [x] Backup completo (DB + config) <!-- EXECUTADO 2026-11-04: db_20261104_172216.sql.gz (0,75 GB) + config_<ts>/; execucao diaria 02:00 via Task Scheduler (wrapper scripts/zabbix/run-backup.cmd); evidencias em docs/68 §1 -->
- [ ] Upgrade para 7.4.15 ou 8.0.0 estável <!-- ADIADO por decisao do responsavel (2026-11-04): manter alpine-trunk ate a tag 8.0 sair de devel; fica para Sprint 2+ -->
- [x] Corrigir ICMP pinger (StartPingers=5) <!-- docker-compose.yml: ZBX_STARTPINGERS/ZBX_STARTPINGERSUNREACHABLE=5; smoke test valida pinger busy < 75% -->
- [x] Exportar configuração inicial para Git <!-- zabbix-config/ (hosts/templates/media-types + actions.json) com varredura de segredos — zabbix-config/README.md -->
- [x] Script de backup automatizado (cron daily) <!-- Task Scheduler "InfraControl Zabbix Backup" diario 02:00 (Windows; equivalente ao cron aprovado pelo responsavel) -->

> **Extra aprovado no Sprint 1** (4 correcoes de alerta aprovadas em 04/11): limiar
> Wi-Fi, gatilho de severidade de impressora, investigacao ICMP e itens
> `printer.alert.severity` — executado, ver docs/68 §2 (problemas 181 → 152).

### Sprint 2 (2 semanas) — Pipeline Base

- [ ] Criar repositório `zabbix-config`
- [ ] Script de export/import config
- [ ] Smoke test automatizado (Python)
- [ ] Workflow GitHub Actions (validate + deploy staging)
- [ ] Rollback script testado

### Sprint 3 (2 semanas) — Segurança

- [ ] Migrar segredos para variáveis de ambiente
- [ ] Criar comunidade SNMP dedicada
- [ ] Script de rotação mensal
- [ ] Dashboard de monitoramento do Zabbix

### Sprint 4 (2 semanas) — Produção

- [ ] Deploy em produção via pipeline
- [ ] Validação ponta-a-ponta (webhook InfraControl)
- [ ] Documentação operacional (runbook)
- [ ] Treinamento da equipe

### Sprint 5+ (opcional) — HA

- [ ] Zabbix Proxy por localidade
- [ ] PostgreSQL HA (Patroni)
- [ ] Load balancer nginx
- [ ] Teste de failover

---

## 8. Riscos e Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
| --- | --- | --- | --- |
| Upgrade quebra API | Média | Alto | Staging mandatory + rollback |
| Perda de dados | Baixa | Crítico | Backup daily + teste restore |
| Segredo exposto | Média | Alto | Vault + rotação mensal |
| Pipeline falha | Baixa | Médio | Manual override documentado |
| SNMP mudança massiva | Alta | Alto | Rollback script + comunicação prévia |

---

## 9. Métricas de Sucesso

| Métrica | Meta Atual | Meta CD |
| --- | --- | --- |
| Tempo de deploy | Manual (horas) | < 15 min (automático) |
| MTTR (Mean Time To Recover) | Desconhecido | < 30 min |
| Uptime do Zabbix | Desconhecido | > 99.5% |
| Itens unsupported | 33/20.8k | < 0.1% |
| Queue backup | 0–1 | < 10 |
| Tempo de rollback | Manual (horas) | < 10 min |

---

## 10. Próximos Passos

1. **Revisão deste plano** com o usuário (aprovação da estratégia)
2. **Execução do Sprint 1** (estabilização P0)
3. **Validação em staging** antes de produção
4. **Documentação de runbook operacional** (escalas, procedimentos)

---

## Referências

- Auditoria técnica: docs/64-AUDITORIA-ZABBIX-PRODUCAO.md
- Integração webhook: docs/27-INTEGRACAO-ZABBIX.md
- ADR-023: Webhook de entrada do Zabbix
- Zabbix 7.4 documentation: https://www.zabbix.com/documentation/7.4/manual
- Zabbix API documentation: https://www.zabbix.com/documentation/current/manual/api
