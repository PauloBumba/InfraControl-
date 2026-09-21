# Cleanup, Retention, Testes e CI/CD

## Cleanup

Worker separado.

Exemplos de retenção:

```text
Outbox processado: 7–30 dias
ProcessedMessages: 30 dias
NotificationDelivery: 90 dias
Logs: 30–90 dias
Métricas: 90–365 dias
Histórico: 1–5 anos
```

Não apagar inventário atual automaticamente.

## Testes

- Unit
- Integration
- Architecture
- Contract
- E2E
- Concurrency
- Security

Também testar:

- Outbox;
- idempotência;
- cache fallback;
- rate limit;
- retry;
- circuit breaker;
- DLQ;
- notificações.

## CI/CD

```text
PR
→ Build
→ Unit
→ Integration
→ Architecture
→ Security
→ Lint/Format
→ E2E
→ Approval
→ Docker
→ Staging
→ Production
```

Falhas críticas bloqueiam o processo.
