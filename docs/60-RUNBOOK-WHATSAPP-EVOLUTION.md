# 60 — Runbook: Canal WhatsApp via Evolution API (F33)

- **Task**: F33-T006 · **ADR**: [ADR-059](adr/ADR-059-WHATSAPP-EVOLUTION-API.md)
- **Propósito**: Documentação operacional do canal WhatsApp real (Evolution API)
  para transporte de eventos do Zabbix ao grupo da empresa.
- **Decisão do usuário (2026-10-18)**: número pessoal como remetente (risco de
  banimento registrado; ver ADR-059 §6 para mitigações).

## Visão geral

O canal WhatsApp leva eventos do Zabbix (incidentes e alertas críticos) ao grupo
de WhatsApp da empresa "Estudo Arquitetura" via Evolution API (API REST que
exporiza o WhatsApp Web). O fluxo completo é:

```text
Zabbix (PROBLEM/OK)
  → POST /api/v1/integrations/zabbix/problems   [F16-T001, ADR-023]
  → MonitoringEventIngestor → Alert + Incident  [F19, ADR-027]
  → IncidentCreatedDomainEvent / IncidentResolvedDomainEvent / AlertResolvedDomainEvent
  → Outbox → RabbitMQ → NotificationEventConsumer [idempotente via Inbox]
  → NotificationDelivery (canal WhatsApp)        [destino global: 1 delivery por notificação]
  → NotificationWhatsAppWorker
  → EvolutionApiWhatsAppNotificationDispatcher  [POST /message/sendText ou /message/sendMedia]
  → Grupo de WhatsApp "Estudo Arquitetura"
```

## Infraestrutura Docker Compose

Profile `evolution` (3 containers, rede `infracontrol`):

| Serviço | Imagem (pinada) | Porta host | Papel |
|---|---|---|---|
| `evolution-api` | `evoapicloud/evolution-api:v2.3.7` | `EVOLUTION_API_PORT` (8081) | API REST + instância WhatsApp |
| `evolution-db` | `postgres:17-alpine` | `EVOLUTION_DB_PORT` (5435) | Estado das instâncias/sessões |
| `evolution-redis` | `redis:7-alpine` | `EVOLUTION_REDIS_PORT` (6380) | Cache/sessão da Evolution |

Sobe com: `docker compose --profile evolution up -d`

A API do InfraControl **não depende** deste profile (opt-in, espelho do `ai`):
sem ele, o canal WhatsApp fica no adaptador de logging (ADR-059 §4).

## Configuração inicial

### 1. Subir a infraestrutura Evolution

```bash
docker compose --profile evolution up -d
```

Verificar que os containers estão saudáveis:

```bash
docker compose ps evolution-api evolution-db evolution-redis
```

Todos devem mostrar `healthy` no status.

### 2. Configurar variáveis de ambiente

Edite o `.env` e preencha as variáveis da seção F33:

```bash
# API key da Evolution (header `apikey` em toda chamada)
# Gere uma forte, ex.: openssl rand -hex 32
EVOLUTION_API_KEY=sua_api_key_aqui

# URL da Evolution CONSUMIDA pela API/Workers do InfraControl
# No compose: http://evolution-api:8080 (DNS interno)
# Fora do Docker: http://localhost:8081
EVOLUTION_BASE_URL=http://evolution-api:8080

# Nome da instância WhatsApp criada na Evolution
WHATSAPP_INSTANCE=Admin

# Destino GRUPO (prioritário sobre WHATSAPP_PHONE)
# Formato: <id>@g.us, ex.: 120363043123456789@g.us
WHATSAPP_GROUP_ID=

# Portas (padrões do .env.example)
EVOLUTION_API_PORT=8081
EVOLUTION_DB_PORT=5435
EVOLUTION_REDIS_PORT=6380

# Credenciais do banco da Evolution (dev — troque em produção)
EVOLUTION_DB_NAME=evolution
EVOLUTION_DB_USER=evolution
EVOLUTION_DB_PASSWORD=evolution_dev_password_2026
```

### 3. Conectar o WhatsApp (uma vez, manual)

1. Acesse o manager UI da Evolution: `http://localhost:8081/manager`
2. Clique em "Create Instance" e insira o nome configurado em `WHATSAPP_INSTANCE`
   (default: `Admin`).
3. Escaneie o QR code com o número remetente (número pessoal — decisão do usuário).
4. **Workaround issue #1408** (pairing code quebrado na v2.3.7): se o QR não conectar
   após ~45s, faça:
   - Delete a instância: `DELETE /instance/logout/{instance}` via manager UI
   - Crie novamente e escaneie o QR
   - Isso foi validado no spike F33-T001
5. A sessão persiste restart do container (creds no Postgres,
   `CACHE_REDIS_SAVE_INSTANCES=false`).

Verificar que a instância está conectada:

```bash
curl http://localhost:8081/instance/connectionState/Admin \
  -H "apikey: $EVOLUTION_API_KEY"
```

Resposta esperada: `{"state":"open"}`

### 4. Descobrir o JID do grupo

O Zabbix envia eventos para o grupo "Estudo Arquitetura". Descubra o JID:

```bash
curl http://localhost:8081/group/fetchAllGroups/Admin?getParticipants=false \
  -H "apikey: $EVOLUTION_API_KEY"
```

Encontre o grupo "Estudo Arquitetura" na resposta e copie o JID
(`<id>@g.us`, ex.: `120363043123456789@g.us`).

Preencha `WHATSAPP_GROUP_ID` no `.env`.

### 5. Ativar o canal no InfraControl

Adicione `WhatsApp` ao fanout de notificações no `.env`:

```bash
NOTIFY_DEFAULT_0=Email
NOTIFY_DEFAULT_1=SignalR
NOTIFY_DEFAULT_2=WhatsApp
```

Reinicie a API e os Workers para aplicar a configuração:

```bash
docker compose --profile app restart api workers
```

O canal agora está ativo e enviando mensagens para o grupo.

## Operação rotineira

### Verificar status da instância

```bash
curl http://localhost:8081/instance/connectionState/Admin \
  -H "apikey: $EVOLUTION_API_KEY"
```

- `open`: conectada e operacional
- `close`: desconectada (precisa de novo QR)
- `connecting`: em processo de conexão

### Verificar grupos disponíveis

```bash
curl http://localhost:8081/group/fetchAllGroups/Admin?getParticipants=false \
  -H "apikey: $EVOLUTION_API_KEY"
```

### Verificar logs da Evolution

```bash
docker compose logs -f evolution-api
```

### Verificar logs do worker WhatsApp

```bash
docker compose logs -f workers
```

## Solução de problemas

### Mensagens não chegam no grupo

1. Verificar que a instância está conectada (`state: open`).
2. Verificar que `WHATSAPP_GROUP_ID` está preenchido corretamente no `.env`.
3. Verificar que `WhatsApp` está em `NOTIFY_DEFAULT_*`.
4. Verificar logs do worker: `docker compose logs workers | grep WhatsApp`
5. Verificar logs da Evolution: `docker compose logs evolution-api`

### Instância desconectada (`state: close`)

1. Acesse o manager UI: `http://localhost:8081/manager`
2. Delete a instância
3. Crie novamente com o mesmo nome
4. Escaneie o QR code
5. A sessão nova é persistente

### Evolution API não responde

1. Verificar que os containers estão saudáveis: `docker compose ps evolution-*`
2. Verificar logs: `docker compose logs evolution-api`
3. Reiniciar o container: `docker compose restart evolution-api`
4. Se persistir, reiniciar todo o profile: `docker compose --profile evolution restart`

### Erro de autenticação (401)

Verificar que `EVOLUTION_API_KEY` está correto e consistente entre:
- `.env` (usado pelo compose)
- Header `apikey` nas chamadas de teste
- Configuração do dispatcher InfraControl

### QR code não conecta (loop infinito)

Workaround issue #1408 (pairing code quebrado na v2.3.7):

1. Delete a instância via manager UI
2. Crie novamente
3. Escaneie o QR imediatamente
4. Se falhar, repita o processo

Isso foi validado no spike F33-T001.

## Desativação (kill switch sem deploy)

Para desativar o canal sem alterar código:

1. Remova `WhatsApp` de `NOTIFY_DEFAULT_*` no `.env`
2. Reinicie API e Workers: `docker compose --profile app restart api workers`

O canal volta ao adaptador de logging (nenhuma chamada real à Evolution).

## Reativação

1. Adicione `WhatsApp` de volta a `NOTIFY_DEFAULT_*`
2. Reinicie API e Workers
3. Verifique que a instância está conectada (`state: open`)

## Migração para chip dedicado (futuro)

Para eliminar o risco de banimento do número pessoal (ADR-059 §6):

1. Adquira um novo número (chip dedicado)
2. Crie uma nova instância na Evolution (novo nome, ex.: `AdminDedicated`)
3. Conecte o QR com o novo número
4. Troque `WHATSAPP_INSTANCE` no `.env` para o novo nome
5. Reinicie API e Workers
6. O canal usa o novo número sem mudar código

A instância antiga pode ser deletada via manager UI.

## Atualização de versão da Evolution

Se precisar atualizar a imagem (ex.: nova tag estável):

1. Edite `docker-compose.yml` e atualize a tag em `evolution-api`
2. Pare o profile: `docker compose --profile evolution down`
3. Suba novamente: `docker compose --profile evolution up -d`
4. Reconecte o QR (sessão pode ser perdida na mudança de versão)
5. Valide com smoke test (trigger no Zabbix → mensagem no grupo)

**Importante**: a tag está **pinada** por contrato da Evolution (v1/v2 mudam).
Qualquer atualização exige spike revalidando o contrato e revisão do ADR-059.

## Smoke test ponta-a-ponta

Validar o fluxo completo:

1. Garanta que Zabbix está rodando: `docker compose --profile zabbix ps`
2. Garanta que Evolution está rodando e conectada: `state: open`
3. Dispare um PROBLEM no Zabbix (gatilho de teste):

```bash
docker exec infracontrol-zabbix-server zabbix_sender -z 127.0.0.1 \
  -s "Zabbix server" -k test.value -o 1
```

4. Verifique no banco InfraControl que `MonitoringEvent`, `Alert` e `Incident`
   foram criados.
5. Verifique que **2 mensagens** chegaram no grupo "Estudo Arquitetura":
   - "Alerta criado" (Critical)
   - "Incidente aberto"
6. Dispare o OK:

```bash
docker exec infracontrol-zabbix-server zabbix_sender -z 127.0.0.1 \
  -s "Zabbix server" -k test.value -o 0
```

7. Verifique que **2 mensagens** chegaram no grupo:
   - "Alerta resolvido"
   - "Incidente resolvido"
8. Se houver histórico no Zabbix, a mensagem deve incluir o gráfico da métrica
   (PNG 800×450 anexado via `sendMedia`).

Logs do worker devem mostrar:

```
[WhatsApp] Delivering notification {id} to group {groupId}
[WhatsApp] Chart generated successfully (800x450, {size}KB)
[WhatsApp] Sent via sendMedia to {groupId} (200)
```

Logs da Evolution devem mostrar:

```
POST /message/sendMedia/Admin - 201
```

## Eventos enviados

Quatro tipos de evento geram mensagem no grupo (ADR-059 decisões 2/3):

1. **Incidente aberto** (`IncidentCreatedDomainEvent`)
2. **Incidente resolvido** (`IncidentResolvedDomainEvent`)
3. **Alerta resolvido** (`AlertResolvedDomainEvent`)
4. **Alerta criado (Critical)** (`AlertCreatedDomainEvent` — só quando Critical)

Decisão explícita do usuário: "Alerta criado (Critical)" **e** "Incidente aberto"
são enviados como **2 mensagens distintas** para o mesmo problema — duplicidade
aceita para dar mais contexto no grupo.

## Formato da mensagem

### Texto puro (sem histórico)

```
🔴 CRITICAL - 21/10/2026 14:32
Dispositivo: srv-web-01 (192.0.2.71)
Alerta: Carga de CPU alta
Métrica: CPU = 92% (threshold: 80%)
```

### Texto + gráfico (com histórico)

Caption (texto acima) + imagem PNG do gráfico da métrica (últimas 2h, 240 pontos).

Fonte da série: API `history` do Zabbix (`IZabbixClient.GetHistoryAsync`).
Sem histórico = fallback automático para texto puro.

## Segurança

- `EVOLUTION_API_KEY` é segredo: nunca commitar no repositório (AGENTS.md).
- `EVOLUTION_BASE_URL` usa DNS interno no compose (`http://evolution-api:8080`),
  nunca exposto publicamente.
- Credenciais do banco Evolution só via `.env` (AGENTS.md).
- Nenhum webhook de entrada do WhatsApp (superfície de ataque zero — ADR-059 §1).
- Nenhum comando via chat (proibido por ADR-059 §1).

## Referências

- [ADR-059: Canal WhatsApp real via Evolution API](adr/ADR-059-WHATSAPP-EVOLUTION-API.md)
- [docs/07-NOTIFICACOES.md: Canal WhatsApp](07-NOTIFICACOES.md#canal-whatsapp-f9-t007--f33--evolution-api)
- [docs/27-INTEGRACAO-ZABBIX.md: Webhook Zabbix](27-INTEGRACAO-ZABBIX.md)
- Evolution API docs: https://doc.evolution-api.com/v2/pt/get-started/introduction
