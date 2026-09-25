# BuildingBlocks do InfraControl

## Objetivo

`InfraControl.BuildingBlocks` é o Shared Kernel técnico da solução. Ele contém contratos e abstrações reutilizáveis entre bounded contexts, seguindo a organização do Sentinel sem copiar dependências específicas da plataforma de alertas.

O projeto é deliberadamente pequeno e não referencia EF Core, PostgreSQL, Redis, RabbitMQ, ASP.NET Core ou qualquer módulo de negócio.

## Estrutura

```text
src/InfraControl.BuildingBlocks/
├── Application/
│   ├── BaseCommand.cs
│   ├── BaseQuery.cs
│   ├── CacheKeyBuilder.cs
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
| Application | `BaseCommand`, `BaseQuery<T>` | Bases concretas de comandos e queries (cache opt-in) |
| Application | `IAuthorizedRequest` | Declarar permissões exigidas por caso de uso |
| Application | `ICacheableQuery` | Declarar cache de queries sem acoplar ao Redis |
| Application | `CacheKeyBuilder` | Chave de cache por usuário + hash de parâmetros |
| Application | `ICurrentUserContext` | Expor usuário, roles e permissões atuais |
| Application | `ApiResponse<T>` + `ApiResponse` (estático) | Envelope de resposta consistente |
| Domain | `IDomainEvent` | Contrato mínimo para eventos de domínio |
| Domain | `IRepository<TEntity>` | Porta genérica mínima de persistência |
| Domain | `IUnitOfWork` | Contrato de commit da aplicação |
| Infrastructure | `ICacheService` | Cache compartilhado com implementação fora do bloco |
| Infrastructure | `IDistributedLock` | Coordenação distribuída sem conhecer Redis |

## Integração atual

O contrato de evento existente em `InfraControl.Domain.Common.Events.IDomainEvent` estende o contrato do BuildingBlocks. O `IUnitOfWork` e o `ICacheService` atuais também estendem os contratos compartilhados, mantendo compatibilidade com o código existente e permitindo migração gradual.

O projeto Domain, Application e Infrastructure referenciam `InfraControl.BuildingBlocks`. A dependência é sempre direcionada para o núcleo compartilhado; o BuildingBlocks não referencia camadas externas.

## Padrão de uso na aplicação (F0-T022)

Todo slice CQRS novo deve seguir este padrão; ele é imposto pelos testes de arquitetura em `tests/InfraControl.ArchitectureTests/Rules/BuildingBlocksStandardizationTests.cs`:

| Elemento | Padrão obrigatório |
| --- | --- |
| Command | Estende `BaseCommand` / `BaseCommand<T>` (nunca `IRequest` cru) |
| Query | Estende `BaseQuery<T>` (cacheável) ou implementa `IQuery<T>` |
| Caso administrativo | Implementa `IAuthorizedRequest` declarando `RequiredPermissions` com nomes do enum `Permission` |
| Porta de repositório | Estende `IRepository<TEntity>`; adaptador implementa `GetByIdAsync`/`AddAsync`/`Remove` |
| Resposta HTTP | Controllers respondem com o envelope `ApiResponse` (`{ isSuccess, data, errors }`); sucesso sem conteúdo continua HTTP 204 sem corpo |

Comportamentos do pipeline ligados a esses contratos:

- `CachingBehavior` só age sobre `IQuery<T>` + `ICacheableQuery` e monta a chave com `CacheKeyBuilder` (usuário, roles e hash dos parâmetros), evitando colisão entre usuários.
- `AuthorizationBehavior` só age sobre `IAuthorizedRequest` e compara `RequiredPermissions` com as permissões de `ICurrentUserContext`.
- `TransactionBehavior` pula queries (`IQuery<T>`) e commita o restante via `IUnitOfWork`.
- O `ApiResponse` estático (sem argumento de tipo) existe porque C# não infere argumentos de tipo em membros estáticos de tipos genéricos — usar `ApiResponse.Ok(resultado)` / `ApiResponse.Fail(erros)` nos controllers.

## Regras

Não colocar no BuildingBlocks:

- entidades de Inventário, Discovery, Monitoring, Incidents ou Identity;
- regras de negócio específicas;
- DbContext ou configurações EF Core;
- clientes Redis, RabbitMQ, SNMP, SSH ou HTTP;
- controllers, endpoints ou DTOs específicos de um módulo;
- implementação concreta de autorização, cache ou lock.

Quando um contrato tiver comportamento específico de um bounded context, ele deve permanecer em `Application` desse módulo. O BuildingBlocks deve receber somente abstrações realmente compartilhadas.

## Global usings (Fase 1 — 2026-09-25)

Para não poluir cada arquivo novo com os mesmos `using`, os namespaces de uso transversal são registrados nos `.csproj` via `<Using Include="..."/>` (mesmo padrão do `Xunit` que já existia nos projetos de teste):

| Projeto | Namespaces registrados |
| --- | --- |
| `InfraControl.Application` | `MediatR`, `InfraControl.BuildingBlocks.Application`, `InfraControl.Application.Common.Interfaces`, `InfraControl.Domain.Authorization` |
| `InfraControl.Api` | `MediatR`, `Microsoft.AspNetCore.Mvc`, `Microsoft.AspNetCore.Authorization`, `InfraControl.BuildingBlocks.Application.Response` |
| `InfraControl.Infrastructure` | `Microsoft.EntityFrameworkCore` |
| `InfraControl.UnitTests` | `FluentAssertions`, `Microsoft.EntityFrameworkCore`, `Microsoft.Data.Sqlite`, `InfraControl.Infrastructure.Persistence` |

Regras:

- `ImplicitUsings` continua `enable` em todos os projetos (System/LINQ/etc. vêm de graça).
- `using` explícito repetido num `.cs` convive sem erro com o global — arquivos antigos não precisam ser reescritos (Fase 2, limpeza cosmética, é opcional e futura).
- Ao registrar namespace novo em csproj: build + suíte completa (nenhum CS0104/CS0121 de ambiguidade).
- Namespaces de módulo (ex.: `Inventory.Devices.Ports`) **não** entram nos global usings — ficam explícitos no arquivo para manter visível de onde cada dependência de domínio vem.

## Registro automático de repositórios (F0-T024 — 2026-09-25)

`RepositoryRegistrar.AddRepositoryRegistrations(interfaceAssembly, implementationAssembly)` (`InfraControl.Infrastructure.Services`) varre os assemblies e registra os pares automaticamente — no espírito do `RegisterServicesFromAssembly` do MediatR:

- **Interface**: tipo `I*Repository` (não genérico) do assembly de contratos (`Application`).
- **Implementação**: classe `*Repository` não abstrata do assembly de implementações (`Infrastructure`) que implemente a interface (casa por atribuição, não por nome — cobre casos como `IUserRoleRepository` → `IdentityUserRoleRepository`).
- **Lifetime**: `Scoped` (igual ao registro manual anterior).
- **Fail-fast**: 0 ou mais de 1 implementações → `InvalidOperationException` **no startup**, nunca em runtime na primeira requisição.
- **Escape hatch**: interface com registro manual existente é pulada (`TryAdd`) — o manual prevalece (lifetime especial, fábrica).

Chamada única no `DependencyInjection.AddInfrastructure` (as 11 linhas manuais `AddScoped<I*Repository, *Repository>()` foram removidas):

```csharp
services.AddRepositoryRegistrations(
    typeof(IDeviceRepository).Assembly,
    typeof(DeviceRepository).Assembly);
```

Consequência prática: **criou `IVlanRepository`/`VlanRepository` seguindo o padrão de nome → registra sozinho, sem linha nova.** Continuam manuais por não serem pares simples: `IUnitOfWork`, `IConnectionFactory`, `IIdempotencyStore`, `ICacheService`, `IMonitoringProvider` (3 implementações simultâneas), `IEventBus` e ports que não são repositório (ex.: `IInvitationAccountPort`).

Coberto por 4 testes em `tests/InfraControl.UnitTests/Infrastructure/RepositoryRegistrationTests.cs` (registro completo Scoped, fail-fast com nome da interface ausente, prevalência do manual, resolução pelo provider).
