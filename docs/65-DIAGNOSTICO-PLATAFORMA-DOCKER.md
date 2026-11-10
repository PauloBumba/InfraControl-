# Diagnóstico — Plataforma de Gerenciamento, Observabilidade e IA Docker

Data: 2026-11-08 · Escopo: spec "Evolução Arquitetural — Plataforma de Gerenciamento,
Observabilidade e Inteligência Docker" · Método: inspeção direta de código, compose e
containers em execução (sem suposições).

## 1. Funcionalidades implementadas (com evidência)

| Área | Evidência |
|---|---|
| Docker Engine via socket | `DockerControlService` (Infrastructure) fala com `/var/run/docker.sock` via `SocketsHttpHandler.ConnectCallback`; api monta o socket RW + `group_add ${DOCKER_GID}` no compose |
| Listagem filtrada por projeto | `BelongsToProject` — label `com.docker.compose.project` primária + fallback por nome com separador (corrige falso positivo `infracontrolk6-*`) |
| Ciclo de vida start/stop/restart | `ControlAsync` + endpoint `POST /admin/cluster/docker/containers/{name}/action`, allowlist, policy `Administer`, auditoria via `RecordDockerActionAuditCommand` |
| Métricas CPU%/memória | `GetContainerStatsAsync` one-shot (`stream=false`), fórmula oficial `docker stats`; endpoint `?stats=true` enriquece em paralelo |
| Logs de container | `GetContainerLogsAsync` com demultiplex do stream de 8 bytes; endpoint `GET .../logs?tail=` (máx. 2000) |
| IA sobre logs e cluster | `POST /admin/cluster/docker/logs-insight` + `GetClusterInsightsCommand` — prompts em PT, limites de payload, sem inventar fatos |
| Registro de instâncias/réplicas | `ServiceInstance` + CRUD + health probe HTTP + distribuição por peso + delete |
| Gateway de IA multimodelo | `AiGateway` + `AiProviderRouter` por capability + `AnthropicProvider` (Claude) + Ollama + providers externos; tela `AiProvidersPage` com cadastro e teste de conexão |
| OpenTelemetry | API instrumentada (traces ASP.NET/HttpClient + métricas) → OTLP → `otel-collector` (profile obs) |
| Logs centralizados | Seq (`seq` profile obs) + `LogsPage` |
| Monitoramento de dispositivos | Módulo `Monitoring` completo (checks, polling, resultados, prevenção Wi-Fi) + Zabbix (server/web/db/agent) |
| Saúde da plataforma | `HealthPage` + `/health` agregado |
| Auditoria | `AuditEntry` via pipeline (`IAuditableRequest`) em todas as operações admin |
| Permissões dinâmicas | ADR-066: matriz role×módulo + perfis customizados (`AccessRole`/`UserAccessRole`) editáveis pela UI |

## 2. Funcionalidades parcialmente implementadas

| Item | O que falta |
|---|---|
| Observabilidade de container (§3) | Hoje: CPU%, memória usada. Faltam: limite de memória, memória %, rede RX/TX, I/O disco, PIDs, reinícios, healthcheck, uptime — todos **disponíveis** via `/containers/{id}/stats` + `/containers/{id}/json` (inspect), só não expostos ainda |
| Topologia (§5) | `docs/diagrams/infracontrol-cloud-topology.html` é snapshot manual do compose (Archify). `TopologyPage` existente é topologia de **rede de dispositivos** (F17), não de containers — não confundir |
| Tráfego/requisições (§4) | OTel já emite traces/métricas de request, mas o collector só exporta `debug`→console. Não há Prometheus/Grafana/Loki/Tempo no compose nem endpoint que agregue latência P50/P95/P99, throughput ou distribuição por instância |
| Claude (§6) | Provider Anthropic existe e roteia por capability; **ferramentas de consulta** (a IA buscar dados autorizados sozinha) não existem — hoje a IA recebe prompt montado, não tem tool-use |
| Escala (§2) | Socket controla containers existentes; criar/escalar exige agente no host ou serviço privilegiado com compose — não implementado |
| Frontend infra (§7) | `ClusterPage` concentra tudo; módulos Overview/Containers/Compose/Topology/Traffic/Resources/Processes/Logs/Traces/AI/Incidents/Audit como experiências separadas não existem |

## 3. Funcionalidades inexistentes

- Agente/serviço de gerenciamento Compose no host (criar, escalar, atualizar serviços, profiles).
- `exec`/`rm`/`scale` Docker (decisão de segurança pendente — spec exige dupla confirmação + motivo + auditoria).
- Métricas de tráfego por instância e rateio real de requisições (não há balanceador interno medido; nginx é o único gateway).
- Backend de métricas (Prometheus), dashboards (Grafana), logs (Loki) e traces (Tempo) — collector hoje é stub `debug`.
- Consumo por processo individual (PID-level) — tecnicamente limitado: stats do cgroup dão PIDs totais, não por processo; exigiria cAdvisor ou agente no host.
- Tool-use da IA sobre dados da plataforma (consultas autorizadas com permissão própria).

## 4. Limitações técnicas

1. **Socket Docker = root no host**: qualquer comprometimento da API = controle total dos containers. Menor privilégio real exigiria socket proxy ( Tecnativa/socket-proxy ) com allowlist — decisão pendente.
2. **Stats one-shot**: CPU%/rede/I/O são amostras instantâneas, não séries temporais. Para tendência é preciso coletor (cAdvisor/Prometheus).
3. **Sem cAdvisor**: PID count existe em `pids_stats`, mas consumo por processo não é exposto pelo Engine API.
4. **Compose scale**: `docker compose up --scale` precisa do arquivo compose + CLI no host; de dentro do container não há como invocar. Alternativa: agente systemd/cron no host ou container privilegiado dedicado.
5. **Tráfego**: nginx não expõe métricas por upstream sem `stub_status`/`prometheus` module; instâncias `api` atrás do nginx não são balanceadas hoje (api é única). Rateio real só existe quando houver réplicas + métrica de request por instância (OTel já tem `http.server` metrics — falta backend).
6. **Healthcheck**: visível via inspect (`State.Health`), mas só para imagens que definem HEALTHCHECK.

## 5. Dependências necessárias

- **Decisão A (métricas)**: Prometheus + cAdvisor + Grafana (profile `obs`) — recomendado; dá séries temporais de container e request. Alternativa leve: coletor próprio persistindo amostras no Postgres.
- **Decisão B (traces/logs)**: Tempo + Loki no collector (hoje debug→console) — ou manter Seq como backend único.
- **Decisão C (escala)**: agente no host (script/systemd timer) vs container privilegiado com docker-cli + compose file montado.
- **Decisão D (socket)**: manter RW direto vs socket-proxy com allowlist (start/stop/restart/logs/stats/inspect apenas).
- **Decisão E (nginx)**: habilitar métricas de upstream (`stub_status` ou módulo prometheus) para tráfego real.
- Claude: nenhuma dependência nova — `AnthropicProvider` já existe; falta tool-use.

## 6. Plano por fases (da spec §9) com estado

| Fase | Escopo | Estado |
|---|---|---|
| 1 Auditoria | Este documento | ✅ concluída |
| 2 Métricas/processos | Expandir stats+inspect (limites, %, rede, I/O, PIDs, restarts, health, uptime); visão por container/projeto/serviço | ✅ concluída |
| 3 Docker/escala real | `compose-agent` dedicado (Decisão C implementada): scale real via `docker compose up -d --no-deps --scale`, `rm` de container parado com dupla confirmação+motivo+auditoria, limites CPU/mem em runtime (`docker update`) | ✅ concluída (scale 1→2→1 validado ao vivo; rm/limits via API) |
| 4 Tráfego/balanceador | Distribuição por instância/réplica implementada (`instance` no `request_metric_buckets` = hostname do container, migração `AddInstanceToRequestMetricBuckets`); backend OTel→Prometheus e métricas nginx seguem pendentes | 🚧 parcial |
| 5 Topologia dinâmica | Endpoint `GET /admin/cluster/docker/topology` (containers+redes+portas+projeto, ao vivo) | ✅ concluída |
| 6 IA com ferramentas | `logs-insight` aceita container, serviço Compose inteiro (todas as réplicas) ou projeto; tool-use completo segue pendente | 🚧 parcial |
| 7 Frontend | ClusterPage unificada (Docker real + cadastro, tráfego por rota e por instância, diálogos de escala/remoção/limites); RetentionPage admin | 🚧 parcial |
| 8 Testes | Integração, segurança (socket/allowlist), desempenho | ⬜ pendente |
