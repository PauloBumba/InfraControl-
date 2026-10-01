import { api } from "./client";

/**
 * F17-T009 — contrato real de `GET /api/v1/monitoring/alerts`
 * (`SearchAlertsQuery` → `PagedResult<AlertDto>`), usado pelo Dashboard e
 * futuro pela AlertsPage. `severity`/`status` chegam como strings
 * `ToString()` ("Critical"/"Warning"; "Open"/"Resolved"/"Suppressed");
 * o parâmetro `status` da query é validado em minúsculas
 * (open|resolved|suppressed — valor fora responde 400 pt-BR).
 *
 * Não há POST público: alertas são criados automaticamente pós-check
 * (F6-T001).
 */
export interface AlertItem {
  id: string;
  deviceId: string;
  deviceName: string;
  check: string;
  severity: string;
  status: string;
  message: string;
  openedAt: string;
  resolvedAt: string | null;
}

/** `PagedResult<T>` — o campo `data` do envelope. */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Valores aceitos pelo parâmetro `status` (minúsculos, no contrato). */
export type AlertStatusFilter = "open" | "resolved" | "suppressed";

export async function listAlerts(filters: {
  status?: AlertStatusFilter;
  page?: number;
  pageSize?: number;
} = {}): Promise<Paged<AlertItem>> {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));

  const data = await api.get<Paged<AlertItem>>(`/monitoring/alerts?${params}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}
