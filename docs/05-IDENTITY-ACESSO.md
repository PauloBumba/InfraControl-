# Identity e Acesso

## Base

ASP.NET Core Identity.

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
