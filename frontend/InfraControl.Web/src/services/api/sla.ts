import { api } from "./client";

/**
 * F17-T005 — conexão da SlaPage ao backend (contrato real de
 * `GET/POST /api/v1/sla/services`, `/sla/slos`, `/sla/policies` e
 * orçamentos em `/sla/slos/{sloId}/budgets`). Espelha `ServiceDto`,
 * `SloDto`, `SlaPolicyDto` e `SloBudgetDto`.
 */
export interface ServiceItem {
  id: string;
  name: string;
  description: string | null;
}

export interface SloItem {
  id: string;
  name: string;
  targetPercent: number;
  windowDays: number;
  serviceId: string;
}

export interface SlaPolicyItem {
  id: string;
  name: string;
  responseMinutes: number;
  resolutionMinutes: number;
  serviceId: string | null;
}

export interface SloBudgetItem {
  id: string;
  sloId: string;
  periodStart: string;
  periodEnd: string | null;
  consumedPercent: number;
}

/** `PagedResult<T>` — o campo `data` do envelope. */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

async function pagedGet<T>(
  path: string,
  page: number,
  pageSize: number,
): Promise<Paged<T>> {
  const data = await api.get<Paged<T>>(path);
  return data ?? { items: [], totalCount: 0, page, pageSize };
}

export function listServices(filters: { term?: string; page?: number; pageSize?: number } = {}): Promise<Paged<ServiceItem>> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return pagedGet<ServiceItem>(`/sla/services?${params}`, filters.page ?? 1, filters.pageSize ?? 20);
}

export function listSlos(filters: { term?: string; serviceId?: string; page?: number; pageSize?: number } = {}): Promise<Paged<SloItem>> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.serviceId) params.set("serviceId", filters.serviceId);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return pagedGet<SloItem>(`/sla/slos?${params}`, filters.page ?? 1, filters.pageSize ?? 20);
}

export function listSlaPolicies(filters: { term?: string; serviceId?: string; page?: number; pageSize?: number } = {}): Promise<Paged<SlaPolicyItem>> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.serviceId) params.set("serviceId", filters.serviceId);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return pagedGet<SlaPolicyItem>(`/sla/policies?${params}`, filters.page ?? 1, filters.pageSize ?? 20);
}

/** Orçamento por SLO — útil p/ drill-down de um SLO; a visão geral usa o agregado. */
export function listSloBudgets(sloId: string, filters: { page?: number; pageSize?: number } = {}): Promise<Paged<SloBudgetItem>> {
  const params = new URLSearchParams();
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return pagedGet<SloBudgetItem>(`/sla/slos/${sloId}/budgets?${params}`, filters.page ?? 1, filters.pageSize ?? 20);
}

/**
 * F18-T003 — agregado por serviço (`GET /sla/slos/budgets?serviceId=`):
 * devolve os orçamentos de todos os SLOs em 1 pedido (a SlaPage deixou
 * de fazer 1 por SLO). `serviceId` omitido = todos os serviços.
 */
export function listServiceBudgets(filters: { serviceId?: string; page?: number; pageSize?: number } = {}): Promise<Paged<SloBudgetItem>> {
  const params = new URLSearchParams();
  if (filters.serviceId) params.set("serviceId", filters.serviceId);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return pagedGet<SloBudgetItem>(`/sla/slos/budgets?${params}`, filters.page ?? 1, filters.pageSize ?? 20);
}

/** Criação de serviço (Operate + antiforgery; validação pt-BR no envelope). */
export function createService(input: {
  name: string;
  description?: string;
}): Promise<ServiceItem | null> {
  return api.post<ServiceItem>("/sla/services", {
    name: input.name,
    description: input.description || undefined,
  });
}

/** Atualiza serviço (PUT /sla/services/{id}; 409 com vínculos). */
export async function updateService(
  id: string,
  input: { name: string; description?: string },
): Promise<null> {
  return api.put<null>(`/sla/services/${id}`, {
    name: input.name,
    description: input.description ?? null,
  });
}

/** Exclui serviço (DELETE /sla/services/{id}; 409 se houver SLOs/políticas). */
export async function deleteService(id: string): Promise<null> {
  return api.delete<null>(`/sla/services/${id}`);
}

/** Cria política de SLA (Operate). */
export function createSlaPolicy(input: {
  name: string;
  responseMinutes: number;
  resolutionMinutes: number;
  serviceId?: string;
}): Promise<SlaPolicyItem | null> {
  return api.post<SlaPolicyItem>("/sla/policies", {
    name: input.name,
    responseMinutes: input.responseMinutes,
    resolutionMinutes: input.resolutionMinutes,
    serviceId: input.serviceId || null,
  });
}

/** Atualiza política de SLA (PUT /sla/policies/{id}). */
export async function updateSlaPolicy(
  id: string,
  input: {
    name: string;
    responseMinutes: number;
    resolutionMinutes: number;
    serviceId?: string;
  },
): Promise<null> {
  return api.put<null>(`/sla/policies/${id}`, {
    name: input.name,
    responseMinutes: input.responseMinutes,
    resolutionMinutes: input.resolutionMinutes,
    serviceId: input.serviceId || null,
  });
}

/** Exclui política de SLA (DELETE /sla/policies/{id}). */
export async function deleteSlaPolicy(id: string): Promise<null> {
  return api.delete<null>(`/sla/policies/${id}`);
}

/** Cria SLO (Operate; serviceId obrigatório). */
export function createSlo(input: {
  name: string;
  targetPercent: number;
  windowDays: number;
  serviceId: string;
}): Promise<SloItem | null> {
  return api.post<SloItem>("/sla/slos", {
    name: input.name,
    targetPercent: input.targetPercent,
    windowDays: input.windowDays,
    serviceId: input.serviceId,
  });
}

/** Atualiza SLO (PUT /sla/slos/{id}). */
export async function updateSlo(
  id: string,
  input: { name: string; targetPercent: number; windowDays: number; serviceId: string },
): Promise<null> {
  return api.put<null>(`/sla/slos/${id}`, {
    name: input.name,
    targetPercent: input.targetPercent,
    windowDays: input.windowDays,
    serviceId: input.serviceId,
  });
}

/** Exclui SLO (DELETE /sla/slos/{id}). */
export async function deleteSlo(id: string): Promise<null> {
  return api.delete<null>(`/sla/slos/${id}`);
}
