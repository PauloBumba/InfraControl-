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
  /** Fase D — `DisplayName` resolvido no reader (join por e-mail); null = usuário removido. */
  actorName: string | null;
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
  /** `datetime-local` no fuso do usuário ("2026-10-01T09:00"). */
  from?: string;
  /** Mesmo formato de `from`; o filtro é inclusivo até o fim do minuto. */
  to?: string;
  page?: number;
  pageSize?: number;
}

/** Valores reais de outcome (AuditBehavior: "Succeeded"/"Failed"). */
export const AUDIT_OUTCOME_SUCCEEDED = "Succeeded";
export const AUDIT_OUTCOME_FAILED = "Failed";

/**
 * Converte o valor do input `datetime-local` (relógio local) para instante
 * ISO — `OccurredAt` é UTC no banco (`timestamp with time zone`), então sem
 * a conversão o corte sairia deslocado pelo fuso. `inclusiveEnd` fecha o
 * minuto escolhido (:59.999) porque o backend compara `<= to`.
 */
function toInstant(value: string, inclusiveEnd: boolean): string | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  if (inclusiveEnd) date.setSeconds(59, 999);
  return date.toISOString();
}

export async function listAuditEntries(
  filters: AuditFilters = {},
): Promise<PagedAudit> {
  const params = new URLSearchParams();
  if (filters.action) params.set("action", filters.action);
  if (filters.outcome) params.set("outcome", filters.outcome);
  const from = filters.from ? toInstant(filters.from, false) : null;
  if (from) params.set("from", from);
  const to = filters.to ? toInstant(filters.to, true) : null;
  if (to) params.set("to", to);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));

  const query = params.toString();
  const data = await api.get<PagedAudit>(
    `/admin/audit${query ? `?${query}` : ""}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}
