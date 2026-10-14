# ADR-049 — Relatório transversal (produtividade · tasks · knowledge · projetos · incidentes)

## Status

Proposed (2026-10-13 — escopo confirmado pelo usuário em revisão do backlog
CRUD/relatórios; implementação por incrementos com checkpoint, AGENTS.md).

## Contexto

Hoje cada vertical tem o seu relatório/dashboard, mas **não existe um
relatório que cruze os módulos**:

| Vertical | Endpoints | Export |
|---|---|---|
| PDI (produtividade) | `indicators`, `reports/individual`, `reports/semester`, `reports/difficulties` + 3 dashboards | ✅ |
| Integrações | `unifi/dashboard`, `zabbix/summary` | ❌ |
| Cross-módulo | só `global-search` (agregado Read, ADR-030) | ❌ |

O usuário pediu um relatório **personalizado/transversal** que agregue
produtividade (PDI), tasks, knowledge, projetos e incidentes resolvidos — o
"teto" que hoje não existe. Precisa de decisão arquitetural antes de código
(AGENTS.md: não alterar arquitetura sem ADR).

## Decisão

**Read-model composto**, sem novo armazenamento:

1. **Um comando de leitura** `GetTransversalReportQuery` (`BaseQuery<T>` +
   `IAuthorizedRequest`, portanto cacheável via `CachingBehavior`/`CacheKeyBuilder`
   — BuildingBlocks §Application) compõe as métricas de cada módulo na
   resposta, injetando os repositórios de cada agregado (leitura direta,
   sem repassar pelos commands/queries existentes — vários são `Administer`
   e repassá-los derrubaria um relatório `Operate` com 403 no meio).
2. **Permissões** alinhadas ao ADR-030: `Operate` (Operator vê o próprio/time,
   Admin vê tudo — fine-grained no handler via guardas de acesso por usuário)
   e `Administer` no agregado global quando agregado por todos os usuários.
3. **Export** reaproveita o renderer já usado no PDI (individual/semester/
   difficulties) para PDF/CSV — nada de novo de render.
4. **Cache** `BaseQuery` (5 min default) cobre o custo de compor vários
   bounded contexts numa resposta; o `CacheKeyBuilder` já separa por
   usuário/roles/params.

**Não** materializamos (tabela/job de refresh) nesta fase: compor é mais
barato de construir e operar; só evoluímos para read-model materializado se
o volume/latência exigir (seria um novo ADR).

## Escopo das métricas (R-a)

| Módulo | Métrica |
|---|---|
| Produtividade (PDI) | planos por status, % conclusão, fases concluídas |
| Tasks | total, concluídas, atrasadas, por estado |
| Knowledge | artigos publicados/rascunho |
| Projetos | total, por status/fase |
| Incidentes | total, resolvidos, tempo médio de resolução |

Filtros: período (de/até), módulo (quais blocos incluir), usuário/time.

## Consequências

- **+1 read-model transversal** sem novo storage; acopla o módulo Reports aos
  repositórios dos 5 módulos (aceitável para leitura; os repositórios são a
  porta de leitura canônica por ADR-007).
- **Risco de performance** ao compor 5 bounded contexts → mitigado pelo cache
  (`ICacheableQuery`) e por filtros de período reduzirem o volume varrido.
- Segue ADR-006 (CQRS — lado de leitura), ADR-007 (vertical slice — módulo
  `Reports`), ADR-030 (papel por entidade), BuildingBlocks (BaseQuery+cache).
- O relatório **não** substitui os dashboards por vertical (PDI/UniFi/Zabbix);
  é a visão agregada de cima.

## Referências

- ADR-006 (CQRS), ADR-007 (vertical slice), ADR-030 (matriz CRUD/papéis),
  ADR-037 (dashboards PDI), `docs/24-BUILDING-BLOCKS.md` (BaseQuery +
  CachingBehavior/ICacheableQuery).
