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
