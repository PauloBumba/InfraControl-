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
KPI cards          ← Dispositivos, Alertas, Incidentes, Clientes, Tarefas (clicáveis)
Layout 2 colunas:
  principal (1.6fr)              lateral (1fr)
  ├ Incidentes Recentes          ├ Alertas Abertos
  ├ Dispositivos                 ├ Tarefas
  ├ Clientes Descobertos         └ Eventos Recentes (auditoria)
  └ Monitoramento (Zabbix)
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
