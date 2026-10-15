import { useCallback, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import {
  exportTransversalReport,
  getTransversalReport,
  summarizeTransversalReport,
  type TransversalAiSummary,
  type TransversalReport,
} from "@/services/api/reports";

/**
 * Relatório transversal (ADR-049 · R-d): visão agregada de cima dos 5
 * módulos (produtividade/PDI, tasks, knowledge, projetos, incidentes) com
 * filtro de período e export PDF/XLSX. Backend: GET /reports/transversal
 * (Operate). Não substitui os dashboards por vertical — é o teto de cima.
 * **Fase E (docs/34 §37)**: resumo interpretativo com IA (LEVEL 0) via
 * POST /reports/transversal/ai-summary — interpreta os números, não age.
 */
export function ReportsPage() {
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [report, setReport] = useState<TransversalReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [aiSummary, setAiSummary] = useState<TransversalAiSummary | null>(null);
  const [aiLoading, setAiLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setReport(await getTransversalReport(from || null, to || null));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o relatório.");
    } finally {
      setLoading(false);
    }
  }, [from, to]);

  async function handleExport(format: "pdf" | "xlsx") {
    setError(null);
    try {
      await exportTransversalReport(from || null, to || null, format);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível exportar o relatório.");
    }
  }

  async function handleAiSummary() {
    setError(null);
    setAiLoading(true);
    try {
      setAiSummary(await summarizeTransversalReport(from || null, to || null));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível gerar o resumo com IA.");
    } finally {
      setAiLoading(false);
    }
  }

  const counts = (map: Record<string, number>) =>
    Object.keys(map).length === 0
      ? "—"
      : Object.entries(map)
          .map(([key, value]) => `${key}=${value}`)
          .join(", ");

  return (
    <div className="uni-page">
      <PageHeader
        title="Relatórios"
        subtitle="Visão transversal de produtividade, tarefas, conhecimento, projetos e incidentes (ADR-049)."
        actions={
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <Button
              variant="ghost"
              onClick={() => void handleAiSummary()}
              disabled={aiLoading}
              title="Resumo interpretativo com IA (LEVEL 0) — lê os mesmos indicadores, não executa ações"
              style={{ padding: "6px 12px" }}
            >
              <i className="pi pi-sparkles" /> {aiLoading ? "Resumindo…" : "Resumir com IA"}
            </Button>
            <Button variant="ghost" onClick={() => void handleExport("pdf")} style={{ padding: "6px 12px" }}>
              <i className="pi pi-file" /> PDF
            </Button>
            <Button variant="ghost" onClick={() => void handleExport("xlsx")} style={{ padding: "6px 12px" }}>
              <i className="pi pi-file-excel" /> XLSX
            </Button>
          </div>
        }
      />

      <div
        style={{
          display: "flex",
          gap: 10,
          alignItems: "flex-end",
          flexWrap: "wrap",
          padding: 14,
          background: "white",
          border: "1px solid #e5e7eb",
          borderRadius: 8,
          marginBottom: 14,
        }}
      >
        <label style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 12, color: "#475569" }}>
          De
          <input
            type="date"
            value={from}
            onChange={(event) => setFrom(event.target.value)}
            style={{ padding: "6px 8px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
          />
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 12, color: "#475569" }}>
          Até
          <input
            type="date"
            value={to}
            onChange={(event) => setTo(event.target.value)}
            style={{ padding: "6px 8px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
          />
        </label>
        <Button variant="primary" onClick={() => void load()} disabled={loading} style={{ padding: "6px 14px" }}>
          {loading ? "Gerando…" : "Gerar relatório"}
        </Button>
      </div>

      {error && <ErrorBanner message={error} />}
      {loading && !report && <LoadingBlock colSpan={1} />}

      {report && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12 }}>
          <Block title="Produtividade (PDI)">
            <Row label="Planos" value={report.pdi.total} />
            <Row label="Concluídos" value={report.pdi.completed} />
            <Row label="Por status" value={counts(report.pdi.byStatus)} />
          </Block>
          <Block title="Tarefas">
            <Row label="Total" value={report.tasks.total} />
            <Row label="Concluídas" value={report.tasks.completed} />
            <Row label="Atrasadas" value={report.tasks.overdue} />
            <Row label="Por estado" value={counts(report.tasks.byState)} />
          </Block>
          <Block title="Conhecimento">
            <Row label="Artigos" value={report.knowledge.total} />
            <Row label="Publicados" value={report.knowledge.published} />
            <Row label="Rascunhos" value={report.knowledge.draft} />
          </Block>
          <Block title="Projetos">
            <Row label="Total" value={report.projects.total} />
            <Row label="Por status" value={counts(report.projects.byStatus)} />
          </Block>
          <Block title="Incidentes">
            <Row label="Total" value={report.incidents.total} />
            <Row label="Resolvidos" value={report.incidents.resolved} />
            <Row
              label="Tempo médio de resolução"
              value={
                report.incidents.averageResolutionHours != null
                  ? `${report.incidents.averageResolutionHours.toFixed(1)} h`
                  : "—"
              }
            />
          </Block>
        </div>
      )}

      {aiSummary && (
        <div
          style={{
            background: "white",
            border: "1px solid #e5e7eb",
            borderRadius: 8,
            padding: 14,
            marginTop: 14,
          }}
        >
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              gap: 8,
              marginBottom: 8,
            }}
          >
            <div style={{ fontSize: 13, fontWeight: 600 }}>Resumo com IA</div>
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              LEVEL 0 · {aiSummary.provider} · {aiSummary.model}
              {aiSummary.usedFallback ? " (fallback)" : ""}
            </span>
          </div>
          <div style={{ fontSize: 13, lineHeight: 1.6, whiteSpace: "pre-wrap" }}>
            {aiSummary.summary}
          </div>
        </div>
      )}

      {!report && !loading && !error && (
        <p style={{ color: "#94a3b8", fontSize: 13 }}>
          Escolha um período (ou deixe em branco para tudo) e clique em “Gerar relatório”.
        </p>
      )}
    </div>
  );
}

function Block({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
      <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>{title}</div>
      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>{children}</div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string | number }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", gap: 8, fontSize: 13 }}>
      <span style={{ color: "#64748b" }}>{label}</span>
      <span style={{ fontWeight: 600 }}>{value}</span>
    </div>
  );
}
