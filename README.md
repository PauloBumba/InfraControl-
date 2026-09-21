# InfraControl

Plataforma on-premises de gerenciamento de infraestrutura de TI.

## Objetivo

Centralizar inventário, rede, descoberta, monitoramento, alertas, incidentes, tarefas, SLA/SLO, conhecimento, notificações, identidade, auditoria e acesso remoto.

## Princípio de execução

> **Dividir para conquistar.**

Nenhuma funcionalidade grande deve ser implementada como um único bloco. Toda execução deve ser quebrada em Tasks e SubTasks pequenas, testáveis e rastreáveis.

## Regra

```text
Requisito
→ Domínio
→ Dados
→ Arquitetura
→ ADR
→ Task pequena
→ Teste
→ Código mínimo
→ Teste
→ Review
→ Validação
→ Documentação
→ Commit
→ Próxima Task
```

## Fonte da verdade

- PostgreSQL: dados e estado persistente.
- Redis: cache distribuído e coordenação.
- RabbitMQ: transporte de eventos.
- Outbox: garantia de publicação.
- Documentação: memória arquitetural e operacional.
- Git: histórico de mudanças.

## Fora da primeira fase

- Kubernetes.
- Microservices.
- Kafka.
- Elasticsearch.
- Prometheus/Grafana como dependência obrigatória.
- Event Sourcing completo.
- Bancos separados de leitura/escrita.
