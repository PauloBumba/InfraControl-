import { api } from "./client";

/**
 * PDI — Plano de Desenvolvimento Individual (docs/29 §8, docs/30,
 * ADR-029). Contrato espelha `PdiDto` (Application): enum como string,
 * `targetDate`/`periodStart`/`periodEnd` DateOnly ("yyyy-MM-dd"). Ciclo
 * de 9 estados com transições da tabela docs/30 §3 (o backend valida —
 * aqui é só a casca de UI). Policy Operate; Admin vê todos, Operador vê
 * só o próprio.
 */
export type PdiStatus =
  | "Draft"
  | "Planned"
  | "AwaitingAcceptance"
  | "Active"
  | "InProgress"
  | "UnderEvaluation"
  | "Completed"
  | "Replanned"
  | "Cancelled";

export interface Pdi {
  userId: string;
  ownerName: string;
  ownerEmail: string;
  strategy: string;
  targetDate: string;
  status: PdiStatus;
  managerId: string | null;
  managerName: string | null;
  periodStart: string | null;
  periodEnd: string | null;
  updateDateTime: string;
}

export type PdiHistoryType = "Created" | "Updated" | "StatusChanged";

export interface PdiHistoryEntry {
  id: string;
  type: PdiHistoryType;
  actorId: string;
  actorName: string | null;
  fromStatus: PdiStatus | null;
  toStatus: PdiStatus | null;
  reason: string | null;
  detail: string | null;
  occurredAt: string;
}

export const PDI_STATUS_OPTIONS: { value: PdiStatus; label: string }[] = [
  { value: "Draft", label: "Rascunho" },
  { value: "Planned", label: "Planejado" },
  { value: "AwaitingAcceptance", label: "Aguardando aceite" },
  { value: "Active", label: "Ativo" },
  { value: "InProgress", label: "Em acompanhamento" },
  { value: "UnderEvaluation", label: "Em avaliação" },
  { value: "Completed", label: "Concluído" },
  { value: "Replanned", label: "Replanejado" },
  { value: "Cancelled", label: "Cancelado" },
];

export const PDI_STATUS_STYLE: Record<
  PdiStatus,
  { background: string; color: string }
> = {
  Draft: { background: "#f1f5f9", color: "#64748b" },
  Planned: { background: "#e0e7ff", color: "#4338ca" },
  AwaitingAcceptance: { background: "#fef3c7", color: "#b45309" },
  Active: { background: "#dcfce7", color: "#15803d" },
  InProgress: { background: "#dbeafe", color: "#1d4ed8" },
  UnderEvaluation: { background: "#fae8ff", color: "#a21caf" },
  Completed: { background: "#d1fae5", color: "#047857" },
  Replanned: { background: "#ffedd5", color: "#c2410c" },
  Cancelled: { background: "#fee2e2", color: "#b91c1c" },
};

/** Setas do diagrama docs/30 §3 — espelho de `PdiTransitions` (ADR-029). */
export const PDI_NEXT_STATUS: Record<PdiStatus, readonly PdiStatus[]> = {
  Draft: ["Planned"],
  Planned: ["AwaitingAcceptance"],
  AwaitingAcceptance: ["Active"],
  Active: ["InProgress", "Replanned", "Cancelled"],
  InProgress: ["UnderEvaluation"],
  UnderEvaluation: ["Completed"],
  Replanned: ["Active"],
  Completed: [],
  Cancelled: [],
};

export const PDI_HISTORY_TYPE_LABEL: Record<PdiHistoryType, string> = {
  Created: "Plano criado",
  Updated: "Campos atualizados",
  StatusChanged: "Status alterado",
};

export const PDI_STATUS_LABEL: Record<PdiStatus, string> =
  Object.fromEntries(
    PDI_STATUS_OPTIONS.map((option) => [option.value, option.label]),
  ) as Record<PdiStatus, string>;

/** Admin: todos os planos · Operador: só o próprio (0..1 itens). */
export async function listPdis(): Promise<Pdi[]> {
  const data = await api.get<Pdi[]>("/pdi");
  return data ?? [];
}

/** Upsert — criar é exclusivo do Admin (403 para Operador). */
export async function upsertPdi(
  userId: string,
  input: {
    strategy: string;
    targetDate: string;
    status: PdiStatus;
    managerId?: string | null;
    periodStart?: string | null;
    periodEnd?: string | null;
    reason?: string | null;
  },
): Promise<Pdi> {
  const data = await api.put<Pdi>(`/pdi/${userId}`, input);
  if (!data) throw new Error("Resposta vazia do servidor ao salvar o PDI.");
  return data;
}

/** Timeline (mais recente primeiro) — Admin: qualquer; Operador: o próprio. */
export async function getPdiHistory(userId: string): Promise<PdiHistoryEntry[]> {
  const data = await api.get<PdiHistoryEntry[]>(`/pdi/${userId}/history`);
  return data ?? [];
}

// ---------------------------------------------------------------------------
// Estrutura do desenvolvimento (docs/30 §5-§8, Fase 2, ADR-032):
// objetivos → estratégias → fases → atividades + catálogo de
// competências. Contrato espelha `PdiStructureDtos` (Application);
// escrita dono ou Admin (403), exclusão com children → 409.
// ---------------------------------------------------------------------------

export type PdiPhaseStatus = "NotStarted" | "InProgress" | "Completed";

export interface PdiCompetency {
  id: string;
  name: string;
  description: string | null;
}

export interface PdiActivityNode {
  id: string;
  title: string;
  dueDate: string | null;
  completed: boolean;
  order: number;
}

export interface PdiPhaseNode {
  id: string;
  title: string;
  description: string | null;
  responsibleId: string | null;
  responsibleName: string | null;
  startDate: string | null;
  endDate: string | null;
  completionCriteria: string | null;
  status: PdiPhaseStatus;
  percent: number;
  order: number;
  activities: PdiActivityNode[];
}

export interface PdiStrategyNode {
  id: string;
  title: string;
  description: string | null;
  order: number;
  phases: PdiPhaseNode[];
}

export interface PdiObjectiveNode {
  id: string;
  title: string;
  description: string | null;
  competencyId: string | null;
  competencyName: string | null;
  currentSituation: string | null;
  desiredSituation: string | null;
  expectedResult: string | null;
  indicator: string | null;
  targetDate: string | null;
  order: number;
  strategies: PdiStrategyNode[];
}

export interface PdiStructure {
  userId: string;
  objectives: PdiObjectiveNode[];
}

export const PDI_PHASE_STATUS_OPTIONS: {
  value: PdiPhaseStatus;
  label: string;
}[] = [
  { value: "NotStarted", label: "Não iniciada" },
  { value: "InProgress", label: "Em andamento" },
  { value: "Completed", label: "Concluída" },
];

export const PDI_PHASE_STATUS_LABEL: Record<PdiPhaseStatus, string> =
  Object.fromEntries(
    PDI_PHASE_STATUS_OPTIONS.map((option) => [option.value, option.label]),
  ) as Record<PdiPhaseStatus, string>;

export const PDI_PHASE_STATUS_STYLE: Record<
  PdiPhaseStatus,
  { background: string; color: string }
> = {
  NotStarted: { background: "#f1f5f9", color: "#64748b" },
  InProgress: { background: "#dbeafe", color: "#1d4ed8" },
  Completed: { background: "#dcfce7", color: "#15803d" },
};

/** Árvore do PDI — Admin: qualquer usuário; Operador: só o próprio. */
export async function getPdiStructure(userId: string): Promise<PdiStructure> {
  const data = await api.get<PdiStructure>(`/pdi/${userId}/structure`);
  return data ?? { userId, objectives: [] };
}

// ---- Catálogo de competências (escrita = Admin) --------------------------

export async function listPdiCompetencies(): Promise<PdiCompetency[]> {
  const data = await api.get<PdiCompetency[]>("/pdi/competencies");
  return data ?? [];
}

export async function createPdiCompetency(input: {
  name: string;
  description?: string | null;
}): Promise<PdiCompetency> {
  const data = await api.post<PdiCompetency>("/pdi/competencies", input);
  if (!data) throw new Error("Resposta vazia do servidor ao criar a competência.");
  return data;
}

export async function updatePdiCompetency(
  id: string,
  input: { name: string; description?: string | null },
): Promise<void> {
  await api.put(`/pdi/competencies/${id}`, input);
}

export async function deletePdiCompetency(id: string): Promise<void> {
  await api.delete(`/pdi/competencies/${id}`);
}

// ---- Objetivos -----------------------------------------------------------

export interface PdiObjectiveInput {
  title: string;
  description?: string | null;
  competencyId?: string | null;
  currentSituation?: string | null;
  desiredSituation?: string | null;
  expectedResult?: string | null;
  indicator?: string | null;
  targetDate?: string | null;
}

export async function createPdiObjective(
  userId: string,
  input: PdiObjectiveInput,
): Promise<PdiObjectiveNode> {
  const data = await api.post<PdiObjectiveNode>("/pdi/objectives", {
    userId,
    ...input,
  });
  if (!data) throw new Error("Resposta vazia do servidor ao criar o objetivo.");
  return data;
}

export async function updatePdiObjective(
  id: string,
  input: PdiObjectiveInput,
): Promise<void> {
  await api.put(`/pdi/objectives/${id}`, input);
}

export async function deletePdiObjective(id: string): Promise<void> {
  await api.delete(`/pdi/objectives/${id}`);
}

// ---- Estratégias ---------------------------------------------------------

export interface PdiStrategyInput {
  title: string;
  description?: string | null;
}

export async function createPdiStrategy(
  objectiveId: string,
  input: PdiStrategyInput,
): Promise<PdiStrategyNode> {
  const data = await api.post<PdiStrategyNode>("/pdi/strategies", {
    objectiveId,
    ...input,
  });
  if (!data) throw new Error("Resposta vazia do servidor ao criar a estratégia.");
  return data;
}

export async function updatePdiStrategy(
  id: string,
  input: PdiStrategyInput,
): Promise<void> {
  await api.put(`/pdi/strategies/${id}`, input);
}

export async function deletePdiStrategy(id: string): Promise<void> {
  await api.delete(`/pdi/strategies/${id}`);
}

// ---- Fases ---------------------------------------------------------------

export interface PdiPhaseInput {
  title: string;
  description?: string | null;
  responsibleId?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  completionCriteria?: string | null;
  status?: PdiPhaseStatus;
  percent?: number;
}

export async function createPdiPhase(
  strategyId: string,
  input: PdiPhaseInput,
): Promise<PdiPhaseNode> {
  const data = await api.post<PdiPhaseNode>("/pdi/phases", {
    strategyId,
    ...input,
  });
  if (!data) throw new Error("Resposta vazia do servidor ao criar a fase.");
  return data;
}

export async function updatePdiPhase(
  id: string,
  input: PdiPhaseInput,
): Promise<void> {
  await api.put(`/pdi/phases/${id}`, input);
}

export async function deletePdiPhase(id: string): Promise<void> {
  await api.delete(`/pdi/phases/${id}`);
}

// ---- Atividades ----------------------------------------------------------

export interface PdiActivityInput {
  title: string;
  dueDate?: string | null;
  completed?: boolean;
}

export async function createPdiActivity(
  phaseId: string,
  input: PdiActivityInput,
): Promise<PdiActivityNode> {
  const data = await api.post<PdiActivityNode>("/pdi/activities", {
    phaseId,
    ...input,
  });
  if (!data) throw new Error("Resposta vazia do servidor ao criar a atividade.");
  return data;
}

export async function updatePdiActivity(
  id: string,
  input: PdiActivityInput,
): Promise<void> {
  await api.put(`/pdi/activities/${id}`, input);
}

export async function deletePdiActivity(id: string): Promise<void> {
  await api.delete(`/pdi/activities/${id}`);
}
