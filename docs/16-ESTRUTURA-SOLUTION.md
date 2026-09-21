# Estrutura da Solution

```text
src/
├── Domain/
├── Application/
├── Infrastructure/
├── Api/
├── Workers/
└── Contracts/

tests/
├── Unit/
├── Integration/
├── Architecture/
├── E2E/
├── Concurrency/
└── Security/
```

## Domain

O domínio segue o padrão do Sentinel, com responsabilidades organizadas diretamente por tipo:

```text
InfraControl.Domain/
├── Entities/
├── Enums/
├── Events/
├── ValueObjects/
└── Common/              # building blocks compartilhados
```

Entidades, enums e eventos de negócio não devem ser colocados em `Common`.

## Application

Organizada por Vertical Slice:

```text
Devices/
├── CreateDevice/
├── UpdateDevice/
├── GetDevice/
└── SearchDevices/

Monitoring/
├── RunCheck/
├── GetStatus/
└── GetMetrics/

Topology/
├── GetTopology/
└── DiscoverTopology/
```

Cada slice deve conter somente o necessário para aquela funcionalidade.
