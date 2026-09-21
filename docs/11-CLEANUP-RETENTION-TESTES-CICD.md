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

## Implementação inicial da F0-T010

O workflow `.github/workflows/ci.yml` executa em pull requests para `main`, pushes em `main` e manualmente. A primeira versão cobre restore, build e testes da solução .NET, com PostgreSQL, Redis e RabbitMQ como serviços do job; também executa `npm ci` e o build do frontend React.

A validação de lint/format, Docker e ambientes de staging/produção permanece separada desta Task porque ainda não existem configuração de lint/format do frontend, imagens Docker versionadas ou ambientes de implantação definidos no repositório. A F0-T010 só deve ser marcada como concluída após uma execução remota bem-sucedida no GitHub Actions. Em 2026-09-21, duas tentativas foram encerradas antes dos steps, com `runner_name` vazio e sem logs, indicando indisponibilidade do executor hospedado pelo GitHub.
