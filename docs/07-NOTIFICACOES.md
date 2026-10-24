# Notificações

## Separação

```text
Evento
→ Notification
→ NotificationDelivery
→ Canal
```

## Fluxo

```text
Domain Event
→ Outbox
→ RabbitMQ
→ Notification Consumer
→ PostgreSQL
→ Notification Worker
→ Canal
```

## Canais

- SignalR
- Email
- Telegram
- WhatsApp
- Webhook
- Slack (F9-T010 — decidido pelo usuário, não existia nesta lista)

### Roteamento configurável (F39)

O fanout está sendo evoluído conforme o escopo F39 aprovado para o
Administrador configurar canais e templates por tipo de evento, incluindo
filtros por host/grupo/tag do Zabbix, preservando destinatários, preferências
e o pipeline de entrega. O contexto Zabbix já é enriquecido na ingestão e
persistido; políticas, templates e tela ainda não estão implementados. Ver
[docs/62 — plano de roteamento Zabbix](62-PLANO-ROTEAMENTO-NOTIFICACOES-ZABBIX.md)
e [ADR-063](adr/ADR-063-CONFIGURABLE-NOTIFICATION-ROUTING.md).

## Destinatários

Pode ser:

- usuário;
- role.

Usuários presentes em múltiplas roles devem receber uma única notificação lógica.

### Implementação (F9-T002)

Tabela `notification_recipients`: `notification_id` (FK `Cascade` →
`notifications`), `user_id` (destinatário usuário) OU `role` (nome da
role) — exatamente um dos dois (pt-BR). Índices únicos
`(notification_id, user_id)` e `(notification_id, role)` garantem o
dedup por notificação.

O `NotificationEventConsumer` anexa os destinatários junto da notificação
(mesma transação, após o inbox de idempotência; desde a F16-T002 aceita
**1..N roles** por notificação, com dedup):

| Evento | Destinatário |
|---|---|
| `UserCreated` / `UserDisabled` | usuário (`user_id`) |
| `UserRoleAssigned` / `UserRoleRemoved` | role (`role`) |
| `InvitationAccepted` | usuário resolvido por e-mail (sem correspondência: notificação sem destinatário + log Warning) |
| `IncidentCreated` (F16-T002) | roles `Operator` e `Admin` — cobre webhook Zabbix (F16-T001), criação manual e auto-abertura (F6) |
| `IncidentResolved` (F19-T007) | roles `Operator` e `Admin` — levantado por `Incident.ChangeStatus(Resolved)`; só o InfraControl decide a resolução (regra 7 da revisão F19) |
| `AlertResolved` (F19-T007) | roles `Operator` e `Admin` — levantado por `Alert.Resolve` (OK do Zabbix, comando da API) |

`GET /api/v1/notifications/{id}/recipients` lista os destinatários;
`GET /api/v1/notifications/{id}/recipients/users` resolve os **usuários
finais únicos**: destinatários diretos + roles convertidas para usuários
ativos, deduplicados — é essa resolução que materializa a "única
notificação lógica" para o usuário em múltiplas roles (consumida pela
entrega nos canais, F9-T004+).

Autorização (ADR-016): leitura em `RoleAccess.Read` (qualquer role
canônica) no pipeline MediatR e `[Authorize]` na rota.

## Preferências (F9-T003)

Tabela `notification_preferences`: `user_id` (FK `Cascade` →
`DomainUsers` — a preferência morre com o usuário), `channel` (enum
`NotificationChannel`: SignalR, Email, Telegram, WhatsApp, Webhook) e
`enabled`; índice único `(user_id, channel)` — uma por combinação.

- `GET api/v1/notifications/preferences?userId=` (`RoleAccess.Read`) —
  404 se o usuário não existe.
- `PUT api/v1/notifications/preferences` (`RoleAccess.Operate`) —
  **upsert** por (usuário, canal): cria ou atualiza `enabled`; 404
  usuário, 400 canal inválido.
- `DELETE .../preferences?userId=&channel=` (`RoleAccess.Operate`) — 404
  se não existe.

Decisão F9-T003: escrita em nível Operar (a ADR-016 não separa
"próprio vs alheio"; preferência alheia é gestão de notificação). A
entrega (F9-T004+) deve consultar `enabled` por canal antes de enviar.

## Canal SignalR (F9-T004)

`GET /hubs/notifications` (SignalR, `[Authorize]`): o usuário conecta e o
`SignalRUserIdProvider` usa a claim `ClaimTypes.NameIdentifier` do login —
`OnConnectedAsync` entra no grupo `user:{userId}`.

- **Enfileira**: o `NotificationEventConsumer`, após notification +
  destinatários, resolve os usuários finais (dedup) e cria uma
  `NotificationDelivery` (`Pending`, canal SignalR) por usuário —
  **preferência desligada = sem delivery** (default habilitado). Canais
  dos F9-T005+ enfileiram os seus.
- **Envia**: `NotificationSignalRWorker` (BackgroundService no Api, 2s)
  lê `Pending` (limite 50), envia via porta `ISignalRNotificationDispatcher`
  (evento `notification` com deliveryId/notificationId/title/message/
  **eventType**/createdAt — o `eventType` espelha o feed persistido,
  entregue 3 do backlog de UX) e marca `Sent` (+`sent_at`) ou `Failed`
  (`last_error` ≤500 pt-BR). Retry/backoff/DLQ = F9-T009.
- **Consome (front)**: `frontend/.../services/signalr/notifications.ts`
  (singleton por aba; negotiate autenticado com antiforgery, retry 10s
  enquanto o layout logado monta). O `AppLayout` sobe o hub e soma o
  contador do sino a cada evento; a `NotificationsPage` insere o item no
  topo da página 1 (dedup por notificationId) e soma o total. O feed
  `GET /notifications` continua fonte de histórico.

Tabela `notification_deliveries`: `notification_id`/`user_id` FKs
`Cascade`, `channel`/`status` (strings), `sent_at`, `last_error`;
índice `(channel, status)` — a fila por canal.

## Canal Email (F9-T005)

Espelho do canal SignalR: o consumer também enfileira
`NotificationDelivery` com canal `Email` para cada usuário final
(respeitando a preferência do canal); **usuário sem e-mail = sem delivery**
(Warning no log). O `NotificationEmailWorker` (mesmo loop base, 2s) lê os
`Pending` do canal e despacha via porta `IEmailNotificationDispatcher`
(`To` = e-mail do destinatário, `Subject` = title, `Body` = message) e
marca `Sent`/`Failed`.

O adaptador concreto é `LoggingEmailNotificationDispatcher`
(Infrastructure) — nenhum SMTP real, igual ao `LoggingIdentityEmailSender`
do identity (docs/05): um provider SMTP/API troca a implementação sem
tocar em Application. `PendingDeliveryItem` ganhou `RecipientEmail`
(join com DomainUsers).

## Configuração de canais (F9-T006)

Decisão do usuário: **o admin escolhe por onde as notificações são
enviadas** — seção `Notifications:Channels` no appsettings (presente na Api
e nos Workers, mesma seção nos dois hosts):

```json
"Notifications": {
  "Channels": {
    "Default": [ "Email", "SignalR" ],
    "Telegram": { "ChatId": "" },
    "WhatsApp": { "Phone": "" },
    "Webhook": { "Url": "" },
    "Slack": { "Channel": "" }
  }
}
```

- `Default` é a **lista do fanout** do `NotificationEventConsumer` (antes
  fixa em código: SignalR + Email). Canal desconhecido na lista é ignorado
  com Warning; duplicado não repete.
- Canal com destino externo **sem a seção preenchida = não enviável**
  (Warning, sem delivery).
- **Fallback de e-mail**: se NENHUM canal da lista for enviável, o consumer
  enfileira Email — a notificação não morre por configuração incompleta.
- A preferência por usuário (F9-T003) continua filtrando cada canal depois
  do fanout — desligada = sem delivery.
- Canais ainda sem worker (Webhook F9-T008, Slack F9-T010)
  não são enviáveis até as tasks deles.

## Canal Telegram (F9-T006)

Espelho do canal Email: o consumer enfileira `NotificationDelivery`
(Telegram) por usuário final quando o canal está em `Default` **e**
`Telegram:ChatId` está preenchido (sem ChatId = Warning, sem delivery). O
`NotificationTelegramWorker` (mesmo loop base, 2s) despacha via porta
`ITelegramNotificationDispatcher` (`ChatId` global + `Title`/`Message`) e
marca `Sent`/`Failed`. O adaptador é `LoggingTelegramNotificationDispatcher`
(Infrastructure) — nenhuma chamada real à API do Telegram; um adaptador com
bot token troca a implementação sem tocar em Application (credencial real =
pendência, junto do SMTP do docs/05). ChatId esvaziado entre enfileirar e
despachar vira `Failed` com erro pt-BR apontando a seção
(`Notifications:Channels:Telegram:ChatId`).

## Canal WhatsApp (F9-T007 + F33 — Evolution API)

### Versão real (F33 — ADR-059)

Desde a F33, o canal WhatsApp pode usar a **Evolution API** como transporte
de saída para eventos do Zabbix (incidentes e alertas críticos). O fluxo
completo é:

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

#### Configuração (ADR-059 §3)

Seção `Notifications:Channels:WhatsApp` estendida com campos para Evolution:

```json
"Notifications": {
  "Channels": {
    "WhatsApp": {
      "Phone": "",                // Destino individual (mantido, retrocompatível)
      "GroupId": "",              // Destino grupo (<id>@g.us) — **prioritário sobre Phone**
      "BaseUrl": "",              // URL da Evolution (http://evolution-api:8080 no compose)
      "ApiKey": "",              // Header apikey — fora de DTO/log/resposta
      "InstanceId": ""            // Nome da instância Evolution
    }
  }
}
```

- `GroupId` ou `Phone` preenchido = canal enviável; ambos vazios = Warning, sem delivery.
- `GroupId` **prioritário** sobre `Phone` — quando há grupo, ignora o número individual.
- `BaseUrl` **vazio** → mantém `LoggingWhatsAppNotificationDispatcher` (dev/teste sem Evolution
  não quebra — linha de DI condicional).
- `BaseUrl` preenchido → dispatcher real `EvolutionApiWhatsAppNotificationDispatcher`.

#### Infraestrutura Evolution (F33-T001 — ADR-059 §2)

Profile `evolution` no Docker Compose (3 containers, rede `infracontrol`):

| Serviço | Imagem (pinada) | Porta host | Papel |
|---|---|---|---|
| `evolution-api` | `evoapicloud/evolution-api:v2.3.7` | `EVOLUTION_API_PORT` (8081) | API REST + instância WhatsApp |
| `evolution-db` | `postgres:17-alpine` | `EVOLUTION_DB_PORT` (5435) | Estado das instâncias/sessões |
| `evolution-redis` | `redis:7-alpine` | `EVOLUTION_REDIS_PORT` (6380) | Cache/sessão da Evolution |

Sobe com: `docker compose --profile evolution up -d`

#### Dispatcher real (F33-T002 — ADR-059 §4)

`EvolutionApiWhatsAppNotificationDispatcher` (Infrastructure/Services):

- **Texto puro** (`sendText`): `POST {BaseUrl}/message/sendText/{InstanceId}` com header `apikey`,
  corpo `{ number: <GroupId|Phone>, text: <mensagem> }` — contrato real validado no spike.
- **Texto + gráfico** (`sendMedia`, F33-T005 — decisão 7 do ADR-059): `POST /message/sendMedia/{InstanceId}`
  com `mediatype=image`, `mimetype=image/png`, `media=<base64>`, `caption=<texto>`, `fileName`.
  Fonte da série = API `history` do Zabbix (janela 2h, até 240 pontos); a
  fábrica associa o título da notificação a um item por nome/chave e exige
  correspondência única. Unidade vem do item; **sem correspondência ou
  histórico** = fallback automático para `sendText`. Threshold só é mostrado
  quando vier de uma fonte confiável.
- Resiliência idêntica ao n8n (F19-T007): retry 3× backoff exponencial 1s/2s/4s com jitter +
  circuit breaker (5 falhas em 30s → aberto 60s); 4xx sem retry; erro de rede/5xx → retry.
- Nenhum SDK da Evolution — só `HttpClient` (padrão do repo).

#### Dedup de fanout (F33-T003 — ADR-059 §5)

Quando `GroupId` está preenchido (destino global em grupo), o canal WhatsApp cria
**1 delivery por notificação** (não por usuário) se ≥1 destinatário tiver o canal
habilitado. Isso evita N mensagens idênticas no grupo quando há N usuários
Operator/Admin. `Phone` individual (sem GroupId) mantém 1 delivery/usuário.

#### Eventos enviados (F33-T004 — ADR-059 decisões 2/3)

Quatro tipos de evento geram mensagem no grupo:

1. **Incidente aberto** (`IncidentCreatedDomainEvent`)
2. **Incidente resolvido** (`IncidentResolvedDomainEvent`)
3. **Alerta resolvido** (`AlertResolvedDomainEvent`)
4. **Alerta criado (Critical)** (`AlertCreatedDomainEvent` — nova fábrica, só quando Critical)

Decisão explícita do usuário: "Alerta criado (Critical)" **e** "Incidente aberto" são
enviados como **2 mensagens distintas** para o mesmo problema — duplicidade aceita
para dar mais contexto no grupo.

#### Risco do número pessoal (ADR-059 §6)

A Evolution API usa conexão não-oficial (WhatsApp Web/Baileys). A Meta pode banir
o número conectado. O usuário decidiu usar o número pessoal (2026-10-18) ciente
do risco. Mitigações:

- Volume mínimo: só 4 tipos de evento, sem alerta Warning, sem spam de polling.
- Sem loop de disparada: retry/DLQ (5 tentativas) + circuit breaker.
- Kill switch sem deploy: desativar **Ativar envio automático de notificações
  pelo WhatsApp** em Administração → WhatsApp; configurações antigas sem escolha
  explícita continuam respeitando `Notifications:Channels:Default`.
- Runbook de emergência: desativar canal na plataforma; reconexão = gerar novo QR.
- Migração futura registrada: chip dedicado (número novo) elimina o risco ao usuário.

#### Runbook de operação

**Subir a infraestrutura Evolution:**

```bash
docker compose --profile evolution up -d
```

**Gerenciar o canal:** em Administração → WhatsApp, informe a URL, a ApiKey
definida no `.env` da Evolution e um nome de instância; salve, crie a instância,
conecte pelo QR, carregue os grupos reais, selecione e salve o destino. Ative
envio automático na mesma tela e confirme com o envio de teste.

**Desativar/reativar:** altere a opção de envio automático na tela e salve; não
é necessário alterar `.env` nem reiniciar API/Workers. Testes manuais continuam
disponíveis com a instância conectada e um destino salvo.

**Legado:** enquanto a configuração não tiver ativação explícita persistida,
`Notifications:Channels:Default` conserva o comportamento antigo. Os valores
`EVOLUTION_BASE_URL`, `WHATSAPP_INSTANCE` e `WHATSAPP_GROUP_ID` são fallback de
configuração, não valores obrigatórios para instalação nova.

**Migrar para chip dedicado:** configure e conecte uma instância com o número
novo na tela; carregue e salve novamente o grupo correspondente. A tela opera
uma instância por instalação.

### Versão original (F9-T007 — adaptador de log)

Antes da F33, o espelho do Telegram (F9-T006): o consumer enfileira
`NotificationDelivery` (WhatsApp) por usuário final quando o canal está em
`Default` **e** `WhatsApp:Phone` está preenchido (sem Phone = Warning, sem
delivery). O `NotificationWhatsAppWorker` (mesmo loop base, 2s) despacha via
porta `IWhatsAppNotificationDispatcher` (`Phone` global + `Title`/`Message`)
e marca `Sent`/`Failed`. O adaptador é `LoggingWhatsAppNotificationDispatcher`
(Infrastructure) — nenhuma chamada real à API; um adaptador com credencial
(Cloud API/Business) troca a implementação sem tocar em Application
(credencial real = pendência, junto do SMTP e do bot token do Telegram).
Phone esvaziado entre enfileirar e despachar vira `Failed` com erro pt-BR
apontando a seção (`Notifications:Channels:WhatsApp:Phone`).

## Canal Webhook (F9-T008)

Espelho do WhatsApp (F9-T007): o consumer enfileira `NotificationDelivery`
(Webhook) por usuário final quando o canal está em `Default` **e**
`Webhook:Url` está preenchida (sem Url = Warning, sem delivery). O
`NotificationWebhookWorker` (mesmo loop base, 2s) despacha via porta
`IWebhookNotificationDispatcher` (`Url` global + `Title`/`Message`) e
marca `Sent`/`Failed`. Desde a **F19-T007/ADR-028** o adaptador é o
`HttpWebhookNotificationDispatcher` (Infrastructure): POST JSON real no
n8n via `IHttpClientFactory` com resiliência Polly no handler — retry 3×
backoff exponencial (1s, 2s, 4s) com jitter + circuit breaker (5 falhas
em 30s → aberto 60s) — e header `X-InfraControl-Secret` quando
`Notifications:Channels:Webhook:Secret` está preenchido (o adaptador de
log foi removido). REGRA (regra 7 da revisão F19/F20/F21): o n8n só
recebe a notificação — o estado do Incidente é 100% do InfraControl.
Url esvaziada entre enfileirar e despachar vira `Failed` com
erro pt-BR apontando a seção (`Notifications:Channels:Webhook:Url`).

## Canal Slack (F9-T010)

Decidido pelo usuário na F9-T006 (não existia em docs/07): espelho do
Telegram (F9-T006) — o consumer enfileira `NotificationDelivery` (Slack)
por usuário final quando o canal está em `Default` **e**
`Slack:Channel` está preenchido (sem Channel = Warning, sem delivery). O
`NotificationSlackWorker` (mesmo loop base, 2s) despacha via porta
`ISlackNotificationDispatcher` (`Channel` global + `Title`/`Message`) e
marca `Sent`/`Failed`. O adaptador é `LoggingSlackNotificationDispatcher`
(Infrastructure) — nenhuma chamada real à API; um adaptador com token do
bot troca a implementação sem tocar em Application (credencial real =
pendência, junto do SMTP e do bot token do Telegram). Channel esvaziado
entre enfileirar e despachar vira `Failed` com erro pt-BR apontando a
seção (`Notifications:Channels:Slack:Channel`).

## Delivery

Estados:

- Pending
- Processing
- Sent
- Failed
- Cancelled

Retry usa backoff e, quando necessário, DLQ.

### Retry/DLQ (F9-T009)

Política no `NotificationChannelWorkerBase` (vale para TODOS os canais):

- falha = `RegisterFailedAttempt` na delivery (`attempt_count` +
  `next_attempt_at`, colunas novas em `notification_deliveries`);
- **backoff exponencial**: 30s → 60s → 120s → 240s (base dobrando);
- o worker lê `Pending` **ou** `Failed` com `next_attempt_at` vencido;
- **5ª falha = DLQ**: `Failed` sem `next_attempt_at` (nunca mais volta à
  fila) + log `Error` identificando a delivery;
- retries ainda no futuro ficam de fora da consulta (índice
  `(channel, status)` atende a fila por canal).
