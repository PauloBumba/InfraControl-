# Runbooks

Runbooks devem existir para:

- PostgreSQL indisponível;
- Redis indisponível;
- RabbitMQ indisponível;
- Outbox acumulada;
- DLQ acumulada;
- Notification Worker parado;
- Discovery falhando;
- Monitoring Worker parado;
- backup;
- restore;
- perda de conectividade.

Cada runbook deve conter diagnóstico, impacto, ações seguras e validação.
