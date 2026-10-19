# Cache, Rate Limiting e Resiliência

## Cache híbrido

```text
Query
→ Redis
→ Fallback Cache
→ PostgreSQL
```

PostgreSQL é a fonte da verdade.

Redis é cache/coordenação.

## Rate limiting

Dois níveis:

```text
HTTP
→ ASP.NET Core Rate Limiter

Application
→ MediatR RateLimitingBehavior
```

Redis guarda estado distribuído.

Políticas podem variar por IP, usuário, endpoint, operação e criticidade.

## Polly

Usar quando apropriado:

- Timeout
- Retry
- Exponential Backoff
- Jitter
- Circuit Breaker
- Fallback

Nunca usar retry cego em operações não idempotentes.

## Falha do Redis

Operações normais devem usar fallback seguro quando possível.

Operações críticas podem adotar política mais restritiva.

## Invalidação por tag (2026-10-18, F1 da caça-bugs backend)

A chave gravada pelo `CachingBehavior` embute usuário, roles e hash dos
parâmetros (`infracontrol:q:{userId}:{roles}:{CacheKey}:{paramsHash}` via
`CacheKeyBuilder.Build`) — um comando do lado da mutation **nunca** consegue
deduzir essa string para apagá-la. Antes desta correção,
`CacheKeysToInvalidate` chamava `RemoveAsync` com a chave bruta declarada:
o resultado era um no-op silencioso (só log de Debug).

O contrato agora fecha pelas tags — `CacheKeyBuilder.TagIndexKey`
(`tagidx:{tag}`), mecanismo que já existia no BuildingBlocks e estava sem
ligação:

```text
Query com CacheTags ["switchPorts:{id}"]
→ CachingBehavior grava a chave E indexa em tagidx:switchPorts:{id}
   (Redis set + índice no MemoryFallbackStore, TTL = CacheDuration)

Mutation com CacheKeysToInvalidate ["switchPorts:{id}"]
→ CacheInvalidationBehavior chama InvalidateTagAsync(tag)
→ resolve a união dos índices (Redis + fallback)
→ apaga cada chave membro nos dois níveis + o próprio índice
```

Regras:

- Query declara as tags em `ICacheableQuery.CacheTags` (default vazio =
  não participa de invalidação).
- Comando declara as **mesmas tags** em `CacheKeysToInvalidate` (o nome do
  membro está preservado — é o contrato existente; não inventar prefix wipe).
- `HybridCacheService.InvalidateTagAsync` cobre Redis caído (invalida só o
  fallback, Warning no log) e a união dos dois índices (chave gravada com o
  Redis fora não fica órfã quando ele volta sem o índice).

Verificado por: `MissingBehaviorTests` (registro de tag + fim a fim
query→mutation→requery) e `HybridCacheServiceTests` (invalidação nos dois
níveis + degradação).

## Implementação (2026-09-24)

### Cache híbrido — ADR-010

```text
Query
→ HybridCacheService.Redis (IRedisCacheStore)
→ MemoryFallbackStore (singleton, TTL)
→ miss → handler → PostgreSQL
```

- `HybridCacheService` implementa `ICacheService`: leitura, gravação,
  remoção, exists e try-acquire nunca propagam falha do Redis — a
  indisponibilidade vira `LogWarning` e o fallback em memória atende.
- Toda gravação também preenche o fallback, para que uma falha subsequente
  do Redis dentro do mesmo TTL continue legível.
- A conexão usa `AbortOnConnectFail=false`: a aplicação sobe com o Redis fora.
- `IIdempotencyStore` continua direto no Redis — operação crítica,
  fail-closed (política mais restritiva).

### Rate limiting — ADR-011

```text
HTTP
→ UseRateLimiter (429 + Retry-After + envelope ApiResponse)
→ GlobalLimiter: DistributedHttpRateLimiter (Redis INCR por categoria+partição)
   ↳ falha do Redis → janela em memória

Application
→ MediatR RateLimitingBehavior (inalterado)
```

- Camada HTTP: `DistributedHttpRateLimiter : PartitionedRateLimiter<HttpContext>`
  (wiring em `AddHttpRateLimiting`, chamado no `Program.cs`).
- Partição: `user:{sub}` quando autenticado, senão `ip:{endereço}`.
- Categorias via `[HttpRateLimit(RateLimitCategory.X)]` no endpoint
  (Default sem marcação): regras na seção `RateLimit` da configuração —
  Default 120/60, Login 5/60, PasswordReset 3/600, Webhook 120/60,
  Ingestion 1000/60.
- Cobertura: `HybridCacheServiceTests` (7), `DistributedHttpRateLimiterTests`
  (6), `HttpRateLimitPipelineTests` (2 — pipeline Kestrel real com
  Redis indisponível de propósito).
