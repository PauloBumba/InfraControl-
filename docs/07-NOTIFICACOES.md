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

## Delivery

Estados:

- Pending
- Processing
- Sent
- Failed
- Cancelled

Retry usa backoff e, quando necessário, DLQ.
