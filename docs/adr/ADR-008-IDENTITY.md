# ADR-008 — ASP.NET Core Identity

## Status
Accepted

## Decisão
ASP.NET Core Identity será a base de autenticação e gestão de usuários.

## Autorização nos controllers (atualização 2026-09-24)

Os controllers autorizam por **role direto** — `[Authorize(Roles = "...")]` — e não
por policy de permissão. O mapeamento segue a matriz `RolePermissions`
(`User → Role → Permission`), sem alterar quem acessa o quê:

| Roles | Endpoints |
|---|---|
| `Admin` | gestão de usuários, roles, convites e auditoria |
| `Admin,User` | gestão de inventário; leitura de usuários |
| `Admin,User,Viewer` | leitura de inventário e monitoramento |

### Justificativa

Menos burocrático que policy por permissão (`RequireClaim`) com o mesmo acesso
resultante: as permissões de cada role em `RolePermissions` já definem esse
mapa. O `[Authorize(Roles)]` lê direto o role do usuário no cookie Identity.

### Consequências

- Policies HTTP por permissão (`RequireClaim` por valor de `Permission`) e as
  legadas `AdminOnly`/`UserAccess`/`ViewerAccess` foram removidas do
  `Program.cs` — nenhum controller as usa mais.
- As claims `permission` continuam existindo (sincronizadas por
  `IdentityUserRoleRepository`/bootstrap) e alimentam o
  `AuthorizationBehavior` do pipeline MediatR — 2ª camada nos handlers,
  invocada também por workers/outros chamadores que não passam pelo HTTP.
- `[Authorize]` sem roles (autenticado) permanece nos endpoints de sessão.
