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
