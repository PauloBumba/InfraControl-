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
