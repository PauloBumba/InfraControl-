# Levantamento — Componentes reutilizáveis, PrimeReact e PrimeFlex (front) + legibilidade (back)

- **Data**: 2026-10-02
- **Pedido**: "ajusta back e front, olha onde pode usar o PrimeReact e o PrimeFlex e deixa legível — faz um levantamento por componentes que podem ser reutilizados."
- **Natureza**: levantamento (só leitura) — nenhuma alteração de código acompanha este documento; a execução entra como fase/task aprovada.

---

## 1. Situação atual das dependências

| Pacote | Versão instalada | Importado? | Em uso real? |
|---|---|---|---|
| `primereact` | ^10.9.4 | CSS do tema `lara-dark-blue` + `primereact.min.css` no `main.tsx` | **0 componentes** — nenhum `import … from "primereact/…"` no código |
| `primeflex` | ^4.0.0 | `primeflex/primeflex.css` no `main.tsx` | **~0 classes** — os greps em `className` só encontram nomes `uni-*-grid` (falsos positivos) |
| `primeicons` | ^7.0.0 | CSS global | **Uso amplo** — `<i className="pi pi-…">` em toda a UI ✓ |

Conclusão: as duas bibliotecas que o usuário quer usar **já estão pagas** (instaladas e com CSS carregado); falta apenas adotar os componentes/utilities.

## 2. Design system próprio existente (`uni-*`)

`src/styles.css` tem ~200 regras `uni-*` (design system próprio, visual UniFi).

**Famílias ativas (com uso real em `.tsx`)**:

| Classe | Arquivos | Papel |
|---|---|---|
| `uni-panel` / `uni-panel-header` / `uni-panel-body` | 18 / 6 / 4 | cartão genérico — base visual do app |
| `uni-clients-table` (+ variações) | 9 | tabela densa estilo UniFi |
| `uni-page-empty` | 5 | estado vazio ilustrado |
| `uni-login-*` (família, ~20 classes) | 3–4 | tela de login completa |
| `uni-alerts-list` / `uni-alert-*` | 2 | lista de alertas |
| `uni-toggle-*`, `uni-settings-*` | 1–2 | Settings |

**CSS órfão (0 uso em `.tsx` — sobra do mock antigo do dashboard que virou contrato real)**: `uni-metric-*`, `uni-gateway-*`, `uni-events-*`, `uni-throughput-*`, `uni-chart-*`, `uni-subsystems`/`uni-subsystem-*`, `uni-signal-*`, `uni-progress-*`, `uni-stat-block`, `uni-connector`, `uni-legend-dot`… **~60 regras**. Candidatas a poda (confirmar com grep de string dinâmica antes de apagar).

**Componentes React compartilhados hoje (só 3)**: `components/auth/AccessDenied`, `components/auth/Can`, `components/layout/AppLayout`. **Não existe `components/ui/`** — toda página recria os mesmos padrões inline.

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

## 6. Componentes a extrair (reutilização imediata)

Ordem de valor (menor esforço × maior replicação):

1. **`ErrorBanner`** — mensagem + botão "Tentar de novo" (19 duplicações);
2. **`Pagination`** — página/atual + Anterior/Próxima + total (7 páginas);
3. **`PageHeader`** — título + subtítulo/contagem + ações à direita (todas);
4. **`FilterBar`** — busca + selects + período + Filtrar/Limpar (Audit já tem o caso mais completo);
5. **`StatusChip`** — cores por vocabulário (`outcome` Succeeded/Failed, severidade, status) com `Tag` do PrimeReact;
6. **`SearchInput`** — input + ícone (5 usos);
7. **`CopyableId`** — valor monoespaçado + clique para copiar (já feito na AuditPage; extrair p/ Sessions/DeviceDetail);
8. **`LoadingRows`** — skeleton de tabela (18 usos de loading manual).

Local: `src/components/ui/` (novo), seguindo os padrões dos componentes existentes (named export, docs em comentário no topo do arquivo, pt-BR).

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

## 10. Mapeamento página × componentes (revisão completa do front — 2026-10-02)

Revisão real das **27 páginas + `AppLayout`** (7.518 linhas de `.tsx` em `src/pages`) com os
widgets efetivamente presentes. Legenda: **PR** = componente do `primereact@10.9.4` a adotar;
**ui/** = componente de `src/components/ui/` reutilizável (criado ✅ / pendente ⏳).

### 10.1 Páginas

| Página | linhas | widgets hoje | PR a adotar | ui/ reutilizáveis |
|---|---:|---|---|---|
| `AppLayout` (topbar/menu) | — | busca `pi-search` inline, botões inline | `InputText` + `IconField`, `Button` | ⏳ `SearchInput` |
| `DashboardPage` | 484 | 6 painéis, métricas, barras, chips, error/loading | `Card`, `ProgressBar`, `Tag` | ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `AlertsPage` | 188 | lista com chips, paginação manual, error/loading | `Tag`, `DataView` (ou `DataTable`) | ⏳ `Pagination`*, ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| **`AuditPage`** | 382 | **piloto ✅ `DataTable`+`PageHeader`+`ErrorBanner`**; restam `datetime-local`, `<select>`, input de ação | `Calendar` (período), `Dropdown` (outcome), `InputText`+`IconField` | ✅ os 3 |
| `ClientsPage` | 222 | form, `<table>`, busca, paginação manual, error/loading | `DataTable`, `InputText`, `Button` | ⏳ `Pagination`*, ✅ `ErrorBanner`, ⏳ `SearchInput`/`LoadingRows` |
| `DeviceDetailPage` | 624 | **6 `<table>`s**, empty state, chips, error/loading | `DataTable` ×6, `Tag` | ⏳ `CopyableId`, ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `DevicesPage` | 562 | form (4 input + 2 select + textarea), chips, error/loading | `InputText`, `Dropdown`, `InputTextarea`, `Button`, `Tag` | ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `ForgotPasswordPage` | 94 | form + erro inline | `InputText`, `Button`, `Message` | ⏳ `FormField`* |
| `HealthPage` | 50 | painel de status | `Tag` (UP/DOWN), `ProgressBar` | — |
| `IncidentsPage` | 318 | form (input+textarea+2 select), `<table>`, paginação, chips | `DataTable`, `Dropdown`, `InputText`, `InputTextarea`, `Button`, `Tag` | ⏳ `Pagination`*, ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `InsightsPage` | 68 | estático/leitura | `Skeleton` (se ganhar fetch) | — |
| `InvitationAcceptancePage` | 244 | form com senha, erros inline | `InputText`, `Password`, `Button`, `Message` | ⏳ `FormField`* |
| `KnowledgePage` | 309 | 2 forms, select, busca, paginação manual | `Dropdown`, `InputText`, `InputTextarea`, `Button` | ⏳ `Pagination`*, ✅ `ErrorBanner` |
| `LocationsPage` | 210 | empty, error, loading | `Skeleton`, `Button` | ✅ `ErrorBanner` (uni-page-empty mantido) |
| `LoginPage` | 256 | form login (usuário/senha), erro | `InputText`, `Password`, `Button`, `Card`, `Message` | ⏳ `FormField`* |
| `LogsPage` | 107 | busca + botões com `buttonStyle` local | `InputText`+`IconField`, `Button` | ⏳ `SearchInput` |
| `NetworkPage` | 239 | form com `type="number"`, paginação manual | `InputNumber`, `Button` | ⏳ `Pagination`*, ✅ `ErrorBanner` |
| `NotificationsPage` | 329 | `<table>`, toggle, paginação, error/loading | `DataTable`, `InputSwitch`, `Tag` | ⏳ `Pagination`*, ✅ `ErrorBanner` |
| `RemotePage` | 366 | `<table>`, chips, error/loading | `DataTable`, `Tag` | ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `SessionsPage` | 241 | `<table>` + **2 `window.confirm` nativos** | `DataTable`, **`ConfirmDialog`** | ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `SettingsPage` | 213 | 6 abas manuais (`activeTab`), **36 toggles**, 9 inputs, 1 select | `TabView` (6 abas), **`InputSwitch` ×36**, `InputText`, `Dropdown`, `Button` | ✅ `ErrorBanner` |
| `SlaPage` | 306 | form + textarea, `<table>`, error/loading | `DataTable`, `InputTextarea`, `InputText`, `Button`, `Tag` | ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `SwitchesPage` | 291 | `<select>`, `<table>`, empty, error/loading | `Dropdown`, `DataTable` | ✅ `ErrorBanner` |
| `TasksPage` | 407 | 2 forms, 4 selects, barras de progresso | `Dropdown`, `InputText`, `InputTextarea`, `ProgressBar`, `Button`, `Tag` | ✅ `ErrorBanner`, ⏳ `LoadingRows` |
| `TopologyPage` | 432 | **SVG próprio** (não trocar por lib de gráfico), chips, empty | `Tag` | ✅ `ErrorBanner` |
| `UsersPage` | 447 | form, `<table>`, busca, 2 abas manuais, chips, exclusão | `DataTable`, `InputText`, `Dropdown`, `Tag`, `TabView`, **`ConfirmDialog`** (excluir) | ⏳ `Pagination`*, ✅ `ErrorBanner`, ⏳ `SearchInput`/`LoadingRows` |
| `WiFiPage` | 120 | error/loading | `Skeleton` | ✅ `ErrorBanner` |
| `NotFoundPage` | 9 | `uni-page-empty` | — | — |

\* `Pagination` já criado (✅) — adotar nas 7 páginas com paginação manual na Fase B.

### 10.2 Inventário PrimeReact 10.9.4 × decisão

| Componente | Instalado | Decisão | Motivo |
|---|---|---|---|
| `DataTable`/`Column`/`Paginator` | ✅ | **adotar** (piloto feito) | 10 tabelas + 7 paginações manuais |
| `Dropdown` | ✅ | **adotar** | 8 `<select>` nativos (v10: `Dropdown`, não `Select`) |
| `InputText` + `IconField`/`InputIcon` | ✅ | **adotar** | buscas inline do topbar + 5 páginas |
| `Button` | ✅ | **adotar** | `buttonStyle` local em 20 arquivos |
| `Tag` | ✅ | **adotar** | ~80 chips de status/severidade medidos |
| `Skeleton` | ✅ | **adotar** | "Carregando…" manual em 19 páginas |
| `Message`/`Messages` | ✅ | **adotar** | erro de formulário inline em 10 páginas |
| `Calendar` | ✅ | **adotar** (Audit) | 2 `datetime-local`; em v10.9.4 o componente é `Calendar` |
| `InputTextarea`, `InputNumber`, `InputSwitch` | ✅ | **adotar** | textareas (6), number (Network), 37 toggles (Settings/Notifications) |
| `Password` | ✅ | **adotar** | 3 telas com senha (Login/Forgot/Invite) |
| `ConfirmDialog` | ✅ | **adotar** | substitui 2 `window.confirm` (Sessions) + exclusão (Users) |
| `Dialog` | ✅ | opcional | forms hoje são inline/colapsáveis — só se o usuário pedir modal |
| `TabView` | ✅ | opcional | Settings (6 abas) e Users (2 abas) manuais |
| `ProgressBar` | ✅ | opcional | Dashboard + Tasks |
| `Card`, `Toolbar`, `Divider` | ✅ | opcional | estilização de painéis novos |
| `Toast` | ✅ | opcional | hoje erros são inline; só se pedir feedback flutuante |
| `Chart`, `Editor`, `FileUpload`, `Galleria`, `Terminal`, `Dock`, `SpeedDial`, `Tree/OrganizationChart`, `Timeline` | ✅ | **fora de escopo** | sem caso de uso no produto (Topology é SVG próprio; Knowledge usa textarea simples) |

### 10.3 Multiplicador de reutilização (componente → consumo)

| `components/ui/` | criado | páginas que se beneficiam |
|---|---|---|
| `ErrorBanner` | ✅ | **19** (banner idêntico hoje) |
| `PageHeader` | ✅ | **todas** (título+contagem+ações) |
| `Pagination` | ✅ | **7** (Anterior/Próxima manual) |
| `SearchInput` | ⏳ | **6** (topbar + Clients/Knowledge/Logs/Users/Audit) |
| `LoadingRows` (Skeleton de tabela) | ⏳ | **19** (loading manual) |
| `StatusChip` (por `Tag`) | ⏳ | **14** (chips de status/severidade/outcome) |
| `FormField` (label+input+erro p/ forms) | ⏳ | **10** (forms com erro inline) |
| `CopyableId` | ⏳ | **3** (Audit ✅ impl., Sessions, DeviceDetail) |
| `FilterBar` (busca+selects+período+ações) | ⏳ | **4** (Audit, Incidents, Knowledge, Clients) |

Observações finais:

- **Reutilização real**: cada `ui/` criado substitui N duplicações (coluna "páginas") — é o ganho
  central, mais do que o componente PR isolado; os PRs entram *dentro* desses componentes
  (ex.: `FilterBar` usa `Dropdown`+`InputText`, `StatusChip` usa `Tag`).
- **Adoção incremental**: nenhum rewrite em massa — cada página migrada quando tocada por outra
  razão (Fases A–D), mantendo contrato e visual UniFi.
- Campos com `*` no §10.3 = criáveis sem dependência de backend; `StatusChip`/`FormField` são os
  de maior multiplicador depois dos já criados.
