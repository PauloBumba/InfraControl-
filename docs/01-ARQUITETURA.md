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

O projeto segue a organização de domínio usada no Sentinel: entidades, enums, eventos e value objects ficam diretamente sob `InfraControl.Domain`. `Common` é reservado aos building blocks compartilhados, como `Entity`, `AggregateRoot`, eventos base e value objects base; não deve receber entidades de negócio.

```text
InfraControl.Domain/
├── Entities/
├── Enums/
├── Events/
├── ValueObjects/
└── Common/             # building blocks compartilhados
```

Na Infrastructure, a organização segue a convenção do Sentinel e não agrupa adaptadores em `Common`:

```text
InfraControl.Infrastructure/
├── Auth/
├── Caching/
├── Persistence/
│   ├── Configurations/
│   └── Migrations/
├── Repositories/
└── Services/
```

`Persistence` concentra DbContext, factories, configurações e migrations; `Repositories` contém adapters de persistência; `Auth` contém a integração técnica de Identity; e `Services` contém mensageria, eventos e serviços transversais.

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
