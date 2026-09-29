# MediatR Pipeline

## Behaviors

1. CorrelationId
2. Logging
3. Observability
4. Performance
5. Validation
6. Authorization
7. RateLimiting
8. Idempotency
9. Caching
10. Transaction
11. CacheInvalidation
12. Audit
13. PostCommitActions

## Query

```text
Correlation
→ Logging
→ Observability
→ Performance
→ Validation
→ Authorization
→ RateLimit
→ Cache
→ Handler
```

## Command

```text
Correlation
→ Logging
→ Observability
→ Performance
→ Validation
→ Authorization
→ RateLimit
→ Idempotency
→ Transaction
→ Handler
→ Commit
→ PostCommit
```

## Regras

Transaction é para comandos que alteram estado.

Cache é aplicado somente a queries cacheáveis.

Invalidação de cache e auditoria de sucesso ocorrem depois do commit.

Audit grava `ActorId` (`ICurrentUserContext.UserName`) e `Resource` explícito via `IAuditableRequest` (F11-T005); sem o marker, `Resource` é `"ApplicationRequest"`.

Eventos de identidade que não são requests MediatR (F1-T016c: login com
segundo fator, MFA ligado/desligado, logout e revogação de sessões) escrevem
`AuditEntry` direto pelo `IAuditWriter`, ANTES do `SaveChanges` da operação —
efeito e audit commitam na mesma transação (helper `SecurityAudit`).
Ações: `LoginWithMfa`, `MfaEnabled`, `MfaDisabled`, `SessionRevoked`,
`SessionsRevoked`, `Logout`; resources `Session`/`Mfa`. Só sucesso e só
metadados, consultáveis em `GET /api/v1/admin/audit`.
