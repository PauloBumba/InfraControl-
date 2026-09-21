# Docker Setup

## Serviços

| Serviço | Porta | Usuário | Senha |
|---|---|---|---|
| PostgreSQL | 5432 | infracontrol | infracontrol_dev_2026 |
| Redis | 6379 | - | - |
| RabbitMQ | 5672 | infracontrol | infracontrol_dev_2026 |
| RabbitMQ Management | 15672 | infracontrol | infracontrol_dev_2026 |

## Comandos

### Iniciar infraestrutura

```bash
docker compose up -d
```

### Verificar status

```bash
docker compose ps
```

### Ver logs

```bash
docker compose logs -f
```

### Parar

```bash
docker compose down
```

### Parar e limpar volumes

```bash
docker compose down -v
```

## URLs de Acesso

- **RabbitMQ Management:** http://localhost:15672
- **PostgreSQL:** localhost:5432

## Variáveis de Ambiente

Copiar `.env.example` para `.env` e ajustar conforme necessário.

## Health Checks

Todos os serviços possuem health checks configurados:

- **PostgreSQL:** `pg_isready`
- **Redis:** `redis-cli ping`
- **RabbitMQ:** `rabbitmq-diagnostics check_port_connectivity`
