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
  concluiu (2026-11-14, contrato estável); embed é candidato a
  complemento (pendência registrada na F54-T007).
- **UniFiSummaryPanel** — `getUniFiDashboard`: sites/switches/APs/
  clientes/portas up/offline, saúde do controller e staleness do sync.
- **EventsCenter** — feed unificado: `listAlerts` (todos os status,
  source Native|Zabbix) + `getZabbixProblems` (incl. recuperados),
  ordenado por severidade e data; painel de auditoria admin segue
  separado ("Eventos Recentes").
- Services novos: `getFleetStatusOverview` (inventory), `getZabbixHostItems`/
  `getZabbixHostSeries` (zabbix), `getMonitoringAvailability` (monitoring).

## Topologia (Fase 53 — mapa interativo estilo UniFi)

`/network/topology` (`pages/TopologyPage.tsx`) — mapa SVG único que
serve as duas visões, sem mutations; contrato em `GET /network/topology`
(`TopologyQuery`, F53-T002). Origem/confiança vivem em `connections`
(migração `AddConnectionOrigin`, F53-T001).

### Hierarquia e faixas (F53-T003)

- Layout em faixas por `layer` — BFS multi-raiz no backend a partir de
  Router/Firewall sobre as Connections reais; sem caminho → "Sem
  hierarquia". Devices tipo→nome em colunas, interfaces em satélite;
  faixa recolhida ocupa só o cabeçalho (`aria-expanded` na barra do
  mapa) e some da altura do SVG.
- Pan por arrasto (clique pós-arrasto suprimido acima de 4px), zoom
  50–150% e "Centralizar" reseta pan+zoom (100%); viewport fixo em
  640px com overflow hidden.

### Filtros e destaque (F53-T004)

- Busca (nome/IP), categoria, localização, VLAN e "só problemas" —
  combinação AND; `filteredDeviceIds` dimensiona as faixas (faixa sem
  nenhum device passando some, mantendo o toggle).
- VLAN do device = união das VLANs nas pontas das Connections dele.
- Selecionado: conexões com traço forte, vizinhos com anel `#93c5fd`,
  resto esmaecido (0.12).

### Visão física ↔ lógica (F53-T006)

- Toggle **Física | Lógica** (`aria-pressed`) no cabeçalho do mapa;
  barra de faixas rotula "Camadas" (físico) / "VLANs" (lógico). Troca
  preserva filtros, seleção, colapso e pan/zoom.
- Lógico: faixas `VLAN N — Nome` (+ "Sem VLAN"); device em várias
  VLANs ocupa um slot em cada faixa (chave `band|node`, cópias não
  colidem). Aresta só nas faixas das VLANs dela; sem VLAN ou pontas em
  faixas disjuntas (ex. r1{10} ↔ fw{20}) a Connection fica só na
  visão física — sem dado não há como posicionar. Colapso lógico usa
  chaves `vlan:N` no mesmo `collapsedLayers` das camadas.

### Painéis de detalhe (F53-T005)

- **Device (11.5)** — ficha do inventário (MAC, série, modelo,
  fabricante, VLAN, local) + `system-info` (uptime) + último poll +
  alertas recentes + Zabbix (match exato host/nome/ip → até 5 leituras
  úteis) + vínculo UniFi por MAC + portas do switch com o que está
  conectado (match da interface nas duas pontas; ghost = "Vizinho não
  gerenciado · porta remota").
- **Conexão (11.3)** — portas das pontas (ghost mostra a porta remota),
  velocidade/duplex, VLANs, estado da porta (`linkUp`) e origem ×
  confiança (`manual`/`lldp`/`cdp`/`unifi` × `confirmed`/`estimated`).
- Honestidade: tipo da conexão = "—" (sem dado no contrato — nunca
  inventado); falha de integração degrada só a linha (loading "…",
  sem dado "—"), nunca o painel inteiro.
