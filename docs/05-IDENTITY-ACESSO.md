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
- endpoints, JWT, convites e permissões permanecem nas Tasks seguintes; o Bootstrap Admin e a sessão de domínio já estão modelados.

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

JWT.

## Social login

Google e Microsoft inicialmente, seguindo regras de ativação/cadastro.

## Segurança

Nunca armazenar senha em texto puro.

O último administrador ativo não deve ser removido ou perder a função administrativa sem existir outro administrador válido.
