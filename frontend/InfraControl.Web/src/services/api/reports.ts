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
