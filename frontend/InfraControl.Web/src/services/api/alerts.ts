import { api } from "./client";

/**
 * F17-T009 — contrato real de `GET /api/v1/monitoring/alerts`
 * (`SearchAlertsQuery` → `PagedResult<AlertDto>`), usado pelo Dashboard,
 * AlertsPage (T011) e DeviceDetail (T017, filtro `deviceId` — parâmetro
 * real do endpoint). `severity`/`status` chegam como strings
 * `ToString()` ("Critical"/"Warning"; "Open"/"Resolved"/"Suppressed");
 * o parâmetro `status` da query é validado em minúsculas
 * (open|resolved|suppressed — valor fora responde 400 pt-BR).
 *
 * Não há POST público: alertas são criados automaticamente pós-check
 * (F6-T001).
 */
export interface AlertItem {
  id: string;
  /** ADR-027: nulo em alerta externo de host não mapeado. */
  deviceId: string | null;
  deviceName: string | null;
  /** ADR-027: nulo em alerta externo (sem CheckKind nativo). */
  check: string | null;
  /** ADR-027: origem — `Native` | `Zabbix`. */
  source: string;
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
  deviceId?: string;
  status?: AlertStatusFilter;
  page?: number;
  pageSize?: number;
} = {}): Promise<Paged<AlertItem>> {
  const params = new URLSearchParams();
  if (filters.deviceId) params.set("deviceId", filters.deviceId);
  if (filters.status) params.set("status", filters.status);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));

  const data = await api.get<Paged<AlertItem>>(`/monitoring/alerts?${params}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}
