# ADR-031 — Vertical Slice na Application + DDD no Domain + SOLID na Infrastructure + TDD

## Status
Proposed

## Sumário

Refatorar **três camadas** (a separação de camadas Clean Architecture já está correta):
1. **Domain**: Organizar por **bounded contexts** (DDD) — não por tipo técnico
2. **Application**: Organizar por **features/use cases** (Vertical Slice) — não por tipo técnico
3. **Infrastructure**: Organizar por **bounded contexts** com princípios **SOLID** — agrupamento por contexto, não por tecnologia misturada
4. **Test-Driven Development (TDD)**: Escrever testes antes da implementação em todas as camadas

Api continua como está (já segue Clean Architecture corretamente).

**Nota sobre Clean Architecture:**
A separação de camadas já está correta (Dependency Rule do Uncle Bob respeitada — Domain não depende de ninguém externo). A refatoração é **organização interna** de cada camada, não mudança de dependências entre camadas.

**Objetivos:**
1. Domain alinhado com DDD (bounded contexts: Inventory, Identity, Network, etc.)
2. Application alinhada com Vertical Slice (use cases: CreateLocation, UpdateLocation, etc.)
3. Infrastructure alinhada com SOLID (Single Responsibility, Open/Closed, Liskov, Interface Segregation, Dependency Inversion)
4. Test-Driven Development (TDD) aplicado — testes escritos antes da implementação
5. Preservar pipeline MediatR (behaviors) sem breaking changes
6. Zero breaking changes na API pública

## Contexto

### DDD no Domain (O que é)

Domain-Driven Design organiza o domínio por **bounded contexts** — limites de contexto de negócio.

**Exemplo de bounded contexts:**
- **Inventory**: Locations, Racks, Devices, NetworkInterfaces
- **Identity**: Users, Invitations, Sessions, Mfa
- **Network**: Switches, SwitchPorts, Vlans, Connections
- **Monitoring**: Checks, Events, Results
- **Alerts**: Alert rules
- **Incidents**: Incident management
- **Notifications**: Notification delivery
- **Tasks**: Task management
- **Knowledge**: Knowledge base
- **Sla**: SLA policies
- **RemoteAccess**: Remote sessions

### Vertical Slice na Application (O que é)

Vertical Slice organiza código por **use case/funcionalidade** — não por tipo técnico.

### SOLID na Infrastructure (O que é)

SOLID são 5 princípios de design orientado a objetos:

1. **S - Single Responsibility Principle**: Cada classe deve ter uma única responsabilidade
2. **O - Open/Closed Principle**: Classes devem estar abertas para extensão, fechadas para modificação
3. **L - Liskov Substitution Principle**: Subclasses devem ser substituíveis por suas classes base
4. **I - Interface Segregation Principle**: Interfaces devem ser específicas, não genéricas
5. **D - Dependency Inversion Principle**: Depender de abstrações, não de implementações concretas

**Na Infrastructure do InfraControl:**
- Agrupar por bounded context (Inventory, Identity, Network) — não tudo junto
- Cada repositório tem responsabilidade única (SRP)
- Extensão via interfaces, não modificação direta (OCP)
- Interfaces específicas por contexto (ISP)
- Depender de interfaces definidas no Application (DIP)

### TDD (Test-Driven Development - O que é)

TDD é uma prática de desenvolvimento onde:
1. **Red**: Escrever um teste que falha (porque a funcionalidade ainda não existe)
2. **Green**: Implementar o código mínimo para fazer o teste passar
3. **Refactor**: Refatorar o código mantendo os testes verdes

**No InfraControl:**
- Escrever testes unitários antes de implementar cada use case
- Escrever testes de domínio antes de implementar entidades/agregados
- Escrever testes de infraestrutura antes de implementar repositórios
- Cobertura mínima de testes: 80% para camada core (Domain + Application)

**Benefícios do TDD:**
- Design melhor (código mais testável naturalmente)
- Documentação viva (testes documentam comportamento)
- Refatoração segura (testes protegem contra regressões)
- Feedback rápido (erros são detectados imediatamente)

**Exemplo de use cases:**
- CreateLocation, UpdateLocation, DeleteLocation, SearchLocations
- CreateDevice, UpdateDevice, DeleteDevice, SearchDevices
- CreateInvitation, AcceptInvitation, RejectInvitation

### Estrutura Atual (INCORRETA)

**Domain:**
```text
Domain/
├── Entities/                 # ❌ Separação por tipo técnico
│   ├── Location.cs
│   ├── Device.cs
│   ├── User.cs
│   └── (todas juntas)
├── Enums/                    # ❌ Separação por tipo técnico
├── Events/                   # ❌ Separação por tipo técnico
├── ValueObjects/             # ❌ Separação por tipo técnico
├── Common/                   # ✅ Building blocks
├── Devices/                  # ❌ Subpastas inconsistentes
├── Alerts/
└── (...)
```

**Application:**
```text
Application/
├── Inventory/
│   ├── Devices/
│   │   ├── Commands/         # ❌ Separação por tipo técnico
│   │   ├── Queries/
│   │   └── Ports/
│   └── Locations/
│       ├── Commands/
│       └── Queries/
└── Identity/
    └── (mesmo padrão)
```

**Infrastructure:**
```text
Infrastructure/
├── Persistence/
│   ├── Configurations/       # ❌ Todas configurações juntas (flat)
│   │   ├── LocationConfiguration.cs
│   │   ├── DeviceConfiguration.cs
│   │   ├── UserConfiguration.cs
│   │   └── (60+ arquivos juntos)
│   └── Migrations/
├── Repositories/             # ❌ Todos repositórios juntos (flat)
│   ├── LocationRepository.cs
│   ├── DeviceRepository.cs
│   ├── UserRepository.cs
│   └── (50+ arquivos juntos)
├── Auth/                     # ✅ OK (tecnologia específica)
├── Caching/                  # ✅ OK (tecnologia específica)
├── Discovery/               # ✅ OK (tecnologia específica)
├── Monitoring/              # ✅ OK (tecnologia específica)
├── RemoteAccess/            # ✅ OK (tecnologia específica)
└── Services/                # ⚠️ Mistura de responsabilidades
```

### Estrutura Alvo (CORRETA)

**Domain (DDD por bounded contexts):**
```text
Domain/
├── Common/                   # Building blocks compartilhados entre TODOS os contexts
│   ├── Entities/
│   │   ├── Entity.cs
│   │   └── AggregateRoot.cs
│   ├── Events/
│   │   ├── IDomainEvent.cs
│   │   └── DomainEvent.cs
│   └── ValueObjects/
│       └── ValueObject.cs
│
├── Inventory/                # Bounded Context: Inventory
│   ├── Locations/
│   │   ├── Location.cs
│   │   └── LocationCreated.cs (Domain Event)
│   ├── Racks/
│   │   ├── Rack.cs
│   │   └── RackCreated.cs
│   ├── Devices/
│   │   ├── Device.cs
│   │   ├── DeviceCreated.cs
│   │   └── DeviceProfileDefaults.cs
│   ├── NetworkInterfaces/
│   │   ├── NetworkInterface.cs
│   │   └── NetworkInterfaceCreated.cs
│   ├── MacAddresses/
│   │   └── MacAddress.cs
│   ├── IpAddresses/
│   │   └── IpAddress.cs
│   └── Common/               # Compartilhado dentro do bounded context Inventory
│
├── Identity/                 # Bounded Context: Identity
│   ├── Users/
│   │   ├── User.cs
│   │   ├── UserCreated.cs
│   │   └── UserUpdated.cs
│   ├── Invitations/
│   │   ├── Invitation.cs
│   │   └── InvitationAccepted.cs
│   ├── Sessions/
│   │   ├── UserSession.cs
│   │   └── SessionCreated.cs
│   └── Mfa/
│       └── UserMfa.cs
│
├── Network/                  # Bounded Context: Network
│   ├── Switches/
│   │   ├── SwitchPort.cs
│   │   └── SwitchPortCreated.cs
│   ├── Vlans/
│   │   ├── Vlan.cs
│   │   └── VlanCreated.cs
│   └── Connections/
│       └── Connection.cs
│
├── Monitoring/               # Bounded Context: Monitoring
│   ├── Checks/
│   │   ├── MonitoringCheck.cs
│   │   └── MonitoringCheckCreated.cs
│   ├── Events/
│   │   ├── MonitoringEvent.cs
│   │   └── MonitoringEventOccurred.cs
│   └── Results/
│       └── MonitoringResult.cs
│
├── Alerts/                   # Bounded Context: Alerts
│   └── Alert.cs
│
├── Incidents/                # Bounded Context: Incidents
│   ├── Incident.cs
│   └── IncidentAlert.cs
│
├── Notifications/            # Bounded Context: Notifications
│   ├── Notification.cs
│   └── NotificationDelivery.cs
│
├── Tasks/                    # Bounded Context: Tasks
│   ├── TaskItem.cs
│   ├── TaskComment.cs
│   └── TaskAssignee.cs
│
├── Knowledge/                # Bounded Context: Knowledge
│   ├── KnowledgeArticle.cs
│   └── KnowledgeCategory.cs
│
├── Sla/                      # Bounded Context: SLA
│   ├── SlaPolicy.cs
│   └── Slo.cs
│
├── RemoteAccess/             # Bounded Context: Remote Access
│   ├── RemoteSession.cs
│   └── DeviceCredential.cs
│
├── Discovery/                 # Bounded Context: Discovery
│   ├── DiscoveryRun.cs
│   └── DiscoveryResult.cs
│
├── Enums/                    # Enums compartilhados entre bounded contexts
│   ├── DeviceType.cs
│   ├── AlertSeverity.cs
│   ├── TaskState.cs
│   └── (...)
│
└── ValueObjects/             # ValueObjects compartilhados entre bounded contexts
    ├── IpAddress.cs
    └── MacAddress.cs
```

**Infrastructure (SOLID por bounded contexts):**
```text
Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/                # Agrupado por bounded context (SOLID - SRP)
│   │   ├── Inventory/
│   │   │   ├── LocationConfiguration.cs
│   │   │   ├── RackConfiguration.cs
│   │   │   ├── DeviceConfiguration.cs
│   │   │   ├── NetworkInterfaceConfiguration.cs
│   │   │   ├── MacAddressConfiguration.cs
│   │   │   └── IpAddressConfiguration.cs
│   │   ├── Identity/
│   │   │   ├── UserConfiguration.cs
│   │   │   ├── InvitationConfiguration.cs
│   │   │   ├── UserMfaConfiguration.cs
│   │   │   └── UserSessionConfiguration.cs
│   │   ├── Network/
│   │   │   ├── SwitchPortConfiguration.cs
│   │   │   ├── VlanConfiguration.cs
│   │   │   └── ConnectionConfiguration.cs
│   │   ├── Monitoring/
│   │   │   ├── MonitoringCheckConfiguration.cs
│   │   │   ├── MonitoringEventConfiguration.cs
│   │   │   └── MonitoringResultConfiguration.cs
│   │   ├── Alerts/
│   │   │   └── AlertConfiguration.cs
│   │   ├── Incidents/
│   │   │   └── IncidentConfiguration.cs
│   │   ├── Notifications/
│   │   │   └── NotificationConfiguration.cs
│   │   ├── Tasks/
│   │   │   └── TaskConfiguration.cs
│   │   ├── Knowledge/
│   │   │   └── KnowledgeArticleConfiguration.cs
│   │   ├── Sla/
│   │   │   └── SlaPolicyConfiguration.cs
│   │   ├── RemoteAccess/
│   │   │   └── RemoteSessionConfiguration.cs
│   │   └── Discovery/
│   │       └── DiscoveryRunConfiguration.cs
│   └── Migrations/
│
├── Repositories/                    # Agrupado por bounded context (SOLID - ISP)
│   ├── Inventory/
│   │   ├── LocationRepository.cs
│   │   ├── RackRepository.cs
│   │   ├── DeviceRepository.cs
│   │   ├── NetworkInterfaceRepository.cs
│   │   ├── MacAddressRepository.cs
│   │   └── IpAddressRepository.cs
│   ├── Identity/
│   │   ├── UserRepository.cs
│   │   ├── InvitationRepository.cs
│   │   └── UserSessionRepository.cs
│   ├── Network/
│   │   ├── SwitchPortRepository.cs
│   │   ├── VlanRepository.cs
│   │   └── ConnectionRepository.cs
│   ├── Monitoring/
│   │   ├── MonitoringCheckRepository.cs
│   │   ├── MonitoringEventRepository.cs
│   │   └── MonitoringResultRepository.cs
│   ├── Alerts/
│   │   └── AlertRepository.cs
│   ├── Incidents/
│   │   └── IncidentRepository.cs
│   ├── Notifications/
│   │   ├── NotificationRepository.cs
│   │   └── NotificationDeliveryRepository.cs
│   ├── Tasks/
│   │   ├── TaskRepository.cs
│   │   └── TaskCommentRepository.cs
│   ├── Knowledge/
│   │   ├── KnowledgeArticleRepository.cs
│   │   └── KnowledgeCategoryRepository.cs
│   ├── Sla/
│   │   ├── SlaPolicyRepository.cs
│   │   └── SloRepository.cs
│   ├── RemoteAccess/
│   │   └── RemoteSessionRepository.cs
│   └── Discovery/
│       └── DiscoveryRunRepository.cs
│
├── Auth/                            # ✅ Manter como está (tecnologia específica)
│   ├── IdentityAuthenticationService.cs
│   ├── IdentityAdministrationService.cs
│   ├── IdentityUser.cs
│   └── (...)
│
├── Caching/                         # ✅ Manter como está (tecnologia específica)
│   ├── HybridCacheService.cs
│   ├── RedisCacheService.cs
│   └── (...)
│
├── Discovery/                       # ✅ Manter como está (tecnologia específica)
│   ├── LldpDiscoveryProvider.cs
│   ├── CdpDiscoveryProvider.cs
│   └── (...)
│
├── Monitoring/                      # ✅ Manter como está (tecnologia específica)
│   ├── PingMonitoringProvider.cs
│   ├── SnmpSystemMonitoringProvider.cs
│   └── (...)
│
├── RemoteAccess/                    # ✅ Manter como está (tecnologia específica)
│   ├── SshRemoteProvider.cs
│   ├── RdpRemoteProvider.cs
│   └── (...)
│
└── Services/                        # ✅ Manter como está (serviços transversais)
    ├── DomainEventService.cs
    ├── OutboxRepository.cs
    ├── MassTransitEventBus.cs
    ├── FeatureFlagProvider.cs
    └── (...)
```

**Application (Vertical Slice por use case):**
```text
Application/
├── Common/                   # Behaviors, interfaces compartilhadas
│   ├── Behaviors/
│   ├── Interfaces/
│   └── DTOs/
│
└── Features/                 # ← VERTICAL SLICE
    ├── Inventory/
    │   ├── Locations/
    │   │   ├── CreateLocation/
    │   │   ├── UpdateLocation/
    │   │   ├── DeleteLocation/
    │   │   ├── SearchLocations/
    │   │   ├── GetLocation/
    │   │   └── Common/
    │   │       ├── ILocationRepository.cs
    │   │       └── LocationDto.cs
    │   ├── Devices/
    │   │   ├── CreateDevice/
    │   │   ├── UpdateDevice/
    │   │   ├── DeleteDevice/
    │   │   ├── SearchDevices/
    │   │   ├── GetDevice/
    │   │   ├── UpdateDeviceProfile/
    │   │   └── Common/
    │   │       ├── IDeviceRepository.cs
    │   │       └── DeviceDto.cs
    │   └── (...)
    ├── Identity/
    │   ├── Invitations/
    │   ├── Authentication/
    │   ├── Users/
    │   ├── Sessions/
    │   └── Mfa/
    └── (...)
```

## Regras de Migração

### 1. Domain (DDD por Bounded Contexts)

**Regras:**
- Remover `Entities/`, `Enums/`, `Events/`, `ValueObjects/` flat
- Organizar tudo por **bounded context** (`Inventory/`, `Identity/`, `Network/`, etc.)
- Dentro de cada bounded context, organizar por **agregado** (`Locations/`, `Devices/`, `Users/`, etc.)
- `Domain/Common/` fica para building blocks compartilhados entre TODOS os contexts
- Enums e ValueObjects compartilhados entre contexts ficam em `Domain/Enums/` e `Domain/ValueObjects/`

**Exemplo de migração:**
```text
ANTES:
Domain/
├── Entities/
│   ├── Location.cs
│   ├── Device.cs
│   └── User.cs
├── Events/
│   ├── LocationCreated.cs
│   └── UserCreated.cs
└── Devices/
    └── DeviceProfileDefaults.cs

DEPOIS:
Domain/
├── Common/ (building blocks)
├── Inventory/
│   ├── Locations/
│   │   ├── Location.cs
│   │   └── LocationCreated.cs
│   ├── Devices/
│   │   ├── Device.cs
│   │   ├── DeviceCreated.cs
│   │   └── DeviceProfileDefaults.cs
└── Identity/
    └── Users/
        ├── User.cs
        └── UserCreated.cs
```

### 2. Application (Vertical Slice por Use Case)

**Regras:**
- Migrar de `Inventory/Devices/Commands/` para `Features/Inventory/Devices/CreateDevice/`
- One use case = one folder
- Command + Handler + Validator + DTO ficam juntos
- Interfaces de repositório e DTOs compartilhados → `Features/{Feature}/{Entity}/Common/`
- **PRESERVAR CONTRATOS DO PIPELINE MEDIATR** (BaseCommand, BaseQuery, IAuthorizedRequest, etc.)

### 3. Infrastructure (SOLID por Bounded Contexts)

**Regras:**
- Agrupar `Persistence/Configurations/` por bounded context (SRP - Single Responsibility)
- Agrupar `Repositories/` por bounded context (ISP - Interface Segregation)
- Manter estrutura por tecnologia (`Auth/`, `Caching/`, `Discovery/`, `Monitoring/`, `RemoteAccess/`) — são tecnologias específicas
- Manter `Services/` para serviços transversais (DomainEventService, OutboxRepository, etc.)
- Atualizar DI (DependencyInjection.cs) para novos namespaces
- Aplicar SOLID:
  - **SRP**: Cada repositório/ configuração tem responsabilidade única
  - **OCP**: Extensão via interfaces, não modificação direta
  - **LSP**: Implementações podem ser substituídas por suas interfaces
  - **ISP**: Interfaces específicas por contexto (não genéricas)
  - **DIP**: Depender de interfaces definidas no Application

**Exemplo de migração:**
```text
ANTES:
Infrastructure/
├── Persistence/
│   └── Configurations/
│       ├── LocationConfiguration.cs
│       ├── DeviceConfiguration.cs
│       ├── UserConfiguration.cs
│       └── (60+ arquivos juntos)
└── Repositories/
    ├── LocationRepository.cs
    ├── DeviceRepository.cs
    ├── UserRepository.cs
    └── (50+ arquivos juntos)

DEPOIS:
Infrastructure/
├── Persistence/
│   └── Configurations/
│       ├── Inventory/
│       │   ├── LocationConfiguration.cs
│       │   └── DeviceConfiguration.cs
│       └── Identity/
│           └── UserConfiguration.cs
└── Repositories/
    ├── Inventory/
    │   ├── LocationRepository.cs
    │   └── DeviceRepository.cs
    └── Identity/
        └── UserRepository.cs
```

### 4. Api

**Regras:**
- Opcional: Agrupar controllers por bounded context
- Apenas atualizar namespaces dos handlers chamados nos controllers

### 5. Pipeline MediatR (CRÍTICO)

**Regras:**
- **NÃO ALTERAR** contratos em `BuildingBlocks/Application/`
- **NÃO ALTERAR** behaviors em `Application/Common/Behaviors/`
- Commands/Queries continuam herdando das mesmas bases
- Apenas namespaces mudam, código NÃO muda

### 6. Test-Driven Development (TDD)

**Regras:**
- Para cada novo use case/migration: escrever teste PRIMEIRO (Red)
- Implementar código mínimo para teste passar (Green)
- Refatorar mantendo testes verdes (Refactor)
- Cobertura mínima: 80% para Domain + Application
- Testes unitários devem ser rápidos (< 100ms por teste)
- Testes de integração devem ser isolados (container Docker se necessário)

**Ciclo TDD por camada:**

**Domain:**
1. Escrever teste de entidade/agregado (ex: `LocationTests.cs`)
2. Implementar entidade com comportamento mínimo
3. Refatorar se necessário
4. Testes unitários de Domain não devem depender de infraestrutura

**Application:**
1. Escrever teste de command/handler (ex: `CreateLocationCommandTests.cs`)
2. Implementar command + handler com mocks de repositório
3. Refatorar se necessário
4. Testes unitários de Application devem usar mocks

**Infrastructure:**
1. Escrever teste de repositório (ex: `LocationRepositoryTests.cs`)
2. Implementar repositório com DbContext in-memory ou PostgreSQL test
3. Refatorar se necessário
4. Testes de integração podem usar banco real (Docker)

**Estrutura de testes:**
```text
tests/
├── InfraControl.UnitTests/
│   ├── Domain/
│   │   ├── Inventory/
│   │   │   ├── LocationTests.cs
│   │   │   └── DeviceTests.cs
│   │   └── Identity/
│   │       └── UserTests.cs
│   └── Application/
│       ├── Features/
│       │   ├── Inventory/
│       │   │   ├── Locations/
│       │   │   │   ├── CreateLocationCommandTests.cs
│       │   │   │   └── UpdateLocationCommandTests.cs
│       │   │   └── Devices/
│       │   └── Identity/
│   └── Infrastructure/
│       ├── Inventory/
│       │   └── LocationRepositoryTests.cs
│       └── Identity/
├── InfraControl.IntegrationTests/
│   └── (testes de integração com banco real)
└── InfraControl.ArchitectureTests/
    └── (regras de arquitetura)
```

## Plano de Migração em Fases

### Fase 0: Preparação
- Criar branch `pre-refactor-ddd-vertical-slice-solid-tdd`
- Backup do código atual
- Análise de dependências entre bounded contexts
- Documentar bounded contexts atuais
- Configurar ferramenta de cobertura de testes (dotnet cover)
- Estabelecer baseline de cobertura atual

### Fase 1: Domain (DDD + TDD) - Piloto Inventory
- **TDD**: Escrever testes para `Location` (Red)
- Migrar `Domain/Inventory/Locations/` para bounded context
- Implementar `Location` com comportamento mínimo (Green)
- **TDD**: Escrever testes para `Rack` (Red)
- Migrar `Domain/Inventory/Racks/`
- Implementar `Rack` (Green)
- **TDD**: Escrever testes para `Device` (Red)
- Migrar `Domain/Inventory/Devices/`
- Implementar `Device` (Green)
- Migrar eventos de `Domain/Events/` para `Domain/Inventory/{Aggregate}/`
- Atualizar namespaces em Infrastructure (Configurations, Repositories)
- Validar build e testes
- Validar cobertura de testes (meta: 80%+)

### Fase 2: Application (Vertical Slice + TDD) - Piloto Inventory/Locations
- **TDD**: Escrever testes para `CreateLocationCommand` (Red)
- Migrar `Application/Inventory/Locations/` para `Features/Inventory/Locations/`
- Criar estrutura por use case (CreateLocation, UpdateLocation, etc.)
- Implementar `CreateLocationCommandHandler` com mocks (Green)
- **TDD**: Escrever testes para `UpdateLocationCommand` (Red)
- Implementar `UpdateLocationCommandHandler` (Green)
- **TDD**: Escrever testes para `DeleteLocationCommand` (Red)
- Implementar `DeleteLocationCommandHandler` (Green)
- **TDD**: Escrever testes para `SearchLocationsQuery` (Red)
- Implementar `SearchLocationsQueryHandler` (Green)
- Migrar Common (ILocationRepository, LocationDto)
- Atualizar Controllers para novos namespaces
- Validar pipeline MediatR (behaviors)
- Validar build e testes
- Validar cobertura de testes (meta: 80%+)
- **Checkpoint**: Aprovar antes de continuar

### Fase 3: Domain (DDD + TDD) - Resto dos Bounded Contexts
- Para cada bounded context (Identity, Network, Monitoring, etc.):
  - **TDD**: Escrever testes primeiro (Red)
  - Migrar entidades para bounded context
  - Implementar comportamento mínimo (Green)
  - Refatorar se necessário
- Remover `Domain/Entities/`, `Domain/Events/`, `Domain/ValueObjects/` flat (se vazios)
- Validar cada bounded context
- Validar cobertura de testes global

### Fase 4: Application (Vertical Slice + TDD) - Resto dos Features
- Para cada feature (Identity, Network, Monitoring, etc.):
  - **TDD**: Escrever testes para cada use case (Red)
  - Migrar para `Features/{Feature}/`
  - Implementar handlers com mocks (Green)
  - Refatorar se necessário
- Remover estrutura antiga (`Inventory/`, `Identity/`, etc. vazios)
- Atualizar todos Controllers
- Atualizar todos testes
- Validar build e testes
- Validar cobertura de testes (meta: 80%+)

### Fase 5: Infrastructure (SOLID + TDD) - OBRIGATÓRIO
- **TDD**: Escrever testes para repositórios (Red)
- Agrupar `Persistence/Configurations/` por bounded context
- Agrupar `Repositories/` por bounded context
- Implementar repositórios (Green)
- Atualizar DI (DependencyInjection.cs) para novos namespaces
- Validar build e testes
- Validar SOLID: verificar SRP, OCP, LSP, ISP, DIP
- Validar cobertura de testes de Infrastructure

### Fase 6: Api (Opcional - Agrupamento)
- Agrupar `Controllers/` por bounded context
- Atualizar rotas se necessário
- Validar build e testes

### Fase 7: Limpeza Final + Validação TDD
- Remover pastas vazias
- Atualizar documentação (README.md, docs/01-ARQUITETURA.md)
- Atualizar regras de arquitetura (ArchitectureTests)
- Atualizar ADR-007 (Vertical Slice) para refletir implementação correta
- Validar build completo
- Validar testes completos
- Validar cobertura de testes final (meta: 80%+ Domain + Application)
- Validar smoke tests da API
- Validar pipeline MediatR (behaviors)
- Commit final

## Consequências

### Positivas

1. **Domain alinhado com DDD**: Bounded contexts claros (Inventory, Identity, Network, etc.)
2. **Application alinhada com Vertical Slice**: Organização por use case, alta coesão
3. **Infrastructure alinhada com SOLID**: Single Responsibility, Open/Closed, Liskov, Interface Segregation, Dependency Inversion
4. **TDD aplicado**: Testes escritos antes da implementação, design melhor, refatoração segura
5. **Alta cobertura de testes**: Meta 80%+ para camadas core (Domain + Application)
6. **Navegação intuitiva**: Para implementar "Create Device", abre uma pasta
7. **Manutenção localizada**: Alterações concentram-se em um bounded context/slice
8. **Prepara para microservices**: Bounded contexts podem evoluir para serviços independentes
9. **Alinha com ADR-007**: Vertical Slice implementado corretamente
10. **Código mais manutenível**: SOLID + TDD facilitam testes e extensões
11. **Clean Architecture respeitada**: Dependency Rule do Uncle Bob mantida

### Negativas

1. **Migração custosa**: Requer mover muitos arquivos em três camadas
2. **Alto esforço**: Várias fases, cada bounded context, feature e repositório precisa de migração
3. **TDD adiciona tempo**: Escrever testes antes da implementação demanda mais tempo inicial
4. **Risco de regressão**: Namespaces mudam em Domain, Application e Infrastructure
5. **Curva de aprendizado**: Equipe precisa entender nova estrutura, SOLID e TDD
6. **Possível fragmentação**: Muitas pastas pequenas (mitigado agrupando por bounded context)

## Alternativas Consideradas

### 1. Manter estrutura atual
- **Prós**: Menos trabalho imediato
- **Contras**: Continua contradizendo DDD, Vertical Slice e SOLID, baixa coesão, sem TDD
- **Veredito**: Rejeitada — o problema persiste

### 2. Refatorar só Application (Vertical Slice)
- **Prós**: Menos esforço que refatoração completa
- **Contras**: Domain continua sem bounded contexts claros, Infrastructure continua flat, sem TDD
- **Veredito**: Rejeitada — parcial, não resolve problema completo

### 3. Refatorar só Domain (DDD)
- **Prós**: Bounded contexts claros
- **Contras**: Application continua sem Vertical Slice, Infrastructure continua sem SOLID, sem TDD
- **Veredito**: Rejeitada — parcial, não resolve problema completo

### 4. Refatorar Domain + Application (sem Infrastructure)
- **Prós**: Maior parte do problema resolvido
- **Contras**: Infrastructure continua flat, viola SOLID (60+ arquivos juntos), sem TDD
- **Veredito**: Rejeitada — parcial, Infrastructure precisa de SOLID

### 5. Refatorar tudo sem TDD
- **Prós**: Refatoração completa mais rápida
- **Contras**: Sem benefícios do TDD (design melhor, refatoração segura, alta cobertura)
- **Veredito**: Rejeitada — TDD é fundamental para qualidade e manutenibilidade

## Referências

- [Clean Architecture — Robert C. Martin (Uncle Bob)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [The Clean Architecture Dependency Rule](https://www.informit.com/articles/article.aspx?p=2832399)
- [Domain-Driven Design — Eric Evans](https://www.domainlanguage.com/ddd/)
- [Vertical Slice Architecture — Jimmy Bogard](https://jimmybogard.com/vertical-slice-architecture/)
- [Vertical Slice Architecture vs Traditional Layered Architecture](https://tech-tech.life/2024/07/06/vertical-slice-architecture-vs-traditional-layered-architecture-a-comprehensive-comparison/)
- [SOLID Principles — Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2011/11/22/SOLID.html)
- [Test-Driven Development — Kent Beck](https://www.amazon.com/Test-Driven-Development-Kent-Beck/dp/0321146530)
- [Minimal Clean Architecture — Ardalis](https://github.com/ardalis/cleanarchitecture/blob/main/docs/content/minimal-clean-architecture.md)
- [The Best Way to Structure .NET Projects: Clean Architecture + Vertical Slices](https://se.dosibridge.com/articles/architecture/the-best-way-to-structure-your-dotnet-projects-with-clean-architecture-and-vertical-slices/)
- ADR-001 — Modular Monolith
- ADR-006 — CQRS
- ADR-007 — Vertical Slice (existente, não implementado corretamente)
