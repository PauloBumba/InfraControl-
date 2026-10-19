# 59 — Checklist de Validação (Caça-bugs Backend — Fase 35)

> Data: 2026-10-19 · Scan: `.bug-hunter/backend-scan-report.json` (runId `backend-arch-2026-10-18`)
> Modo: scan-only single-pass (local-sequential, foco arquitetura)
> Cobertura honesta: **30/1190 arquivos** — scan parcial declarado; os 1160 restantes não foram caçados.
> Status: **3/3 REAL_BUG** — F1 e F2 corrigidos; F3 corrigido (2026-10-19).

## Achados confirmados (3 REAL_BUG · 0 descartados · 0 manual-review)

### F1 — Invalidação de cache por tag nunca casa (Medium, confiança 92)
- [x] **Diagnóstico**: `CachingBehavior` grava `infracontrol:q:{userId}:{roles}:{CacheKey}:{paramsHash}`; `CacheInvalidationBehavior` remove a chave **bruta** — nenhum valor declarável casa. `TagIndexKey` com zero chamadas de produção. `SyncSwitchPortsFromUnifiCommand` é a única invalidação declarada e não tem query cacheável correspondente.
- [x] **Corrigido (F35-T001, 2026-10-18)**: `CacheKeysToInvalidate` virou **tags** resolvidas (`tagidx:{tag}`) — query declara `CacheTags`, `CachingBehavior` indexa, `CacheInvalidationBehavior` invalida via `InvalidateTagAsync` nos dois níveis (Redis + fallback memória). Fim do no-op silencioso.
- [x] Validação: build 0, unit 2849/2849, arch 159/159, integração 206/206 (na época da task).

### F2 — Rate limit respondia 409 em vez de 429 (Low, confiança 85)
- [x] **Diagnóstico**: `RateLimitingBehavior` lançava `InvalidOperationException` → `GlobalExceptionHandler` casava como **409 Conflict**. Rate limit é **429 Too Many Requests** com `Retry-After`. Interface `IRateLimitRequest` sem implementação de produção (gatilho latente), mas o mapeamento errado já existia no behavior registrado.
- [x] **Corrigido (F35-T002, 2026-10-19)**:
  - `RateLimitExceededException` (BuildingBlocks.Application) com `RetryAfter` opcional;
  - `IRateLimiter.TryAcquireAsync` devolve `(bool Acquired, TimeSpan? RetryAfter)` — o `RedisRateLimiter` expõe o TTL da categoria rejeitada;
  - `RateLimitingBehavior` lança a exceção dedicada;
  - `GlobalExceptionHandler` mapeia para **429** e seta header `Retry-After` (arredondado para cima, mínimo 1s).
- [x] Testes: `RateLimitingBehaviorTests` (3: exceção com RetryAfter, pass-through sem IRateLimitRequest, next quando permite).
- [x] Validação (2026-10-19): build 0, unit **2871/2871**, arch 159/159.

### F3 — Convite: e-mail depois do save sem rollback real (Low, confiança 88)
- [x] **Diagnóstico**: `CreateInvitationCommandHandler` fazia `AddAsync` + `SaveChangesAsync` **antes** de `SendInvitationAsync`; o comentário prometia rollback via `TransactionBehavior`, mas o pipeline **não cria transação** (só `next()` + SaveChanges pós-sucesso). Com provider SMTP real, falha de envio deixaria linha commitada → recriar dava 409 "convite pendente".
- [x] **Corrigido (F35-T003, 2026-10-19)**: e-mail enviado **antes** de `AddAsync`/`SaveChangesAsync` — `invitation.Id` nasce no domínio (`Invitation.Create`), o `acceptUrl` funciona sem persistir. Falha de envio propaga com **nenhuma linha** no banco; o admin recria limpo. Comentário atualizado (a promessa de TransactionBehavior era falsa).
- [x] Teste: `CreateHandler_ShouldNotPersistInvitationWhenEmailFails` (sender que joga → exceção propaga → `GetPendingByEmailAsync` retorna null).
- [x] Validação (2026-10-19): build 0, unit 2871/2871, arch 159/159.

## Pendências do scan backend

1. **Cobertura parcial** — 30/1190 arquivos no scan de arquitetura; 1160 arquivos (controllers, services, workers, queries) nunca caçados. Novo loop `--loop` do Bug Hunter para ampliar cobertura.
2. **F2 latente** — `IRateLimitRequest` continua sem implementação de produção; quando um comando adotar a interface, o 429 já está correto.
3. **F3 latente** — `LoggingIdentityEmailSender` continua só logando; com provider SMTP real a ordem já é a correta, mas um teste e2e com provider que falha de propósito seria o smoke definitivo.

## Validação técnica (2026-10-19)

| Suíte | Resultado |
|---|---|
| Unit | **2871/2871** |
| Architecture | **159/159** |
| Build Release | 0 erros |
