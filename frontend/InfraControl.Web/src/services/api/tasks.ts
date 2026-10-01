import { api } from "./client";

/**
 * F17 — conexão da TasksPage ao backend (contrato real de
 * `GET/POST /api/v1/tasks`). Espelha `TaskDto` (Application) e as enums
 * `TaskKind`/`TaskState` — os valores chegam em PascalCase
 * ("Manual", "InProgress") e a API os aceita case-insensitive nos filtros.
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
}

/**
 * Lista paginada (F7-T001). `pageSize` é clampado em 1..100 no handler;
 * enum de `kind`/`state` inválido responde 400 com a lista válida no
 * envelope — por isso sempre enviamos valores do catálogo (enums acima).
 */
export async function listTasks(filters: TaskFilters = {}): Promise<PagedTasks> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.kind) params.set("kind", filters.kind);
  if (filters.state) params.set("state", filters.state);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));

  const query = params.toString();
  const data = await api.get<PagedTasks>(`/tasks${query ? `?${query}` : ""}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}

/**
 * Criação de task (Operate no backend + antiforgery pelo client). O 403/400
 * chega como ApiError com a mensagem pt-BR do envelope — a tela só exibe.
 */
export function createTask(input: {
  title: string;
  kind: string;
  description?: string;
}): Promise<TaskItem | null> {
  return api.post<TaskItem>("/tasks", {
    title: input.title,
    kind: input.kind,
    description: input.description || undefined,
  });
}
