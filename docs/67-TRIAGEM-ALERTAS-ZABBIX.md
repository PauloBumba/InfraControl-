# 67. Triagem de Alertas Zabbix — Guia de Operação

> **Objetivo:** Diferenciar alertas operacionais críticos de ruído/situação aceitável.
> **Contexto:** Hospital com 152 hosts, ~200 alertas ativos (câmeras, impressoras, switches, APs).

---

## 🎯 Regra de Ouro

**Só responde (acknowledge) problemas que você PRECISA tratar AGORA.**

Alertas de monitoramento que representam situação operacional aceitável (câmera realmente ausente, impressora com toner baixo mas funcionando) **não precisam de resposta imediata** — eles ficam na lista como evidência, mas não poluem a operação com acknowledgment.

---

## 📊 Classificação de Alertas

### 🔴 CRÍTICOS — Responder IMEDIATAMENTE

| Tipo | Exemplo | Ação |
|------|---------|-------|
| **Servidor down** | Host indisponível | Verificar servidor, reiniciar se necessário |
| **Switch core down** | Switch principal sem link | Verificar energia, link, dispositivo |
| **Serviço essencial parado** | Windows/Zabbix agent down | Reiniciar serviço |
| **Temperatura crítica** | Switch > 60°C | Verificar resfriamento, risk de hardware |
| **Alerta crítico sem recuperação** | Incidente Critical > 1h sem OK | Investigar causa raiz |

### ⚠️ IMPORTANTE — Monitorar, mas não urgente

| Tipo | Exemplo | Ação |
|------|---------|-------|
| **Toner baixo** (5-10%) | Impressora com toner 8% | Planejar troca, não urgente |
| **Link sem link** (porta não usada) | Switch: port 13 sem link | Se porta não usada, desabilitar trigger/item |
| **Interface half-duplex** | Porta em half-duplex | Verificar cabo/negociação se porta ativa |
| **Temperature warning** | Switch 50-60°C | Monitorar, não crítico se estável |
| **Serviço não crítico parado** | Google Updater não running | Ignorar (serviço de update) |

### 🟡 RUÍDO ACEITÁVEL — Ignorar

| Tipo | Exemplo | Ação |
|------|---------|-------|
| **Câmera absent** | Camera X absent | Câmera realmente não existe — desabilitar trigger |
| **ICMP ping indisponível** (host não crítico) | Printer offline | Se impressora desligada/fora de uso, ignorar |
| **Serviço de segurança Windows** | BitLocker não enabled | Se não é requisito de segurança, ignorar |
| **Alerta não mapeado** | Alerta 7 - severidade | Revisar template ou excluir |

---

## 🔧 Como Tratar Alertas por Tipo de Host

### 1. Câmeras (43 hosts)

**Problema atual:** ~50 alertas "Camera X absent"

**Diagnóstico:**
- Câmeras realmente não existem no momento (foram planejadas mas não instaladas)
- Triggers ficaram desabilitados anteriormente, foram reabilitados, agora voltaram a absent

**Solução:**
```bash
# Identificar triggers de câmera absent
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{
    "jsonrpc": "2.0",
    "method": "trigger.get",
    "params": {
      "search": {"description": "absent"},
      "output": ["triggerid", "description"]
    },
    "auth": "<TOKEN>",
    "id": 1
  }'

# Desabilitar triggers de câmeras que não existem (operação massiva)
# Isso para que os alerts parem de aparecer na lista
```

**Recomendação:**
- Se a câmera NÃO vai ser instalada → desabilitar trigger
- Se a câmera vai ser instalada → deixar habilitado, vai resolver quando conectar

---

### 2. Impressoras (37 hosts)

**Problema atual:** Alertas de toner baixo (5-10%)

**Classificação:**
- **Toner < 5% (crítico):** Planejar troca URGENTE (risco de parada)
- **Toner 5-10% (warning):** Planejar troca na próxima semana
- **Toner > 10%:** Normal, ignorar

**Solução:**
- Não responder (acknowledge) o alerta de toner baixo
- Usar o alerta como lembrete para comprar toner
- Só responder se a impressora parar de funcionar

**Flapping `printer.alert.severity`:**
- Se item alterna supported/unsupported a cada 5 min → desabilitar item específico
- O item tenta ler índice que a impressora não expõe

---

### 3. Switches (21 hosts)

**Problema atual:** Portas sem link, velocidade reduzida, temperatura

**Classificação:**
- **Porta sem link (porta não usada):** Desabilitar trigger/item da porta
- **Porta sem link (porta usada):** Verificar cabo/dispositivo
- **Velocidade reduzida (10/100 em vez de 1G):** Verificar negociação/cabo
- **Temperatura > 50°C (warning):** Monitorar, investigar se > 60°C

**Solução - desabilitar trigger de porta não usada:**
```bash
# Via UI Zabbix:
# 1. Ir em Configuration → Hosts → Switch
# 2. Items → encontrar item da porta (ex: net.if.status[ifOperStatus.13])
# 3. Desabilitar (status: Disabled)
# 4. Ou desabilitar o trigger associado à porta
```

**Recomendação:**
- Mapear quais portas estão realmente em uso
- Desabilitar monitoramento de portas vazias (reduz ruído)

---

### 4. Access Points (86 hosts)

**Problema atual:** High error rate em wifi0, wifi1ap5

**Classificação:**
- **High error rate > 2/5m:** Se AP está funcionando e usuários sem reclamação → pode ser normal (retransmissão wireless)
- **AP completamente offline:** Investigar energia/conexão

**Solução:**
- Se AP está no ar e funcionando → diminuir limiar do trigger ou desabilitar
- Se AP tem problemas reais → investigar interferência/posição

---

### 5. Servidores/Computadores (2 hosts)

**Problema atual:** Serviços Windows não running (Google Updater, BitLocker)

**Classificação:**
- **Google Updater/AVP não running:** Ruído, ignorar (serviços de update/antivírus não críticos)
- **Zabbix agent down:** CRÍTICO — reiniciar serviço
- **BitLocker não enabled:** Se não é requisito de segurança → ignorar

**Solução:**
- Desabilitar triggers de serviços não críticos
- Deixar apenas serviços essenciais monitorados

---

## 🛠️ Ações Massivas via API

### Desabilitar triggers por padrão

**Padrão para câmeras absent:**
```bash
# Listar todos triggers com "absent"
TRIGGERS=$(curl -s -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "jsonrpc": "2.0",
    "method": "trigger.get",
    "params": {
      "search": {"description": "absent"},
      "output": ["triggerid"]
    },
    "auth": "'$TOKEN'",
    "id": 1
  }' | jq -r '.result[].triggerid')

# Desabilitar em lote (JSON array)
curl -X POST http://localhost:8086/api_jsonrpc.php \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "jsonrpc": "2.0",
    "method": "trigger.update",
    "params": [
      {"triggerid": "39728", "status": 1},
      {"triggerid": "39729", "status": 1},
      ...
    ],
    "auth": "'$TOKEN'",
    "id": 1
  }'
```

---

## 📋 Checklist de Triagem Diária

### Passo 1: Filtrar por severidade
- Ir em Monitoring → Problems
- Filtrar: Severity = **Alta (High)** + **Desastre (Disaster)**
- Esses são os CRÍTICOS → responder primeiro

### Passo 2: Filtrar por host type
- Filtrar: Host groups = **Servidores** + **Switches Core**
- Verificar apenas hosts críticos

### Passo 3: Ignorar ruído conhecido
- Host groups = **Cameras** → ignorar absent (câmeras não instaladas)
- Host groups = **Impressoras** → toner baixo é planejamento, não urgência
- Host groups = **APs** → error rate sem reclamação de usuário = normal

### Passo 4: Acknowledge SE problemas que você está tratando
- Quando iniciar investigação → acknowledge com mensagem "Investigando..."
- Quando resolver → acknowledge com "Resolvido: [causa]"

---

## 🎯 Recomendação Prática

### 1. Criar Dashboard Operacional Limpo

Criar dashboard no Zabbix com apenas:
- Hosts CRÍTICOS (servidores, switches core)
- Severidade Alta/Desastre
- Filtro: "recent problems only"

Isso permite ver rapidamente o que importa, sem ruído.

### 2. Usar Maintenance Period

Para hosts que ficam offline propositalmente (manutenção, impressora desligada fora de uso):
- Configuration → Host → [host] → Maintenance
- Criar período de manutenção
- Durante manutenção, alertas são suprimidos automaticamente

### 3. Ajustar Limiares de Trigger

Para alertas que disparam demais (error rate wifi):
- Configuration → Templates → Template → Triggers
- Aumentar limiar (ex: de >2 para >5 por 5 min)
- Ou aumentar período de avaliação (de 5m para 15m)

---

## 📚 Documentação Operacional

### Criar "Estado Esperado" por Tipo de Host

| Host Type | Problemas Aceitáveis | Problemas Críticos |
|-----------|---------------------|-------------------|
| **Câmera** | Nenhum (se não existe, desabilitar) | Câmera instalada mas offline |
| **Impressora** | Toner 5-10% (planejamento) | Impressora offline, papel travado |
| **Switch** | Porta não usada sem link | Porta usada sem link, temperatura >60°C |
| **AP** | Error rate sem reclamação usuário | AP offline, degradando rede |
| **Servidor** | Nenhum | Servidor down, serviço essencial parado |

---

## 🔗 Próximos Passos

1. **Desabilitar triggers de câmeras absent** (operação massiva via API)
2. **Desabilitar itens de portas não usadas** nos switches
3. **Ajustar limiares de error rate** em APs
4. **Criar dashboard operacional limpo** (sem ruído)
5. **Documentar padrão de acknowledgment** para a equipe

---

## Referências

- Auditoria técnica: docs/64-AUDITORIA-ZABBIX-PRODUCAO.md
- Correções aplicadas: docs/66-EXECUCAO-CORRECOES.md
- Integração webhook: docs/27-INTEGRACAO-ZABBIX.md
