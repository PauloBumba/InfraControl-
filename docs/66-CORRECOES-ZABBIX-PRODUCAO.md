# 66. Correções Zabbix — Bugs Críticos para Produção

> **Objetivo:** corrigir bugs críticos do Zabbix e deixar pronto para produção.
> **Escopo:** APENAS Zabbix (sem alterações em switches/impressoras/equipamentos sem autorização).
> **Notificações WhatsApp:** filtrar apenas incidentes CRÍTICOS e alertas RESOLVIDOS (no InfraControl).

---

## BUGS Críticos a Corrigir

### 1. 🔴 ICMP Pinger 100% — Gargalo confirmado

**Problema:** `StartPingers` padrão (1) insuficiente para 152 hosts → utilização 100% → ping atrasado/falso negativo.

**Correção (via API Zabbix):**

```bash
#!/bin/bash
# fix-pinger.sh — correção do ICMP pinger via API
AUTH_TOKEN=$(curl -s -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "user.login",
    "params": {
      "username": "Admin",
      "password": "zabbix"
    },
    "id": 1
  }' | jq -r '.result')

# Aumentar pingers de 1 para 5 (base: 1 pinger ~ 100 hosts)
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{
    \"jsonrpc\": \"2.0\",
    \"method\": \"configuration.update\",
    \"params\": {
      \"start_pingers\": 5,
      \"start_pingers_unreachable\": 5
    },
    \"auth\": \"$AUTH_TOKEN\",
    \"id\": 1
  }"

echo "Pingers aumentados para 5. Reinicie o Zabbix server para aplicar."
```

**Validação após correção:**
- Verificar `zabbix[process,pinger]` → utilização < 75%
- Verificar `zabbix[queue,10m]` → deve ser 0
- Smoke test de ping em 20 hosts aleatórios

---

### 2. 🔴 Flapping `printer.alert.severity` — 5 impressoras

**Problema:** item LLD `printer.alert.severity[1.x]` criado para índice inexistente → alterna `became not supported` ↔ `became supported` a cada 5 min.

**Diagnóstico:**
```bash
# Identificar hosts com flapping
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "item.get",
    "params": {
      "output": ["itemid", "name", "hostid", "key_"],
      "search": {"key_": "printer.alert.severity"}
    },
    "auth": "'$AUTH_TOKEN'",
    "id": 1
  }' | jq '.result[]'
```

**Correção (desabilitar item afetado, não o template):**

```bash
#!/bin/bash
# fix-printer-flapping.sh
# Para cada host com flapping, desabilitar o item específico

# Lista de hosts afetados (exemplo)
HOSTS=(
  "prn-192-168-22-16"
  # adicionar outros conforme diagnóstico
)

for host in "${HOSTS[@]}"; do
  # Obter hostid
  HOSTID=$(curl -s -X POST http://localhost:8086/api_jsonrpc.php \
    -H "Content-Type: application/json" \
    -d "{
      \"jsonrpc\": \"2.0\",
      \"method\": \"host.get\",
      \"params\": {
        \"filter\": {\"host\": \"$host\"},
        \"output\": [\"hostid\"]
      },
      \"auth\": \"$AUTH_TOKEN\",
      \"id\": 1
    }" | jq -r '.result[0].hostid')

  # Obter itemid do printer.alert.severity
  ITEMID=$(curl -s -X POST http://localhost:8086/api_jsonrpc.php \
    -H "Content-Type: application/json" \
    -d "{
      \"jsonrpc\": \"2.0\",
      \"method\": \"item.get\",
      \"params\": {
        \"hostids\": \"$HOSTID\",
        \"search\": {\"key_\": \"printer.alert.severity\"},
        \"output\": [\"itemid\"]
      },
      \"auth\": \"$AUTH_TOKEN\",
      \"id\": 1
    }" | jq -r '.result[0].itemid')

  # Desabilitar item
  curl -X POST http://localhost:8086/api_jsonrpc.php \
    -H "Content-Type: application/json" \
    -d "{
      \"jsonrpc\": \"2.0\",
      \"method\": \"item.update\",
      \"params\": {
        \"itemid\": \"$ITEMID\",
        \"status\": 1
      },
      \"auth\": \"$AUTH_TOKEN\",
      \"id\": 1
    }"

  echo "Item $ITEMID desabilitado no host $host"
done
```

---

### 3. ⚠️ 64 Problemas Órfãos — Triggers de Câmera Desabilitadas

**Problema:** 68 triggers desabilitadas ainda com `value=1` (64 de câmera N absent) → problemas permanentemente abertos.

**Correção segura (reabilitar + aguardar recuperação OU supressão documentada):**

```bash
#!/bin/bash
# fix-orphaned-triggers.sh

# Opção A: Reabilitar triggers (se os problemas forem reais)
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{
    \"jsonrpc\": \"2.0\",
    \"method\": \"trigger.update\",
    \"params\": {
      \"triggerid\": \"<triggerid>\",
      \"status\": 0
    },
    \"auth\": \"$AUTH_TOKEN\",
    \"id\": 1
  }"

# Opção B: Suprimir problemas documentados (se triggers devem permanecer desabilitadas)
# Criar supressão manual via UI ou API
```

**Recomendação:** investigar primeiro por que as triggers foram desabilitadas. Se as câmeras realmente estão ausentes e não há plano de restauração, suprimir os problemas. Se forem temporariamente desabilitadas, reabilitar.

---

### 4. ⚠️ 18 Itens Órfãos — AP `ap-192-168-11-167`

**Problema:** interfaces `br0`/`br0.13` (LLD ifTable) com `No Such Instance` → interfaces não existem mais no AP.

**Correção:**

```bash
#!/bin/bash
# fix-ap-orphaned-items.sh

HOST="ap-192-168-11-167"

# Confirmar via SNMP walk que br0/br0.13 não existem
# snmpwalk -v2c -c public 192.168.11.167 IF-MIB::ifDescr

# Se não existirem, desabilitar itens órfãos
HOSTID=$(curl -s -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{
    \"jsonrpc\": \"2.0\",
    \"method\": \"host.get\",
    \"params\": {
      \"filter\": {\"host\": \"$HOST\"},
      \"output\": [\"hostid\"]
    },
    \"auth\": \"$AUTH_TOKEN\",
    \"id\": 1
  }" | jq -r '.result[0].hostid')

# Listar itens com erro No Such Instance
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{
    \"jsonrpc\": \"2.0\",
    \"method\": \"item.get\",
    \"params\": {
      \"hostids\": \"$HOSTID\",
      \"filter\": {\"error\": \"No such instance\"},
      \"output\": [\"itemid\", \"name\", \"key_\"]
    },
    \"auth\": \"$AUTH_TOKEN\",
    \"id\": 1
  }" | jq -r '.result[].itemid' | while read ITEMID; do
    curl -X POST http://localhost:8086/api_jsonrpc.php \
      -H "Content-Type: application/json" \
      -d "{
        \"jsonrpc\": \"2.0\",
        \"method\": \"item.update\",
        \"params\": {
          \"itemid\": \"$ITEMID\",
          \"status\": 1
        },
        \"auth\": \"$AUTH_TOKEN\",
        \"id\": 1
      }"
    echo "Item $ITEMID desabilitado"
  done
```

---

### 5. ⚠️ Sem Backup/Exportação da Configuração

**Problema:** nenhuma exportação realizada → rollback impossível.

**Correção (script de backup automatizado):**

```bash
#!/bin/bash
# backup-zabbix.sh
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups/zabbix"
mkdir -p "$BACKUP_DIR"

# Login
AUTH_TOKEN=$(curl -s -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "user.login",
    "params": {
      "username": "Admin",
      "password": "zabbix"
    },
    "id": 1
  }' | jq -r '.result')

# Export configuração completa
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d "{
    \"jsonrpc\": \"2.0\",
    \"method\": \"configuration.export\",
    \"params\": {
      \"format\": \"json\",
      \"options\": {
        \"hosts\": true,
        \"items\": true,
        \"triggers\": true,
        \"graphs\": true,
        \"templates\": true
      }
    },
    \"auth\": \"$AUTH_TOKEN\",
    \"id\": 1
  }" > "$BACKUP_DIR/config_$TIMESTAMP.json"

# Dump PostgreSQL
docker exec infracontrol-zabbix-db pg_dump -U zabbix zabbix \
  > "$BACKUP_DIR/db_$TIMESTAMP.sql"

# Retenção (últimos 7 dias)
find "$BACKUP_DIR" -name "*.sql" -mtime +7 -delete
find "$BACKUP_DIR" -name "*.json" -mtime +7 -delete

echo "Backup completo: config_$TIMESTAMP.json + db_$TIMESTAMP.sql"
```

**Agendar no crontab:**
```bash
# Backup diário às 2h
0 2 * * * /path/to/backup-zabbix.sh
```

---

### 6. 🟡 Config Cache 73,9% — Monitorar

**Ação:** acompanhar; se passar de 85%, aumentar `CacheSize`.

```bash
# Monitorar via API
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "item.get",
    "params": {
      "filter": {"key_": "vm.memory.size[available,pct]"},
      "output": ["lastvalue"]
    },
    "auth": "'$AUTH_TOKEN'",
    "id": 1
  }'
```

---

## Correções NÃO executáveis sem autorização

**NÃO modificar sem aprovação do usuário:**

- **Comunidade SNMP `public`** — requer mudança em 131 equipamentos
- **IEAS-Impressoes-Servidor** — host externo, requer investigação
- **HSDS_SUPTI666** — agent inacessível, requer acesso ao host

---

## Notificações WhatsApp — Filtro de Severidade

**Problema atual:** o InfraControl envia TODOS os incidentes para WhatsApp (spam).

**Solução:** filtrar no `NotificationEventConsumer` para enviar apenas:
- Incidentes CRÍTICOS (severidade High/Critical) ao abrir
- Incidentes RESOLVIDOS (qualquer severidade) ao fechar

**Arquivo a alterar:** `src/InfraControl.Workers/Consumers/Notifications/NotificationEventConsumer.cs`

### Alteração 1: Filtro em `IncidentCreatedDomainEvent` (linha 76)

**Código atual:**
```csharp
public Task Consume(ConsumeContext<IncidentCreatedDomainEvent> context)
    => HandleAsync(
        context.Message.EventId,
        context.Message.EventType,
        "Incidente aberto",
        $"O incidente '{context.Message.Title}' foi aberto ({context.Message.Severity}).",
        recipientUserId: null,
        recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
        context.CancellationToken,
        deviceId: context.Message.DeviceId);
```

**Código corrigido (filtro de severidade para WhatsApp):**
```csharp
public Task Consume(ConsumeContext<IncidentCreatedDomainEvent> context)
{
    var message = context.Message;

    // Filtro: só enviar CRÍTICOS (High/Critical) para WhatsApp
    var channels = message.Severity == IncidentSeverity.High ||
                   message.Severity == IncidentSeverity.Critical
        ? null // usa fanout padrão (inclui WhatsApp se configurado)
        : [NotificationChannel.SignalR, NotificationChannel.Email]; // SEM WhatsApp

    return HandleAsync(
        message.EventId,
        message.EventType,
        "Incidente aberto",
        $"O incidente '{message.Title}' foi aberto ({message.Severity}).",
        recipientUserId: null,
        recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
        context.CancellationToken,
        channels: channels,
        deviceId: message.DeviceId);
}
```

### Alteração 2: `IncidentResolvedDomainEvent` (linha 91) — SEM filtro

**Código atual (já está correto — resolução sempre vai para WhatsApp):**
```csharp
public Task Consume(ConsumeContext<IncidentResolvedDomainEvent> context)
    => HandleAsync(
        context.Message.EventId,
        context.Message.EventType,
        "Incidente resolvido",
        $"O incidente '{context.Message.Title}' foi resolvido ({context.Message.Severity}).",
        recipientUserId: null,
        recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
        context.CancellationToken,
        deviceId: context.Message.DeviceId);
```

**Justificativa:** resolução de incidente (independente da severidade original) é informação importante para o grupo de operações — deve sempre ir para WhatsApp.

### Testes necessários

Após aplicar a correção, executar:

```bash
# Testes unitários do consumer
dotnet test tests/InfraControl.UnitTests --filter "NotificationEventConsumer"

# Testes de notificações
dotnet test tests/InfraControl.UnitTests --filter "Notification"
```

### Smoke test manual

1. Criar incidente de severidade **Low** → NÃO deve ir para WhatsApp
2. Criar incidente de severidade **High** → DEVE ir para WhatsApp
3. Criar incidente de severidade **Critical** → DEVE ir para WhatsApp
4. Resolver incidente de qualquer severidade → DEVE ir para WhatsApp

**Task para InfraControl:** aplicar as alterações acima no `NotificationEventConsumer.cs`.

---

## Plano de Execução

### Parte 1: Correções ZABBIX (escopo deste documento)

#### Passo 1: Backup
- [ ] Executar `backup-zabbix.sh` (antes de qualquer alteração)

#### Passo 2: Correções Críticas
- [ ] Corrigir ICMP pinger (StartPingers=5)
- [ ] Validar pinger < 75%
- [ ] Diagnosticar flapping `printer.alert.severity`
- [ ] Desabilitar itens flapping
- [ ] Investigar triggers desabilitadas (68)
- [ ] Resolver 64 problemas órfãos de câmera
- [ ] Desabilitar 18 itens órfãos do AP
- [ ] Agendar backup diário (crontab)

#### Passo 3: Validação Zabbix
- [ ] Smoke test de ping em 20 hosts
- [ ] Verificar queue < 10
- [ ] Verificar unsupported itens < 10
- [ ] Verificar problemas ativos (esperado: < 50 após correções)

### Parte 2: Correção InfraControl (filtro WhatsApp — separado)

#### Passo 4: Alterar código
- [ ] Abrir `src/InfraControl.Workers/Consumers/Notifications/NotificationEventConsumer.cs`
- [ ] Aplicar filtro em `IncidentCreatedDomainEvent` (linha 76)
- [ ] Executar testes unitários
- [ ] Testar manualmente no grupo de WhatsApp

---

## Comandos Úteis

### Verificar saúde do Zabbix
```bash
# Queue
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"item.get","params":{"filter":{"key_":"zabbix[queue,10m]"}},"auth":"'$AUTH_TOKEN'","id":1}'

# Pinger utilization
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"item.get","params":{"filter":{"key_":"zabbix[process,pinger]"}},"auth":"'$AUTH_TOKEN'","id":1}'

# Itens unsupported
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"item.get","params":{"filter":{"status":3},"output":["name","hostid","error"]},"auth":"'$AUTH_TOKEN'","id":1}'
```

### Listar problemas ativos
```bash
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"problem.get","params":{"recent":true,"output":["name","severity"],"selectHosts":["host"]},"auth":"'$AUTH_TOKEN'","id":1}'
```

---

## Próximos Passos

1. **Executar backup** (obrigatório antes de qualquer alteração)
2. **Corrigir ICMP pinger** (crítico)
3. **Corrigir flapping impressoras** (alto)
4. **Resolver problemas órfãos** (alto)
5. **Desabilitar itens órfãos do AP** (médio)
6. **Agendar backup diário** (crítico)
7. **Verificar filtro WhatsApp no InfraControl** (separado)

---

## Referências

- Auditoria técnica: docs/64-AUDITORIA-ZABBIX-PRODUCAO.md
- Integração webhook: docs/27-INTEGRACAO-ZABBIX.md
- Roteamento notificações: docs/62-PLANO-ROTEAMENTO-NOTIFICACOES-ZABBIX.md
- ADR-059: WhatsApp Evolution API
