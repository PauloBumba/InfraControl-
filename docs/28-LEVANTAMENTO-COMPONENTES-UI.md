# Levantamento — Componentes reutilizáveis, PrimeReact e PrimeFlex (front) + legibilidade (back)

- **Data**: 2026-10-02 (revisão mapeamento página×PR: **2026-10-03**)
- **Pedido**: "ajusta back e front, olha onde pode usar o PrimeReact e o PrimeFlex e deixa legível — faz um levantamento por componentes que podem ser reutilizados." + "mapeia DataTable/DataView e o que vira reutilizável".
- **Natureza**: levantamento vivo — §10 atualizado após Fases A/B/C parciais; execução continua em TASKS (Fases B/C).

---

## 1. Situação atual das dependências *(atualizado 2026-10-03)*

| Pacote | Versão instalada | Importado? | Em uso real? |
|---|---|---|---|
| `primereact` | ^10.9.4 | tema `lara-light-blue` + `primereact.min.css` + `styles/prime-overrides.css` | **Sim** — `DataTable`/`Column` (Audit, Users), `Tag` (via `StatusChip`), `Skeleton` (via `LoadingRows`), `Dropdown` (via `SelectField`, ainda sem páginas) |
| `primeflex` | ^4.0.0 | `primeflex/primeflex.css` no `main.tsx` | uso pontual nos wrappers `ui/` (flex/gap); páginas ainda majoritariamente `uni-*` / inline |
| `primeicons` | ^7.0.0 | CSS global | **Uso amplo** — `<i className="pi pi-…">` ✓ |

Conclusão: bibliotecas pagas e **parcialmente adotadas**. O ganho agora é migrar páginas e adotar wrappers já criados (ex.: `SelectField` existe e **0 páginas** o importam).

## 2. Design system próprio existente (`uni-*`) + `components/ui/`

`src/styles.css` tem ~200 regras `uni-*` (design system próprio, visual UniFi) + skin Prime em `styles/prime-overrides.css`.

**Famílias ativas (com uso real em `.tsx`)**:

| Classe | Papel |
|---|---|
| `uni-panel` / `uni-panel-header` / `uni-panel-body` | cartão genérico — base visual do app |
| `uni-clients-table` | legado das tabelas densas UniFi — removido das áreas migradas para `DataTable`; poda da regra fica para o fechamento da Fase C |
| `uni-page-empty` | estado vazio ilustrado |
| `uni-login-*` | tela de login |
| `uni-alerts-list` / `uni-alert-*` | lista (Alerts + feed Notifications) — candidatas a `DataView` |
| `uni-toggle-*`, `uni-settings-*` | Settings + toggle Notifications |
| `uni-btn-*` | wrapper `Button` |

**CSS órfão (~60 regras do mock antigo)**: `uni-metric-*`, `uni-gateway-*`, `uni-events-*`, `uni-throughput-*`, `uni-chart-*`, `uni-logs-table` (órfã desde Fase A)… — poda na Fase C.

**`components/ui/` (13 wrappers — 2026-10-03)**: `Button`, `CopyableId`, `Dialog`/`ConfirmDialog`, `ErrorBanner`, `FormField`/`FormError`, `LoadingBlock`, `LoadingRows`, `NoticeBanner`, `PageHeader`, `Pagination`, `SearchInput`, `SelectField`, `StatusChip`. Auth/layout: `AccessDenied`, `Can`, `AppLayout`.

## 3. Padrões duplicados medidos (o que grita "extraia um componente")

| Padrão repetido | Nº de arquivos/páginas | Onde |
|---|---|---|
| Banner de erro inline (mesmas strings) | **19** | quase todas as páginas |
| `useState`/`setLoading(true)` manual | **18** | idem |
| Tabela manual `<table>` + cabeçalho `uni-logs-table`/`uni-clients-table` | **10** | Sessions, Audit, Clients, Incidents, Remote, Sla, Switches, Users, Notifications, DeviceDetail |
| `<select>` nativo estilizado à mão | **8** | Settings, Audit, Devices, Incidents, Knowledge, Switches, Tasks, Users |
| Paginação manual "Anterior/Próxima" | **7** | Audit, Clients, Incidents, Knowledge, Network, Notifications, Alerts |
| Input de busca com `pi-search` | **5** | topbar + páginas |
| `datetime-local` (período) | 1 | Audit |
| `buttonStyle: CSSProperties` local | LogsPage + botões "Filtrar/Limpar" repetidos | vários |

## 4. Mapa de adoção PrimeReact (substituições concretas)

| Componente PrimeReact | Substitui | Páginas beneficiadas | Ganho |
|---|---|---|---|
| `DataTable` + `Paginator` | tabelas manuais + paginação manual | 10 tabelas / 7 paginações | ordenação, paginação, responsividade e visual prontos |
| `Dropdown` (ou `Select`) | `<select>` nativo | 8 | visual uniforme, filtro consistente |
| `Calendar` / `DatePicker` | `datetime-local` | Audit | fuso/hora com UI correta |
| `InputText` | inputs manuais com `pi-search` | 5 | consistência + `pt-BR` |
| `Button` | `buttonStyle` local e botões repetidos | LogsPage + dezenas | variantes (primário/secundário/ghost) |
| `Tag` | chips de cor colados (`outcomeChip`, severidade) | Audit, Alerts, Incidents, Tasks | vocabulário visual único |
| `Message` / `Toast` | banner de erro duplicado | **19 arquivos** | erro centralizado + feedback de ação |
| `Skeleton` | "Carregando…" manual | 18 | loading sem piscar layout |
| `Dialog` | confirmações embutidas | Users, Settings, deletos | acessibilidade + foco |

Tema `lara-dark-blue` já está no `main.tsx` — adoção não muda a paleta, só entrega componentes.

## 5. PrimeFlex — onde usar

Hoje importado e ocioso. Uso recomendado (**sem** reescrever as páginas existentes — `uni-*` já resolve):

- nos **novos** componentes de `components/ui/` (grid/flex/gap: `grid`, `col-*`, `flex`, `gap-*`, `flex-wrap`) no lugar de `style={{ display:"flex", gap:12, flexWrap:"wrap" }}` repetido;
- na refatoração de pages quando mexer nelas por outro motivo;
- **não** fazer rewrite em massa só por causa do PrimeFlex (risco alto, ganho baixo).

## 6. Componentes a extrair (reutilização) — progresso 2026-10-03

| # | Wrapper | Status | Notas |
|---|---|---|---|
| 1 | `ErrorBanner` | ✅ | adoção ampla |
| 2 | `Pagination` | ✅ | 6 páginas; some em páginas que forem p/ DataTable lazy |
| 3 | `PageHeader` | ✅ | ampla |
| 4 | `StatusChip` | ✅ | usa `Tag` |
| 5 | `SearchInput` | ✅ | falta AppLayout |
| 6 | `CopyableId` | ✅ | falta Sessions/DeviceDetail |
| 7 | `LoadingRows` / `LoadingBlock` | ✅ | adoção parcial |
| 8 | `FormField` | ✅ | adoção parcial |
| 9 | `Button` / `Dialog` | ✅ | Users/Sessions |
| 10 | `SelectField` | ✅ adotado | 0 `<select>` em pages |
| 11 | `SwitchField` (`InputSwitch`) | ✅ | Settings + Notifications |
| 12 | `MetricChart` (`Chart`+chart.js) | ✅ piloto | Dashboard doughnut; DeviceDetail line depois |
| 13 | `FilterBar` / `DateTimeField` | ⏳ opcional | só se o markup continuar se repetindo |

Local: `src/components/ui/` (named export, comentário no topo, pt-BR).

## 7. Legibilidade no BACKEND (pendências — `src/` está com a sessão paralela da Fase 19)

| Item | O quê | Observação |
|---|---|---|
| `AuditEntrySummary` ganhar `actorName` | join com `DomainUsers.DisplayName` no reader | eliminaria a resolução client-side feita na AuditPage (mapa `listUsers()`); **task pequena, rodar após a Fase 19** |
| `action` humanizado | manter **cru** no contrato (filtro é igualdade exata) | a humanização fica no front (já feito); não mudar backend |
| Mensagens Serilog | já há pt-BR parcial ("Requisição GET … respondeu …") | padronizar quando mexer no logger; baixa prioridade |
| Logs de workers/outbox | formato atual aceitável | sem mudança proposta |

## 8. Execução proposta (fases separadas — aprovação do usuário)

- **Fase A (front, minha sessão)**: criar `src/components/ui/` com `ErrorBanner`, `Pagination`, `PageHeader` e adotar nas páginas que já vou mexer; piloto de `DataTable` na **AuditPage** (menor risco, caso de uso completo: filtros, paginação, ator legível).
- **Fase B (front)**: `Dropdown` nos 8 selects + `Tag` nos chips + `Skeleton` nos loadings.
- **Fase C (front)**: demais páginas de tabela para `DataTable`; poda do CSS órfão (~60 regras) após grep de confirmação.
- **Fase D (back, pós-Fase 19)**: `actorName` no reader de auditoria + smoke.

## 9. Critérios de aceite globais

- `npx tsc -b`, `npm run lint`, `npm run build` verdes a cada fase;
- nada de mudança de comportamento de API/contrato;
- visual UniFi preservado (tema atual);
- container reconstruído + smoke das telas afetadas após cada fase;
- nenhuma task de F19/F20/F21 antecipada (F22-T001).

---

## 10. Mapeamento página × componentes *(revisão 2026-10-03 — pós Fases A/B/C parciais)*

Revisão real das **27 páginas + `AppLayout`** (~8.019 linhas em `src/pages` + layout).
Legenda: **PR** = PrimeReact; **ui/** = wrapper em `src/components/ui/`;
✅ feito · 🟡 parcial · ⏳ pendente · ❌ não aplicar.

### 10.0 Decisão rápida: DataTable vs DataView vs manter

| Quando usar | Critério | Páginas |
|---|---|---|
| **`DataTable`** | linhas com colunas fixas (CSV mental), sort/paginação, ações por linha | Sessions, Clients, Incidents, Remote, Sla, Switches, Notifications prefs, DeviceDetail ×6 |
| **`DataView`** | lista/card (ícone + título + detalhe), layout não-tabular | ~~**Alerts**, **Notifications feed**, **Knowledge**~~ **dispensado em 2026-10-03** (ver nota abaixo) |
| **Manter custom** | kanban, SVG, painéis métricos, empty-state | Tasks (kanban), Topology (SVG), Dashboard, Locations, WiFi |

Em `DataTable`: **AuditPage** (lazy server-side), **UsersPage** (client-side), **SessionsPage**, **ClientsPage** (lazy), **IncidentsPage** (lazy), **RemotePage**, **SlaPage**, detalhe de portas de **SwitchesPage**, preferências de **NotificationsPage** e as 6 tabelas de **DeviceDetailPage**.

### 10.1 Páginas (estado atual × próximo passo)

| Página | ln | Hoje (relevante) | PR próximo | ui/ (estado) |
|---|---:|---|---|---|
| `AppLayout` | 563 | busca `pi-search` inline | `SearchInput` no topbar | ⏳ adotar `SearchInput`/`Button` |
| `DashboardPage` | 480 | 6 painéis + "Carregando…" | `Skeleton`/`Tag` (opcional `ProgressBar`) | ✅ `ErrorBanner` · ⏳ `LoadingBlock` |
| **`AlertsPage`** | 167 | `uni-alerts-list` + chips + `Pagination` | **`DataView`** (não tabela) | ✅ `Pagination`/`ErrorBanner`/`PageHeader`/`StatusChip` · ⏳ `LoadingRows` |
| **`AuditPage`** | 335 | ✅ `DataTable` lazy; restam `<select>` + 2× `datetime-local` | `SelectField`, `Calendar` | ✅ `PageHeader`/`ErrorBanner`/`StatusChip`/`CopyableId`/`SearchInput` |
| **`ClientsPage`** | 202 | `<table>` + busca + `Pagination` | **`DataTable`** | ✅ `Pagination`/`ErrorBanner`/`PageHeader`/`SearchInput`/`StatusChip` · ⏳ `LoadingRows` |
| **`DeviceDetailPage`** | 625 | **6 `<table>`** + "Carregando…" ×N | **`DataTable` ×6** | ✅ `ErrorBanner` · ⏳ `CopyableId`/`LoadingRows`/`StatusChip` |
| `DevicesPage` | 550 | form: 2× `<select>` + textarea + lists | `SelectField`, `InputTextarea`, `Button` | ✅ `ErrorBanner`/`PageHeader`/`NoticeBanner` · ⏳ `FormField`/`LoadingBlock` |
| `ForgotPasswordPage` | 94 | form + mensagem | `InputText`, `Button`, `Message` | ⏳ `FormField`/`Button` |
| `HealthPage` | 49 | status | — | ✅ `PageHeader`/`StatusChip` |
| **`IncidentsPage`** | 297 | `<table>` + 2× `<select>` + textarea + `Pagination` | **`DataTable`**, `SelectField`, `InputTextarea` | ✅ `Pagination`/`ErrorBanner`/`PageHeader`/`StatusChip`/`FormField` · ⏳ `LoadingRows`/`Button` |
| `InsightsPage` | 68 | estático | — | — |
| `InvitationAcceptancePage` | 244 | senha ×2 | `Password`, `Button`, `Message` | ⏳ `FormField`/`Button` |
| **`KnowledgePage`** | 300 | **grid de cards** + `<select>` + busca + `Pagination` | **`DataView`** (layout=grid), `SelectField` | ✅ `Pagination`/`ErrorBanner`/`PageHeader`/`SearchInput`/`StatusChip`/`FormError` · ⏳ `Button`/`LoadingBlock` |
| `LocationsPage` | 204 | empty/list + `LoadingBlock` | — (uni-page-empty ok) | ✅ `LoadingBlock`/`ErrorBanner` |
| `LoginPage` | 256 | senha + erro | `Password`, `Button`, `Message` | ⏳ `FormField`/`Button` |
| `LogsPage` | 103 | `SearchInput` + **`buttonStyle` local** | — | ✅ `PageHeader`/`SearchInput` · ⏳ `Button` (tirar `buttonStyle`) |
| `NetworkPage` | 219 | `type="number"` + `Pagination` | `InputNumber` | ✅ `Pagination`/`ErrorBanner`/`PageHeader`/`FormField` · ⏳ `Button`/`LoadingBlock` |
| **`NotificationsPage`** | 309 | **feed** `uni-alerts-list` + **prefs** `<table>` + toggle CSS + `Pagination` | feed → **`DataView`**; prefs → **`DataTable`** + `InputSwitch` | ✅ `Pagination`/`ErrorBanner`/`PageHeader` · ⏳ `LoadingRows`/`StatusChip`/`SwitchField`* |
| **`RemotePage`** | 358 | `<table>` + chips | **`DataTable`** | ✅ `ErrorBanner`/`NoticeBanner`/`PageHeader`/`StatusChip` · ⏳ `LoadingRows` |
| **`SessionsPage`** | 223 | `<table>` + ✅ `ConfirmDialog` + `LoadingRows` | **`DataTable`** (próximo piloto fácil) | ✅ `ErrorBanner`/`NoticeBanner`/`PageHeader`/`Button`/`ConfirmDialog`/`LoadingRows`/`StatusChip` |
| `SettingsPage` | 214 | 6 abas manuais + ~15 toggles CSS + 1 `<select>` | `TabView` (opcional), **`InputSwitch`**, `SelectField` | ✅ `PageHeader` · ⏳ `SwitchField`*/`SelectField`/`FormField` |
| **`SlaPage`** | 303 | `<table>` + textarea | **`DataTable`**, `InputTextarea` | ✅ `ErrorBanner`/`PageHeader`/`FormField` · ⏳ `LoadingRows`/`Button` |
| **`SwitchesPage`** | 282 | `<table>` + `<select>` + `LoadingBlock` | **`DataTable`**, `SelectField` | ✅ `ErrorBanner`/`LoadingBlock`/`PageHeader` |
| `TasksPage` | 404 | **kanban custom** + 4× `<select>` + textarea | ❌ não DataTable/DataView; `SelectField` nos filters/move | ✅ `ErrorBanner`/`NoticeBanner`/`PageHeader`/`SearchInput`/`FormField` · ⏳ `Button`/`StatusChip` |
| `TopologyPage` | 426 | **SVG próprio** | ❌ não trocar | ✅ `ErrorBanner`/`LoadingBlock` |
| **`UsersPage`** | 622 | ✅ `DataTable` + Dialogs; restam 2× `<select>` + abas manuais | `SelectField`; `TabView` opcional | ✅ `DataTable`/`ErrorBanner`/`PageHeader`/`StatusChip`/`SearchInput`/`FormField`/`Dialog`/`Button` |
| `WiFiPage` | 113 | lista AP + loading | `Skeleton`/`LoadingBlock` | ✅ `ErrorBanner` |
| `NotFoundPage` | 9 | `uni-page-empty` | — | — |

\* `SwitchField` = wrapper novo fino de `InputSwitch` (Settings + Notifications prefs).

### 10.2 Inventário PrimeReact 10.9.4 × decisão *(atualizado)*

| Componente | Decisão | Estado | Onde |
|---|---|---|---|
| `DataTablePanel` + `DataTable`/`Column` | **adotar** | ✅ | casca reutilizável UniFi para painel, tamanho e largura; Sessions, Clients, Incidents, Remote, Sla, Switches e Notifications a consomem; DeviceDetail mantém colunas específicas |
| `DataView` | **dispensar** (2026-10-03) | ✅ | feed de Notifications já migrou para `Timeline` (Fase H); Alerts (`uni-alert-row`) e Knowledge (grid de cards) têm markup próprio único — a duplicação que motivou o `DataView` não existe mais; trocar o wrapper por `p-dataview` seria DOM novo sem ganho funcional, contra a diretriz "sem mexer no layout" (decisão do usuário 2026-10-03) |
| `Dropdown` | **adotar** | 🟡 wrapper pronto | ✅ `SelectField` · **0 páginas adotam** (Audit, Users×2, Devices×2, Incidents×2, Knowledge, Switches, Tasks×4, Settings) |
| `Tag` | **adotar** | ✅ via `StatusChip` | várias páginas |
| `Skeleton` | **adotar** | 🟡 | ✅ `LoadingRows`/`LoadingBlock` · falta ~14 páginas com "Carregando…" |
| `Button` (PR) | via wrapper | ✅ | `ui/Button` adotado em Dialog/Pagination/Users/Sessions + **lote 2026-10-03**: Logs, Knowledge, Incidents, Locations, Clients, Switches, Tasks, Devices, DeviceDetail, Topology, Sla, Remote, Network (32 botões de ação — variant + `style` mínimo de fidelia, `disabled` nativo cobre o `opacity` manual); **mantidos nativos por decisão**: tabs/abas, filtros-chip com cor dinâmica, botões icon-only 28×28/zoom SVG, link do Dashboard, chip "Encerrar" da coluna Remote e as 3 telas de autenticação (Login/Forgot/Invitation — visual sensível, lote próprio) |
| `InputSwitch` | **adotar** | ⏳ | Settings (~15) + Notifications prefs → criar `SwitchField` |
| `Calendar` | **adotar** | ⏳ | Audit (2× `datetime-local`) |
| `InputTextarea` / `InputNumber` | **adotar** | ⏳ | forms Incidents/Sla/Knowledge/Devices/Tasks; Network number |
| `Password` | **adotar** | ⏳ | Login / Invite (Forgot não usa campo senha) |
| `ConfirmDialog` / `Dialog` | **adotar** | ✅ via `ui/Dialog` | Users + Sessions |
| `TabView` | opcional | ⏳ | Settings (6) / Users (2) / Notifications (2) — hoje CSS manual ok |
| `ProgressBar` / `Card` | opcional | — | só sob pedido |
| `Toast` | **adotar** | ✅ Fase F | provider global + ações em Users/Sessions/Notifications |
| `Chart`, `Editor`, `FileUpload`, `Tree`, `Timeline`, … | **fora** | — | Topology = SVG; Knowledge = textarea |

### 10.3 Multiplicador de reutilização (`ui/` → consumo)

| `components/ui/` | criado | adoção nas páginas | próximo |
|---|---|---|---|
| `ErrorBanner` | ✅ | ampla (~20) | — |
| `PageHeader` | ✅ | ampla | AppLayout/auth se fizer sentido |
| `Pagination` | ✅ | Alerts/Clients/Incidents/Knowledge/Network/Notifications | some quando a página for p/ `DataTable` lazy |
| `SearchInput` | ✅ | Audit/Clients/Knowledge/Logs/Users/Tasks | **AppLayout** topbar |
| `LoadingRows` / `LoadingBlock` | ✅ | Sessions + Locations/Topology/Switches | demais loadings inline |
| `StatusChip` (`Tag`) | ✅ | Audit/Alerts/Clients/Health/Incidents/Knowledge/Remote/Sessions/Users | Tasks chips locais |
| `FormField` / `FormError` | ✅ | Users/Incidents/Network/Sla/Tasks/Knowledge | Devices + auth |
| `CopyableId` | ✅ | Audit | Sessions, DeviceDetail |
| `Button` | ✅ | Dialog/Pagination/Users/Sessions | Logs (`buttonStyle`) + forms |
| `Dialog` / `ConfirmDialog` | ✅ | Users, Sessions | — |
| `SelectField` (`Dropdown`) | ✅ | **Audit, Users×2, Devices×2, Incidents×2, Knowledge, Switches, Tasks×4, Settings** | — |
| `NoticeBanner` | ✅ | Sessions/Remote/Devices/Incidents/Tasks | — |
| `SwitchField` (`InputSwitch`) | ✅ | Settings (toggles locais) + Notifications prefs | — |
| `MetricChart` (`Chart`+chart.js) | ✅ | Dashboard doughnut (piloto Fase E) | DeviceDetail line na sequência |
| `FilterBar` | ⏳ opcional | — | Audit/Incidents/Knowledge/Clients (só se repetir markup) |
| `DateTimeField` (`Calendar`) | ⏳ opcional | — | Audit |

### 10.4 Fila de alto ganho (ordem sugerida)

1. **Adotar `SelectField`** nas páginas com `<select>` (wrapper já existe — zero criação).
2. **`DataTable` em `SessionsPage`** (já tem LoadingRows/ConfirmDialog/StatusChip — menor atrito).
3. **`DataTable` lote**: Clients → Incidents → Remote → Sla → Switches → Notifications prefs.
4. ~~**`DataView`**: Alerts → Notifications feed → Knowledge.~~ **Dispensado** (2026-10-03 — feed em `Timeline`; Alerts/Knowledge com markup próprio, sem duplicação; decisão do usuário registrada na §10.0).
5. **`SwitchField`** + Settings/Notifications toggles.
6. **`DeviceDetailPage` DataTable ×6** (maior volume, deixar por último neste ciclo).
7. ~~Fase C: poda CSS `uni-*` órfão após as migrações de tabela.~~ **EXECUTADA 2026-10-03**: 122 regras removidas (69 classes órfãs + modificadores e trechos de `@media`) — `styles.css` 967 → 731 linhas; garantia por diff: nenhuma classe removida tem uso em `.ts/.tsx` (0 de 125 classes usadas afetadas).

Observações:

- **Reutilização > PR cru**: preferir `ui/` que já embute PR (`SelectField`→Dropdown, `StatusChip`→Tag, `LoadingRows`→Skeleton).
- **Incremental**: uma página (ou um wrapper) por vez; `tsc`/`build` verdes; visual UniFi via `prime-overrides.css`.
- **Não** forçar DataTable em kanban (Tasks), SVG (Topology) ou cards métricos (Dashboard).

---

## 11. Catálogo ampliado PrimeReact 10.9 — plano (pesquisa 2026-10-03)

Fonte: [v10.primereact.org](https://v10.primereact.org/) (80+ componentes; app em `primereact@^10.9.4`).
O inventário antigo (§10.2) cobriu só o óbvio (tabela/form). Abaixo: o que **ainda dá** para usar
com caso real no InfraControl — inclusive **Chart**, que a visão do produto pede e ainda **não**
está no `package.json` (exige `chart.js` como peer).

### 11.1 Matriz: componente × onde encaixa × prioridade

| Componente PR | Onde no produto | Wrapper `ui/`? | Prioridade | Nota |
|---|---|---|---|---|
| **`Chart`** (Chart.js: line/bar/doughnut/pie/radar/polar/combo) | Dashboard (contagens/tendência), DeviceDetail (histórico de polls), Sla (uptime/error budget), Health (latência) | `MetricChart` | **P1** | **dep nova**: `npm i chart.js`; tipos line/bar/doughnut bastam no início |
| **`DataView`** | Alerts, Notifications feed, Knowledge | — | ~~P1~~ **dispensado** | decisão 2026-10-03 (§10.0) |
| **`DataTable`** | 8 páginas + DeviceDetail×6 | — | **P1** | já na fila §10.4 |
| **`Timeline`** | feed Notifications / Audit “linha do tempo” / histórico DeviceDetail / ciclo Incidente | `EventTimeline` | **P2** | vertical; markers com cor de severidade |
| **`MeterGroup`** | Sla uptime/error budget, toner/drum %, Health uptime | `UsageMeter` | **P2** | escalar 0–100; melhor que barra CSS solta |
| **`Knob`** (readonly) | Health latency / score, Dashboard “saúde” | opcional | **P3** | dial; usar só se couber no visual UniFi |
| **`ProgressBar`** | Tasks progressão, jobs Remote | — | **P2** | já no inventário; fácil |
| **`Toast`** | feedback pós-ação (convite, revogar sessão, salvar prefs) | `useToast` / provider no App | **P1** | hoje só banner inline; Toast não substitui `ErrorBanner` de load |
| **`Message`** | erro de formulário auth/forms | já parcialmente `FormError` | **P2** | alinhar `FormError` a `Message` |
| **`Badge`** / **`Chip`** | contagem no menu (alertas abertos), filtros removíveis | `CountBadge` | **P2** | Chip ≠ StatusChip (Tag) — Chip é removível/filtro |
| **`Avatar`** / **`AvatarGroup`** | ator na Audit/Sessions, responsáveis Tasks | — | **P3** | iniciais do displayName |
| **`Toolbar`** | barra de filtros+ações (Audit, Incidents, Clients) | `FilterBar` vira Toolbar | **P2** | substitui flex inline repetido |
| **`SelectButton`** | filtro segmented (Alerts status, Notifications tabs leves) | — | **P2** | melhor a11y que botões CSS |
| **`MultiSelect`** | filtros multi (roles Users, severidade Incidents) | via `SelectField` multi | **P2** | |
| **`AutoComplete`** | busca global AppLayout (F2-T007 search) | estender `SearchInput` | **P2** | quando a barra topbar ligar no backend |
| **`Calendar`** | Audit período | `DateTimeField` | **P2** | |
| **`Accordion`** / **`Panel`** | DeviceDetail seções, Settings blocos | — | **P3** | Settings hoje é nav lateral — Accordion só se unificar mobile |
| **`OverlayPanel`** | preview rápido de device/cliente na lista | — | **P3** | popover sem navegar |
| **`ConfirmPopup`** | confirmação leve inline (vs Dialog modal) | — | **P3** | Sessions/Users já têm ConfirmDialog |
| **`Breadcrumb`** | Devices → DeviceDetail | — | **P2** | navegação hierárquica |
| **`SplitButton`** | “Novo” + variantes (device types, export) | — | **P3** | |
| **`Tooltip`** | tooltips ricos (porta switch, truncates) | — | **P3** | hoje `title=` nativo |
| **`BlockUI`** / **`ProgressSpinner`** | overlay em save longo | — | **P3** | |
| **`VirtualScroller`** | Logs / Clients listas longas | — | **P3** | só se paginação server ficar curta demais |
| **`Tree` / `TreeTable`** | Locations→Racks, VLANs/ports | — | **P3** | só se modelar hierarquia na UI |
| **`TabView`** | Settings / Users / Notifications | — | **P3** | opcional — CSS atual ok |
| **`InputOtp`** | Login MFA (código 6 dígitos) | — | **P2** | hoje 1 campo texto; ganho UX |
| **`Password`** | Login / Invite | — | **P2** | strength/toggle eye |
| **`InputMask`** | MAC/IP em forms Devices | — | **P3** | |
| **`ScrollTop`** | páginas longas (DeviceDetail, Audit) | — | **P3** | |

### 11.2 Explicitamente fora (rico no catálogo, sem caso no produto)

| Componente | Por quê |
|---|---|
| `OrganizationChart` | Topology é **SVG próprio** (pan/zoom/filtros) — não substituir |
| `Editor` (Quill) | Knowledge usa textarea; editor rich = escopo produto novo |
| `Galleria` / `Image` / `Carousel` | sem galeria de mídia |
| `FileUpload` | sem upload de anexos hoje |
| `Terminal` / `Dock` / `SpeedDial` | estética desktop alienígena ao UniFi |
| `OrderList` / `PickList` / `Rating` / `ColorPicker` | sem domínio |
| `MegaMenu` / `Menubar` / `SlideMenu` / `TieredMenu` | sidebar custom `AppLayout` já resolve |
| `Stepper` / `Steps` | só se nascer wizard (ex.: onboarding device) — não há |

### 11.3 Fases propostas (além de A–D atuais)

| Fase | Escopo | Entregáveis | Critério de aceite |
|---|---|---|---|
| **B′** (fechar B) | adoções baratas | `SelectField` nas páginas; `SwitchField`; Skeleton restante; `Button` no Logs | selects nativos sumiram das páginas tocadas; tsc/build |
| **C′** (fechar C) | dados | DataTable lote + ~~DataView~~ dispensado (§10.0) + poda `uni-*` ✅ | 0 `<table className="uni-clients-table">` nas páginas migradas · **0 classes usadas removidas na poda** |
| **E — Chart** *(nova)* | visão | instalar `chart.js`; wrapper `MetricChart`; piloto Dashboard (doughnut status + bar contagens) **ou** DeviceDetail (line de Succeeded/Failed ao longo do tempo) | chart renderiza com dados reais da API; a11y `aria-label`; tema UniFi em `prime-overrides` |
| **F — Feedback** | UX ação | `Toast` provider no App + adoção em Users/Sessions/Notifications; `Message` no `FormError`; `Badge` contagem alertas no menu | toast em pt-BR; ErrorBanner de load permanece |
| **G — Chrome** | navegação/filtros | `Toolbar`/`FilterBar`, `Breadcrumb` DeviceDetail, `SelectButton` Alerts, `AutoComplete` topbar (se search API), `InputOtp` MFA | sem regressão de filtro/URL |
| **H — Métricas extras** | ops | `Timeline` feed; `MeterGroup` Sla/Health; `ProgressBar` Tasks; opcional `Knob` | visual coerente; sem inventar endpoint |

**Dependência crítica da Fase E:** `chart.js` **não** está instalado — PrimeReact Chart só encapsula; sem o pacote o import quebra em runtime.

**Ordem recomendada de execução (checkpoint por fase):**

```text
B′ (SelectField + SwitchField)
→ C′ DataTable Sessions + lote
→ C′ DataView (Alerts…)
→ E Chart piloto (Dashboard OU DeviceDetail — escolher 1)
→ F Toast/Badge
→ G Toolbar/Breadcrumb/SelectButton/InputOtp
→ H Timeline/MeterGroup
→ poda CSS órfão
```

### 11.4 Decisões (fechadas 2026-10-03 — autorização "documenta tudo e começa")

| # | Pergunta | Decisão |
|---|---|---|
| 1 | Piloto Chart | **Dashboard primeiro** (visão executiva); DeviceDetail line chart na sequência |
| 2 | Toast flutuante | **Sim** — Fase F; visual UniFi via `prime-overrides` (não substitui ErrorBanner de load) |
| 3 | Topology | **SVG próprio** — OrganizationChart fora |
| 4 | Ordem | **B′ agora** (SelectField + SwitchField) **em paralelo com wrapper Chart** (`MetricChart` + `chart.js`); piloto Dashboard na mesma leva se build verde |

Execução desta leva: wrappers reutilizáveis + adoção SelectField/SwitchField + Chart no Dashboard.

### 11.5 Execução — Fase F (2026-10-03)

- `ToastProvider` e `useToast` em `components/ui/` centralizam o feedback
  transitório em pt-BR (5 s, canto superior direito).
- Users, Sessions e Preferences de Notifications exibem sucesso e falha de
  ações via Toast; erros de carga continuam em `ErrorBanner`, com retry onde
  o contrato já o oferece.
- `FormError` renderiza o `Message` acessível do PrimeReact e `Badge` renderiza
  os totais dinâmicos da navegação lateral. As regras em `prime-overrides.css`
  preservam o acabamento UniFi.

### 11.6 Execução — Fase G (2026-10-03)

- `FilterBar` encapsula `Toolbar` nos filtros de Audit, Clients e Incidents.
- `BreadCrumb` no detalhe do dispositivo torna explícito o retorno para a lista;
  `SelectButton` controla o filtro `status` de Alertas sem alterar os params.
- O login usa `InputOtp` para TOTP de seis dígitos e mantém o campo textual
  alternativo para recovery codes. A busca global server-side existente já
  oferece autocomplete e foi preservada.

### 11.7 Execução — Fase H (2026-10-03)

- `Timeline` organiza o feed paginado de notificações por evento e horário.
- `MeterGroup` mostra consumo/disponibilidade do Error Budget e o uptime já
  exposto na tela de Health.
- `ProgressBar` mostra a conclusão agregada do quadro de tarefas. Não há
  percentual individual no contrato, então esse componente não cria um dado
  novo: usa somente os estados `Resolved` e `Closed` retornados pela listagem.

### 11.8 Execução — ADR-030 Fase 5 (2026-10-08)

- `components/ui/EntityView.tsx` expõe o seletor de visão reutilizável das
  páginas índice: `EntityView<T>` renderiza **tabela** (o `<DataTablePanel>`
  da página), **grid** de cards ou **kanban** (colunas por estado via
  `kanban={{ groupOf, columns, overflowColumnKey }}`; sem config cai no
  grid), `ViewSwitcher` de 3 ícones e `useEntityViewMode(entityKey,
  fallback)` que persiste a preferência **por entidade** em
  `localStorage entityView:<key>` (defensivo + sync entre abas via evento
  `storage`).
- `hooks/queries.ts` é a camada React Query (v5) do dashboard:
  `usePagedQuery`/`useListQuery` para leitura com chave por domínio e
  `useCreateMutation`/`useUpdateMutation`/`useDeleteMutation` que invalidam
  por prefixo da queryKey (criar/editar/excluir dispara refetch automático);
  `mutationErrorMessage`/`asApiError` preenchem `ErrorBanner`/formulários
  com a mensagem pt-BR do envelope.
- 10 páginas migradas de `useState+useEffect+load()` para React Query com
  CRUD completo espelhando a matriz do ADR-030 (escrita atrás de
  `<Can level="operate|administer">` — só esconde; o backend autoriza):
  **Locations/Racks** (kanban de racks por local), **Devices** (kanban por
  tipo + editar/excluir com `updateDevice`/`deleteDevice`), **Switches**
  (kanban por localização; CRUD vive na DevicesPage), **Network** (VLANs),
  **Tasks** (default kanban por `TaskState`, identificação + exclusão),
  **Incidents** (kanban por status + `PUT {id}/status`/acknowledge, sem
  DELETE), **Knowledge** (abas artigos/categorias, edit/delete/status),
  **SLA** (abas serviços/políticas/SLOs + Error Budget), **Invitations**
  (editar roles/cancelar atrás de `Can administer`) e **Remote Sessions**
  (abrir/encerrar SSH-RDP, kanban por status da sessão).
- Validação: `npx tsc -b` · `npm run lint` · `npm run build` verdes;
  backend intocado — unit 1852 · arch 119 · integration 194, 0 falhas.
