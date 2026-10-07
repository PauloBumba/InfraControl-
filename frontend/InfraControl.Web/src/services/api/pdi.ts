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

export type PdiHistoryType =
  | "Created"
  | "Updated"
  | "StatusChanged"
  | "EvidenceAdded"
  | "EvidenceReviewed";

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
  EvidenceAdded: "Evidência anexada",
  EvidenceReviewed: "Evidência revisada",
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

// ---------------------------------------------------------------------------
// Acompanhamento da fase (docs/30 §9-§11, §13, Fase 3 — ADR-033):
// progresso/comentários, check-ins bilaterais, impedimentos e feedbacks.
// Contrato espelha `PdiAccompanimentDtos` (Application); datas "yyyy-MM-dd";
// escrita: progresso/check-in/impedimento = dono ou Admin (403 pt-BR),
// editar impedimento também o gestor, feedback = qualquer um que vê o
// plano e exclusão de feedback = só Admin.
// ---------------------------------------------------------------------------

export type PdiImpedimentStatus = "Open" | "Resolved";

export interface PdiProgress {
  id: string;
  phaseId: string;
  authorId: string;
  authorName: string | null;
  percent: number | null;
  comment: string;
  createdAt: string;
}

export interface PdiCheckIn {
  id: string;
  phaseId: string;
  checkInDate: string;
  createdBy: string;
  createdByName: string | null;
  whatDone: string;
  whatLearned: string | null;
  difficulty: string | null;
  needSupport: string | null;
  managerFeedback: string | null;
  nextAction: string | null;
  respondedAt: string | null;
  respondedByName: string | null;
}

export interface PdiImpediment {
  id: string;
  phaseId: string;
  type: string;
  description: string;
  impact: string | null;
  occurredAt: string;
  responsibleId: string | null;
  responsibleName: string | null;
  actionTaken: string | null;
  status: PdiImpedimentStatus;
  resolvedAt: string | null;
}

export interface PdiFeedback {
  id: string;
  phaseId: string;
  authorId: string;
  authorName: string | null;
  recipientId: string;
  recipientName: string | null;
  type: string;
  content: string;
  feedbackDate: string;
}

export interface PdiAccompaniment {
  userId: string;
  progresses: PdiProgress[];
  checkIns: PdiCheckIn[];
  impediments: PdiImpediment[];
  feedbacks: PdiFeedback[];
  evidences: PdiEvidence[];
}

export const PDI_IMPEDIMENT_STATUS_LABEL: Record<
  PdiImpedimentStatus,
  string
> = {
  Open: "Aberto",
  Resolved: "Resolvido",
};

export const PDI_IMPEDIMENT_STATUS_STYLE: Record<
  PdiImpedimentStatus,
  { background: string; color: string }
> = {
  Open: { background: "#fee2e2", color: "#b91c1c" },
  Resolved: { background: "#dcfce7", color: "#15803d" },
};

export function parsePdiImpedimentStatus(
  value: string,
): PdiImpedimentStatus {
  return value.toLowerCase() === "resolved" ? "Resolved" : "Open";
}

/** Lista única do acompanhamento — Admin: qualquer; dono/gestor: o próprio. */
export async function getPdiAccompaniment(
  userId: string,
): Promise<PdiAccompaniment> {
  const data = await api.get<PdiAccompaniment>(`/pdi/${userId}/accompaniment`);
  return data ?? { userId, progresses: [], checkIns: [], impediments: [], feedbacks: [], evidences: [] };
}

// ---- Progresso (§9 — relato + comentário, append-only) ------------------

export async function createPdiProgress(input: {
  phaseId: string;
  percent?: number | null;
  comment: string;
}): Promise<void> {
  await api.post("/pdi/progresses", input);
}

export async function deletePdiProgress(id: string): Promise<void> {
  await api.delete(`/pdi/progresses/${id}`);
}

// ---- Check-ins (§10 — bilateral: relato do dono, resposta do gestor) ----

export async function createPdiCheckIn(input: {
  phaseId: string;
  checkInDate: string;
  whatDone: string;
  whatLearned?: string | null;
  difficulty?: string | null;
  needSupport?: string | null;
}): Promise<void> {
  await api.post("/pdi/checkins", input);
}

/** PUT por lado: só os campos enviados são gravados (403 pt-BR do backend). */
export async function updatePdiCheckIn(
  id: string,
  input: {
    whatDone?: string | null;
    whatLearned?: string | null;
    difficulty?: string | null;
    needSupport?: string | null;
    managerFeedback?: string | null;
    nextAction?: string | null;
  },
): Promise<void> {
  await api.put(`/pdi/checkins/${id}`, input);
}

export async function deletePdiCheckIn(id: string): Promise<void> {
  await api.delete(`/pdi/checkins/${id}`);
}

// ---- Impedimentos (§11 — entidade própria com status) ------------------

export async function createPdiImpediment(input: {
  phaseId: string;
  type: string;
  description: string;
  occurredAt: string;
  impact?: string | null;
  responsibleId?: string | null;
  actionTaken?: string | null;
}): Promise<void> {
  await api.post("/pdi/impediments", input);
}

export async function updatePdiImpediment(
  id: string,
  input: {
    type: string;
    description: string;
    occurredAt: string;
    impact?: string | null;
    responsibleId?: string | null;
    actionTaken?: string | null;
    status: PdiImpedimentStatus;
  },
): Promise<void> {
  await api.put(`/pdi/impediments/${id}`, input);
}

export async function deletePdiImpediment(id: string): Promise<void> {
  await api.delete(`/pdi/impediments/${id}`);
}

// ---- Feedback (§13 — histórico, nunca sobrescreve; excluir = Admin) -----

export async function createPdiFeedback(input: {
  phaseId: string;
  recipientId: string;
  type: string;
  content: string;
  feedbackDate: string;
}): Promise<void> {
  await api.post("/pdi/feedbacks", input);
}

export async function deletePdiFeedback(id: string): Promise<void> {
  await api.delete(`/pdi/feedbacks/${id}`);
}

// ---- Evidências (§12 — upload, tipos fixos, validação/rejeição) ----------

export type PdiEvidenceType =
  | "Certificate"
  | "Document"
  | "Image"
  | "File"
  | "CompletedActivity"
  | "Assessment"
  | "Feedback"
  | "Result"
  | "Other";

export type PdiEvidenceStatus = "Pending" | "Validated" | "Rejected";

export type PdiEvidenceReviewDecision = "Validated" | "Rejected";

export interface PdiEvidence {
  id: string;
  phaseId: string;
  type: PdiEvidenceType;
  status: PdiEvidenceStatus;
  description: string | null;
  fileName: string;
  contentType: string;
  fileSize: number;
  createdBy: string;
  createdByName: string | null;
  createdAt: string;
  reviewedBy: string | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  rejectionReason: string | null;
}

export const PDI_EVIDENCE_TYPE_LABEL: Record<PdiEvidenceType, string> = {
  Certificate: "Certificado",
  Document: "Documento",
  Image: "Imagem",
  File: "Arquivo",
  CompletedActivity: "Atividade realizada",
  Assessment: "Avaliação",
  Feedback: "Feedback",
  Result: "Resultado",
  Other: "Outro",
};

export const PDI_EVIDENCE_STATUS_LABEL: Record<PdiEvidenceStatus, string> = {
  Pending: "Pendente",
  Validated: "Validada",
  Rejected: "Rejeitada",
};

export const PDI_EVIDENCE_STATUS_STYLE: Record<
  PdiEvidenceStatus,
  { background: string; color: string }
> = {
  Pending: { background: "#fef3c7", color: "#b45309" },
  Validated: { background: "#dcfce7", color: "#15803d" },
  Rejected: { background: "#fee2e2", color: "#b91c1c" },
};

export function parsePdiEvidenceStatus(value: string): PdiEvidenceStatus {
  return value.toLowerCase() === "validated"
    ? "Validated"
    : value.toLowerCase() === "rejected"
      ? "Rejected"
      : "Pending";
}

export function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Upload multipart (arquivo + phaseId + type + description) — XSRF via header. */
export async function uploadPdiEvidence(input: {
  phaseId: string;
  type: PdiEvidenceType;
  description?: string | null;
  file: File;
}): Promise<void> {
  const form = new FormData();
  form.append("file", input.file);
  form.append("phaseId", input.phaseId);
  form.append("type", input.type);
  if (input.description) form.append("description", input.description);
  await api.post("/pdi/evidences", form);
}

/** Revisão única do gestor: validada ou rejeitada com motivo. */
export async function reviewPdiEvidence(
  id: string,
  input: {
    decision: PdiEvidenceReviewDecision;
    rejectionReason?: string | null;
  },
): Promise<void> {
  await api.put(`/pdi/evidences/${id}/review`, {
    decision: input.decision,
    rejectionReason: input.rejectionReason ?? null,
  });
}

/** Download autenticado — blob → objectURL → clique no link temporário. */
export async function downloadPdiEvidence(
  id: string,
  fileName: string,
): Promise<void> {
  const blob = await api.download(`/pdi/evidences/${id}/download`);
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
