import { api } from "./client";
import type { AiChatEvidence } from "./ai";

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
  /**
   * Nome do enum (Open/Investigating/Identified/Monitoring/Resolved) ou o
   * alias `active` = qualquer estado != Resolved (M7 — o Dashboard usa o
   * totalCount do servidor para o KPI "em aberto").
   */
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

/**
 * Troca de status (PUT /incidents/{id}/status) — ciclo por estado no
 * backend (409/400 pt-BR quando a transição não é permitida). Sem DELETE
 * (ADR-030: ciclo por estado, exclusão não existe).
 */
export async function changeIncidentStatus(
  id: string,
  status: string,
): Promise<IncidentItem | null> {
  return api.put<IncidentItem>(`/incidents/${id}/status`, { status });
}

/** Acknowledge (POST /incidents/{id}/acknowledge) — Open → Investigating. */
export async function acknowledgeIncident(id: string): Promise<IncidentItem | null> {
  return api.post<IncidentItem>(`/incidents/${id}/acknowledge`);
}

/**
 * CRUD-Incidentes (2026-10-12) — edição dos campos descritivos (título,
 * descrição, severidade). O status NÃO muda aqui (ciclo por estado via
 * `changeIncidentStatus`, ADR-030) e a exclusão não existe por decisão de
 * arquitetura.
 */
export function updateIncident(
  id: string,
  input: { title: string; description?: string | null; severity: string },
): Promise<IncidentItem | null> {
  return api.put<IncidentItem>(`/incidents/${id}`, {
    title: input.title,
    description: input.description ?? null,
    severity: input.severity,
  });
}

/**
 * AI Investigation (ADR-053 · Fase C) — `POST /incidents/{id}/investigate`
 * (Operate). Análise LEVEL 0 do incidente com evidências do Knowledge;
 * 503 amigável quando nenhum provider está configurado (estado degradado §44).
 */
export interface InvestigationResult {
  analysis: string;
  evidences: AiChatEvidence[];
  provider: string;
  model: string;
  usedFallback: boolean;
}

export function investigateIncident(id: string): Promise<InvestigationResult> {
  return api.post<InvestigationResult>(
    `/incidents/${id}/investigate`,
  ) as Promise<InvestigationResult>;
}

/**
 * Knowledge Engineer (ADR-054 · Fase D) —
 * `POST /incidents/{id}/generate-knowledge` (Operate): rascunho de
 * artigo com IA. Nasce `Draft` (workflow ADR-034 intacto) — a publicação
 * é transição humana na página Knowledge. 503 amigável sem provider
 * configurado (§44/§57).
 */
export interface KnowledgeDraft {
  id: string;
  title: string;
  content: string;
  status: number;
}

export function generateIncidentKnowledge(id: string): Promise<KnowledgeDraft> {
  return api.post<KnowledgeDraft>(
    `/incidents/${id}/generate-knowledge`,
  ) as Promise<KnowledgeDraft>;
}
