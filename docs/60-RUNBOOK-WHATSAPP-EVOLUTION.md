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

### 2. Configurar a chave da infraestrutura

A Evolution precisa de uma chave no startup. Defina uma chave forte em `.env`
e informe **a mesma chave** na tela Administração → WhatsApp:

```bash
# Gere uma chave forte, por exemplo: openssl rand -hex 32
EVOLUTION_API_KEY=sua_api_key_aqui

# Portas (padrões do .env.example)
EVOLUTION_API_PORT=8081
EVOLUTION_DB_PORT=5435
EVOLUTION_REDIS_PORT=6380

# Credenciais do banco da Evolution (dev — troque em produção)
EVOLUTION_DB_NAME=evolution
EVOLUTION_DB_USER=evolution
EVOLUTION_DB_PASSWORD=evolution_dev_password_2026
```

### 3. Configurar, conectar e escolher o grupo na plataforma

1. Entre em **Administração → WhatsApp** com uma conta Admin.
2. Informe a URL acessível pela API do InfraControl (no Compose:
   `http://evolution-api:8080`), a ApiKey definida acima e um nome de instância
   escolhido para esta instalação; salve.
3. Clique **Criar instância**. A tela confirma o nome, se foi criada ou já
   existia e o estado retornado. Falhas HTTP aparecem como falhas.
4. Clique **Gerar QR Code** e escaneie no WhatsApp do número remetente. O estado
   deve passar para **Conectado**. A sessão persiste no banco da Evolution.
5. Clique **Carregar grupos**, escolha um grupo retornado da instância conectada
   e salve a configuração. A tela distingue o destino persistido de uma
   seleção ainda não salva.
6. Marque **Ativar envio automático de notificações pelo WhatsApp** e salve.
   Use **Enviar mensagem de teste**; só considere concluído quando a tela
   confirmar sucesso e a mensagem chegar ao grupo.

O nome da instância e o grupo são escolhidos na plataforma; não preencha
`WHATSAPP_INSTANCE` nem `WHATSAPP_GROUP_ID` em uma instalação nova.
`EVOLUTION_BASE_URL`, `WHATSAPP_INSTANCE`, `WHATSAPP_GROUP_ID` e
`Notifications:Channels:Default` continuam como fallback legado para
instalações sem escolha explícita salva. Ao ativar/desativar WhatsApp na tela,
não é necessário editar o fanout nem reiniciar API/Workers.

### Substituir o número configurado

A instalação gerencia uma instância por vez. Para trocar o número, altere o
nome da instância e salve. A tela pede confirmação, limpa grupo/telefone
anteriores e desativa as notificações automáticas antes de operar o destino
novo. Cancele a confirmação para não gravar a troca. A instância anterior
permanece cadastrada na Evolution e não é apagada nem desconectada
automaticamente. Após confirmar, crie/identifique a nova instância, pareie pelo
QR, carregue os grupos, escolha e salve o destino, teste o envio e, se
desejado, habilite novamente as notificações automáticas.

## Operação rotineira

### Verificar status da instância

O estado atual aparece na tela Administração → WhatsApp. Para diagnóstico,
consulte a Evolution diretamente:

```bash
curl http://localhost:8081/instance/connectionState/NOME_DA_INSTANCIA \
  -H "apikey: $EVOLUTION_API_KEY"
```

- `open`: conectada e operacional
- `close`: desconectada (precisa de novo QR)
- `connecting`: em processo de conexão

### Verificar grupos disponíveis

Prefira **Carregar grupos** na plataforma. A consulta abaixo é apenas para
diagnóstico:

```bash
curl "http://localhost:8081/group/fetchAllGroups/NOME_DA_INSTANCIA?getParticipants=false" \
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

1. Verificar na plataforma se a instância está conectada (`state: open`).
2. Verificar se o grupo desejado está selecionado e salvo.
3. Verificar se **Ativar envio automático de notificações pelo WhatsApp** está marcado.
4. Verificar logs do worker: `docker compose logs workers | grep WhatsApp`
5. Verificar logs da Evolution: `docker compose logs evolution-api`

### Instância desconectada (`state: close`)

1. Abra Administração → WhatsApp.
2. Gere um novo QR e escaneie no celular.
3. Se a Evolution continuar presa em `close`/`connecting`, use **Desconectar** e
   gere outro QR (workaround issue #1408 validado no spike F33-T001).

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

1. Na plataforma, clique **Desconectar** e depois **Gerar QR Code**.
2. Escaneie o QR imediatamente.
3. Se falhar, gere outro QR pela tela e tente novamente.

Isso foi validado no spike F33-T001.

## Desativação (kill switch sem deploy)

Para desativar o envio automático sem alterar código ou reiniciar serviços,
desmarque na plataforma **Ativar envio automático de notificações pelo
WhatsApp** e salve. Configurações legadas sem escolha na plataforma continuam
respeitando `NOTIFY_DEFAULT_*` até serem gerenciadas.

O teste manual do canal continua disponível quando a instância está conectada
e há um destino salvo; a opção controla somente as notificações automáticas.

## Reativação

Marque novamente a opção **Ativar envio automático de notificações pelo
WhatsApp**, salve e confirme que a instância está conectada (`state: open`).

## Migração para chip dedicado (futuro)

Para eliminar o risco de banimento do número pessoal (ADR-059 §6):

1. Adquira um novo número (chip dedicado).
2. Configure um novo nome de instância em Administração → WhatsApp.
3. Crie a instância e conecte o QR com o novo número.
4. Busque e selecione o grupo de destino, salve e envie um teste.

Esta tela gerencia uma instância por instalação; a remoção de uma instância
antiga ou a gestão simultânea de múltiplas instâncias exige uma tarefa própria.

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
   (PNG 1024×768 anexado via `sendMedia`), desde que o título corresponda de
   forma única a um item do host. Sem correspondência inequívoca, o worker
   envia somente o texto.

Logs do worker devem mostrar:

```
[WhatsApp] Delivering notification {id} to group {groupId}
WhatsApp {deliveryId} enviado via Evolution (sendMedia, {bytes} bytes PNG)
para {groupId}: HTTP {statusCode}.
```

Logs da Evolution devem mostrar:

```
POST /message/sendMedia/{NOME_DA_INSTANCIA} - 201
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
Métrica: CPU = 92%
```

### Texto + gráfico (com histórico)

Caption (texto acima) + cartão PNG 1024×768 do gráfico da métrica (últimas 2h,
até 240 pontos). O item associado precisa corresponder ao título; unidade é
obtida do item Zabbix. O limite/threshold é omitido enquanto a integração não
disponibilizar a expressão ou o valor real do trigger.

Fonte da série: API `history` do Zabbix (`IZabbixClient.GetHistoryAsync`).
Sem histórico = fallback automático para texto puro.

## Segurança

- `EVOLUTION_API_KEY` é segredo: nunca commitar no repositório (AGENTS.md).
- Mantenha `EVOLUTION_LOG_LEVEL=WARN` (ou mais restritivo). Em `INFO`/`DEBUG`,
  a Evolution pode registrar payloads de mensagens e dados de sessão; habilite
  esses níveis somente temporariamente para diagnóstico controlado e trate os
  logs gerados como sensíveis.
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
