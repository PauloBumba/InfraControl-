# UI/UX

## Princípio

Interface semelhante à experiência de plataformas modernas de gerenciamento de infraestrutura, mas vendor-agnostic.

## Áreas

- Dashboard
- Devices
- Switches
- Ports
- Topology
- Monitoring
- Alerts
- Incidents
- Tasks
- Services
- SLA/SLO
- Knowledge
- Notifications
- Locations
- Racks
- Users
- Roles
- Audit
- Remote Access
- Settings

## Frontend

React + TypeScript + PrimeReact + PrimeFlex + PrimeIcons.

## Real time

SignalR.

## Estado

React Query para server state.

Redux Toolkit para estado global da aplicação.

## Dashboard (Fase 40 — estilo UniFi)

O `/dashboard` (home do app autenticado) segue a hierarquia visual do UniFi Network:

```text
Convites pendentes (quando houver)
Ação rápida (Novo projeto)
HealthBar          ← dots de estado: Alertas / Incidentes / Tarefas / Inventário
Faixa Frota (F54)  ← totais por status consolidado + tipos + alertas Open
PerformanceSection ← seletor 1h/24h/7d/30d + 2 gráficos (disponibilidade,
                     métrica Zabbix por host/item)
KPI cards          ← Dispositivos, Alertas, Incidentes, Clientes, Tarefas (clicáveis)
Layout 2 colunas:
  principal (1.6fr)              lateral (1fr)
  ├ Incidentes Recentes          ├ Eventos recentes (alertas + Zabbix — F54)
  ├ Dispositivos                 ├ Alertas Abertos
  ├ Clientes Descobertos         ├ Tarefas
  ├ Monitoramento (Zabbix)       └ Eventos Recentes (auditoria)
  ├ Topologia UniFi embutida (F54)
  └ Resumo UniFi (F54)
```

- **HealthBar** (`components/dashboard/HealthBar.tsx`): presentacional; status
  `ok | warning | critical | unknown` — `unknown` = sem dado (zero ≠ erro,
  convenção F26 8.2). A página deriva os itens dos dados reais já buscados.
- **DeviceHealthCard** (`components/dashboard/DeviceHealthCard.tsx`): card de
  KPI com `tone` semântico (blue/purple/green/warning/critical); vira
  `<button>` quando tem `onClick`.
- **Painel Zabbix**: `getZabbixSummary` carregado independentemente — falha ou
  não-configurado oculta só o painel, nunca a página. Gráfico de barras de
  problemas por severidade (`problemsBySeverity`).
- **CSS**: `src/styles/pages/dashboard.css`, todas as classes `uni-dash-*`
  com tokens `--uni-*` — dark mode automático via `[data-theme="dark"]`,
  zero cores fixas. Layout colapsa para 1 coluna ≤1100px.
- O doughnut "Distribuição operacional" foi removido (F40-T002): misturava
  contagens incomparáveis.

### Dashboard F54 — central de observabilidade

Blocos novos em `components/dashboard/DashboardInsights.tsx`, todos
self-fetching com falha isolada (um endpoint fora do ar nunca derruba os
demais) e dados 100% reais:

- **FleetStatusStrip** — `GET /inventory/devices/status-overview`
  (F54-T001): KPIs de status consolidado (Available/Degraded/Unavailable/
  Unknown), totais por tipo e alertas Open; cada KPI navega para
  `/devices?status=…` ou `/alerts`.
- **PerformanceSection** — seletor `1h/24h/7d/30d` compartilhado:
  - *Disponibilidade dos checks internos* —
    `GET /monitoring/results/availability` (% de polls com sucesso por
    bucket; buckets sem polls ficam como lacuna, nunca zero inventado).
  - *Métrica Zabbix* — seletor de host (read-model `/hosts`) + item
    (`/hosts/{id}/items`) + série real (`/hosts/{id}/series`, history.get
    em lote); estados honestos para "sem histórico"/"indisponível".
- **TopologyEmbed** — `UniFiTopologyMap` (topologia UniFi ao vivo,
  `getUnifiTopology`) em painel resumido com link para `/unifi`. O mapa
  interno da F53 (`/network/topology`) ainda não foi embutido — a F53
  está em andamento na árvore; integrar quando o contrato estabilizar.
- **UniFiSummaryPanel** — `getUniFiDashboard`: sites/switches/APs/
  clientes/portas up/offline, saúde do controller e staleness do sync.
- **EventsCenter** — feed unificado: `listAlerts` (todos os status,
  source Native|Zabbix) + `getZabbixProblems` (incl. recuperados),
  ordenado por severidade e data; painel de auditoria admin segue
  separado ("Eventos Recentes").
- Services novos: `getFleetStatusOverview` (inventory), `getZabbixHostItems`/
  `getZabbixHostSeries` (zabbix), `getMonitoringAvailability` (monitoring).
