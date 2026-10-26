# F46 — Relatório consolidado (T005) · Engenharia de produto

> Data: 2026-10-25 · Status: **entregue**
> Categorias: ponta-a-ponta · latência/N+1 · integridade/perda de dados ·
> tempo real · saúde dos serviços · Zabbix.
> Relatórios de detalhe: `F46-T002-varredura-e2e.md`, `F46-T003-latencia.md`,
> `F46-T004-integridade-signalr.md`, `F46-T006-saude-servicos.md`,
> `F46-T007-zabbix.md`.

## Resumo executivo

| Categoria | Situação | Evidência |
|---|---|---|
| Ponta-a-ponta (front→API→banco) | ✅ 11/11 fluxos verdes | T002 |
| Latência p50/p95 | ✅ p95 **< 50ms** em todos os endpoints | T003 (`artifacts/latency-e2e.json`) |
| N+1 | ✅ **nenhum detectado** (10 rotas varridas) | T003 (regressão permanente) |
| Perda de dados (outbox/correlation) | ✅ bug **corrigido** + teste | T004 |
| Tempo real SignalR | ✅ consumer→worker→hub→cliente E2E | T004 |
| Saúde de todos os containers | ✅ agregador + links + aviso SignalR | T006 |
| Zabbix (Agent2 + autodiscovery) | ✅ 6 hosts novos, 23 containers, 7 triggers | T007 |

## 1. Cobertura (T002)

Suíte `EndpointSweepE2ETests` prova escrita+leitura no banco dos fluxos das
telas principais (incidents, locations, devices, tasks, notification rules,
knowledge, audit, SLA, alerts) com o contrato exato do front (envelope,
XSRF, papéis). Correção de fixture: admin usa `AdminPassword`.
→ Regressão de contrato/perda de dados quebra teste.

## 2. Latência e consultas (T003)

- **Nenhuma otimização de latência é necessária hoje** — p95 de tudo < 50ms
  (pior: POST /tasks 40,2ms; melhor: /auth/me 13,9ms), teto assertivo 1500ms.
- Sem N+1: `/tasks` com 10 SQLs é **fixo por design** (group joins F7-T002).
- Instrumentos permanentes: `QueryCountRegressionE2ETests` (orçamentos por
  rota) + `LatencySweepE2ETests` + `DbCommandCounter` (contagem por host).

## 3. Integridade e tempo real (T004)

- **Bug corrigido**: `HttpContextCorrelation` nunca era registrado no DI →
  todo `OutboxMessage` da API nascia sem `CorrelationId` (viola o ADR-021).
  Fix no `Program.cs`; teste agora exige o campo.
- Fluxo realtime E2E verde: evento → fila → `NotificationSignalRWorker`
  (poll 2s) → hub → cliente autenticado (cookie+XSRF) → delivery `Sent`.
- Dedup por inbox validada (reentrega idêntica = 1 delivery).

## 4. Saúde dos serviços (T006)

- `/health` cobre agora: postgres, redis, ai-runtime, **outbox-backlog**
  (workers: >5min Degraded / ≥100 Unhealthy) + um check HTTP por serviço
  irmão via `Health:Services` (frontend, rabbitmq, zabbix, evolution,
  guacamole, adminer) com **link de console** na HealthPage.
- Aviso em tempo real: `HealthStatusNotifier` publica `health-status` no hub;
  HealthPage recarrega e mostra banner na mudança.

## 5. Zabbix (T007)

- Agent2 com plugin Docker + LLD: **23 containers** (CPU/mem/rede/I/O).
- Hosts novos `infracontrol-{docker,postgres,redis,rabbitmq,services}` no
  grupo InfraControl; interface do host `Zabbix server` **corrigida** (antes
  127.0.0.1, sem coleta); templates oficiais com macros.
- Latência/disponibilidade por serviço: 14 itens TCP + 7 triggers (sev 4).
- Provisionamento idempotente: `scripts/zabbix/provision-infracontrol.py`.

## 6. Priorização do que otimizar/apurar (base da T008)

| # | Item | Prioridade | Esforço | Nota |
|---|---|---|---|---|
| 1 | Revisar rate limit `Default` (120 req/min) em produção | **Média** | Baixo | rajadas legítimas de dashboard (F5, varreduras) são derrubadas (T003 §4) |
| 2 | Consolidar group joins de `/tasks` com `json_agg` (se volume crescer) | Baixa | Médio | hoje O(1)/10 SQLs fixos — sem dor (T003 §5) |
| 3 | `restart: unless-stopped` no serviço `adminer` | Baixa | Trivial | adminer foi encontrado `Exited (0)` — T007 |
| 4 | Triggers/itens `not-supported` transitórios do Zabbix (bootstrap) | Baixa | — | acompanhar próxima checagem; template "Zabbix server health" com 11 itens em avaliação (T007 §4) |
| 5 | Web scenario `web.test.*` não grava itens (bug trunk 8.0rc2) | Baixa | — | contornado com TCP checks; reavaliar em upgrade do Zabbix (T007 §3) |
| 6 | Push SignalR limitado ao poll de 2s do worker | Informativa | — | aceitável para notificações (T004 §3) |

**Não há pendência crítica de performance, integridade ou dados perdidos.**

## 7. Validação executada

- `dotnet build InfraControl.slnx` · suíte unit (3022/3024, 2 falhas Skia
  pré-existentes) · integração/Testcontainers · suíte E2E completa (31/31 na
  janela da T003/T004) · front `vitest`/`tsc`/`build` no escopo alterado.
