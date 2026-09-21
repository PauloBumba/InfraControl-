# Identity e Acesso

## Base

ASP.NET Core Identity.

### F1-T001 — Fundação implementada

- `InfraControlUser` usa `Guid` como chave e possui estado ativo;
- `AppDbContext` herda de `IdentityDbContext` e mantém Outbox e auditoria existentes;
- `UserManager` e roles store são registrados no módulo de Infrastructure;
- senha exige no mínimo 12 caracteres, número, maiúscula, minúscula e caractere não alfanumérico;
- migration `AddIdentityFoundation` cria as tabelas padrão do Identity no PostgreSQL;
- endpoints, JWT, bootstrap admin, convites, permissões e controle de sessão permanecem nas Tasks seguintes.

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
