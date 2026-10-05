import { api } from "./client";

/**
 * PDI — Plano de Desenvolvimento Individual (docs/29 §8, decisão
 * 2026-10-03). Contrato espelha `PdiDto` (Application): enum como string
 * ("InProgress"…), `targetDate` DateOnly ("yyyy-MM-dd"). Policy Operate;
 * Admin vê todos, Operador vê só o próprio.
 */
export type PdiStatus = "InProgress" | "Completed" | "Cancelled";

export interface Pdi {
  userId: string;
  ownerName: string;
  ownerEmail: string;
  strategy: string;
  targetDate: string;
  status: PdiStatus;
  updateDateTime: string;
}

export const PDI_STATUS_OPTIONS = [
  { value: "InProgress", label: "Em andamento" },
  { value: "Completed", label: "Concluído" },
  { value: "Cancelled", label: "Cancelado" },
] as const;

export const PDI_STATUS_STYLE: Record<
  PdiStatus,
  { background: string; color: string }
> = {
  InProgress: { background: "#dbeafe", color: "#1d4ed8" },
  Completed: { background: "#dcfce7", color: "#15803d" },
  Cancelled: { background: "#f1f5f9", color: "#64748b" },
};

/** Admin: todos os planos · Operador: só o próprio (0..1 itens). */
export async function listPdis(): Promise<Pdi[]> {
  const data = await api.get<Pdi[]>("/pdi");
  return data ?? [];
}

/** Upsert — criar é exclusivo do Admin (403 para Operador). */
export async function upsertPdi(
  userId: string,
  input: { strategy: string; targetDate: string; status: PdiStatus },
): Promise<Pdi> {
  const data = await api.put<Pdi>(`/pdi/${userId}`, input);
  if (!data) throw new Error("Resposta vazia do servidor ao salvar o PDI.");
  return data;
}
