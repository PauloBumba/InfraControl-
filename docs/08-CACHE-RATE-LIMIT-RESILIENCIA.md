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
