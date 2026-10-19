# 55 — Relatório de caça-bugs FRONTEND (2026-10-17)

> **Método**: Bug Hunter (scan-only, passo único) — triage determinística
> (`triage.json`, 201 arquivos, estratégia `large-codebase`, `needsLoop: true`)
> → 6 Hunters paralelos → Skeptic+Referee (verificação manual contra o código).
> **Cobertura parcial honesta**: ver "Cobertura" abaixo. Nenhum código foi alterado.
> Checklist de método: `docs/54-CHECKLIST-CACA-BUGS.md`.

## Resumo

> **Status dos fixes**: H1–H7 e M1–M4/M6 **CORRIGIDOS** em 2026-10-17
> (F33-T001/T002/T003, ADR-061 aceito; lote 1 FECHADO). Restam: M5
> (payload opcional do Zabbix — task própria), 34 `manual-review` e o
> response-side legado de enums (TASKS.md Fase 33).

| | |
|---|---|
| Achados totais | **48** |
| **Confirmados (REAL_BUG)** | **14** (7 altos · 7 médios) |
| Em revisão (manual-review) | 34 (médios/baixos não re-verificados 1:1 nesta passada) |
| Descartados | 0 |

### Confirmados — 7 altos

| # | Arquivo:linha | Operação/botão | Bug | Trigger |
|---|---|---|---|---|
| H1 | `services/api/invitations.ts:73` | Convites → **Editar roles** → Salvar | Envia `roles: string[]` mas `UpdateInvitationViewModel` é enum `UserRole` **numérico** e o MVC **não tem** `JsonStringEnumConverter` global → **400 em 100%** | Admin edita roles de convite pendente e salva |
| H2 | `services/api/projects.ts:298` | Projeto → Evidências → **Validar/Rejeitar** | `decision: "Validated"` string → view model enum numérico sem conversor → **400**; módulo PDI prova a omissão (tem `[JsonStringEnumConverter]` explícito) | Operate/Admin revisa evidência pendente |
| H3 | `services/api/knowledge.ts:208` | Knowledge → **mudar status** (Draft→…) | Envia `status: Number(...)` mas `ChangeKnowledgeArticleStatusViewModel.Status` é **`string`** → STJ lança → **400**; comentário do front (L200) justifica errado | Qualquer transição dos 8 status (ADR-034) |
| H4 | `pages/DevicesPage.tsx:178-205` | Aba **"Por Switch"** | Loop infinito de render: `items: query.data?.items ?? []` cria array novo/ render + `setPortsBySwitch(new Map())` novo/ render enquanto query pendente ou **em erro (loop permanente)** | Abrir Devices e clicar "Por Switch" com rede lenta/erro |
| H5 | `pages/TaskDetailPage.tsx:233` + `:2387` | Link pai↔**subtarefa** na URL `/tasks/:id`; botão **Excluir** | `load()` não faz `setLoading(true)`/reset ao trocar `:id` (rota sem `key` → sem remount): task A fica na tela sob URL de B e **Excluir confirma A e exclui A** | Navegar entre task e subtarefa; excluir logo depois |
| H6 | `pages/SwitchesPage.tsx:585` | Porta UniFi → botão **Habilitar/Desabilitar** | `body: { disabled: !port.linkUp }` com `linkUp: boolean\|null`: `!null === true` → clicar **"Habilitar" numa porta com link null DESABILITA**; porta com link false nunca pode ser desabilitada (estado administrativo inferido do estado de link) | Porta com cabo desplugado (null/false) na aba UniFi ao vivo |
| H7 | `pages/IncidentsPage.tsx:162` | Incidente → **Investigar com IA** | Mutation única compartilhada: `onSuccess` faz `setInvestigation(data)` sem checar id — resposta **lenta do incidente A abre no painel do incidente B** (cabeçalho=B, corpo=A) | Investigar A, fechar, investigar B (resposta A chega depois) |

### Confirmados — 7 médios

| # | Arquivo:linha | Operação | Bug |
|---|---|---|---|
| M1 | `pages/DeviceDetailPage.tsx:145,261` | Abas Monitoramento/Endereços/Portas em `/devices/:id` | `requested` (useRef) nunca zera no troca de `:id` → aba clicada em B não recarrega e **mostra dados do device A** sob URL/nome de B |
| M2 | `pages/TaskDetailPage.tsx:312-325,444` | 8 diálogos de mutação (Dados, Identificação, Planejamento, Subtarefa, Impedimento…) | `run()` engole a exceção; helpers fecham o diálogo e limpam o texto **mesmo na falha** → digitação perdida, usuário vê só banner |
| M3 | `pages/AiProvidersPage.tsx:183-184` | Provedor → **Listar modelos** | `onSuccess` escreve `newModels` **e** `editModels` incondicionalmente → com form de edição de A aberto, clicar "Listar modelos" de B **substitui models de A** (payload errado no Salvar de A) |
| M4 | `pages/NotificationsPage.tsx:153` | Toggle de canal de notificação | GET de preferências falhou → mapa `{}` → `next = !undefined = true` → clicar **"desligar" REATIVA** o canal (UI mostra `?? true` como Ativo) |
| M5 | `pages/ZabbixSettingsPage.tsx:188` | Zabbix → **Testar conexão** | `testZabbixConnection()` posta **sem payload** → testa a config **salva**, não a editada; "Conexão OK" ao lado de credenciais nunca testadas |
| M6 | `pages/SettingsPage.tsx:170` | Configurações → toggle **Modo Escuro** | Persiste `darkMode` no backend mas **nenhum consumidor** existe no app → toggle não faz nada visível |
| M7 | `pages/DashboardPage.tsx:164,284` | KPI **Incidentes** ("N em aberto") | Filtra `openIncidents` sobre `pageSize: 5` → subtítulo "0 em aberto" com 6+ abertos fora da 1ª página |

### Em revisão (34 — manual-review, plausíveis com evidência do Hunter)

**services/api** · **router/store** · **components** · **pages**:

- SignalR: negotiate não cancelado → hub vazado/eventos ×2 (`signalr/notifications.ts:58`); reconexão morta após ~42s sem `onclose` (`connection.ts:18`); rota catch-all `*` sem ProtectedRoute monta AppLayout anônimo com 5×401 + retry (`router/index.tsx:466`); `/invitations` fora de `ROUTE_ACCESS` (Viewer vê menu) (`access.ts:13`); lookup exato → `/projects/:id` herda `read` (`access.ts:37`).
- Components: Escape fecha **todos** os diálogos aninhados (`ui/Dialog.tsx:29`); data default PDI em UTC (`PdiAccompanimentDialog.tsx:92`); `isOverdue` compara com UTC ≠ "hoje local" (`taskCatalogs.ts:191`); badge de tarefas conta só 100 (`AppLayout.tsx:148`); clipboard sem `.catch` (`CopyableId.tsx:34`); `preventDefault` ineficaz em `onWheel` React 19 (`UniFiTopologyMap.tsx:108`).
- Pages: corridas de lista sem cancelamento (busca `ProjectsPage:428`, filtros `AlertsPage:59`, `KnowledgePage:149`); `invalidate: []` refetcha **tudo** (`IncidentsPage:160,182`); auto-seleção não revalida device removido (Servers/Printers/Cameras/Phones/Switches); banner de check preso ao device anterior; delete na última página sem clamp (Knowledge, Network VLAN); PUT de status sem dedup (`IncidentsPage:264`); topologia com `pageSize` 100 (`TopologyPage:119`); Zabbix: teste não limpa OK antigo, sucesso apaga edição do form, retry refetcha query errada, filtro aplica termo não confirmado; Remote: sessão sem confirmação + aviso com device errado; Reports: período novo × relatório antigo; SLA esconde erro como "Nenhum orçamento"; Locations: rack sempre no 1º local + listas truncadas em 100 sem paginator; ResetPassword mantém token na URL; UniFiLive conta clientes já filtrados; botões de escrita sem gate de role (Projects 365, ProjectDetail 634 → 403 p/ Viewer).

## Cobertura

- **Escaneados**: `services/api` (33), `api/`, `types/`, `app/router`, `app/store`, `context/`, `hooks/`, `services/signalr`, `utils/`, `data/`, `components` produção (não-test), e 45 páginas de produção (Tasks/TaskDetail/Devices/DeviceDetail/Projects/ProjectDetail/Pdi/Users/Sessions/Account/AiProviders/Invitations/Settings/Dashboard/Alerts/Incidents/Knowledge/Printers/Servers/Cameras/Phones/Network/Switches/WiFi/Virtualization/Topology/Reports/Insights/Copilot/Audit/Logs/Notifications/Remote/Sla/Clients/Locations/Zabbix×3/UniFi×2/Login/Forgot/Reset).
- **Não cobertos nesta passada** (`needsLoop: true`): páginas estáticas/marketing (Home, Features, Contact, Health, NotFound), testes (context-only) e refino 1:1 dos 34 manual-review.
- **Como fechar a cobertura**: nova passada com `--loop` sobre `frontend/InfraControl.Web/src/pages` + revisão Skeptic dos 34.

## Priorização sugerida (não executada — scan-only)

1. **Contrato 400 (H1–H3)** — 3 linhas de fix cada (numérico↔string no front **ou** `JsonStringEnumConverter` no back; decidir por ADR/contrato; espelhar PDI).
2. **H6** (`disabled: !linkUp`) — bug de escrita em equipamento real: corrigir para usar estado administrativo ou desabilitar o botão em `null`.
3. **H4/H5/H7** — loop de render, exclusão de task errada, dados de incidente cruzados.
4. Corridas/listas e gates de role (médios/baixos) em lote.
