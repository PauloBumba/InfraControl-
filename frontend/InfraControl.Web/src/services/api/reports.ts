import { api } from "./client";

/**
 * Relatório transversal (ADR-049) — visão agregada de cima dos 5 módulos
 * (produtividade/PDI, tasks, knowledge, projetos, incidentes). Read-model
 * composto no backend; aqui é só a casca de consumo. Policy Operate.
 */
export interface TransversalReport {
  from: string | null;
  to: string | null;
  pdi: { total: number; byStatus: Record<string, number>; completed: number };
  tasks: {
    total: number;
    byState: Record<string, number>;
    completed: number;
    overdue: number;
  };
  knowledge: { total: number; published: number; draft: number };
  projects: { total: number; byStatus: Record<string, number> };
  incidents: { total: number; resolved: number; averageResolutionHours: number | null };
}

function buildQuery(from?: string | null, to?: string | null, format?: string): string {
  const params = new URLSearchParams();
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  if (format) params.set("format", format);
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

/** Snapshot agregado; filtros de período opcionais ("yyyy-MM-dd"). */
export async function getTransversalReport(
  from?: string | null,
  to?: string | null,
): Promise<TransversalReport> {
  return (await api.get<TransversalReport>(
    `/reports/transversal${buildQuery(from, to)}`,
  )) as TransversalReport;
}

/** Export autenticado — blob → objectURL → clique no link temporário. */
export async function exportTransversalReport(
  from?: string | null,
  to?: string | null,
  format: "pdf" | "xlsx" = "pdf",
): Promise<void> {
  const blob = await api.download(
    `/reports/transversal/export${buildQuery(from, to, format)}`,
  );
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `relatorio-transversal.${format}`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/**
 * Fase E (docs/34 §37) — `POST /reports/transversal/ai-summary`
 * (Operate): resumo interpretativo LEVEL 0 do mesmo read-model. Sem
 * provider configurado → 503 amigável (§44/§57).
 */
export interface TransversalAiSummary {
  summary: string;
  provider: string;
  model: string;
  usedFallback: boolean;
}

export function summarizeTransversalReport(
  from?: string | null,
  to?: string | null,
): Promise<TransversalAiSummary> {
  return api.post<TransversalAiSummary>(
    `/reports/transversal/ai-summary${buildQuery(from, to)}`,
  ) as Promise<TransversalAiSummary>;
}

/**
 * F25-T001 (docs/33 §2–§4) — indicadores detalhados de Tasks/PDI/Knowledge.
 * Policy Administer (métrica agregada de todos). Consumo na seção
 * "Indicadores" da ReportsPage, visível só a administradores.
 */
export interface ReportIndicators {
  from: string | null;
  to: string | null;
  tasks: {
    total: number;
    byState: Record<string, number>;
    completionRatePercent: number;
    overdue: number;
    blocked: number;
    replannings: number;
    impediments: number;
    impedimentsResolved: number;
    updates: number;
    withResult: number;
    byPriority: Record<string, number>;
    byKind: Record<string, number>;
    byResponsible: {
      responsibleId: string | null;
      responsibleName: string | null;
      count: number;
      percent: number;
    }[];
  };
  pdi: {
    totalPlans: number;
    byStatus: Record<string, number>;
    completedPlans: number;
    completionRatePercent: number;
    objectivesTotal: number;
    competenciesWorked: number;
    totalImpediments: number;
    impedimentsResolved: number;
  };
  knowledge: {
    total: number;
    byStatus: Record<string, number>;
    published: number;
    inReview: number;
    draft: number;
    pendingReview: number;
    obsolete: number;
    archived: number;
    byOrigin: Record<string, number>;
    versioned: number;
    topTags: { tag: string; count: number }[];
  };
}

/** Indicadores detalhados (Administer). */
export async function getReportIndicators(
  from?: string | null,
  to?: string | null,
): Promise<ReportIndicators> {
  return (await api.get<ReportIndicators>(
    `/reports/indicators${buildQuery(from, to)}`,
  )) as ReportIndicators;
}

/**
 * F25-T002 (docs/33 §5) — cruzamentos entre módulos (Chamados→Tasks,
 * Tasks→Knowledge, PDI→Knowledge, Knowledge→Chamados, Task→PDI).
 * Policy Administer.
 */
export interface CrossingStat {
  sources: number;
  targets: number;
  links: number;
}

export interface ReportCrossings {
  from: string | null;
  to: string | null;
  incidentsToTasks: CrossingStat;
  tasksToKnowledge: CrossingStat;
  pdiToKnowledge: CrossingStat;
  knowledgeToIncidents: CrossingStat;
  competenciesDemandedByTasks: {
    competencyId: string;
    name: string;
    tasks: number;
  }[];
}

/** Cruzamentos integrados (Administer). */
export async function getReportCrossings(
  from?: string | null,
  to?: string | null,
): Promise<ReportCrossings> {
  return (await api.get<ReportCrossings>(
    `/reports/crossings${buildQuery(from, to)}`,
  )) as ReportCrossings;
}

/**
 * F25-T003 (docs/33 §6–§8) — dashboards por papel. Colaborador/Gestor em
 * Operate; Administrativo em Administer (filtros período/responsável/tipo).
 */
export interface ColaboradorDashboard {
  userId: string | null;
  myTasks: { pending: number; inProgress: number; overdue: number; completed: number };
  myPdi: {
    hasPlan: boolean;
    progressPercent: number;
    phases: number;
    nextActivities: number;
    checkIns: number;
    difficulties: number;
  };
  myKnowledge: { created: number; inReview: number; published: number };
}

export interface GestorDashboard {
  tasks: { inProgress: number; overdue: number; blocked: number; completed: number; improvements: number };
  pdi: { active: number; attention: number; completed: number; replanned: number };
  knowledge: { published: number; inReview: number; pending: number; outdated: number };
  upcomingDeadlines: { taskId: string; title: string; dueDate: string }[];
  openImpediments: number;
  priorityTasks: { taskId: string; title: string; priority: string }[];
  plansNeedingFollowUp: number;
  knowledgePendingApproval: number;
}

export interface AdministrativoDashboard {
  from: string | null;
  to: string | null;
  tasks: {
    total: number;
    completed: number;
    overdue: number;
    blocked: number;
    completionRatePercent: number;
  };
  byResponsible: { responsibleId: string | null; responsibleName: string | null; count: number }[];
  pdi: { totalPlans: number; completed: number; completionRatePercent: number };
  knowledge: { total: number; published: number; inReview: number; draft: number };
}

export function getColaboradorDashboard(): Promise<ColaboradorDashboard> {
  return api.get<ColaboradorDashboard>("/reports/dashboard/colaborador") as Promise<ColaboradorDashboard>;
}

export function getGestorDashboard(): Promise<GestorDashboard> {
  return api.get<GestorDashboard>("/reports/dashboard/gestor") as Promise<GestorDashboard>;
}

export async function getAdministrativoDashboard(
  from?: string | null,
  to?: string | null,
  responsibleId?: string | null,
  kind?: string | null,
): Promise<AdministrativoDashboard> {
  const params = new URLSearchParams();
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  if (responsibleId) params.set("responsibleId", responsibleId);
  if (kind) params.set("kind", kind);
  const qs = params.toString();
  return (await api.get<AdministrativoDashboard>(
    `/reports/dashboard/administrativo${qs ? `?${qs}` : ""}`,
  )) as AdministrativoDashboard;
}

/**
 * F25-T004 (docs/33 §9–§12) — relatórios estruturados por módulo. Operate.
 */
interface NameCount {
  name: string | null;
  count: number;
}

export interface TasksReport {
  from: string | null;
  to: string | null;
  summary: {
    quantity: number;
    completed: number;
    overdue: number;
    blocked: number;
    completionRatePercent: number;
  };
  execution: {
    principalTasks: { taskId: string; title: string; priority: string; state: string }[];
    responsibles: NameCount[];
    difficulties: number;
    replannings: number;
    withResult: number;
  };
  improvements: { implementations: number; preventives: number; automations: number; total: number };
  impact: { withResult: number };
}

export interface PdiReport {
  from: string | null;
  to: string | null;
  colaborador: string | null;
  objectives: number;
  strategies: number;
  phases: number;
  progressPercent: number;
  checkIns: number;
  difficulties: number;
  evidences: number;
  feedbacks: number;
}

export interface KnowledgeReport {
  from: string | null;
  to: string | null;
  created: number;
  published: number;
  byOrigin: NameCount[];
  usageLinks: number;
  mostUsed: { articleId: string; title: string; uses: number }[];
  outdated: number;
  versions: number;
}

export interface GerencialReport {
  from: string | null;
  to: string | null;
  tasks: TasksReport["summary"];
  improvements: number;
  pdi: { plans: number; completed: number; progressPercent: number };
  knowledge: { created: number; used: number };
  principaisImpedimentos: string[];
  pontosAtencao: string[];
}

function reportQuery(from?: string | null, to?: string | null): string {
  return buildQuery(from, to);
}

export async function getTasksReport(from?: string | null, to?: string | null): Promise<TasksReport> {
  return (await api.get<TasksReport>(`/reports/tasks-report${reportQuery(from, to)}`)) as TasksReport;
}

export async function getPdiReport(from?: string | null, to?: string | null): Promise<PdiReport> {
  return (await api.get<PdiReport>(`/reports/pdi-report${reportQuery(from, to)}`)) as PdiReport;
}

export async function getKnowledgeReport(from?: string | null, to?: string | null): Promise<KnowledgeReport> {
  return (await api.get<KnowledgeReport>(`/reports/knowledge-report${reportQuery(from, to)}`)) as KnowledgeReport;
}

export async function getGerencialReport(from?: string | null, to?: string | null): Promise<GerencialReport> {
  return (await api.get<GerencialReport>(`/reports/gerencial${reportQuery(from, to)}`)) as GerencialReport;
}

/**
 * F25-T005 (docs/33 §13–§14) — série histórica mensal + export CSV/PDF/XLSX.
 * Operate.
 */
export interface ReportHistory {
  from: string | null;
  to: string | null;
  months: {
    month: string;
    tasks: number;
    completedTasks: number;
    knowledge: number;
    pdis: number;
  }[];
}

export function getReportHistory(from?: string | null, to?: string | null): Promise<ReportHistory> {
  return api.get<ReportHistory>(`/reports/history${reportQuery(from, to)}`) as Promise<ReportHistory>;
}

/** Export do histórico — blob → objectURL → clique no link temporário. */
export async function exportReportHistory(
  from?: string | null,
  to?: string | null,
  format: "csv" | "pdf" | "xlsx" = "csv",
): Promise<void> {
  const blob = await api.download(
    `/reports/history/export${buildQuery(from, to, format)}`,
  );
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `historico-indicadores.${format}`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/**
 * F25-T006 (docs/33 §15–§16) — relatório gerencial por cadência (§15) e
 * interpretação por IA do gerencial (§16, LEVEL 0). Operate.
 */
export type ReportCadence = "mensal" | "trimestral" | "semestral" | "anual";

export interface AutomaticReport {
  cadence: string;
  from: string;
  to: string;
  report: GerencialReport;
}

export function getAutomaticReport(cadence: ReportCadence): Promise<AutomaticReport> {
  const params = new URLSearchParams({ cadence });
  return api.get<AutomaticReport>(`/reports/automatic?${params}`) as Promise<AutomaticReport>;
}

export function summarizeGerencialReport(
  from?: string | null,
  to?: string | null,
): Promise<{ summary: string; provider: string; model: string; usedFallback: boolean }> {
  return api.post(
    `/reports/gerencial/ai-interpretation${buildQuery(from, to)}`,
  ) as Promise<{ summary: string; provider: string; model: string; usedFallback: boolean }>;
}
