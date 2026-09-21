# Messaging, Outbox e RabbitMQ

## Regra

Não fazer:

```text
DB commit
→ RabbitMQ
```

sem garantia.

## Padrão

```text
Transaction
├── PostgreSQL
└── Outbox
↓
Commit
↓
Outbox Worker
↓
RabbitMQ
↓
Consumer
```

## Envelope

```json
{
  "eventId": "uuid",
  "eventType": "DeviceStatusChanged",
  "version": 1,
  "occurredAt": "...",
  "correlationId": "uuid",
  "causationId": "uuid",
  "source": "InfraControl.Monitoring",
  "payload": {}
}
```

## Idempotência

`ProcessedMessage` deve possuir unicidade em:

```text
(EventId, Consumer)
```

## DLQ

Mensagens que excedem a política de retry devem ir para DLQ.
