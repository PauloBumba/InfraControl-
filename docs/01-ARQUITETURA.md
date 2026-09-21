# Arquitetura

## Visão

```text
React + TypeScript
        ↓
REST / SignalR
        ↓
ASP.NET Core
        ↓
MediatR
        ↓
Pipeline Behaviors
        ↓
Vertical Slice + CQRS
        ↓
Application
        ↓
Domain / DDD
        ↓
Infrastructure
 ├── PostgreSQL
 ├── Redis
 └── Providers
        ↓
Outbox
        ↓
RabbitMQ
        ↓
Consumers / Workers
```

## Princípios

- Modular Monolith.
- Clean Architecture.
- DDD.
- CQRS.
- Vertical Slice.
- SOLID.
- Dependency Inversion.

## Backend

```text
src/
├── Domain/
├── Application/
├── Infrastructure/
├── Api/
├── Workers/
└── Contracts/
```

## Frontend

```text
src/
├── app/
├── components/
├── features/
├── services/
├── hooks/
├── types/
└── utils/
```

## Não usar inicialmente

Kubernetes, microservices, Kafka, Elasticsearch, Event Sourcing completo e Prometheus/Grafana como dependência obrigatória.
