# Identity e Acesso

## Base

ASP.NET Core Identity.

### F1-T001 — Fundação implementada

- `InfraControl.Domain.Identity.User` é o agregado de negócio e possui estado ativo, email e roles;
- `UserRole` define `Admin`, `User` e `Viewer`, com atribuição controlada pelo agregado;
- `UserSession` é o agregado responsável por expiração, refresh token hash e revogação de sessão;
- eventos de domínio cobrem criação, ativação, desativação, role e ciclo de sessão;
- `InfraControlUser` é somente o adaptador técnico de credenciais do ASP.NET Identity;
- `AppDbContext` herda de `IdentityDbContext` e mantém Outbox, auditoria e agregados de domínio;
- `UserManager` e roles store são registrados no módulo de Infrastructure;
- senha exige no mínimo 12 caracteres, número, maiúscula, minúscula e caractere não alfanumérico;
- migration `AddIdentityFoundation` cria as tabelas padrão do Identity no PostgreSQL;
- endpoints, convites e login social permanecem evoluções futuras; os endpoints de autenticação, cookie de sessão e refresh já estão implementados.

### F1-T002 — Bootstrap Admin

O bootstrap é um caso de uso da camada `Application` e usa uma porta (`IAdminIdentityProvisioner`) para solicitar a persistência ao adaptador de `Infrastructure`. Ele é desativado por padrão e só executa quando `BootstrapAdmin:Enabled=true`.

Os valores devem ser fornecidos por configuração segura, preferencialmente variáveis de ambiente:

```text
BootstrapAdmin__Enabled=true
BootstrapAdmin__Email=admin@example.com
BootstrapAdmin__DisplayName=InfraControl Admin
BootstrapAdmin__Password=<secret>
```

O processo é idempotente: se já existir qualquer administrador ou se o email configurado já estiver cadastrado, nenhuma duplicação é criada. A senha é entregue ao `UserManager` para hash e nunca é escrita em logs. A criação do usuário técnico, role `Admin` e agregado de domínio ocorre em transação única.

### F1-T003 — Roles e RBAC

O gerenciamento de roles segue Vertical Slice/CQRS. Os comandos `AssignUserRoleCommand` e `RemoveUserRoleCommand`, com seus handlers, ficam em `Application/Identity/Roles`; o domínio mantém as invariantes do agregado `User`; e `IUserRoleRepository` é a porta usada pelos handlers. O `IdentityUserRoleRepository` é o adaptador técnico em `Infrastructure`, responsável por sincronizar a role do domínio com o ASP.NET Core Identity em uma transação.

As policies HTTP são `AdminOnly`, `UserAccess` e `ViewerAccess`. O controller HTTP apenas converte a requisição em comando e envia pelo MediatR. A alteração é autorizada por `AdminOnly`, o último administrador ativo não pode perder a role `Admin` e um usuário não pode ficar sem role. Permissões granulares e auditoria permanecem em Tasks próprias.

### F1-T004 — Permissions

O catálogo de permissões fica em `Domain/Authorization/Permission.cs`, sem dependência de ASP.NET Core. A matriz padrão é definida em `RolePermissions`: `Viewer` recebe permissões de leitura; `User` herda as permissões de leitura e recebe permissões de gestão operacional; `Admin` recebe o catálogo completo. A união de múltiplas roles não produz duplicatas.

O adaptador `IdentityUserRoleRepository` sincroniza as claims `permission` após cada alteração de role. O bootstrap do administrador inicial também grava todas as claims de permissão. O API registra uma policy para cada valor de `Permission`, mantendo as policies legadas de role para compatibilidade. A claim deve ser renovada quando a sessão for reemitida; auditoria e concessões customizadas continuam fora do escopo desta Task.

## API de autenticação e dashboard

O backend expõe os fluxos de Identity inspirados no Sentinel:

| Endpoint | Função | Acesso |
|---|---|---|
| `POST /api/v1/auth/register` | Cria usuário com role inicial `Viewer` | Anônimo |
| `POST /api/v1/auth/login` | Valida credenciais, cria cookie e sessão refresh | Anônimo |
| `POST /api/v1/auth/refresh` | Rotaciona refresh token e reemite cookie | Anônimo |
| `POST /api/v1/auth/logout` | Revoga sessão e cookie | Autenticado |
| `POST /api/v1/auth/forgot-password` | Gera reset sem revelar existência do email | Anônimo |
| `POST /api/v1/auth/reset-password` | Redefine senha com token Redis single-use | Anônimo |
| `POST /api/v1/auth/change-password` | Troca senha e revoga sessões | Autenticado |
| `GET /api/v1/auth/me` | Retorna usuário, roles e permissões | Autenticado |

A área administrativa possui `GET /api/v1/admin/users`, alteração de status e atribuição/remoção de roles. Os endpoints usam `IdentityUsersRead` e `IdentityUsersManage`. O sender de email atual é um adaptador de logging; SMTP/provider deve ser conectado por `IIdentityEmailSender` sem alterar Application.

## Modelo

```text
User → Role → Permission
```

## Recursos

- Bootstrap Admin.
- Invitation.
- Accept/Reject.
- Password Change.
- Password Reset.
- Disable/Enable.
- External Login.
- Audit.
- Session control.

## Convite

```text
Admin
→ Invitation
→ Outbox
→ RabbitMQ
→ Notification Worker
→ Email
→ Accept/Reject
```

Status:

- Pending
- Accepted
- Rejected
- Expired
- Cancelled

Token deve ser aleatório, temporário, single-use e armazenado somente como hash.

## Web

Cookie HttpOnly/Secure/SameSite.

## API

A autenticação HTTP atual usa cookie `HttpOnly`, `Secure` e `SameSite=Strict`, junto de refresh sessions persistidas com hash. JWT pode ser adicionado como outro adapter quando houver necessidade de clients não-browser; não é requisito para o dashboard web atual.

## Social login

Google e Microsoft inicialmente, seguindo regras de ativação/cadastro.

## Segurança

Nunca armazenar senha em texto puro.

O último administrador ativo não deve ser removido ou perder a função administrativa sem existir outro administrador válido.
