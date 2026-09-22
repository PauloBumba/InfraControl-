# Adaptação de capacidades do Sentinel

## Objetivo

O Sentinel é usado como referência arquitetural do InfraControl, mas seus componentes não devem ser copiados sem adaptação. O Sentinel é uma plataforma de alertas e observabilidade; o InfraControl será uma plataforma on-premises de inventário, rede, discovery, monitoramento, incidentes e operação de TI.

A regra de adoção é trazer mecanismos transversais quando eles reduzem duplicação, melhoram segurança ou protegem integrações. Capacidades específicas do Sentinel só entram depois de existir um módulo de negócio equivalente no InfraControl.

## Capacidades priorizadas

| Prioridade | Capacidade | Decisão | Motivo |
| --- | --- | --- | --- |
| P0 | Behaviors do MediatR | Implementar em slices | Padronizar correlação, logging, desempenho e idempotência |
| P0 | Outbox Dispatcher | Implementar em slice próprio | Publicar eventos persistidos sem perder mensagens |
| P0 | Health checks operacionais | Implementar agora | Diferenciar processo vivo de dependências prontas |
| P1 | Auditoria persistida | Próximo slice | Requer contrato de AuditEntry e política de retenção |
| P1 | Resiliência | Próximo slice | Necessária para SNMP, HTTP, SSH, RDP e providers externos |
| P1 | Inbox de consumidores | Após primeiros consumers | Evitar reprocessamento de eventos RabbitMQ |
| P1 | Retenção e cleanup | Após Audit/Monitoring | Requer política por tipo de dado |
| P2 | Feature flags | Após módulos funcionais | Liberar recursos gradualmente |
| P2 | Sistema de providers/extensões | Após contratos de Discovery/Monitoring | Permitir SNMP, ICMP, HTTP e fornecedores adicionais |
| P2 | MCP/IA local | Após dados confiáveis | Não criar interface inteligente sobre dados incompletos |
| P2 | Multi-tenancy | Fora da fundação inicial | O alvo inicial é execução on-premises de uma organização |

## Entrega atual

A primeira entrega implementa o núcleo transversal que não depende de módulos futuros:

- `CorrelationIdBehavior`, `LoggingBehavior`, `PerformanceBehavior` e `ValidationBehavior` no pipeline MediatR;
- `IdempotencyBehavior` opt-in por request, usando aquisição atômica no Redis;
- `AuditBehavior` e `AuditEntry`, persistindo somente metadados e nunca payloads sensíveis;
- publicação tipada do Outbox por `IEventBus`;
- `OutboxDispatcherWorker` com retry controlado e persistência do estado processado;
- `IInboxStore` com unicidade por evento e consumidor;
- `IResilienceExecutor` com retry exponencial, timeout e circuit breaker por nome de provider;
- `CacheInvalidationBehavior` opt-in após execução bem-sucedida;
- `IFeatureFlagProvider` com defaults de configuração e override temporário no Redis;
- `IProviderRegistry<TProvider>` para Discovery e Monitoring sem acoplar o core a um fornecedor;
- `/health/live`, `/health/ready` e `/health/startup` na API;
- documentação e registro do roadmap incremental.

## Pipeline pretendido

```text
Correlation
→ Logging
→ Observability
→ Performance
→ Authorization
→ Validation
→ Idempotency
→ Caching
→ Transaction
→ Handler
→ Cache Invalidation
→ Audit
```

Nesta entrega, os comportamentos implementados são os que possuem contratos estáveis no código atual. A auditoria registra metadados da requisição e a migration `AddAuditEntries` cria a tabela correspondente. Payloads de comandos, senhas e tokens não são persistidos. Providers concretos de Discovery e Monitoring continuam sendo implementados somente quando os agregados e portas de cada módulo estiverem prontos.

## Próximos slices

1. Criar `AuditEntry` no domínio, contrato de redaction e repositório persistente.
2. Adicionar `AuditBehavior` pós-commit, sem capturar segredos ou senhas.
3. Criar `InboxMessage` e deduplicação por consumidor.
4. Adicionar políticas Polly por provider, com timeout e jitter.
5. Criar workers de Discovery e Monitoring somente após os agregados e portas correspondentes.
6. Criar políticas de retenção para Outbox, Inbox, auditoria, métricas e sessões.

## Regra arquitetural

Os behaviors podem depender de portas em `Application`; implementações de Redis, PostgreSQL, MassTransit e hosted services ficam em `Infrastructure` ou `Workers`. O domínio não conhece MediatR pipeline, Redis, RabbitMQ, ASP.NET Core ou tipos de infraestrutura.
