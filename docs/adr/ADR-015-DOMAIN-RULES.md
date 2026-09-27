# ADR-015 — Domain Rules

## Status
Accepted

## Decisão

Regras obrigatórias para entidades de domínio e relacionamentos:

1. Entities must not expose `ICollection<T>` with public setter
2. Collections must use private `List<T>`
3. Expose collections as `IReadOnlyCollection<T>`
4. Relationships between aggregates should use IDs
5. EF Core configurations belong in Infrastructure
6. Never put EF Core configuration inside Domain
7. Domain entities must protect state changes through methods
8. Every relationship must have its cardinality explicitly defined

## Motivo

Garantir encapsulamento, integridade do domínio e separação de responsabilidades entre Domain e Infrastructure. Entidades de domínio devem ser imutáveis em relação a alterações externas e todas as mudanças de estado devem passar por métodos protegidos.

## Conseqüências

- Entidades não podem ter propriedades de coleção com setter público
- Todas as coleções são `List<T>` privadas expostas como `IReadOnlyCollection<T>`
- Configurações EF Core ficam em `Infrastructure/Persistence/Configurations`
- Mudanças de estado só via métodos públicos com validação
- Relacionamentos entre agregados usam IDs (ex: `DeviceId` em vez de `Device`)
- Cardinalidade explícita em todas as relações (1:N, N:N, 1:1)
