# Backlog de Operação e UX

## Objetivo

Evoluir a operação diária sem criar dados fictícios no frontend. Cada entrega
consome contratos existentes ou começa pelo slice backend, com testes e API
documentada antes da tela.

## Entregas

1. **Feedback contextual — concluída**
   - `Messages` inline em Users, Sessions e Notifications; sucesso e erro de
     ação ficam visíveis até fechamento manual.

2. **Governança de convites — concluída (2026-10-03)**
   - Backend: consulta administrativa paginada de convites por estado
     `Pending`/`Accepted`/`Rejected`/`Expired`, com e-mail, role, criação,
     expiração e aceite/recusa — `SearchInvitationsQuery` +
     `GET /api/v1/admin/invitations` (Admin).
   - Backend: `GET /api/v1/invitations/me` (convites do e-mail logado),
     `POST /api/v1/invitations/{id}/accept` e `/{id}/reject` em tempo real +
     `Invitation` no `RetentionPolicy` (cleanup automático).
   - Frontend: `InvitationsPage` (aba/rota Convites) com filtros por status,
     busca e ações aceitar/recusar; **seção "Convites pendentes" no
     Dashboard** do usuário (carga independente — falha não derruba os
     demais painéis; só renderiza com pendentes ou erro, com aviso de
     sucesso/erro e recarga própria).
   - Não expõe token nem senha; apenas Administrador na lista completa.

3. **Notificações operacionais — concluída (2026-10-04)**
   - Backend: hub `GET /hubs/notifications` + evento `notification`
     confirmados e documentados (docs/07 §Canal SignalR) — payload ganhou
     **`eventType`** (espelho do feed persistido) via
     `PendingDeliveryItem`/`NotificationSignalRMessage`; fluxo
     persistência → delivery → push inalterado (worker verde, 16 testes).
   - Frontend: `services/signalr/notifications.ts` (singleton por aba,
     antiforgery no negotiate, retry 10s, reconexão automática) consumido
     pelo **AppLayout** (hub vivo enquanto logado; cada evento soma o
     **contador do sino**) e pela **NotificationsPage** (insere o item no
     topo da página 1 com dedup por notificationId e soma o total; fora da
     página 1 o próximo load traz na posição certa). O feed persistido
     continua fonte de histórico. `eventType` exibido no item; não há
     destinatário/canal no contrato do push (sem esse campo hoje).

4. **Topologia SVG — concluída (2026-10-03)**
   - SVG próprio mantido (decisão existente); interação melhorada:
     **seleção por clique** em device (anel + foco: conexões dele destacadas,
     vizinhos preservados, resto esmaecido) e em **aresta** (hit-area
     invisível de 12px, conexão em destaque), **painel de Detalhes** na
     sidebar (device: tipo/IP/local/interfaces/conexões + botão **Ver
     device** → `/devices/:id`; conexão: origem/destino com navegação para
     cada device), **Esc** limpa a seleção, clique no fundo do mapa limpa,
     dica de uso na legenda. Hover/tooltip, filtros, zoom e legenda
     existentes preservados. Não trocado por componente de árvore genérico.

5. **Base de conhecimento**
   - Validar commands de edição/publicação existentes; completar backend se
     faltar atualização. Frontend com visualização lista/tabela e quadro por
     status, sem mudar o conteúdo dos artigos.

6. **Tarefas e operação**
   - Antes de UI: definir no domínio se responsável é um `UserId` obrigatório
     ou opcional e as regras de atribuição/reatribuição/auditoria.
   - Depois: filtro por operador, cartões com responsável, gráfico de estados
     e relatório operacional baseado em dados reais de tarefas e histórico.

7. **Relatórios**
   - Definir indicadores e período: tarefas por operador/estado, incidentes,
     alertas e SLA. O backend fornece agregados; o frontend só apresenta.

8. **PDI — Plano de Desenvolvimento Individual (mínimo viável implementado)**
   - Significado definido pelo usuário em 2026-10-03: **PDI = Plano de
     Desenvolvimento Individual** (não é Plano Diretor de Infraestrutura).
     Conceito de pessoas: trilhas/capacitação dos operadores da plataforma.
   - **Implementado em 2026-10-04** (pedido "checklist B1–B4 + commit"):
     1 PDI por usuário (upsert, sem histórico), campos **estratégia**
     (≤4000), **prazo** (`target_date`) e **status**
     (InProgress/Completed/Cancelled); permissões — Admin cria/edita/lista
     tudo, Operador só edita o próprio e enxerga o seu, Viewer sem acesso
     (rota nível `operate`); endpoints `GET/PUT /api/v1/pdi` + `PdiPage`
     em `/pdi`. Criação exclusiva do Admin (Operador → 403).
   - Parte distribuída (padrão do domínio): `PdiCreated/UpdatedDomainEvent`
     → outbox → RabbitMQ → `NotificationEventConsumer` — **criação** notifica
     o dono por **Email + SignalR**; **edição** notifica o dono (ou Admin,
     quando age sobre o próprio) só por **SignalR**; regra "quem fez não
     recebe" (o ator determina o destinatário).
   - Evolução do módulo (diagnóstico → ciclo completo, fases, check-ins,
     evidências, avaliação, dashboards/indicadores/relatórios): plano de
     produto integral em **`docs/30-PLANO-MODULO-PDI.md`** (colado pelo
     usuário em 2026-10-04) — execução por fases (Fase 1…8), **uma task
     por fase**, nunca tudo de uma vez.
   - **Fase 1 — Fundamento executada em 2026-10-04** (ADR-029): ciclo de
     vida de **9 status** com transições validadas (tabela da §3 do
     docs/30, `PdiTransitions`, inválida = 400 pt-BR), **gestor**
     (`manager_id`, só Admin define) e **período**
     (`period_start/period_end`, fim ≥ início), **histórico/timeline**
     append-only (`individual_development_plan_history` —
     Created/Updated/StatusChanged + motivo, endpoint
     `GET /pdi/{userId}/history`, append-only, "não apagar histórico" da
     §3), permissões — Operador só edita o próprio e **só o aceite
     (Aguardando aceite → Ativo)** muda status; criação continua só do
     Admin e começa em **Rascunho**. Migração
     `20261005011124_PdiLifecycleManagerPeriodHistory` aplicada no dev
     (remapeamento de dados `InProgress` → `Active`); front com seletor
     dos próximos estados válidos, gestor/período/motivo e timeline;
     testes `PdiCommandTests` 15 + `PdiTransitionsTests` 20.
   - **Fase 2 — Estrutura do desenvolvimento executada em 2026-10-05**
     (ADR-032): catálogo **global** de competências (`pdi_competencies`,
     escrita `Administer`, duplicata em lowercase → 409) + árvore
     **objetivos → estratégias → fases → atividades** (FKs `Restrict` →
     409 ao excluir com filhos, ordenação `sort_order`, fase com status
     provisório `NotStarted/InProgress/Completed` + progresso e
     responsável, prazos), ownership centralizado em `PdiAccess` (Admin
     gerencia qualquer plano, Operador só o próprio → 403),
     `PdiStructureLookup` resolve a cadeia filho → plano, rota
     `GET /pdi/{userId}/structure` + CRUD dos 5 níveis, migração
     `20261006001904_PdiDevelopmentStructure`; front `PdiStructureDialog`
     (árvore, forms por nível, catálogo com criação Admin, responsável
     Admin=usuários / Operador=plano+gestor) no botão "Estrutura" da
      `PdiPage`; testes `PdiStructureTests` 11; Fase 2 **não** escreve
      histórico/eventos (auditoria entregue na Fase 5, ADR-035).
   - **Fase 3 — Acompanhamento executada em 2026-10-06** (ADR-033):
     quatro entidades sob a **fase**: `pdi_progresses` (relato +
     comentário do colaborador, append-only), `pdi_check_ins`
     (**bilateral** — relato do dono e resposta do gestor em campos
     distintos; PUT por lado: campo enviado grava (`""` limpa), não
     enviado preserva; `RespondedAt/RespondedById` só mudam no lado
     resposta), `pdi_impediments` (§11: tipo/descrição/impacto/data/
     responsável/ação + status `Open→Resolved` com `ResolvedAt` derivado
     e reopen limpa; tipo texto livre — lookup fora do escopo, pendente (Fase 8 decidiu manter texto livre, ADR-038)) e
     `pdi_feedbacks` (§13 histórico autor→destinatário/tipo/conteúdo/
     data; **excluir = só Admin**); FKs `Restrict` → 409; permissão por
     ação em `PdiAccess` (ler = Admin/dono/gestor, relato = Admin/dono,
     responder = Admin/gestor, editar impedimento = Admin/dono/gestor,
     criar feedback = quem vê); rota única
     `GET /pdi/{userId}/accompaniment` + `POST/PUT/DELETE` de
     progresses/checkins/impediments/feedbacks; migração
     `20261006013525_PdiAccompaniment`; front `PdiAccompanimentDialog`
     (4 abas com contadores, forms, chips Aberto/Resolvido,
     `ConfirmDialog`) pelo botão **"Acompanhar"** de cada fase do
     `PdiStructureDialog`; testes `PdiAccompanimentTests` 9 (inclui
     regressão "PUT de relato não re-carimba a resposta"); Fase 3
     **não** escreve histórico/eventos (auditoria entregue na Fase 5, ADR-035).
   - **Fase 4 — Evidências executada em 2026-10-07** (ADR-034):
     `PdiEvidence` / `pdi_evidences` sob a **fase** (FK `Restrict`) com
     os **9 tipos** do §12 (`PdiEvidenceType` + rótulos pt-BR) e status
     `Pending→Validated/Rejected` — **revisão única** (2ª → 409),
     rejeição com motivo obrigatório, arquivo ≤10 MB com nome saneado
     (`Path.GetFileName` — `../evil.sh` → `evil.sh`) e storage em disco
     `var/pdi-evidence` (`IPdiEvidenceFileStore`, arquivo gravado antes
     do DB com rollback best-effort; **sem delete** — retention pendente);
     rotas `POST /pdi/evidences` (multipart), `PUT
     /pdi/evidences/{id}/review` (enum com `JsonStringEnumConverter`
     explícito), `GET .../download` (`nosniff` + attachment); 5ª lista
     de `GET /pdi/{userId}/accompaniment`; histórico
     `EvidenceAdded`/`EvidenceReviewed` (conversion string, **sem
     migração**); permissão por ação (`EnsureCanReport` enviar,
     `EnsureCanReview` revisar = Admin/gestor, `EnsureCanView` ler);
     aba "Evidências" no `PdiAccompanimentDialog` (upload, revisão,
     download blob); migração `20261007015235_PdiEvidence`; testes
     `PdiEvidenceTests` 10. **Correções de deployment capturadas pelo
     E2E**: diretório `/app/var` não-gravável pelo UID da API (→ `mkdir/
     chown` no Dockerfile + named volume `pdi-evidence`), nginx em 1 MB
     de `client_max_body_size` (→ 16m; senão 413 antes do teto de 10 MB)
     e binding de enum só por número (→ 400 em toda revisão).
   - **Fase 5 — Governança executada em 2026-10-07** (ADR-035):
     §14 — `Update` do plano exige **motivo** quando campo muda (status
     sozinho mantém opcional) e o histórico `Updated` registra
     `valor anterior → novo` (estratégia/prazo/gestor/período) +
     motivo; §15.2 — `PdiPhaseStatus.PendingValidation` com
     `POST /pdi/phases/{id}/submit` (dono/Admin) e
     `PUT /pdi/phases/{id}/validation` (Admin/gestor; rejeição com
     motivo obrigatório → volta a `InProgress`, validação → `Completed`
     e **auto-start** da próxima fase), guard no PUT genérico —
     não-Admin não conclui/reabre fase (400 "…aprovação…"); §17 —
     timeline ganha `PhaseStarted/Submitted/Validated/Rejected`,
     `CheckInCreated`, `ImpedimentCreated/Resolved` (sem migração);
     **auditoria** — os 30 comandos PDI implementam `IAuditableRequest`
     (`AuditResource = "PDI"`); front `PdiStructureDialog`
     (Concluir/Validar/Rejeitar + diálogo de motivo, status restrito
     por papel) e `PdiPage` (motivo obrigatório quando campo difere do
     original); testes `PdiGovernanceTests` 11; E2E **63/63** (+
     regressão Fase 4 50/50); retenção de evidências **não** entrou
     (continua pendente).
   - **Fase 6 — Avaliação executada em 2026-10-07** (ADR-036):
     `PdiAssessment` (`pdi_assessments`, 1 linha por lado — `Self`
     autoavaliação / `Manager` avaliação do gestor, autor + conteúdo
     ≤4000, upsert) e `PdiResult` (`pdi_results`, único por plano — 4
     desfechos do §16 *atingido · parcialmente atingido · não atingido ·
     replanejado* + nota opcional + finalizador); fluxo do §15.3
     imposto pelo backend: autoavaliação (dono) leva o plano a **Em
     avaliação** com `StatusChanged` no timeline, avaliação do
     gestor/Admin exige a autoavaliação (400) e plano em avaliação
     (409), resultado exige as duas avaliações e é **único/imutável**
     (2ª → 409, plano concluído congela → 409); **seta nova**
     `UnderEvaluation → Replanned` para o resultado "Replanejado"
     (§16 + §3) — os demais vão a `Completed`; rotas
     `GET /pdi/{userId}/evaluation`, `PUT .../assessment`,
     `PUT .../result` (`EnsureCanEvaluate` = Admin/gestor), histórico
     `AssessmentCreated`/`ResultFinalized` (sem migração de dados),
     auditoria nos 2 comandos novos (32 no total com "PDI"); front
     botão **"Avaliar"** + `PdiEvaluationDialog` (snapshot do plano,
     as 2 avaliações e o resultado — somente leitura depois de
     finalizar) e `UnderEvaluation: ["Completed","Replanned"]` no
     seletor; migração `20261007233505_PdiEvaluation` aplicada no dev;
     testes `PdiEvaluationTests` 15 (+ seta em `PdiTransitionsTests`);
     E2E **31/31** (`/tmp/opencode/pdi_fase6_e2e.py`: guards na ordem
     do §15.3, transição + timeline, upsert por linha, congelamento,
     ciclo `Replanejado → Replanned → Active`, auditoria SQL, 2
     colaboradores convidados, purga de resíduo) + regressão Fase 5
     **63/63**.
   - **Fase 7 — Gestão executada em 2026-10-07** (ADR-037): 3
     dashboards de leitura **sem migração** — §18 `GET
     /pdi/{userId}/dashboard` (progresso = média dos % das fases,
     chips de fase ✓●◐○, próximo prazo = menor `EndDate` não
     concluída, pendências = impedimentos abertos + totais de
     evidências/check-ins; `EnsureCanView` + 404 sem plano + 403
     estranho), §19 `GET /pdi/manager/dashboard` (novo
     `ListByManagerAsync`; partição exata 🔴 atrasado (meta vencida) ×
     🟡 atenção (impedimento `Open` | evidência `Pending` | fase
     `PendingValidation`) × 🟢 dentro do prazo (inclui
     concluídos/cancelados), fases a vencer em 7 dias, agregados e
     `Plans[]` com flags + nome = "acesso direto aos casos"), §20 `GET
     /pdi/admin/dashboard` (policy `Administer` + check no handler;
     contagens por status em partição exata — "em andamento" =
     complementar —, taxa de conclusão e agregados globais); front
     `PdiDashboardPanels.tsx` (faixa dentro do card "Meu plano", card
     "Meus PDIs — N colaboradores" com 🟢/🟡/🔴 e botão "Abrir" →
     diálogo de avaliação, card "visão administrativa" com 2 faixas de
     `Stat`); painéis opcionais (erro não derruba a página); rótulo de
     período do mock **não** entrou (entrou na Fase 8 — ADR-038); acesso
     direto abre a **avaliação** — estrutura segue restrita a
     dono/Admin (guard ADR-032); testes `PdiDashboardTests` **7/7**;
     E2E **32/32** (`/tmp/opencode/pdi_fase7_e2e.py`) + regressão Fase
     6 **31/31** e Fase 5 **63/63**.
   - **Fase 8 — Analytics executada em 2026-10-07** (ADR-038): 4
     queries de leitura **sem migração** com escopo único de período
     (`TargetDate ∈ [de..até]`, lados nulos = sem limite; `'de' >
     'até'` = 400; data fora de AAAA-MM-DD = 400) — §21 `GET
     /pdi/indicators` (Administer + `EnsureAdmin`; 4 blocos:
     execução com `AvgCompletionDays` do timeline, desenvolvimento
     com os 4 desfechos do §16 + evolução por competência,
     acompanhamento com check-ins por plano, dificuldades com
     distribuição % por tipo e média de resolução), §22 `GET
     /pdi/reports/individual/{userId}` (Operate + `EnsureCanView` →
     404 sem plano / 403 estranho; compõe estrutura + acompanhamento
     + avaliação via builders extraídos das queries existentes),
     `GET /pdi/reports/semester` (Administer; semestre corrente por
     padrão, rótulo "Nº SEMESTRE YYYY", `Trends` vs. janela anterior
     de mesmo tamanho, linhas de gestores/competências/dificuldades)
     e `GET /pdi/reports/difficulties` (Administer; % por tipo +
     resolvidos + média); 3 endpoints de **export × 2 formatos = 6
     saídas PDF/Excel**
     (`?format=pdf|xlsx`, padrão PDF, inválido = 400) com QuestPDF
     2026.9.1 + ClosedXML 0.105.1 em `Api/Reporting/`
     (`PdiExportFormat` + `PdiReportRenderers`); `GET
     /pdi/admin/dashboard` ganhou `from`/`to` (deferido da Fase 7);
     front: `pdi.ts` seção Analytics (tipos + 4 getters +
     `downloadPdiReport` blob) e `PdiReportsDialog.tsx` (4 abas
     Indicadores/Dificuldades/Individual/Semestral, filtros De/Até,
     seletor de colaborador, botões PDF/Excel; não-admin só a aba
     Individual) com botões "Relatórios" (header admin) e
     "Relatório" (card próprio) no `PdiPage`; **escopos
     deliberados** — lookup de tipo de impedimento (texto livre,
     pendente) e **setores** (sem campo no domínio PDI) fora do
     escopo; testes `PdiAnalyticsTests` **16/16**; suíte unit
     **1644/1644**, arch **119/119**, PDI **189/189**, front
     tsc/lint/build verdes; E2E **76/76**
     (`/tmp/opencode/pdi_fase8_e2e.py`) + regressão Fase 7
     **32/32**, Fase 6 **31/31**, Fase 5 **63/63** e Fase 4
     **50/50** (sem migração). **§25 do docs/30 concluído — módulo
     PDI completo.**

## Ordem de execução

`Convites → Notificações SignalR → Topologia SVG → Conhecimento → Tarefas com
operador → Relatórios` (PDI mínimo concluído 2026-10-04 — item 8 acima;
evolução do módulo por fases em `docs/30-PLANO-MODULO-PDI.md`).
