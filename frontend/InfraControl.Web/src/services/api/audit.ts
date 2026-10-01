import { api } from "./client";

/**
 * F17-T002 — conexão da AuditPage ao backend (contrato real de
 * `GET /api/v1/admin/audit`, policy Administer). Espelha
 * `AuditEntrySummary` (Application). `outcome` é gravado por
 * `AuditBehavior`/`SecurityAudit` como **"Succeeded" | "Failed"** (não
 * "Success"/"Failure") e `action` é igualdade exata no reader: ou é um
 * nome de request MediatR ("CreateTaskCommand") ou um evento de segurança
 * ("LoginWithMfa", "Logout", "SessionRevoked"...).
 */
export interface AuditEntry {
  id: string;
  action: string;
  resource: string;
  outcome: string;
  actorId: string | null;
  correlationId: string | null;
  error: string | null;
  occurredAt: string;
}

/** `PagedResult<AuditEntrySummary>` — o campo `data` do envelope. */
export interface PagedAudit {
  items: AuditEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditFilters {
  action?: string;
  outcome?: string;
  page?: number;
  pageSize?: number;
}

/** Valores reais de outcome (AuditBehavior: "Succeeded"/"Failed"). */
export const AUDIT_OUTCOME_SUCCEEDED = "Succeeded";
export const AUDIT_OUTCOME_FAILED = "Failed";

export async function listAuditEntries(
  filters: AuditFilters = {},
): Promise<PagedAudit> {
  const params = new URLSearchParams();
  if (filters.action) params.set("action", filters.action);
  if (filters.outcome) params.set("outcome", filters.outcome);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));

  const query = params.toString();
  const data = await api.get<PagedAudit>(
    `/admin/audit${query ? `?${query}` : ""}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}
