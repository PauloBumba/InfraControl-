# ADR-016 — Autorização por roles (fonte única)

## Status
Accepted

## Decisão

A autorização é padronizada em **3 roles canônicas**, que passam a ser a **fonte
única** do acesso: `Admin`, `Operator` e `Viewer`. O modelo de permissões
(`Permission`, `RolePermissions`, claims `permission`, policies HTTP por
permissão) é **removido**.

`RoleAccess` (`src/InfraControl.Domain/Enums/RoleAccess.cs`) é a fonte única da
hierarquia e é usado pelos **dois** pontos de checagem — policies HTTP e pipeline
MediatR — para que não divirjam:

| Nível      | HTTP                          | MediatR (`RequiredRoles`) | Roles                       |
|------------|-------------------------------|----------------------------|-----------------------------|
| Ler        | `[Authorize]`                 | `RoleAccess.Read`          | Viewer, Operator, Admin     |
| Operar     | `[Authorize(Policy = Operate)]`   | `RoleAccess.Operate`   | Operator, Admin             |
| Administrar| `[Authorize(Policy = Administer)]` | `RoleAccess.Administer` | Admin                       |

A leitura **não** é represented por lista vazia: `RoleAccess.Read` lista
explicitamente as três roles, para que a decisão "qualquer role autenticada" seja
um fato declarado e testável, e não um efeito colateral de um `Count == 0`.

## Contexto

Até 2026-09-27 o sistema tinha **dois** modelos de autorização coexistindo:

- **Role** — 3 roles em `UserRole` (`Admin`, `User`, `Viewer`) com a matriz
  `RolePermissions` (role → permissões).
- **Permissão** — 21 permissões em `Permission`, emitida como claim `permission`
  no login e validada no pipeline MediatR.

### Estado real medido antes da mudança

| Item                                                    | Quantidade |
|---------------------------------------------------------|-----------:|
| `[Authorize(Roles = "...")]` com literais de role       | 46 (18 `Admin,User,Viewer` / 18 `Admin,User` / 10 `Admin`) |
| `[Authorize(Policy = "<Permission>")]` nos controllers    | 80        |
| `RequiredPermissions` em requests da Application         | 126 (68 arquivos) |
| Policies por permissão geradas por `AddPermissionPolicies` | 21      |

### Problema

1. **Drift documentado**: o ADR-008 (atualização 2026-09-24) havia decidido
   remover as policies por permissão do `Program.cs` e passar a autorizar por
   `[Authorize(Roles)]`. Os slices F6/F7/F8 **reintroduziram** `AddPermissionPolicies`
   e 12 slices passaram a usar `[Authorize(Policy = "<Permission>")]`. Passaram a
   existir dois estilos de autorização ao mesmo tempo, sem que o ADR-008 fosse
   revisto.
2. **Indireção sem ganho**: role → permissão → claim → validação, quando
   role → validação basta.
3. **Literal de role como fonte de verdade**: 46 strings em controllers
   (`"Admin,User,Viewer"`) que precisam casar com a matriz `RolePermissions`;
   nada garante que casem.
4. **Camada de claims órfã**: `SynchronizePermissionClaimsAsync`
   (`IdentityUserRoleRepository`) e as claims de permissão do admin
   (`IdentityAdminProvisioner`) existiam só para sustentar o modelo removido.

## Alternativas consideradas

### Manter a granularidade por permissão
**Rejeitada.** Nenhum requisito de negócio atual precisa de "permissão X sem Y"
para uma role. Os 46 literais de role revelam que a granularidade real é por
*nível*, não por permissão. Manter os dois modelos mantém a duplicação e o drift
descrito acima.

### Policies por permissão no HTTP + roles na Application (hybrid)
**Rejeitada.** Mantém as duas fontes de verdade e a indireção; o único ganho seria
policies com nomes mais finos, o que nenhum endpoint atual usa.

### Nomes de policy em `string` livre (`"Operate"`, `"Administer"`)
**Rejeitada.** `[Authorize]` exige argumento de compilação, então literais
funcionariam — mas literais de nome de policy é a mesma classe de problema que
os 46 literais de role. Use-se `AuthorizationPolicies.Operate` /
`.Administer` (constantes).

## Consequências

### Positivas
- 3 conceitos canônicos no lugar de 46 literais + 21 permissões.
- Hierarquia declarada **uma vez** (`RoleAccess`) e reutilizada em HTTP e MediatR.
- Nada de claims de permissão: menos superfície em cookie, `IdentityUserClaim`
  e `/auth/me`.
- `[Authorize]` para leitura não depende de lista de roles: qualquer usuário
  autenticado lê, sem lista para manter.

### Negativas
- **Perda de granularidade**: não é possível "permissão X sem Y" para role
  customizada. Aceito como trade-off: nenhum requisito atual exige isso, e o
  enum `Permission` pode ser reintroduzido revertendo este ADR.
- **Role customizada futura** precisa ser encaixada em um nível (seção
  Extensibilidade) ou exigiria um 4º nível.

### Segurança
- Toda `IAuthorizedRequest` passa a exigir usuário **autenticado** mesmo sem
  `RequiredRoles` declarado, e a mensagem de 401 passou a ser determinística
  (`Roles ausentes: …`, ordenado ordinalmente) para poder ser testada.

## Supersede

Esta decisão **substitui a seção "Autorização nos controllers" do ADR-008**.
O ADR-008 continua válido para o ASP.NET Core Identity como base de autenticação
e gestão de usuários, mas a autorização por permissão e o `AuthorizationBehavior`
baseado em claim `permission` não existem mais.

## Estratégia de migração

1. **Núcleo** — `UserRole.User` → `UserRole.Operator`; remover `Permission`,
   `PermissionClaim` e `RolePermissions`; `ICurrentUserContext.Permissions`
   removido; `IAuthorizedRequest.RequiredPermissions` → `RequiredRoles`;
   `AuthorizationBehavior` passa a checar roles (e autenticação) em vez de
   claims de permissão.
2. **Policies HTTP** — `PermissionPolicies`/`AddPermissionPolicies` removidos;
   `AuthorizationPolicies.Operate` e `.Administer` registradas em `Program.cs`.
3. **Application** — 126 `RequiredPermissions` convertidos para `RoleAccess`
   conforme a tabela de equivalência abaixo.
4. **Controllers** — 126 `[Authorize]` convertidos (52 leitura, 64 `Operate`,
   10 `Administer`).
5. **Frontend** — labels e mocks `User` → `Operator`; a tela de matriz de
   permissões sai (não existe mais o conceito).
6. **Dados** — renomear a role persistida `'User'` → `'Operator'` e revogar
   sessões antigas (ver seção Migração de dados).
7. **Testes e documentação** — remover testes de permissão, cobrir os 3 níveis,
   atualizar docs 03/05/12.

## Equivalência de acesso (garantida)

A matriz `RolePermissions` do `HEAD` mapeava 1:1 nesses 3 níveis, então **quem
acessa o quê não muda** — só o nome `User` vira `Operator`:

| Permissão anterior                                        | Nível novo        |
|-----------------------------------------------------------|-------------------|
| `DashboardView`, `InventoryRead`, `MonitoringRead`, `IncidentsRead`, `TasksRead`, `SlaRead`, `RemoteSessionsRead` | Ler     |
| `IdentityUsersRead`, `InventoryManage`, `MonitoringManage`, `IncidentsManage`, `TasksManage`, `SlaManage`, `RemoteSshConnect`, `RemoteRdpConnect` | Operar   |
| `IdentityUsersManage`, `IdentityRolesManage`, `AuditRead`, `CredentialUse`, `CredentialManage`, `RetentionManage` | Administrar |

`Admin` continua com acesso total: os três níveis o incluem.

## Migração de dados

`UserRole` é persistido como **string** (`HasConversion<string>()`), então o
valor gravado precisa mudar, ou um `Operator` novo receberia o acesso de `User`:

| Tabela               | Coluna | Mudança |
|----------------------|--------|---------|
| `DomainUserRoles`    | `Role` | `'User'` → `'Operator'` |
| `AspNetRoles`        | `Name` | `'User'` → `'Operator'` (mesmo `Id`, portanto `AspNetUserRoles.RoleId` não muda) |
| `AspNetRoles`        | `NormalizedName` | `'USER'` → `'OPERATOR'` — é a coluna que o `RoleManager` consulta; sem ela o rename criaria uma role `Operator` duplicada e órfã |
| `AspNetUserRoles`    | `RoleId` | sem mudança — resolve para a role renomeada |

Além disso:

- **Sessões de refresh revogadas e stamp rotacionado**: `UserSessions` é
  esvaziada e o `SecurityStamp` muda. O cookie em si é **auto-contido**
  (`AddCookie` com `ClaimsPrincipal` próprio, 14 dias) e não consulta o stamp,
  então ele não é invalidado no instante — um cookie emitido antes carrega
  `User`, que deixou de ser role válida, e é **negado** em `Operate` e
  `Administer` até o usuário entrar de novo. Fail-closed: o efeito é exigir
  novo login, nunca abrir acesso.
- Idempotente: rodar duas vezes não faz nada na segunda.

## Extensibilidade

Para uma role customizada no futuro:

1. Decidir em qual nível ela se encaixa (`Read`/`Operate`/`Administer`) e
   representá-la em `RoleAccess`; se não couber, criar um 4º nível com novo
   `UserRole` + nova policy + atualização desta matriz de equivalência.
2. Se a necessidade for realmente "permissão X sem Y", reverter este ADR
   (restaurar `Permission`/`RolePermissions`) em vez de acumular um terceiro
   modelo ao lado das roles.

## Critérios de aceite

- [x] Nenhum literal de role ou nome de policy de permissão em `src/`
- [x] Nenhuma ocorrência de `Permission`/`RolePermissions`/`PermissionClaim` em `src/`
- [x] Hierarquia em um único lugar (`RoleAccess`) usado por HTTP e MediatR
- [x] Testes dos 3 níveis (HTTP e MediatR) e teste de arquitetura anti-literal
- [x] Migração de dados idempotente + revogação de sessões
- [x] `User` → `Operator` em código, dados, frontend e documentação
- [x] A matrix 3×3 da 2ª camada (quem lê/opera/administra) tem teste positivo —
      sem ele, uma regressão para AND passaria verde e negaria a leitura

**Evidência**: `AuthorizationPolicyTests` (9), `AuthorizationBehaviorTests`
(12), `RoleAuthorizationStandardizationTests` (arquitetura), 23 casos de
integração ajustados para `Roles ausentes: …`, `RenameUserRoleToOperatorMigrationTests`
(3, executando o SQL da própria migration). Reposição: 1080 unit, 73 arquitetura,
123/123 integração; `npm run build` + `tsc -b` do frontend.

## Referências

- ADR-008 — ASP.NET Core Identity (seção de autorização substituída por este ADR)
- `src/InfraControl.Domain/Enums/RoleAccess.cs` — hierarquia canônica
- `src/InfraControl.Api/Authorization/AuthorizationPolicies.cs` — policies HTTP
- `src/InfraControl.Application/Common/Behaviors/AuthorizationBehavior.cs` — 2ª camada
- `src/InfraControl.Domain/Enums/UserRole.cs` — `Viewer`, `Operator`, `Admin`
- `src/InfraControl.Infrastructure/Persistence/Migrations/20260929010000_RenameUserRoleToOperator.cs` — dados
