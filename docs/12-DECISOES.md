# Decisões Arquiteturais Consolidadas

## Stack

### Frontend

React + TypeScript.

### Backend

ASP.NET Core / C#.

### Banco

PostgreSQL.

### Cache

Redis + fallback.

### Mensageria

RabbitMQ.

### Persistência de eventos

Outbox.

### ORM

EF Core.

### Queries especializadas

Dapper.

### CQRS

MediatR.

### Organização

Vertical Slice.

### Domínio

DDD.

### Arquitetura

Clean Architecture + Modular Monolith.

### Identity

ASP.NET Core Identity.

### Autorização

Role como fonte única, 3 níveis canônicos (`Admin`, `Operator`, `Viewer`).
Sem permissões: o enum `Permission`, `RolePermissions`, as claims `permission`
e as policies por permissão foram removidos (ADR-008 → ADR-016). A hierarquia é
declarada uma vez em `RoleAccess` e consumida pelas policies HTTP
(`Operate`/`Administer`) e pelo `AuthorizationBehavior` (`RequiredRoles`).
Endpoints de leitura usam `[Authorize]`; nenhum controller pode usar literal de
role ou policy por permissão.

### Resiliência

Polly.

### Observabilidade

OpenTelemetry + Serilog.

## Princípio de evolução

Primeiro correto → depois estável → depois rápido → depois escalar.
