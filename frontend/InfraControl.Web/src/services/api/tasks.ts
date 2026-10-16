import { api } from "./client";
import type { KnowledgeArticle } from "./knowledge";

/**
 * F17 — conexão da TasksPage ao backend (contrato real de
 * `GET/POST /api/v1/tasks`). Espelha `TaskDto` (Application) e as enums
 * `TaskKind`/`TaskState` — os valores chegam em PascalCase
 * ("Manual", "InProgress") e a API os aceita case-insensitive nos filtros.
 * Identicação básica (F23-T001): `priority` (TaskPriority em PascalCase),
 * `dueDate` (`yyyy-MM-dd` ou null) e `responsibleId`/`responsibleName`.
 * Planejamento/resultado (F23-T003): `plannedStart`/`plannedEnd`
 * (`yyyy-MM-dd` ou null) e `result` (texto livre ou null).
 * Subtarefas/progresso (F23-T004/P4a): `parentTaskId`/`parentTitle`
 * (auto-relação) e `progress` derivado das subtarefas (concluídas ÷
 * não canceladas; null = sem subtarefas ativas).
 */
export interface TaskItem {
  id: string;
  title: string;
  description: string | null;
  kind: string;
  state: string;
  openedAt: string;
  assigneeCount: number;
  assigneeIds: string[];
  deviceCount: number;
  deviceIds: string[];
  incidentCount: number;
  incidentIds: string[];
  articleCount: number;
  articleIds: string[];
  pdiCount: number;
  pdiUserIds: string[];
  priority: string;
  dueDate: string | null;
  responsibleId: string | null;
  responsibleName: string | null;
  plannedStart: string | null;
  plannedEnd: string | null;
  result: string | null;
  parentTaskId: string | null;
  parentTitle: string | null;
  subtaskCount: number;
  progress: number | null;
  createdById: string | null;
  createdByName: string | null;
}

/** `PagedResult<TaskDto>` — o campo `data` do envelope. */
export interface PagedTasks {
  items: TaskItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface TaskFilters {
  term?: string;
  kind?: string;
  state?: string;
  page?: number;
  pageSize?: number;
  /** F23-T004/P4a — só as subtarefas deste pai. */
  parentId?: string;
  /** F23-T004/P4a — só tasks sem pai (o kanban usa). Exclusivo com `parentId`. */
  rootOnly?: boolean;
}

/**
 * Lista paginada (F7-T001). `pageSize` é clampado em 1..100 no handler;
 * enum de `kind`/`state` inválido responde 400 com a lista válida no
 * envelope — por isso sempre enviamos valores do catálogo (enums acima).
 * `parentId` + `rootOnly` juntos → 400 (filtros incompatíveis).
 */
export async function listTasks(filters: TaskFilters = {}): Promise<PagedTasks> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.kind) params.set("kind", filters.kind);
  if (filters.state) params.set("state", filters.state);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
  if (filters.parentId) params.set("parentId", filters.parentId);
  if (filters.rootOnly) params.set("rootOnly", "true");

  const query = params.toString();
  const data = await api.get<PagedTasks>(`/tasks${query ? `?${query}` : ""}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}

/**
 * Criação de task (Operate no backend + antiforgery pelo client). O 403/400
 * chega como ApiError com a mensagem pt-BR do envelope — a tela só exibe.
 * `priority` null = padrão Medium no backend (F23-T001). `parentId`
 * cria a task como subtarefa (F23-T004/P4a): 404 se o pai não existe,
 * 400 se o pai é ele mesmo subtarefa.
 */
export function createTask(input: {
  title: string;
  kind: string;
  description?: string;
  priority?: string;
  dueDate?: string;
  responsibleId?: string;
  parentId?: string;
}): Promise<TaskItem | null> {
  return api.post<TaskItem>("/tasks", {
    title: input.title,
    kind: input.kind,
    description: input.description || undefined,
    priority: input.priority || undefined,
    dueDate: input.dueDate || undefined,
    responsibleId: input.responsibleId || undefined,
    parentTaskId: input.parentId || undefined,
  });
}

/**
 * Identificação básica (F23-T001 → `PUT /api/v1/tasks/{id}/identification`,
 * Operate). Comando de SUBSTITUIÇÃO: envia os 3 campos juntos; sem
 * `dueDate`/`responsibleId` o backend limpa os valores. `priority`
 * obrigatória (400 pt-BR quando vazia/inválida) e `responsibleId`
 * desconhecido → 404 — ambos chegam como ApiError com o envelope.
 */
export function setTaskIdentification(
  id: string,
  input: { priority: string; dueDate?: string; responsibleId?: string },
): Promise<TaskItem | null> {
  return api.put<TaskItem>(`/tasks/${id}/identification`, {
    priority: input.priority,
    dueDate: input.dueDate || null,
    responsibleId: input.responsibleId || null,
  });
}

/* ── F23-T002 — tela de detalhe `/tasks/:id` ─────────────────────────── */

/** `TaskCommentDto` (F7-T005). */
export interface TaskComment {
  id: string;
  taskId: string;
  authorId: string;
  authorName: string | null;
  content: string;
  createdAt: string;
}

/** `TaskHistoryDto` (F7-T006). */
export interface TaskHistoryEntry {
  id: string;
  taskId: string;
  actorId: string | null;
  actorName: string | null;
  action: string;
  detail: string | null;
  createdAt: string;
}

/** `TaskSlaDto` (F8-T003) — snapshot dos prazos. */
export interface TaskSla {
  taskId: string;
  slaPolicyId: string;
  responseMinutes: number;
  resolutionMinutes: number;
  responseDueAt: string;
  resolutionDueAt: string;
}

/** `TaskSlaPauseDto` (F8-T004) — `endAt` nulo = pausa em curso. */
export interface TaskSlaPause {
  id: string;
  taskId: string;
  startAt: string;
  endAt: string | null;
}

/** `GET /tasks/{id}` — 404 pt-BR quando a task não existe. */
export function getTask(id: string): Promise<TaskItem | null> {
  return api.get<TaskItem>(`/tasks/${id}`);
}

/** `PUT /tasks/{id}` (Operate) — título/descrição; 204 → `null`. */
export function updateTask(
  id: string,
  input: { title: string; description?: string },
): Promise<null> {
  return api.put<null>(`/tasks/${id}`, {
    title: input.title,
    description: input.description ?? null,
  });
}

/** `DELETE /tasks/{id}` (Operate); 204 → `null`. */
export function deleteTask(id: string): Promise<null> {
  return api.delete<null>(`/tasks/${id}`);
}

export function getTaskComments(taskId: string): Promise<TaskComment[] | null> {
  return api.get<TaskComment[]>(`/tasks/${taskId}/comments`);
}

/** `POST /tasks/{id}/comments` (Operate) — autor = usuário logado. */
export function addTaskComment(taskId: string, content: string): Promise<TaskComment | null> {
  return api.post<TaskComment>(`/tasks/${taskId}/comments`, { content });
}

export function deleteTaskComment(commentId: string): Promise<null> {
  return api.delete<null>(`/tasks/comments/${commentId}`);
}

export function getTaskHistory(taskId: string): Promise<TaskHistoryEntry[] | null> {
  return api.get<TaskHistoryEntry[]>(`/tasks/${taskId}/history`);
}

/** `POST/DELETE /tasks/{id}/assignees/{userId}` (Operate). */
export function assignTaskUser(taskId: string, userId: string): Promise<null> {
  return api.post<null>(`/tasks/${taskId}/assignees/${userId}`);
}

export function unassignTaskUser(taskId: string, userId: string): Promise<null> {
  return api.delete<null>(`/tasks/${taskId}/assignees/${userId}`);
}

/** `POST/DELETE /tasks/{id}/devices/{deviceId}` (Operate). */
export function linkTaskDevice(taskId: string, deviceId: string): Promise<null> {
  return api.post<null>(`/tasks/${taskId}/devices/${deviceId}`);
}

export function unlinkTaskDevice(taskId: string, deviceId: string): Promise<null> {
  return api.delete<null>(`/tasks/${taskId}/devices/${deviceId}`);
}

/** `POST/DELETE /tasks/{id}/incidents/{incidentId}` (Operate). */
export function linkTaskIncident(taskId: string, incidentId: string): Promise<null> {
  return api.post<null>(`/tasks/${taskId}/incidents/${incidentId}`);
}

export function unlinkTaskIncident(taskId: string, incidentId: string): Promise<null> {
  return api.delete<null>(`/tasks/${taskId}/incidents/${incidentId}`);
}

/** `POST/DELETE /tasks/{id}/articles/{articleId}` (Operate) — P5a. */
export function linkTaskArticle(taskId: string, articleId: string): Promise<null> {
  return api.post<null>(`/tasks/${taskId}/articles/${articleId}`);
}

export function unlinkTaskArticle(taskId: string, articleId: string): Promise<null> {
  return api.delete<null>(`/tasks/${taskId}/articles/${articleId}`);
}

/** `POST/DELETE /tasks/{id}/pdi/{userId}` (Operate) — P5b (1 PDI/usuário). */
export function linkTaskPdi(taskId: string, userId: string): Promise<null> {
  return api.post<null>(`/tasks/${taskId}/pdi/${userId}`);
}

export function unlinkTaskPdi(taskId: string, userId: string): Promise<null> {
  return api.delete<null>(`/tasks/${taskId}/pdi/${userId}`);
}

/**
 * F24-T006/K6 — `POST /tasks/{id}/generate-knowledge` (Operate): gera
 * rascunho Knowledge a partir dos registros da task CONCLUÍDA
 * (docs/32 §3, ADR-035 — montagem determinística, nunca inventa).
 * O artigo nasce Draft e passa pela revisão normal (ADR-034).
 */
export async function generateTaskKnowledge(
  taskId: string,
): Promise<KnowledgeArticle> {
  const data = await api.post<KnowledgeArticle>(
    `/tasks/${taskId}/generate-knowledge`,
  );
  if (!data) throw new Error("Resposta vazia do servidor ao gerar o rascunho.");
  return data;
}

/**
 * `GET /tasks/{id}/sla` — **404 é fluxo normal** (sem SLA aplicado):
 * o chamador trata `ApiError.status === 404` como "sem SLA".
 */
export function getTaskSla(taskId: string): Promise<TaskSla | null> {
  return api.get<TaskSla>(`/tasks/${taskId}/sla`);
}

/** `POST /tasks/{id}/sla` (Operate) — 409 se já aplicado. */
export function applyTaskSla(taskId: string, slaPolicyId: string): Promise<TaskSla | null> {
  return api.post<TaskSla>(`/tasks/${taskId}/sla`, { slaPolicyId });
}

export function getTaskSlaPauses(taskId: string): Promise<TaskSlaPause[] | null> {
  return api.get<TaskSlaPause[]>(`/tasks/${taskId}/sla/pauses`);
}

/** Pausa/resume o relógio (Operate) — 404 se não houver SLA. */
export function pauseTaskSla(taskId: string): Promise<null> {
  return api.post<null>(`/tasks/${taskId}/sla/pause`);
}

export function resumeTaskSla(taskId: string): Promise<null> {
  return api.post<null>(`/tasks/${taskId}/sla/resume`);
}

/* ── F23-T003 — planejamento, resultado, replanejo e critérios ──────── */

/**
 * `PUT /tasks/{id}/planning` (Operate) — substituição: datas nulas
 * limpam; fim antes do início → 400 pt-BR.
 */
export function updateTaskPlanning(
  id: string,
  input: { plannedStart?: string; plannedEnd?: string },
): Promise<TaskItem | null> {
  return api.put<TaskItem>(`/tasks/${id}/planning`, {
    plannedStart: input.plannedStart || null,
    plannedEnd: input.plannedEnd || null,
  });
}

/** `PUT /tasks/{id}/result` (Operate) — null/vazio limpa (máx. 2000). */
export function updateTaskResult(
  id: string,
  result?: string,
): Promise<TaskItem | null> {
  return api.put<TaskItem>(`/tasks/${id}/result`, {
    result: result || null,
  });
}

/**
 * `POST /tasks/{id}/replan` (Operate) — início/fim/prazo + motivo
 * obrigatório (400 se vazio ou sem mudança real).
 */
export function replanTask(
  id: string,
  input: {
    plannedStart?: string;
    plannedEnd?: string;
    dueDate?: string;
    reason: string;
  },
): Promise<TaskItem | null> {
  return api.post<TaskItem>(`/tasks/${id}/replan`, {
    plannedStart: input.plannedStart || null,
    plannedEnd: input.plannedEnd || null,
    dueDate: input.dueDate || null,
    reason: input.reason,
  });
}

/** `TaskCriterionDto` — critério de aceite (F23-T003 — §12). */
export interface TaskCriterion {
  id: string;
  taskId: string;
  description: string;
  isDone: boolean;
  doneAt: string | null;
  createdAt: string;
}

export function getTaskCriteria(taskId: string): Promise<TaskCriterion[] | null> {
  return api.get<TaskCriterion[]>(`/tasks/${taskId}/criteria`);
}

/** `POST /tasks/{id}/criteria` (Operate) — 201 com o critério. */
export function addTaskCriterion(
  taskId: string,
  description: string,
): Promise<TaskCriterion | null> {
  return api.post<TaskCriterion>(`/tasks/${taskId}/criteria`, { description });
}

/**
 * `PUT /tasks/criteria/{id}` (Operate) — campos nulos mantêm o valor
 * atual (descrição e/ou marcação de atendido).
 */
export function updateTaskCriterion(
  criterionId: string,
  input: { description?: string; done?: boolean },
): Promise<TaskCriterion | null> {
  return api.put<TaskCriterion>(`/tasks/criteria/${criterionId}`, {
    description: input.description ?? null,
    done: input.done ?? null,
  });
}

export function deleteTaskCriterion(criterionId: string): Promise<null> {
  return api.delete<null>(`/tasks/criteria/${criterionId}`);
}

/* ── F23-T004/P4a — subtarefas e progresso ──────────────────────────── */

/**
 * `PUT /tasks/{id}/parent` (Operate) — move a task para outro pai ou
 * desvincula (`parentId` null → task vira raiz). 400 pt-BR para
 * auto-relação ("própria subtarefa") e para pai que é ele mesmo
 * subtarefa (profundidade ≤ 2); 404 se task/pai não existem. No-op
 * (mesmo pai) responde 200 sem histórico.
 */
export function setTaskParent(
  id: string,
  parentId?: string,
): Promise<TaskItem | null> {
  return api.put<TaskItem>(`/tasks/${id}/parent`, {
    parentId: parentId || null,
  });
}

/** Filhos da task (lista paginada; use `pageSize` alto na tela de detalhe). */
export function listSubtasks(taskId: string): Promise<PagedTasks> {
  return listTasks({ parentId: taskId, pageSize: 100 });
}

/* ── F23-T004/P4b — impedimentos estruturados (§10) ─────────────────── */

/** `TaskImpedimentDto` — `resolvedAt` null = em aberto. */
export interface TaskImpediment {
  id: string;
  taskId: string;
  description: string;
  createdAt: string;
  resolvedAt: string | null;
}

export function getTaskImpediments(taskId: string): Promise<TaskImpediment[] | null> {
  return api.get<TaskImpediment[]>(`/tasks/${taskId}/impediments`);
}

/** `POST /tasks/{id}/impediments` (Operate) — 201; descrição ≤300. */
export function addTaskImpediment(
  taskId: string,
  description: string,
): Promise<TaskImpediment | null> {
  return api.post<TaskImpediment>(`/tasks/${taskId}/impediments`, { description });
}

/**
 * `PUT /tasks/impediments/{id}` (Operate) — campos nulos mantêm o valor
 * atual; `resolved: true` resolve (carimba), `false` reabre.
 */
export function updateTaskImpediment(
  impedimentId: string,
  input: { description?: string; resolved?: boolean },
): Promise<TaskImpediment | null> {
  return api.put<TaskImpediment>(`/tasks/impediments/${impedimentId}`, {
    description: input.description ?? null,
    resolved: input.resolved ?? null,
  });
}

export function deleteTaskImpediment(impedimentId: string): Promise<null> {
  return api.delete<null>(`/tasks/impediments/${impedimentId}`);
}

/* ── F23-T004/P4c — evidências anexadas (§11, ADR-033) ──────────────── */

/** `TaskEvidenceDto` — metadados; o binário vem do `downloadTaskEvidence`. */
export interface TaskEvidence {
  id: string;
  taskId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  note: string | null;
  uploadedAt: string;
}

export function getTaskEvidence(taskId: string): Promise<TaskEvidence[] | null> {
  return api.get<TaskEvidence[]>(`/tasks/${taskId}/evidences`);
}

/** `POST /tasks/{id}/evidences` (Operate) — multipart; `file` ≤10 MB. */
export function uploadTaskEvidence(
  taskId: string,
  file: File,
  note?: string,
): Promise<TaskEvidence | null> {
  const form = new FormData();
  form.append("file", file);
  if (note) form.append("note", note);
  return api.post<TaskEvidence>(`/tasks/${taskId}/evidences`, form);
}

/**
 * `GET /tasks/evidences/{id}/download` (leitura) — baixa o binário e
 * salva no disco do usuário com o nome original (blob + `download`).
 */
export async function downloadTaskEvidence(evidence: TaskEvidence): Promise<void> {
  const blob = await api.get<Blob>(
    `/tasks/evidences/${evidence.id}/download`,
    { raw: true },
  );
  if (!blob) return;

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = evidence.fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

export function deleteTaskEvidence(evidenceId: string): Promise<null> {
  return api.delete<null>(`/tasks/evidences/${evidenceId}`);
}

/* ── F23-T004/P4d — fases/etapas dentro da task (§7) ────────────────── */

/** `TaskPhaseDto` — etapa nomeada por task; `completedAt` null = em aberto. */
export interface TaskPhase {
  id: string;
  taskId: string;
  name: string;
  position: number;
  isCurrent: boolean;
  completedAt: string | null;
}

export function getTaskPhases(taskId: string): Promise<TaskPhase[] | null> {
  return api.get<TaskPhase[]>(`/tasks/${taskId}/phases`);
}

/** `POST /tasks/{id}/phases` (Operate) — 201; a primeira já nasce como a atual. */
export function addTaskPhase(taskId: string, name: string): Promise<TaskPhase | null> {
  return api.post<TaskPhase>(`/tasks/${taskId}/phases`, { name });
}

/**
 * `PUT /tasks/phases/{id}` (Operate) — campos nulos mantêm o valor
 * atual; `current: true` limpa o ponteiro dos irmãos (máx. 1 fase
 * atual por task) e `completed` carimba/limpa a conclusão.
 */
export function updateTaskPhase(
  phaseId: string,
  input: { name?: string; current?: boolean; completed?: boolean },
): Promise<TaskPhase | null> {
  return api.put<TaskPhase>(`/tasks/phases/${phaseId}`, {
    name: input.name ?? null,
    current: input.current ?? null,
    completed: input.completed ?? null,
  });
}

export function deleteTaskPhase(phaseId: string): Promise<null> {
  return api.delete<null>(`/tasks/phases/${phaseId}`);
}

/* ── F23-T004/P4d — timeline unificada tipada (§8) ──────────────────── */

/** Entrada da timeline unificada (`type` ∈ `TIMELINE_TYPES` do catálogo). */
export interface TaskTimelineEntry {
  /** Opaco e prefixado ("h:" histórico | "c:" comentário). */
  id: string;
  type: string;
  /** `TaskHistoryAction` em PascalCase; null para comentários. */
  action: string | null;
  detail: string | null;
  /** Texto integral do comentário; null para entradas de histórico. */
  comment: string | null;
  at: string;
  actorId: string | null;
  actorName: string | null;
}

/**
 * `GET /tasks/{id}/timeline` (leitura) — histórico + comentários
 * unificados e tipados (13 tipos; sem tabela nova). `types` é
 * comma-separated — tipo desconhecido → 400 pt-BR. `limit` padrão 100
 * (teto 500 no backend); mais recente primeiro.
 */
export function getTaskTimeline(
  taskId: string,
  types?: string[],
  limit?: number,
): Promise<TaskTimelineEntry[] | null> {
  const params = new URLSearchParams();
  if (types && types.length > 0) params.set("types", types.join(","));
  if (limit) params.set("limit", String(limit));
  const query = params.toString();
  return api.get<TaskTimelineEntry[]>(
    `/tasks/${taskId}/timeline${query ? `?${query}` : ""}`,
  );
}

/**
 * Transição de estado (F7-T007 → `PUT /api/v1/tasks/{id}/state`, Operate).
 * Body `{state}` aceita case-insensitive; estado desconhecido = 400 pt-BR e
 * transição fora do mapa do domínio = 409 pt-BR ("Transição inválida…") —
 * ambos chegam como ApiError com a mensagem do envelope.
 */
export function changeTaskState(id: string, state: string): Promise<TaskItem | null> {
  return api.put<TaskItem>(`/tasks/${id}/state`, { state });
}
