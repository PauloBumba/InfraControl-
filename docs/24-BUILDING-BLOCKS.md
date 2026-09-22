# BuildingBlocks do InfraControl

## Objetivo

`InfraControl.BuildingBlocks` é o Shared Kernel técnico da solução. Ele contém contratos e abstrações reutilizáveis entre bounded contexts, seguindo a organização do Sentinel sem copiar dependências específicas da plataforma de alertas.

O projeto é deliberadamente pequeno e não referencia EF Core, PostgreSQL, Redis, RabbitMQ, ASP.NET Core ou qualquer módulo de negócio.

## Estrutura

```text
src/InfraControl.BuildingBlocks/
├── Application/
│   ├── CurrentUser.cs
│   ├── MediatorContracts.cs
│   └── Response/ApiResponse.cs
├── Domain/
│   ├── IDomainEvent.cs
│   └── RepositoryContracts.cs
├── Infrastructure/
│   └── InfrastructureContracts.cs
└── InfraControl.BuildingBlocks.csproj
```

## Contratos trazidos do Sentinel

| Área | Contratos | Uso |
| --- | --- | --- |
| Application | `ICommand`, `IQuery` | Padronizar slices CQRS |
| Application | `IAuthorizedRequest` | Declarar permissões exigidas por caso de uso |
| Application | `ICacheableQuery` | Declarar cache de queries sem acoplar ao Redis |
| Application | `ICurrentUserContext` | Expor usuário, roles e permissões atuais |
| Application | `ApiResponse<T>` | Envelope de resposta consistente |
| Domain | `IDomainEvent` | Contrato mínimo para eventos de domínio |
| Domain | `IRepository<TEntity>` | Porta genérica mínima de persistência |
| Domain | `IUnitOfWork` | Contrato de commit da aplicação |
| Infrastructure | `ICacheService` | Cache compartilhado com implementação fora do bloco |
| Infrastructure | `IDistributedLock` | Coordenação distribuída sem conhecer Redis |

## Integração atual

O contrato de evento existente em `InfraControl.Domain.Common.Events.IDomainEvent` estende o contrato do BuildingBlocks. O `IUnitOfWork` e o `ICacheService` atuais também estendem os contratos compartilhados, mantendo compatibilidade com o código existente e permitindo migração gradual.

O projeto Domain, Application e Infrastructure referenciam `InfraControl.BuildingBlocks`. A dependência é sempre direcionada para o núcleo compartilhado; o BuildingBlocks não referencia camadas externas.

## Regras

Não colocar no BuildingBlocks:

- entidades de Inventário, Discovery, Monitoring, Incidents ou Identity;
- regras de negócio específicas;
- DbContext ou configurações EF Core;
- clientes Redis, RabbitMQ, SNMP, SSH ou HTTP;
- controllers, endpoints ou DTOs específicos de um módulo;
- implementação concreta de autorização, cache ou lock.

Quando um contrato tiver comportamento específico de um bounded context, ele deve permanecer em `Application` desse módulo. O BuildingBlocks deve receber somente abstrações realmente compartilhadas.
