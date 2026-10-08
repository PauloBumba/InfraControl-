# Task, SLA, SLO e Knowledge Base

## Task

Task é uma unidade genérica de trabalho.

Tipos:

- Manual
- Incident
- Alert
- Monitoring
- Discovery
- Maintenance
- Change
- Preventive
- Corrective
- Project
- System

Pode relacionar-se a múltiplos dispositivos, usuários, incidentes e serviços.

### Implementação (F7-T001)

A entidade é `TaskItem` (não `Task` — evita a colisão com
`System.Threading.Tasks.Task` em todo o código assíncrono). Persistência:
tabela `tasks`, enums como `string(20)`.

- `TaskKind` = os 11 tipos acima; `TaskState` = os 7 estados acima.
- `Title` (120, obrigatório) e `Description` (500, opcional); nasce em `Open`.
- Transições de estado são `F7-T007` (Workflow); vínculos (assignees,
  devices, incidentes), comentários e histórico: `F7-T002`→`T006`.

### Identificação básica (F23-T001)

Prioridade, prazo e responsável da spec "TASKS — Plano de Execução" §5
(análise completa em `docs/31-TASKS-PLANO-EXECUCAO.md`):

- `Priority` = `TaskPriority` Low/Medium/High/Critical (espelha
  `IncidentSeverity`), obrigatória, `string(20)`; nasce `Medium` no create e
  nas tasks existentes (backfill da migração `AddTaskIdentification`).
- `DueDate` = `DateOnly?` (coluna `date`, precedente `target_date` do PDI);
  prazo no passado é permitido (task atrasada ≠ erro de domínio).
- `ResponsibleId` = `Guid?` **sem FK** (precedente `manager_id`, ADR-029 —
  usuário excluído não quebra a task); nome resolvido em leitura via
  `DomainUsers.DisplayName`. Responsável ≠ assignee: assignees são os
  participantes (N:N), responsável é o dono da identificação.
- `PUT /api/v1/tasks/{id}/identification` (Operate) substitui os 3 campos:
  `priority` obrigatória (400 pt-BR), `dueDate`/`responsibleId` nulos limpam
  o valor, responsável desconhecido → 404. Histórico grava
  `PriorityChanged`/`DueDateChanged`/`ResponsibleChanged` (detalhe
  "de → para", um por campo alterado; regravação idêntica = no-op).
- `TaskDto` expõe `priority`, `dueDate`, `responsibleId`, `responsibleName`;
  a criação aceita os mesmos campos opcionais.

### Planejamento e resultado (F23-T003)

Continuação do §5 da spec — datas previstas do plano e o resultado da
execução (análise completa em `docs/31-TASKS-PLANO-EXECUCAO.md` §6):

- `PlannedStart`/`PlannedEnd` = `DateOnly?` — início/fim **previstos**;
  o prazo de compromisso continua no `DueDate` (campos distintos, sem
  duplicar semântica). Fim antes do início → 400 pt-BR (domínio).
- `Result` = `string?` ≤ 2000 — o que a execução entregou; `null`/vazio
  limpa. Setters silenciosos: quem grava o histórico é o handler.
- `PUT /api/v1/tasks/{id}/planning` (Operate) — substituição das 2 datas
  (`null` limpa), histórico `PlanningChanged` por campo alterado
  ("início …", "fim …", no-op sem registro).
- `PUT /api/v1/tasks/{id}/result` (Operate) — histórico `ResultChanged`
  por troca ("de → para"; textos truncados para caber no `detail` ≤200).
- `TaskDto` expõe `plannedStart`, `plannedEnd`, `result`.

### Assignees (F7-T002)

N:N Task ↔ Usuário na tabela `task_assignees` (PK composta, FKs
`Cascade` — apagar a task ou o usuário limpa os vínculos).

- `POST/DELETE api/v1/tasks/{taskId}/assignees/{userId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404.
- `TaskDto` expõe `assigneeCount` + `assigneeIds`.

### Devices vinculados (F7-T003)

N:N Task ↔ Device na tabela `task_devices` (PK composta; FK da task
`Cascade`, FK do device `Restrict` — excluir um device com tasks
vinculadas é bloqueado no `DeleteDeviceCommand`).

- `POST/DELETE api/v1/tasks/{taskId}/devices/{deviceId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404.
- `TaskDto` expõe `deviceCount` + `deviceIds`.

### Incidentes vinculados (F7-T004)

N:N Task ↔ Incident na tabela `task_incidents` (PK composta; FK da task
`Cascade`, FK do incidente `Restrict`).

- `POST/DELETE api/v1/tasks/{taskId}/incidents/{incidentId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404.
- `TaskDto` expõe `incidentCount` + `incidentIds`.

### Knowledge vinculada (F23-T005/P5a)

N:N Task ↔ KnowledgeArticle na tabela `task_knowledge_articles` (PK
composta; FK da task `Cascade`, FK do artigo `Cascade` — a exclusão
física do artigo, ADR-030 §5, limpa os vínculos, mesmo
comportamento de `knowledge_article_incidents`).

- `POST/DELETE api/v1/tasks/{taskId}/articles/{articleId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404.
- `TaskDto` expõe `articleCount` + `articleIds`.
- Histórico grava `KnowledgeLinked`/`KnowledgeUnlinked` (tipo de
  timeline `vinculo`); card **"Knowledge"** no detalhe com select de
  vinculação (spec Knowledge — `docs/32` §10; tasks §16).

### PDI vinculado (F23-T005/P5b)

N:N Task ↔ PDI na tabela `task_pdis` (PK composta `(task_id, user_id)`;
FK da task `Cascade`; **`user_id` sem FK** — critério do ADR-029 para
refs de usuário, e a coluna é justamente a chave do plano no produto:
1 plano vigente por usuário, API `/pdi/{userId}`). A existência do plano
é validada no handler (linkar exige PDI criado).

- `POST/DELETE api/v1/tasks/{taskId}/pdi/{userId}`
  (policy `TasksManage`); duplicidade → 409, inexistente → 404
  (`PDI do usuário '{id}' não encontrado.`).
- `TaskDto` expõe `pdiCount` + `pdiUserIds` (ids = donos dos planos).
- Histórico grava `PdiLinked`/`PdiUnlinked` (tipo `vinculo`); card
  **"PDI"** no detalhe: nomes dos donos vêm de `listUsers` (a leitura
  não depende da policy `Operate` do `/pdi` — Viewer continua vendo os
  vinculados), select de vinculação com `listPdis` (403 de Viewer
  silencia → lista vazia).

### Comentários (F7-T005)

Tabela `task_comments` (`task_id`, `author_id`, `content` ≤ 500; FK da
task `Cascade`, FK do autor `Restrict`). Sem edição — histórico de
alterações vem em `F7-T006`.

- `GET api/v1/tasks/{taskId}/comments` (policy `TasksRead`) — ordem
  cronológica, com `authorName` (DisplayName).
- `POST .../comments` (policy `TasksManage`) — autor = usuário logado.

### Histórico (F7-T006)

Tabela `task_histories` (`task_id`, `actor_id` nullable, `action`,
`detail` ≤ 200; FK da task `Cascade`, sem FK no ator). Todo comando grava
uma entrada — `TaskHistoryAction` (42 ações): `Created`, `Updated`,
`Assigned`, `Unassigned`, `PriorityChanged`, `DueDateChanged`,
`ResponsibleChanged`, `DeviceLinked`, `DeviceUnlinked`, `IncidentLinked`,
`IncidentUnlinked`, `Commented`, `StateChanged`, `SlaApplied`,
`SlaPaused`, `SlaResumed`, `PlanningChanged`, `ResultChanged`,
`Replanned`, `CriterionAdded`, `CriterionUpdated`, `CriterionRemoved`,
`ParentChanged`, `SubtaskAdded`, `SubtaskRemoved`, `ImpedimentAdded`,
`ImpedimentUpdated`, `ImpedimentResolved`, `ImpedimentReopened`,
`ImpedimentRemoved`, `EvidenceAdded`, `EvidenceRemoved`, `PhaseAdded`,
`PhaseUpdated`, `PhaseCurrentChanged`, `PhaseCompleted`,
`PhaseReopened`, `PhaseRemoved`, `KnowledgeLinked`,
`KnowledgeUnlinked`, `PdiLinked`, `PdiUnlinked`.

- `GET api/v1/tasks/{taskId}/history` (policy `TasksRead`) — ordem
  cronológica, com `actorName` (DisplayName).

### Workflow (F7-T007)

Transições válidas (mapa `TaskStateWorkflow` no domínio); par fora do
mapa → `InvalidOperationException` → 409 pt-BR; mesmo estado é no-op
sem histórico:

```text
Open       → InProgress, Cancelled
InProgress → Waiting, Blocked, Resolved, Cancelled
Waiting    → InProgress, Blocked, Cancelled
Blocked    → InProgress, Cancelled
Resolved   → Closed, InProgress
Cancelled  → Open
Closed     → Open
```

- `PUT api/v1/tasks/{taskId}/state` (`TasksManage`) com body
  `{ "state": "..." }` — devolve o `TaskDto` atualizado; estado
  desconhecido → 400, task inexistente → 404.
- Cada transição grava `StateChanged` no histórico com o detalhe
  `"De → Para"`.

API (`api/v1/tasks`): `Search` (term/kind/state/**parentId/rootOnly**,
policies `TasksRead`), `Get` (`TasksRead`), `Create` (`TasksManage` —
`parentTaskId?` cria subtarefa), `Update`/`Delete`/`ChangeState`
(`TasksManage`), **`SetParent`** `PUT {id}/parent` (`TasksManage` —
move/desvincula o pai da subtarefa), **impedimentos** `GET/POST
{id}/impediments` + `PUT/DELETE impediments/{id}` (leitura `Authorize`,
escrita `Operate`), **evidências** `GET {id}/evidences`,
`POST {id}/evidences` (multipart ≤10 MB), `GET
evidences/{id}/download`, `DELETE evidences/{id}` (leitura `Authorize`,
escrita `Operate` — ADR-033).

### Critérios de aceite e replanejamento (F23-T003)

Spec §12–§13: itens verificáveis que a task precisa cumprir e o ciclo de
novo plano com justificativa (precedente: replanejamento do PDI,
`docs/30` §14):

- Tabela `task_criteria` (`description` ≤ 300, `is_done`,
  `done_at timestamp?`; FK da task `Cascade`, índice em `task_id`) —
  molde exato dos comentários, com eventos de domínio CRUD (ADR-030).
- `GET .../tasks/{taskId}/criteria` (leitura `Authorize`) — ordem de
  criação; `POST .../{taskId}/criteria` (Operate) — 201.
  `PUT .../tasks/criteria/{criterionId}` (Operate) — descrição e/ou
  `done` (campo `null` mantém; `done=true` carimba `doneAt`, `false`
  limpa); `DELETE .../criteria/{criterionId}` → 204. 404 pt-BR para
  task/critério inexistente.
- Histórico: `CriterionAdded`/`CriterionUpdated` (descrição ou marcação,
  um registro por aspecto alterado)/`CriterionRemoved` — detalhe truncado
  em 190 chars; no-op não grava nada.
- **Replanejamento**: `POST /api/v1/tasks/{id}/replan` (Operate) aceita
  `plannedStart`/`plannedEnd`/`dueDate` + **`reason` obrigatório** (≤80
  chars — garante o `detail` ≤200 do histórico); sem mudança real → 400
  "Nada a replanejar…"; grava **UMA** entrada `Replanned` no formato
  `"início a → b, prazo c → d — motivo: …"`.

### Subtarefas e progresso (F23-T004 — P4a)

Spec "subtarefas, progresso" (design derivado, sujeito a revisão):

- Subtarefa É task: `TaskItem.ParentTaskId` (auto-relação na própria
  `tasks`; FK `ON DELETE SET NULL` — pai excluído → filho sobrevive e
  vira raiz; índice em `ParentTaskId`). Profundidade ≤ 2: "Subtarefas
  não podem ter subtarefas" (400 pt-BR); auto-relação → 400 (domínio).
- `POST /api/v1/tasks` aceita `parentTaskId?` (404 se o pai não existe,
  400 se o pai é subtarefa); `PUT /api/v1/tasks/{id}/parent`
  (Operate) move ou desvincula (`parentId` null → task raiz) — no-op
  (mesmo pai) → 200 sem histórico.
- `GET /api/v1/tasks` ganhou `parentId` (filhos de X) e `rootOnly`
  (só raízes) — mútuos → 400 "filtros incompatíveis".
- Progresso DERIVADO no `TaskDto`: `progress` = concluídas
  (Resolved/Closed) ÷ não canceladas × 100; sem subtarefas ativas →
  null (canceladas ficam fora do denominador). `subtaskCount`,
  `parentTaskId` e `parentTitle` completam o DTO (2 consultas por
  página, sem N+1).
- Histórico (ações 23–25): `ParentChanged` no filho ("de → para" com
  títulos truncados em 95 → detail ≤193), `SubtaskAdded`/`SubtaskRemoved`
  nos pais afetados (título do filho ≤120); criação com pai grava
  `SubtaskAdded` no pai; no-op não grava nada.

### Impedimentos estruturados (F23-T004 — P4b)

Spec §10 (design derivado, sujeito a revisão) — o "porquê está travado"
auditável:

- Tabela `task_impediments` (`description` ≤ 300, `resolved_at
  timestamp?` null = em aberto; FK da task `Cascade`, índice em
  `task_id`) — molde exato dos comentários/critérios, com eventos de
  domínio CRUD (ADR-030). Quem detectou/resolveu vive no histórico
  (actor), não na linha.
- **O workflow não muda**: registrar impedimento NÃO move a task para
  `Blocked` (mudança de ciclo de estados exigiria ADR) — um `Blocked`
  sem impedimento estruturado e vice-versa são válidos. Múltiplos
  impedimentos por task.
- `GET .../tasks/{taskId}/impediments` (leitura `Authorize`) — ordem de
  criação; `POST .../{taskId}/impediments` (Operate) — 201, 404 task,
  400 do domínio (vazio/>300). `PUT .../tasks/impediments/{id}`
  (Operate) — descrição e/ou `resolved` (campo `null` mantém; `true`
  carimba `resolved_at`, `false` limpa/reabre); `DELETE
  .../impediments/{id}` → 204. 404 pt-BR para impedimento inexistente.
- Histórico (ações 26–30): `ImpedimentAdded`/`ImpedimentRemoved`
  (descrição truncada em 190), `ImpedimentUpdated` (descrição
  "de → para", 70/70), `ImpedimentResolved`/`ImpedimentReopened`
  ("resolvido: …"/"reaberto: …", 185); no-op não grava nada.

### Evidências anexadas (F23-T004 — P4c)

Spec §11 — anexo de arquivo na task. **Armazenamento decidido pelo
usuário no checkpoint: disco local (ADR-033)**:

- Tabela `task_evidences`: `FileName` ≤ 200 (normalizado para o último
  segmento), **`StoredName` = GUID + extensão gerada no domínio**,
  `ContentType` ≤ 100 (default `application/octet-stream`), `FileSize`,
  `Note?` ≤ 300; FK da task `Cascade` + índice. Nome original é só
  metadado — nunca caminho.
- Limites (400 pt-BR, domínio): **10 MB**, allowlist `png jpg jpeg gif
  webp pdf txt log csv json xml zip`, arquivo vazio, nome/nota/tipo
  acima do limite. `Storage:EvidencePath` (default `evidence-files`),
  porta `IEvidenceFileStore` → `LocalEvidenceFileStore`.
- `GET .../tasks/{taskId}/evidences` (leitura `Authorize`) — ordem de
  upload; `POST .../{taskId}/evidences` **multipart** (`file` +
  `note?`, Operate) → 201; `GET .../tasks/evidences/{id}/download`
  (leitura) devolve o binário com `Content-Disposition` do nome
  original (arquivo sumido do disco → 404 pt-BR); `DELETE
  .../evidences/{id}` (Operate) → 204 e **apaga o arquivo junto**
  (erro de IO logado não bloqueia — banco é fonte de verdade).
- Histórico (ações 31–32): `EvidenceAdded`/`EvidenceRemoved` com
  `"nome.ext (kB)"` (nome truncado em 150 → detail ≤ 200); exclusão
  grava histórico antes do `Remove`.
- Front (`client.ts`): `FormData` sem `Content-Type` fixo (o fetch
  monta o boundary) e opção `raw` para `blob` no download — sessão e
  antiforgery intactos (`X-XSRF-TOKEN` também no multipart).
- Consequências aceitas (ADR-033): backup do PostgreSQL não leva os
  arquivos; exclusão em massa de task deixa órfão o arquivo; uma
  réplica só (storage compartilhado exigiria novo ADR).

### Fases da task (F23-T004 — P4d)

Spec §7 — **decisão escolhida pelo usuário no checkpoint: etapas
nomeadas por task** (sem conjunto fixo; o texto original da spec não
está no repositório):

- Tabela `task_phases`: `Name` ≤ 120 required, `Position` (ordem;
  **gaps aceitos** após exclusão — novas fases entram em `max+1`),
  `IsCurrent` (ponteiro de "fase atual", **máx. 1 por task** — o PUT
  limpa os irmãos no mesmo save), `CompletedAt?`; FK da task `Cascade`
  + índice `(task_id, position)`.
- Fases são **ortogonais ao workflow** (§6): estado = situação, fase =
  etapa de execução — criar fase não muda estado.
- `GET .../tasks/{taskId}/phases` (leitura `Authorize`) — ordem de
  posição; `POST .../{taskId}/phases` (Operate) → 201, **a primeira
  fase já nasce atual**; `PUT .../tasks/phases/{id}` (Operate) —
  campos nulos mantêm o valor (`name` 400 pt-BR se vazio/>120, `current`
  move o ponteiro, `completed` carimba/limpa) e grava **um histórico por
  aspecto alterado** (no-op = 200 sem registro); `DELETE
  .../phases/{id}` (Operate) → 204 (a fase atual excluída **não**
  promove ninguém). 404 pt-BR para task/fase inexistente.
- Histórico (ações 33–38): `PhaseAdded`, `PhaseUpdated` (renomear —
  detail `nome: de → para`), `PhaseCurrentChanged`, `PhaseCompleted`,
  `PhaseReopened`, `PhaseRemoved` (nome truncado em 190).
- Front: card **"Fases (x/y)"** (progresso derivado = concluídas ÷
  total) com checkbox concluir/reabrir, chip "Atual", botão tornar
  atual, excluir com confirm e cadastro inline; rename sem UI (PUT
  testado — mesmo molde dos critérios).

### Linha do tempo unificada (F23-T004 — P4d)

Spec §8 — **decisão escolhida pelo usuário no checkpoint: timeline
unificada tipada, derivada em leitura (sem tabela nova)**:

- `GET .../tasks/{taskId}/timeline?types=&limit=` (leitura
  `Authorize`) funde **histórico + comentários** em entradas
  `{id, type, action, detail, comment, at, actorId, actorName}`:
  impedimentos/evidências/SLA/fases entram **pelo histórico** (mesmas
  fontes — sem duplicata) e a ação `Commented` é descartada, com o
  comentário vindo da fonte rica (texto integral).
- **13 tipos** (`TaskTimelineTypes`, espelhados no front):
  `criacao`, `edicao`, `estado`, `atribuicao`, `vinculo`,
  `planejamento`, `criterio`, `subtarefa`, `bloqueio`, `anexo`, `sla`,
  `fase`, `comentario` — as 42 ações do histórico mapeiam para eles
  (default `edicao`).
- `types` comma-separated (tipo desconhecido → **400 pt-BR** com a
  lista aceita), `limit` clamp 1..500 (default 100), mais recente
  primeiro; `id` opaco prefixado (`h:` histórico / `c:` comentário).
- Front: o card "Histórico" virou **"Linha do tempo"** — chips de tipo
  (só os presentes na resposta, filtro local sobre `limit=500`),
  comentário exibido com texto integral, demais entradas com rótulo da
  ação + detalhe.

### Tela de detalhe (F23-T002)

Rota `/tasks/:id` no dashboard (`TaskDetailPage.tsx`) — a tela que o kanban
não tinha, ligando os endpoints acima (P2 sem mudança de backend; a P3
enriqueceu a tela com os cards novos):

- **Leitura** = nível `read` (qualquer role autenticada, mesmo padrão de
  `/devices/:id`); toda escrita fica atrás de `Can operate` — o backend
  continua sendo a autoridade (403 no envelope).
- Seções: dados editáveis (`PUT /tasks/{id}`, título ≤120/descrição ≤500 —
  fecha a pendência da P1 de editar título/descrição sem UI), identificação
  em diálogo (`PUT /tasks/{id}/identification`), **planejamento** (editar
  datas ou **replanejar** com motivo em diálogo), **resultado** (texto
  ≤2000 em diálogo), **critérios de aceite** (checklist com progresso
  `x/y`: adicionar, marcar/desmarcar, excluir com confirm),
  **subtarefas** (F23-T004/P4a: lista com estado + link para o filho,
  barra de progresso do pai, criação em diálogo herdando o tipo, botão
  desvincular com confirm; breadcrumb "Subtarefa de …" no subtítulo da
  task filha), **fases** (F23-T004/P4d: progresso `x/y`, concluir/
  reabrir, chip "Atual" + tornar atual, excluir com confirm, cadastro
  inline), **impedimentos** (F23-T004/P4b: chips Aberto/Resolvido,
  resolver/reabrir e excluir com confirm, registro em diálogo ≤300 —
  sem mexer no estado do workflow), **evidências** (F23-T004/P4c:
  upload com seletor de arquivo + nota opcional, lista com tamanho e
  nota, clique baixa o binário com o nome original, excluir com confirm
  avisando que o arquivo sai do disco), transição de estado (409
  do domínio → banner), comentários (criar/excluir, máx. 500),
  **linha do tempo** (F23-T004/P4d: histórico + comentários tipados em
  13 tipos com chips de filtro), participantes e vínculos de
  device/incidente/knowledge/PDI (F23-T005/P5a–P5b; selects só com o que
  ainda não está vinculado; nome resolvido no front com fallback para id
  curto quando o vocabulário não carrega) e SLA (snapshot + pausas +
  aplicar/pausar/retomar; `GET .../sla` **404 = "sem SLA aplicado"**,
  fluxo normal, não banner).
- Catálogo único `components/tasks/taskCatalogs.ts` (estados, kinds,
  prioridade, prazo, rótulos das 42 ações do histórico + os 13 tipos da
  linha do tempo) compartilhado com
  o kanban (`TasksPage` — o título do card virou link para o detalhe;
  F23-T004/P4a: o quadro lista só tasks raiz via `rootOnly=true` e o
  card exibe barra de progresso quando há subtarefas).

## Estados

```text
Open
InProgress
Waiting
Blocked
Resolved
Cancelled
Closed
```

Transições entre estados: seção **Workflow (F7-T007)** acima.

## SLA

SLA controla compromisso de resposta e resolução.

Entidades:

- SlaPolicy
- TaskSla
- TaskSlaPause

O SLA aplicado deve preservar os prazos históricos.

## SLO

SLO mede objetivo de serviço.

Exemplo:

```text
Availability >= 99.95%
Window = 30 days
```

Entidades:

- Service
- Slo
- SloMeasurement
- SloBudget

### Service (F8-T001)

Serviço de TI prestando — raiz do módulo SLA/SLO (`SlaPolicy` F8-T002 e
`Slo` F8-T005 referenciam Service). Padrão Location: `Name` (120,
obrigatório, único case-insensitive — pré-check + índice
`ux_services_name ON services (LOWER(name))`) e `Description` (500,
opcional); tabela `services`.

- `GET api/v1/sla/services` (busca term/page/pageSize) e
  `GET .../{id}` (policy `SlaRead`); `POST`/`PUT`/`DELETE`
  (policy `SlaManage`); duplicidade → 409, id inexistente → 404.
- Permissões novas `SlaRead`/`SlaManage` cobrem todo o módulo Fase 8
  (SLA e SLO); Viewer ganha `SlaRead`, User ganha `SlaManage`.

### SlaPolicy (F8-T002)

Política de SLA — compromisso de **resposta** e **resolução** (docs/04).
Tabela `sla_policies`: `Name` (120, obrigatório), `ResponseMinutes` e
`ResolutionMinutes` (inteiros > 0 — 400 pt-BR caso contrário) e
`ServiceId` opcional (FK `Restrict` para `services` — política global
`null` ou vinculada a um Service).

- `GET api/v1/sla/policies` (filtros `term` e `serviceId`) e
  `GET .../{id}` (policy `SlaRead`); `POST`/`PUT`/`DELETE`
  (`SlaManage`); 404 para política/serviço inexistente.
- `DELETE /api/v1/sla/services/{id}` passa a bloquear com 409 quando
  houver políticas vinculadas (pré-check + FK `Restrict`).
- Os prazos são snapshotados no `TaskSla` (F8-T003) para preservar os
  prazos históricos (docs/04).

### TaskSla (F8-T003)

SLA aplicado a uma Task — 1:1 (PK `task_id`), tabela `task_slas`:
`SlaPolicyId` (FK `Restrict`), snapshot `ResponseMinutes`/`ResolutionMinutes`
e os vencimentos `ResponseDueAt`/`ResolutionDueAt` (= abertura da task +
minutos). Mudanças futuras na política não alteram o snapshot.

- `POST api/v1/tasks/{taskId}/sla` (policy `SlaManage`, body
  `{ "slaPolicyId": "..." }`) — 409 se já aplicado; grava `SlaApplied`
  no histórico com os prazos.
- `GET .../tasks/{taskId}/sla` (policy `SlaRead`) — 404 se não aplicado.
- `DELETE api/v1/sla/policies/{id}` bloqueia com 409 enquanto houver
  tasks com o SLA aplicado (pré-check + FK `Restrict`).
### TaskSlaPause (F8-T004)

Pausa do relógio de SLA — tabela `task_sla_pauses` (`task_id` FK
`Cascade` para `task_slas`: apagar a task/SLA limpa as pausas;
`start_at`, `end_at` nullable = pausa em aberto).

- `POST .../tasks/{taskId}/sla/pause` (`SlaManage`) — 404 sem SLA,
  409 se já há pausa em aberto; grava `SlaPaused`.
- `POST .../tasks/{taskId}/sla/resume` (`SlaManage`) — 409 se não há
  pausa em aberto; **soma a duração aos `*DueAt`** (snapshot de minutos
  intacto) e grava `SlaResumed`.
- `GET .../tasks/{taskId}/sla/pauses` (`SlaRead`) — cronológico;
  404 se o SLA não está aplicado.

### Slo (F8-T005)

Objetivo de serviço de um Service (docs/04: `Availability >= 99.95%`,
`Window = 30 days`). Tabela `slos`: `Name` (120), `TargetPercent`
(`numeric(6,3)`, 0 < alvo ≤ 100 — 400 pt-BR), `WindowDays` (> 0) e
`ServiceId` obrigatório (FK `Restrict` → `services`).

- `GET api/v1/sla/slos` (filtros `term` e `serviceId`) e `GET .../{id}`
  (policy `SlaRead`); `POST`/`PUT`/`DELETE` (`SlaManage`).
- `DELETE /api/v1/sla/services/{id}` passa a bloquear com 409 quando
  houver SLOs vinculados (pré-check + FK `Restrict`).
- Medição chega em `SloMeasurement` (abaixo) e orçamento em
  `SloBudget` (F8-T007).

### SloMeasurement (F8-T006)

Medição do SLO em um momento — valor observado comparável ao
`TargetPercent`. Tabela `slo_measurements`: `SloId` (FK `Restrict` →
`slos`), `MeasuredAt` (DateTime) e `ValuePercent` (`numeric(6,3)`,
0–100 — 400 pt-BR fora da faixa). Registro imutável (sem `PUT`).

- `GET api/v1/sla/slos/{sloId}/measurements` (`SlaRead`) — mais
  recentes primeiro; `GET api/v1/sla/slos/measurements/{id}` (404).
- `POST .../slos/{sloId}/measurements` (`SlaManage`) — 201; 404 para
  SLO inexistente.
- `DELETE .../slos/measurements/{id}` (`SlaManage`).
- `DELETE /api/v1/sla/slos/{id}` bloqueia com 409 enquanto houver
  medições (pré-check + FK `Restrict`).

### SloBudget (F8-T007)

Consumo do erro budget do SLO em uma janela. Tabela `slo_budgets`:
`SloId` (FK `Restrict` → `slos`), `PeriodStart`, `PeriodEnd` (null =
período aberto; se presente deve ser posterior ao início) e
`ConsumedPercent` (`numeric(6,3)`, 0–100 — 400 pt-BR fora da faixa).

- `GET api/v1/sla/slos/{sloId}/budgets` (`SlaRead`) — mais recentes
  primeiro; `GET api/v1/sla/slos/budgets/{id}` (404).
- `POST .../slos/{sloId}/budgets` (`SlaManage`) — 201; 404 para SLO
  inexistente.
- `DELETE .../slos/budgets/{id}` (`SlaManage`).
- `DELETE /api/v1/sla/slos/{id}` bloqueia com 409 também com orçamentos
  (pré-check + FK `Restrict`).

## Knowledge Base

Artigos:

- Draft
- Published
- Archived

Relacionamentos possíveis com Device, Incident e Service.

### Categorias (F10-T001)

`KnowledgeCategory` (tabela `knowledge_categories`): `name` (120, **único
case-insensitive** — pré-check no handler → 409 + índice
`ux_knowledge_categories_name ON (LOWER(name))` via SQL bruto,
precedente da Location) e `description` (500, opcional).

Rotas `api/v1/knowledge/categories` (`KnowledgeController`): `GET` (busca
com `term`, `RoleAccess.Read`) + `GET/{id}` (404), `POST` (201), `PUT`,
`DELETE` (`RoleAccess.Operate`; 404). Escrita 400 pt-BR via
`[Required]`/`[MaxLength]` no ViewModel. A FK dos artigos (F10-T002)
vem com a própria task.

### Artigos (F10-T002)

`KnowledgeArticle` (tabela `knowledge_articles`): `title` (200,
obrigatório), `content` (20.000, obrigatório), `category_id` (FK →
`knowledge_categories`, **SetNull** — apagar a categoria não apaga nem
bloqueia o artigo) e `status` (`Draft`/`Published`/`Archived`; nasce
**Draft** — ciclo em §Publicação). Busca por termo no
título/conteúdo, mais recentes primeiro.

Rotas `api/v1/knowledge/articles` (`KnowledgeArticlesController` — o
`KnowledgeController` da T001 já ocupa a rota de categorias): `GET`
(busca com `term`, `RoleAccess.Read`) + `GET/{id}` (404), `POST` (201,
nasce Draft), `PUT` (não mexe no status), `DELETE` (`RoleAccess.Operate`;
404). Categoria de destino inexistente = 404 pt-BR antes de persistir.
Escrita 400 pt-BR via `[Required]`/`[MaxLength]` no ViewModel.

### Identificação (F24-T001/K1)

Spec `docs/32` §5–§6 — campos de identificação do artigo (Fase 24):

- **Tipo** — enum `KnowledgeArticleType` com os **14 tipos da §5**
  (Procedimento, Manual, Guia, FAQ, Solução conhecida, Configuração,
  Documentação técnica, Boas práticas, Política, Norma interna,
  Troubleshooting, Conhecimento de sistema, Conhecimento de
  infraestrutura, Conhecimento operacional), guardado como string
  (coluna `type` 32). **Opcional**: nulo = artigo ainda não
  classificado (a spec não define tipo padrão para os existentes).
  Valor fora da lista = 400 `Tipo inválido: 'x'. Use 'procedure', …`
  (moldo do status); rótulos pt-BR espelhados em
  `frontend/src/components/knowledge/knowledgeCatalogs.ts`.
- **Descrição** (`description`, 500, opcional) — resumo da
  identificação; o conteúdo segue no `content`.
- **Tags** (`tags` — coleção primitiva do EF, `text[]` no Postgres/JSON
  no Sqlite): normalizadas no domínio (trim + lowercase + dedup na
  ordem), cada uma ≤50 e no máximo 20 — limites em pt-BR → 400.
- **Autor** (`author_id`) — preenchido no servidor com o usuário
  corrente no `POST` e **imutável** depois (`SetIdentification` não
  toca). **Responsável** (`responsável_id`) — opcional, vem do cliente
  (404 `Usuário '{id}' não encontrado.`).
- Ambas as FKs para `DomainUsers` são **SetNull** (mesma decisão da
  categoria: apagar não apaga nem bloqueia o artigo; o produto tem
  exclusão dura de usuário — `Restrict` do `TaskComment` bloquearia).
- `KnowledgeArticleDto` expõe `type` (nome do enum em string),
  `description`, `tags`, `authorId/authorName`,
  `responsibleId/responsibleName` — **nomes resolvidos no backend**
  (join em `DomainUsers`): o front não precisa (nem pode, sem a policy
  do `/admin/users`) resolver por conta própria. Valores novos como
  init-props: DTOs de mutação antigos seguem intactos.
- Formulário de criação da `KnowledgePage` ganhou tipo, responsável,
  descrição e tags (campo por vírgula); o card mostra chip do tipo,
  descrição, chips de tags e a linha "Autor · Responsável".
- Migração `20261008001827_AddKnowledgeArticleIdentification` (script
  validado; não aplicada — Docker).

### Publicação (F10-T003, evolução F24-T002/K2 — ADR-034)

Transições de status em `POST api/v1/knowledge/articles/{id}/status`
(corpo `{"status": "..."}`, `RoleAccess.Operate`, 200 devolve o artigo
com o status novo). A spec `docs/32` §7 pede revisão com aprovação — o
ciclo de 3 estados do F10-T003 virou **workflow de 8 estados**
(**ADR-034**):

| De | Para |
|---|---|
| `Draft` (Rascunho) | `InReview` |
| `InReview` (Em revisão) | `Approved`, `Updated`, `Rejected`, `Draft` |
| `Approved` (Aprovada) | `Published`, `Draft` |
| `Published` (Publicada) | `InReview`, `Archived`, `Obsolete` |
| `Updated` (Atualizada) | `InReview`, `Archived`, `Obsolete` |
| `Archived` (Arquivada) | `Draft` |
| `Obsolete` (Obsoleta) | `Draft` |
| `Rejected` (Rejeitada) | `Draft` |

Publicar exige a cadeia `Draft → InReview → Approved → Published`;
revisar o publicado é `Published → InReview → Updated` (`InReview` não
guarda a origem, por isso aceita os dois fechamentos). Qualquer outra
transição (incluindo repetir o mesmo estado) = **409**
`Transição inválida: 'X' → 'Y'.` Status fora do enum = 400
`Status inválido: 'X'. Use 'draft', 'inreview', 'approved', 'published',
'updated', 'archived', 'obsolete' ou 'rejected'.`

Armazenado como string (linhas antigas `Draft/Published/Archived`
valem sem migração); no contrato JSON os valores são **números** (enum
sem `JsonStringEnumConverter`) — `Draft=0`, `Published=1`,
`Archived=2` legados estáveis, novos 3–7. Rótulos da UI seguem a §7
verbatim (feminino: "Publicada", "Arquivada").

**Nunca-publicados (`Draft`, `InReview`, `Approved`, `Rejected`) só
para quem escreve** (generalização da regra F10-T003 — ADR-034 §4): a
busca de quem não tem nível de escrita (ADR-016 ≡ `RoleAccess.Operate`)
exclui esses estados e o `GET/{id}` de um deles responde **404 como se
não existisse** — não vaza a existência. `Published`/`Updated`/
`Archived`/`Obsolete` ficam visíveis para todo mundo com
`RoleAccess.Read`. Consequência: rejeitar a revisão de um artigo
publicado o tira da vista até republicar (modelo de estado único — se
a spec pedir "publicado com revisão rejeitada", é histórico de
revisões + ADR novo).

### Versionamento (F24-T003/K3 — spec docs/32 §8)

Nenhuma versão é perdida: cada edição do artigo abre uma nova linha em
`knowledge_article_versions` com snapshot completo (título, conteúdo,
descrição, tipo, tags, categoria) + os 5 campos da §8: **alteração**
(o próprio snapshot), **autor**, **data** (`CreateDateTime`), **motivo**
(`reason`, obrigatório) e **aprovador** (`approved_by_id`, carimbado
quando o status vira `Approved` — ADR-034).

Numeração (domínio, `KnowledgeArticle.BumpVersion`):

- criação = **v1.0** (snapshot semeado junto com o artigo, motivo
  "Criação do artigo");
- cada edição abre **minor**: v1.0 → v1.1 → v1.2…;
- aprovar põe o "piso" do próximo major — a próxima edição abre
  **v2.0**, depois v3.0… (cadeia da §8: v1.0 → v1.1 → v2.0);
- o artigo expõe a versão corrente no DTO (`version`); a tabela é
  única por `(article_id, version)` com FK Cascade (apagar artigo
  apaga o histórico); autor/aprovador com FK SetNull.

Endpoints (mesma visibilidade do artigo — ADR-034 §4: estado nunca-
publicado sem escrita = 404):

- `GET api/v1/knowledge/articles/{id}/versions` (`Read`) — lista mais
  recente primeiro, com autor/aprovador nomeados e `isCurrent`;
- `GET api/v1/knowledge/articles/{id}/versions/compare?from=1.0&to=1.1`
  (`Read`) — devolve os dois snapshots lado a lado (o diff é de quem
  consome); lado inexistente = 404 `Versão 'x' não encontrada.`

PUT (edição) ganhou o campo obrigatório `reason` no corpo (400
`O motivo da alteração é obrigatório.` via VM; motivo > 500 = 400
`O motivo da alteração pode ter no máximo 500 caracteres.` no
domínio). Artigos anteriores à K3 receberam a v1.0 por backfill na
migration (mesmo conteúdo corrente, motivo fixo "Criação do artigo").

### Origem (F24-T004/K4 — spec docs/32 §9)

O artigo mantém sua origem com **múltiplas origens**: Task, PDI e
Chamado (a §9 lista também Projetos/documentos/treinamentos — essas
entidades não existem no repo e não foram inventadas; se nascerem,
entra valor novo no enum + validação).

- Tabela `knowledge_article_origins` (`article_id` FK **Cascade** +
  `origin_type` string + `origin_id` **sem FK** — alvo de outro
  agregado, precedente ADR-033; existência validada no handler por
  tipo; chave única artigo+tipo+id). Diferente do vínculo de uso
  (§10/F10-T004/P5a — "esta task usa este artigo"), a origem é
  artigo-centro ("de onde veio").
- Endpoints (mesmo molde dos vínculos F10-T004):
  - `POST api/v1/knowledge/articles/{id}/origins/{type}/{originId}`
    (`Operate`) — `type` ∈ `task|pdi|incident` (case-insensitive);
    tipo fora = 400 `Origem inválida: 'x'. Use 'task', 'pdi' ou
    'incident'.`; alvo inexistente = 404 `Task '{id}' não encontrada.` /
    `PDI '{id}' não encontrado.` / `Incidente '{id}' não encontrado.`;
    duplicado = 409 `A origem já está vinculada ao artigo.`;
  - `DELETE` na mesma rota — vínculo inexistente = 404
    `Origem '{type}' '{id}' não encontrada para o artigo '{id}'.`
- `origins` (`[{type, id}]`, tipo como nome do enum) vem embutido no
  **GET/{id}** e na **busca** (lote por página — round-trip único, sem
  N+1, diferente dos vínculos F10-T004 que são só do detalhe): o card
  mostra as origens como chips ("Task"/"PDI"/"Chamado").
- A figura da §9 fica fechada: origem (K4) + autor (K1) + revisão =
  aprovador (K2/K3) + versão (K3).

### Relacionamentos (F10-T004)

Vínculos N:N do artigo (tabelas `knowledge_article_devices`,
`knowledge_article_incidents` e `knowledge_article_services`, chave
composta `article_id + <target_id>`), espelhando `task_devices`:
`POST`/`DELETE api/v1/knowledge/articles/{id}/{devices|incidents|
services}/{targetId}` (`RoleAccess.Operate`, **204** nos dois
sentidos). Artigo ou alvo inexistente = 404 pt-BR; duplicidade = 409
`O device já está vinculado ao artigo.` (e os espelhos "incidente" /
"serviço").

Os ids aparecem só no `GET/{id}` (`deviceIds`/`incidentIds`/
`serviceIds`) — a busca não detalha (sem N+1 por página). **Delete em
cascata dos dois lados (decisão do usuário, 30/09/2026)**: apagar o
artigo limpa os vínculos e apagar device/incident/service remove **só o
vínculo** — o artigo permanece (nenhum pré-check em
`DeleteDeviceCommand`/`DeleteServiceCommand`; FKs `ON DELETE CASCADE`
na migration `AddKnowledgeArticleLinks`).
