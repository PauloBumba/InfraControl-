import { api } from "./client";

/**
 * F17-T003 — conexão da IncidentsPage ao backend (contrato real de
 * `GET/POST /api/v1/incidents`). Espelha `IncidentDto` (Application).
 * Os enums vêm em PascalCase (`Severity`: Low/Medium/High/Critical;
 * `Status`: Open/Investigating/Identified/Monitoring/Resolved) e a API os
 * aceita case-insensitive nos filtros/body.
 */
export interface IncidentItem {
  id: string;
  title: string;
  description: string | null;
  severity: string;
  status: string;
  deviceId: string | null;
  openedAt: string;
  resolvedAt: string | null;
  alertCount: number;
  alertIds: string[];
  rootCause: string | null;
}

/** `PagedResult<IncidentDto>` — o campo `data` do envelope. */
export interface PagedIncidents {
  items: IncidentItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface IncidentFilters {
  status?: string;
  deviceId?: string;
  page?: number;
  pageSize?: number;
}

/** Lista paginada (F6-T003) — pageSize clampado em 1..100 no handler. */
export async function listIncidents(
  filters: IncidentFilters = {},
): Promise<PagedIncidents> {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.deviceId) params.set("deviceId", filters.deviceId);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));

  const query = params.toString();
  const data = await api.get<PagedIncidents>(`/incidents${query ? `?${query}` : ""}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}

/**
 * Criação manual (Operate no backend + antiforgery pelo client). `deviceId`
 * é opcional — sem vínculo de dispositivo a linha nasce sem ele.
 */
export function createIncident(input: {
  title: string;
  severity: string;
  description?: string;
  deviceId?: string;
}): Promise<IncidentItem | null> {
  return api.post<IncidentItem>("/incidents", {
    title: input.title,
    severity: input.severity,
    description: input.description || undefined,
    deviceId: input.deviceId || undefined,
  });
}
