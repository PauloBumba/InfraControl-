# F54 — Redesenho do Dashboard Principal (referência visual: UniFi Network)

> **Status:** PLANEJADO (F54-T001 concluída em 2026-11-12)
> **Pedido do usuário:** transformar o dashboard inicial em central de
> gerenciamento/observabilidade de infraestrutura, com experiência UniFi Network,
> dados 100% reais e sem tocar desnecessariamente nos módulos existentes.

## 1. Decisões do usuário (2026-11-12)

| # | Decisão |
| --- | --- |
| D1 | **Estrutura:** cabeçalho + **KPIs globais sempre visíveis** + **3 abas** — `Network`, `Infrastructure`, `Operations` (não 3 rotas novas, não página única em blocos). |
| D2 | **Tema:** respeitar o `data-theme` global (claro/escuro existente); identidade visual UniFi aplicada sobre o tema corrente. |
| D3 | **Tráfego/banda:** séries históricas vêm do **Zabbix history** (não duplica coleta); UniFi contribui com **snapshot ao vivo** (clientes, ports, DPI). Persistir telemetria UniFi continuaria proibido pela **REGRA 8 do ADR-028** — se um dia for preciso, exige ADR novo. |
| D4 | **Topologia:** só dados reais (`/network/topology` com `isGhost` e `/integrations/unifi/topology` com `confidence`); conexão estimada/ não confirmada deve ser identificada na UI. |
| D5 | **Zero dado fictício:** os mocks órfãos de `src/data/*.ts` (gatewayData, trafficData, topologyNodes…) **não** podem ser usados; só `navigation`/`secondaryNavigation` permanecem vivos. |
| D6 | **Zabbix continua a fonte de coleta/detecção** de métricas; InfraControl consolida e apresenta. |

## 2. Auditoria de fontes (F54-T001 — resumo da auditoria completa)

### 2.1 Blocos × fontes reais

| Bloco (spec §8) | Fonte real | Endpoint(s) | Estado |
| --- | --- | --- | --- |
| 1–2. Cabeçalho + KPIs | Inventário, alertas, incidentes, clientes | `GET /inventory/devices?type=&pageSize=1` (totalCount por tipo), `GET /monitoring/alerts?status=open`, `GET /incidents?status=active`, `GET /integrations/unifi/clients` | ⚠️ sem agregado único |
| 3. Gráficos | Polls, eventos, Zabbix history | `GET /monitoring/results/{id}`, **endpoint novo** `GET /dashboard/series`, `GET /integrations/zabbix/history?itemId&from&to` (92 dias) | ⚠️ precisa do endpoint novo |
| 4. Topologia | Connections/LLDP | `GET /network/topology` (nodes+edges+`isGhost`), `GET /integrations/unifi/topology` (`confidence`) | ✅ pronto |
| 5. UniFi | Integração F26 | `GET /integrations/unifi/dashboard` (sites/switches/APs/ports/deviceStates/controllerReachable), `/wifi`, `/insights`, `/clients`, `/networks`, `/switches/{mac}/ports` | ✅ pronto (snapshot ao vivo) |
| 6. Zabbix | Integração F27/F19 | `GET /integrations/zabbix/summary`, `/problems?minSeverity`, `/hosts`, `/history` | ✅ pronto |
| 7–8. Eventos/alertas | Alertas, incidentes, audit, notificações, MonitoringEvents | `GET /monitoring/alerts`, `GET /incidents`, `GET /admin/audit`, `GET /notifications`; **`MonitoringEvents` persistido SEM endpoint de leitura** | ⚠️ precisa de feed unificado |
| Status por device | Consolidado (cache 30s) | `GET /inventory/devices/{id}/status` → `Available/Degraded/Unavailable` | ⚠️ só por device |

### 2.2 Lacunas a implementar (mínimo viável)

1. **`GET /api/v1/dashboard/overview`** (F54-T002) — 1 chamada: contagens por
   tipo de device, status consolidado agregado (online/degraded/offline),
   alertas abertos por severidade, incidentes ativos, clientes conectados,
   impressoras com suprimento crítico. Cache curto (60s).
2. **`GET /api/v1/dashboard/series?period=1h|24h|7d|30d`** (F54-T003) —
   séries para os gráficos: disponibilidade/últimos polls por dia,
   alertas abertos/fechados por dia, incidentes abertos/resolvidos por dia.
3. **`GET /api/v1/dashboard/events?limit=`** (F54-T004) — feed unificado com
   severidade: `MonitoringEvents` (PROBLEM/OK — hoje persistido e órfão de
   leitura) + alertas + recuperações, ordenado por ocorrência.
4. **Catálogo de itens Zabbix p/ tráfego** — expor discovery de itens de
   banda por host para alimentar `/zabbix/history` nos gráficos (D3).
5. **Fix SignalR:** `startNotificationsConnection()` **nunca é chamado** em
   produção (bug achado na auditoria) — push `notification`/`health-status`
   está morto; religar no boot do AppLayout.

### 2.3 Regras transversais aplicáveis

- Envelope `ApiResponse`; `BaseQuery` + `IAuthorizedRequest` (RoleAccess) nos
  novos endpoints (docs/24); policies `Operate` para leitura de indicadores.
- UniFi: REGRA 8 ADR-028 — nada de persistir telemetria ao vivo.
- Retenção: `MonitoringResults` 180d, `MonitoringEvents` 90d (já existente).
- Sem N+1: agregados no banco (LINQ/EF), não por device no client.

## 3. Divisão em Tasks (ordem de execução, uma IN_PROGRESS por vez)

- [ ] **F54-T001** Doc de plano (matriz fonte×bloco, decisões, lacunas) — docs/69 + registro no TASKS/MASTER INDEX.
- [x] **F54-T002** Backend `GET /dashboard/overview` (contagens + status agregado) + testes unit/integração. <!-- 2026-11-12: clientes conectados ficaram fora do overview (D3 — vão de /integrations/unifi/dashboard ao vivo); status agregado usa sinais locais (ping + alertas), o detalhe com Zabbix continua em /inventory/devices/{id}/status. -->
- [ ] **F54-T003** Backend `GET /dashboard/series` (períodos 1h/24h/7d/30d) + testes.
- [ ] **F54-T004** Backend `GET /dashboard/events` (feed unificado c/ severidade) + fix do boot do SignalR.
- [ ] **F54-T005** Frontend: novo shell do dashboard — cabeçalho com status geral + KPIs globais clicáveis (navegam para a área já com filtro) — tema global (D2), React Query.
- [ ] **F54-T006** Frontend: aba **Network** — gráficos de tráfego/banda (Zabbix history + snapshot UniFi), clientes, VLANs, topologia embutida (D4).
- [ ] **F54-T007** Frontend: aba **Infrastructure** — disponibilidade, uptime, CPU/mem/disco (Zabbix), impressoras/toner, servidores/VMs/câmeras.
- [ ] **F54-T008** Frontend: aba **Operations** — incidentes, alertas, eventos recentes (feed novo), evolução no tempo, resumo Zabbix.
- [ ] **F54-T009** Testes da página, atualização automática (refetchInterval + SignalR), docs, TASKS/PROJECT_STATE, validação ao vivo.

## 4. Atualização dinâmica (sem sobrecarregar o backend)

- React Query `refetchInterval` por bloco (60s KPIs; 5–15 min para séries
  pesadas), reutilizando os caches existentes (`CachingBehavior` 30–60s no
  backend para summary/status).
- Push SignalR (`useOperationalRefresh`) para refetch imediato em
  alerta/incidente novo — depende do fix do boot (T004).
- Botão "Atualizar" global já existente no topbar dispara
  `window "infracontrol:refresh"` (mantido).

## 5. Critérios de aceite globais

1. Todo número exibido vem de endpoint real (zero mock/fictício em produção).
2. KPIs clicáveis levam à área correspondente com filtro aplicado.
3. Topologia não inventa conexões; estimativas identificadas (`isGhost`/`confidence`).
4. Séries de banda vêm do Zabbix; UniFi só snapshot (D3).
5. Tema claro/escuro segue o toggle global (D2).
6. Testes verdes (vitest front + unit/arch back), build `tsc`/vite OK.
7. Módulos existentes não alterados desnecessariamente.
