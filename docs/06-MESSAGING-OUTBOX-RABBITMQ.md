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

## Messaging Operations Center (F55)

Central operacional em `/messaging` (10 abas) sobre RabbitMQ + pipeline
Outbox/Inbox/NotificationDelivery. Backend `Application/Messaging` +
`Infrastructure/Messaging` (`IRabbitMqManagementClient` — named HttpClient,
Basic auth por ambiente, timeout+retry+circuit breaker, `Reachable=false`
com motivo em falha). Credenciais nunca saem do backend; browser não fala
com a Management API.

Operações administrativas (pause de consumers, resume via reciclagem de
conexões, replay de delivery) exigem `Administer` + `IAuditableRequest` e
são broker-level — não param containers nem executam comandos arbitrários.
Replay é não-destrutivo (requeue do delivery) e respeita Inbox.

Payload de mensagens é sanitizado (`[REDACTED]` para chaves proibidas)
antes de ir à UI. Métricas `infracontrol_messaging_*` (meter
`InfraControl.Messaging`, labels source/channel — nunca MessageId) são
coletadas por `MessagingBrokerMetricsWorker` (poll 30s) e instrumentadas
no OutboxDispatcherWorker e nos workers de canal de notificação.
