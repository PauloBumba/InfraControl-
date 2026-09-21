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

## Delivery

Estados:

- Pending
- Processing
- Sent
- Failed
- Cancelled

Retry usa backoff e, quando necessário, DLQ.
