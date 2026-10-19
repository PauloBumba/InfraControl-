# Bug Hunter Report

- Findings reviewed: 3
- Confirmed: 3
- Dismissed: 0
- Manual review: 0
- Unreviewed: 0

## Confirmed Bugs
- F1-cache-invalidation-never-matches | Medium | src/InfraControl.Application/Common/Behaviors/CacheInvalidationBehavior.cs | A invalidação de cache (CacheKeysToInvalidate) é sempre um no-op: a chave bruta declarada pelo comando nunca corresponde à chave construída pelo CachingBehavior, então mutations não limpam cache e leituras ficam stale até o TTL.
  Confidence: 92 (high) | INDEPENDENTLY_VERIFIED
  Analysis: Re-li em separado: CachingBehavior.cs:19-33 grava `infracontrol:{CacheKeyBuilder.Build(...)}` (Build = q:{userId}:{roles}:{CacheKey}:{paramsHash}); CacheInvalidationBehavior.cs:22-23 remove a chave BRUTA com RemoveAsync exato; HybridCacheService.cs:57-69 e RedisCacheService.cs:41-44 não resolvem tag/prefixo; CacheKeyBuilder.TagIndexKey (linha 29) tem zero chamadas de produção; SyncSwitchPortsFromUnifiCommand.cs:32 é a única invalidação declarada e nenhuma query existe com aquele CacheKey (GetSwitchPortQuery em SwitchPortQueries.cs:49 é IQuery cru). Tracei o gatilho: mutation com CacheKeysToInvalidate → RemoveAsync(chave inexistente) → no-op silencioso (só Debug log) → qualquer ICacheableQuery futura nesse recurso fica stale até o TTL default de 5 min. O contrato é incoerente por construção — nenhum valor declarável pode casar com a chave gravada. Impacto operacional atual é zero (nada cacheado casa), mas é falha silenciosa garantida de um cross-cutting concern do CQRS e armadilha de slice — Medium justificado (fails silently), não Critical (sem corrupção de dados).
- F2-rate-limit-409-not-429 | Low | src/InfraControl.Application/Common/Behaviors/RateLimitingBehavior.cs | Quando o RateLimitingBehavior do pipeline MediatR dispara, ele lança InvalidOperationException que o GlobalExceptionHandler mapeia para 409 Conflict — status errado para rate limit (deveria ser 429 Too Many Requests com Retry-After).
  Confidence: 85 (medium) | INDEPENDENTLY_VERIFIED
  Analysis: Re-li RateLimitingBehavior.cs:24-30 (throw InvalidOperationException("Limite de requisições excedido.")) e GlobalExceptionHandler.cs:86 (InvalidOperationException => 409 Conflict). O mapeamento está errado: rate limit é 429 Too Many Requests com Retry-After, e clientes/monitoramento que tratam 429 quebram. Confirmei que IRateLimitRequest não tem implementação de produção (grep em src/: só interface + behavior) — gatilho exige um comando futuro que implemente a interface, mas o código errado já existe no comportamento registrado para todos os requests. Severidade Low (requires unusual conditions — primeiro uso futuro da interface), mantida do Hunter.
- F3-invitation-email-no-rollback | Low | src/InfraControl.Application/Identity/Invitations/Commands/InvitationCommands.cs | O comentário (e TASKS F1-T017) prometem que falha de envio de e-mail desfaz a linha do convite via TransactionBehavior, mas não existe transação no pipeline e o handler persiste ANTES do envio — falha real de e-mail deixa o convite órfão pendente e o admin toma 409 "convite pendente" ao recriar.
  Confidence: 88 (high) | INDEPENDENTLY_VERIFIED
  Analysis: Re-li InvitationCommands.cs:44-75: AddAsync + SaveChangesAsync (linhas 59-61) ANTES de SendInvitationAsync (74); o comentário 64-67 promete rollback via TransactionBehavior. Re-li TransactionBehavior.cs completo: só next() + SaveChangesAsync pós-sucesso — sem BeginTransactionAsync; AppDbContext.cs:124-133 (SaveChangesAsync = audit fields + domain events + base) também não cria transação; único BeginTransactionAsync do projeto está fora do pipeline (IdentityAuthenticationService/IdentityAdminProvisioner/InvitationAccountPort). LoggingIdentityEmailSender (IdentityAuthenticationService.cs:500-513) só loga e nunca lança — gatilho hoje impossível, mas a decisão documentada F1-T017 descreve comportamento que o código não implementa: com provider SMTP real, falha de envio deixa a linha commitada, pendente, e o recriar toma 409 do GetPendingByEmailAsync. Low (requires unusual conditions — provider real ainda não existe).

## Manual Review
- None

## Dismissed Findings
- None

## Unreviewed Findings
- None

## Coverage

- **Escopo**: `src/` (.NET) — 1190 arquivos no triage; single-pass com budget de 30 arquivos, foco em arquitetura: pipeline MediatR/behaviors (ordem, transação, audit, cache, rate limit), middleware (Program.cs), persistência/transações, cache CQRS, integrações singleton, DI de workers.
- **Cobertura**: PARCIAL — não é varredura completa de todos os arquivos do backend (estratégia large-codebase, `needsLoop: true`). Achados de domínio específico (SNMP/ONVIF/monitoramento, handlers individuais fora do pipeline, testes) não foram esmiuçados nesta passada.
- **Artefatos**: `.bug-hunter/hunter-findings.json` → `.bug-hunter/skeptic.json` → `.bug-hunter/referee.json` → este relatório (`backend-scan-report.json` é a versão canônica da máquina).
