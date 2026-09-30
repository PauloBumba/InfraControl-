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

O `NotificationEventConsumer` anexa o destinatário junto da notificação
(mesma transação, após o inbox de idempotência):

| Evento | Destinatário |
|---|---|
| `UserCreated` / `UserDisabled` | usuário (`user_id`) |
| `UserRoleAssigned` / `UserRoleRemoved` | role (`role`) |
| `InvitationAccepted` | usuário resolvido por e-mail (sem correspondência: notificação sem destinatário + log Warning) |

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
  (evento `notification` com deliveryId/notificationId/title/message/createdAt)
  e marca `Sent` (+`sent_at`) ou `Failed` (`last_error` ≤500 pt-BR).
  Retry/backoff/DLQ = F9-T009.

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

## Canal WhatsApp (F9-T007)

Espelho do Telegram (F9-T006): o consumer enfileira `NotificationDelivery`
(WhatsApp) por usuário final quando o canal está em `Default` **e**
`WhatsApp:Phone` está preenchido (sem Phone = Warning, sem delivery). O
`NotificationWhatsAppWorker` (mesmo loop base, 2s) despacha via porta
`IWhatsAppNotificationDispatcher` (`Phone` global + `Title`/`Message`) e
marca `Sent`/`Failed`. O adaptador é `LoggingWhatsAppNotificationDispatcher`
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
marca `Sent`/`Failed`. O adaptador é `LoggingWebhookNotificationDispatcher`
(Infrastructure) — nenhum POST real; um adaptador com HTTP
(`IHttpClientFactory`) troca a implementação sem tocar em Application
(endpoint real = pendência, junto do SMTP e das credenciais dos demais
canais). Url esvaziada entre enfileirar e despachar vira `Failed` com
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
