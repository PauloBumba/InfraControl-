# F46-T003 — Latência de commands/queries (p50/p95) + caça a N+1

> Fase: F46 (engenharia de produto) · Data: 2026-10-25 · Status: **concluída**
> Ambiente de coleta: host E2E real (`WebApplicationFactory<Program>` + Postgres e Redis **reais** via Testcontainers) — caminho idêntico ao do navegador (envelope, cookie, XSRF, papéis).

## 1. Instrumentação criada

| Artefato | Papel |
|---|---|
| `tests/.../Common/DbCommandCounter.cs` | Interceptor `DbCommandInterceptor` que conta comandos SQL do EF **por host** (porta mapeada do Postgres Testcontainers — seguro com classes E2E em paralelo) |
| `src/.../DependencyInjection.cs` | `AddDbContext` agora aplica `IInterceptor` do DI (`AddInterceptors(provider.GetServices<IInterceptor>())`) — **vazio em produção**, comportamento idêntico |
| `tests/.../E2EHost.cs` | Registra o contador por host + sobe limites de rate limit (`Default` 120→100000/min) para o sweep não se autoderrubar com 429 |
| `tests/.../QueryCountRegressionE2ETests.cs` | Orçamento de SQLs por request (guarda de regressão de N+1) |
| `tests/.../LatencySweepE2ETests.cs` | Sweep de 20 iterações/endpoint, p50/p95/max → `artifacts/latency-e2e.json` |

## 2. Latência ponta-a-ponta (p50/p95, 20 iterações, ordenado por p95)

| Método | Rota | p50 | p95 | max |
|---|---|---:|---:|---:|
| POST | `/api/v1/tasks` | 24,3ms | **40,2ms** | 48,4ms |
| GET | `/api/v1/knowledge/articles?pageSize=20` | 13,8ms | 33,4ms | 38,4ms |
| GET | `/api/v1/tasks?pageSize=20` | 15,3ms | 33,1ms | 33,8ms |
| GET | `/api/v1/incidents?pageSize=50` | 18,5ms | 27,5ms | 29,7ms |
| POST | `/api/v1/incidents` | 18,6ms | 26,9ms | 35,9ms |
| GET | `/api/v1/admin/audit?pageSize=20` | 11,7ms | 25,8ms | 27,4ms |
| GET | `/api/v1/incidents?pageSize=20` | 11,6ms | 24,1ms | 34,3ms |
| GET | `/api/v1/sla/services?pageSize=20` | 10,8ms | 21,6ms | 39,9ms |
| GET | `/api/v1/monitoring/alerts?pageSize=20` | 13,6ms | 20,9ms | 26,9ms |
| GET | `/api/v1/inventory/locations?pageSize=20` | 11,4ms | 17,5ms | 18,2ms |
| GET | `/api/v1/inventory/devices?pageSize=20` | 12,1ms | 17,2ms | 19,5ms |
| GET | `/api/v1/notifications/rules` | 11,8ms | 15,7ms | 15,8ms |
| GET | `/api/v1/admin/users?pageSize=20` | 10,9ms | 15,1ms | 20,0ms |
| GET | `/api/v1/auth/me` | 6,3ms | 13,9ms | 28,9ms |

**Veredito:** p95 de **todos** os endpoints < 50ms (teto do teste: 1500ms). **Nenhuma otimização de latência é necessária** no caminho hot atual. Dados brutos: `artifacts/latency-e2e.json`.

## 3. Comandos SQL por request (caça a N+1)

| Rota | SQLs | Orçamento | Avaliação |
|---|---:|---:|---|
| POST `/incidents` (create) | 2–3 | 14 | Insert + audit — ótimo |
| GET `/incidents?page=1&pageSize=50` | 4 | 6 | page+count+extras — sem N+1 |
| GET `/tasks?page=1&pageSize=50` | 10 | 12 | Group joins **fixos por design** (F7-T002: assignees/devices/incidents/articles/pdi/stats/pais) — O(1) por página, **não** N+1 |
| GET `/inventory/devices?page=1&pageSize=50` | 3 | 6 | Sem N+1 |
| GET `/inventory/locations?page=1&pageSize=50` | 3 | 6 | Sem N+1 |
| GET `/monitoring/alerts?page=1&pageSize=20` | 3 | 6 | Sem N+1 |
| GET `/knowledge/articles?page=1&pageSize=20` | 4 | 6 | Sem N+1 |
| GET `/admin/audit?page=1&pageSize=20` | 3 | 6 | Sem N+1 |
| GET `/notifications/rules` | 2 | 6 | Catálogo — ótimo |
| GET `/sla/services?page=1&pageSize=50` | 3 | 6 | Sem N+1 |

**Veredito:** **nenhum N+1 detectado** nos módulos varridos. A listagem de Tasks é a mais cara (10 SQLs fixos); é intencional e documentado, mas é candidata a consolidação com JSON aggregation do Postgres se o volume crescer (ver §5).

## 4. Achado operacional corrigido durante a coleta

- **Rate limit `Default` (120 req/min)** derruba qualquer carga de teste/rajada legítima de dashboard (varreduras, F5). Os hosts de teste agora o sobem; em produção vale revisar se 120/min por usuário é adequado para telas que disparam várias chamadas (relatório consolidado §otimizações).

## 5. Oportunidades de otimização (baixa prioridade — sem dor hoje)

1. **Tasks list (10 SQLs fixos):** consolidar os group joins em 1–2 queries com `json_agg`/CTEs do Postgres, se latência crescer com volume. Hoje: O(1) — sem ação.
2. **Rate limit Default:** revisar regra para telas multi-requisição (dashboard) em produção.
3. Latência de `POST /tasks` (p95 40ms) é a mais alta do conjunto — dentro do aceitável; monitorar se crescer com SLA/audit payload.

## 6. Regressão permanente

`QueryCountRegressionE2ETests` + `LatencySweepE2ETests` (suíte E2E, rodam no CI de testes): qualquer N+1 novo ou regressão grosseira de latência **quebra o build de testes** com mensagem nomeando a rota.
