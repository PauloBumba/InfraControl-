import { useCallback, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { useCan } from "@/app/router/access";
import {
  exportReportHistory,
  exportTransversalReport,
  getAdministrativoDashboard,
  getAutomaticReport,
  getColaboradorDashboard,
  getGerencialReport,
  getGestorDashboard,
  getKnowledgeReport,
  getPdiReport,
  getReportCrossings,
  getReportHistory,
  getReportIndicators,
  getTasksReport,
  getTransversalReport,
  summarizeGerencialReport,
  summarizeTransversalReport,
  type AdministrativoDashboard,
  type AutomaticReport,
  type ColaboradorDashboard,
  type GestorDashboard,
  type GerencialReport,
  type KnowledgeReport,
  type PdiReport,
  type ReportCadence,
  type ReportCrossings,
  type ReportHistory,
  type ReportIndicators,
  type TasksReport,
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
  const canAdminister = useCan("administer");
  const [indicators, setIndicators] = useState<ReportIndicators | null>(null);
  const [indLoading, setIndLoading] = useState(false);
  const [crossings, setCrossings] = useState<ReportCrossings | null>(null);
  const [crossLoading, setCrossLoading] = useState(false);
  const [colab, setColab] = useState<ColaboradorDashboard | null>(null);
  const [gestor, setGestor] = useState<GestorDashboard | null>(null);
  const [admin, setAdmin] = useState<AdministrativoDashboard | null>(null);
  const [dashLoading, setDashLoading] = useState<string | null>(null);
  const [tasksRep, setTasksRep] = useState<TasksReport | null>(null);
  const [pdiRep, setPdiRep] = useState<PdiReport | null>(null);
  const [knowRep, setKnowRep] = useState<KnowledgeReport | null>(null);
  const [gerRep, setGerRep] = useState<GerencialReport | null>(null);
  const [repLoading, setRepLoading] = useState<string | null>(null);
  const [history, setHistory] = useState<ReportHistory | null>(null);
  const [histLoading, setHistLoading] = useState(false);
  const [cadence, setCadence] = useState<ReportCadence>("mensal");
  const [automatic, setAutomatic] = useState<AutomaticReport | null>(null);
  const [autoLoading, setAutoLoading] = useState(false);
  const [gerAi, setGerAi] = useState<string | null>(null);
  const [gerAiLoading, setGerAiLoading] = useState(false);

  const loadAutomatic = useCallback(async () => {
    setAutoLoading(true);
    setError(null);
    try {
      setAutomatic(await getAutomaticReport(cadence));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível gerar o relatório automático.");
    } finally {
      setAutoLoading(false);
    }
  }, [cadence]);

  const loadGerencialAi = useCallback(async () => {
    setGerAiLoading(true);
    setError(null);
    try {
      const result = await summarizeGerencialReport(from || null, to || null);
      setGerAi(result.summary);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "IA não disponível (sem provider configurado).");
    } finally {
      setGerAiLoading(false);
    }
  }, [from, to]);

  const loadHistory = useCallback(async () => {
    setHistLoading(true);
    setError(null);
    try {
      setHistory(await getReportHistory(from || null, to || null));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o histórico.");
    } finally {
      setHistLoading(false);
    }
  }, [from, to]);

  const loadReport = useCallback(
    async (kind: "tasks" | "pdi" | "knowledge" | "gerencial") => {
      setRepLoading(kind);
      setError(null);
      try {
        const from2 = from || null;
        const to2 = to || null;
        if (kind === "tasks") setTasksRep(await getTasksReport(from2, to2));
        else if (kind === "pdi") setPdiRep(await getPdiReport(from2, to2));
        else if (kind === "knowledge") setKnowRep(await getKnowledgeReport(from2, to2));
        else setGerRep(await getGerencialReport(from2, to2));
      } catch (caught) {
        setError(caught instanceof Error ? caught.message : "Não foi possível gerar o relatório.");
      } finally {
        setRepLoading(null);
      }
    },
    [from, to],
  );

  const loadColaborador = useCallback(async () => {
    setDashLoading("colab");
    setError(null);
    try {
      setColab(await getColaboradorDashboard());
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o dashboard do colaborador.");
    } finally {
      setDashLoading(null);
    }
  }, []);

  const loadGestor = useCallback(async () => {
    setDashLoading("gestor");
    setError(null);
    try {
      setGestor(await getGestorDashboard());
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o dashboard do gestor.");
    } finally {
      setDashLoading(null);
    }
  }, []);

  const loadAdministrativo = useCallback(async () => {
    setDashLoading("admin");
    setError(null);
    try {
      setAdmin(await getAdministrativoDashboard(from || null, to || null));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o dashboard administrativo.");
    } finally {
      setDashLoading(null);
    }
  }, [from, to]);

  const loadCrossings = useCallback(async () => {
    setCrossLoading(true);
    setError(null);
    try {
      setCrossings(await getReportCrossings(from || null, to || null));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar os cruzamentos.");
    } finally {
      setCrossLoading(false);
    }
  }, [from, to]);

  const loadIndicators = useCallback(async () => {
    setIndLoading(true);
    setError(null);
    try {
      setIndicators(await getReportIndicators(from || null, to || null));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar os indicadores.");
    } finally {
      setIndLoading(false);
    }
  }, [from, to]);

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
            {canAdminister && (
              <Button
                variant="ghost"
                onClick={() => void loadIndicators()}
                disabled={indLoading}
                title="Indicadores detalhados de Tasks, PDI e Knowledge (§2–§4) — Admin"
                style={{ padding: "6px 12px" }}
              >
                <i className="pi pi-chart-bar" /> {indLoading ? "Carregando…" : "Indicadores"}
              </Button>
            )}
            {canAdminister && (
              <Button
                variant="ghost"
                onClick={() => void loadCrossings()}
                disabled={crossLoading}
                title="Cruzamentos entre módulos (§5) — Admin"
                style={{ padding: "6px 12px" }}
              >
                <i className="pi pi-sitemap" /> {crossLoading ? "Carregando…" : "Cruzamentos"}
              </Button>
            )}
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

      {indicators && (
        <div style={{ marginTop: 18 }}>
          <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>
            Indicadores detalhados
            <span style={{ fontWeight: 400, color: "#94a3b8", marginLeft: 8, fontSize: 12 }}>
              Tasks · PDI · Knowledge (§2–§4)
            </span>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 12 }}>
            <Block title="Tasks (§2)">
              <Row label="Total" value={indicators.tasks.total} />
              <Row label="Taxa de conclusão" value={`${indicators.tasks.completionRatePercent}%`} />
              <Row label="Atrasadas" value={indicators.tasks.overdue} />
              <Row label="Bloqueadas" value={indicators.tasks.blocked} />
              <Row label="Replanejamentos" value={indicators.tasks.replannings} />
              <Row label="Impedimentos (resolvidos)" value={`${indicators.tasks.impediments} (${indicators.tasks.impedimentsResolved})`} />
              <Row label="Atualizações" value={indicators.tasks.updates} />
              <Row label="Com resultado" value={indicators.tasks.withResult} />
              <Row label="Por prioridade" value={counts(indicators.tasks.byPriority)} />
              <Row label="Por tipo" value={counts(indicators.tasks.byKind)} />
              <Row label="Por estado" value={counts(indicators.tasks.byState)} />
            </Block>
            <Block title="PDI (§3)">
              <Row label="Planos" value={indicators.pdi.totalPlans} />
              <Row label="Concluídos" value={indicators.pdi.completedPlans} />
              <Row label="Taxa de conclusão" value={`${indicators.pdi.completionRatePercent}%`} />
              <Row label="Objetivos" value={indicators.pdi.objectivesTotal} />
              <Row label="Competências" value={indicators.pdi.competenciesWorked} />
              <Row label="Impedimentos (resolvidos)" value={`${indicators.pdi.totalImpediments} (${indicators.pdi.impedimentsResolved})`} />
              <Row label="Por status" value={counts(indicators.pdi.byStatus)} />
            </Block>
            <Block title="Knowledge (§4)">
              <Row label="Artigos" value={indicators.knowledge.total} />
              <Row label="Publicados" value={indicators.knowledge.published} />
              <Row label="Em revisão" value={indicators.knowledge.inReview} />
              <Row label="Rascunhos" value={indicators.knowledge.draft} />
              <Row label="Revisão pendente" value={indicators.knowledge.pendingReview} />
              <Row label="Obsoletos" value={indicators.knowledge.obsolete} />
              <Row label="Arquivados" value={indicators.knowledge.archived} />
              <Row label="Versionados" value={indicators.knowledge.versioned} />
              <Row label="Por origem" value={counts(indicators.knowledge.byOrigin)} />
              <Row
                label="Top tags"
                value={
                  indicators.knowledge.topTags.length === 0
                    ? "—"
                    : indicators.knowledge.topTags.slice(0, 5).map((t) => `${t.tag}(${t.count})`).join(", ")
                }
              />
            </Block>
          </div>
          {indicators.tasks.byResponsible.length > 0 && (
            <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 14, marginTop: 12 }}>
              <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Tasks por responsável</div>
              <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                {indicators.tasks.byResponsible.map((r, index) => (
                  <Row
                    key={r.responsibleId ?? `sem-${index}`}
                    label={r.responsibleName ?? "Sem responsável"}
                    value={`${r.count} (${r.percent}%)`}
                  />
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {crossings && (
        <div style={{ marginTop: 18 }}>
          <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>
            Cruzamentos entre módulos
            <span style={{ fontWeight: 400, color: "#94a3b8", marginLeft: 8, fontSize: 12 }}>§5</span>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12 }}>
            <Block title="Chamados → Tasks">
              <Row label="Chamados que geraram tasks" value={crossings.incidentsToTasks.sources} />
              <Row label="Tasks alcançadas" value={crossings.incidentsToTasks.targets} />
              <Row label="Vínculos" value={crossings.incidentsToTasks.links} />
            </Block>
            <Block title="Tasks → Knowledge">
              <Row label="Tasks concluídas que geraram doc." value={crossings.tasksToKnowledge.sources} />
              <Row label="Artigos alcançados" value={crossings.tasksToKnowledge.targets} />
              <Row label="Vínculos" value={crossings.tasksToKnowledge.links} />
            </Block>
            <Block title="PDI → Knowledge">
              <Row label="Planos que geraram artigo" value={crossings.pdiToKnowledge.sources} />
              <Row label="Artigos alcançados" value={crossings.pdiToKnowledge.targets} />
              <Row label="Vínculos" value={crossings.pdiToKnowledge.links} />
            </Block>
            <Block title="Knowledge → Chamados">
              <Row label="Artigos usados" value={crossings.knowledgeToIncidents.sources} />
              <Row label="Chamados resolvidos" value={crossings.knowledgeToIncidents.targets} />
              <Row label="Vínculos" value={crossings.knowledgeToIncidents.links} />
            </Block>
          </div>
          {crossings.competenciesDemandedByTasks.length > 0 && (
            <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 14, marginTop: 12 }}>
              <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>
                Competências exigidas pelas Tasks (Task → PDI)
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                {crossings.competenciesDemandedByTasks.map((c) => (
                  <Row key={c.competencyId} label={c.name || "—"} value={`${c.tasks} task(s)`} />
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      <div style={{ marginTop: 18 }}>
        <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>
          Dashboards por papel
          <span style={{ fontWeight: 400, color: "#94a3b8", marginLeft: 8, fontSize: 12 }}>§6–§8</span>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 12 }}>
          <Button variant="ghost" onClick={() => void loadColaborador()} disabled={dashLoading === "colab"} style={{ padding: "6px 12px" }}>
            <i className="pi pi-user" /> {dashLoading === "colab" ? "Carregando…" : "Colaborador"}
          </Button>
          <Button variant="ghost" onClick={() => void loadGestor()} disabled={dashLoading === "gestor"} style={{ padding: "6px 12px" }}>
            <i className="pi pi-users" /> {dashLoading === "gestor" ? "Carregando…" : "Gestor"}
          </Button>
          {canAdminister && (
            <Button variant="ghost" onClick={() => void loadAdministrativo()} disabled={dashLoading === "admin"} style={{ padding: "6px 12px" }}>
              <i className="pi pi-building" /> {dashLoading === "admin" ? "Carregando…" : "Administrativo"}
            </Button>
          )}
        </div>

        {colab && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12, marginBottom: 12 }}>
            <Block title="§6 Minhas Tasks">
              <Row label="Pendentes" value={colab.myTasks.pending} />
              <Row label="Em andamento" value={colab.myTasks.inProgress} />
              <Row label="Atrasadas" value={colab.myTasks.overdue} />
              <Row label="Concluídas" value={colab.myTasks.completed} />
            </Block>
            <Block title="§6 Meu PDI">
              <Row label="Possui plano" value={colab.myPdi.hasPlan ? "Sim" : "Não"} />
              <Row label="Progresso" value={`${colab.myPdi.progressPercent}%`} />
              <Row label="Fases" value={colab.myPdi.phases} />
              <Row label="Próximas atividades" value={colab.myPdi.nextActivities} />
              <Row label="Check-ins" value={colab.myPdi.checkIns} />
              <Row label="Dificuldades" value={colab.myPdi.difficulties} />
            </Block>
            <Block title="§6 Meu conhecimento">
              <Row label="Criadas" value={colab.myKnowledge.created} />
              <Row label="Em revisão" value={colab.myKnowledge.inReview} />
              <Row label="Publicadas" value={colab.myKnowledge.published} />
            </Block>
          </div>
        )}

        {gestor && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12, marginBottom: 12 }}>
            <Block title="§7 Tasks">
              <Row label="Em andamento" value={gestor.tasks.inProgress} />
              <Row label="Atrasadas" value={gestor.tasks.overdue} />
              <Row label="Bloqueadas" value={gestor.tasks.blocked} />
              <Row label="Concluídas" value={gestor.tasks.completed} />
              <Row label="Melhorias" value={gestor.tasks.improvements} />
            </Block>
            <Block title="§7 PDI">
              <Row label="Ativos" value={gestor.pdi.active} />
              <Row label="Atenção" value={gestor.pdi.attention} />
              <Row label="Concluídos" value={gestor.pdi.completed} />
              <Row label="Replanejados" value={gestor.pdi.replanned} />
            </Block>
            <Block title="§7 Knowledge">
              <Row label="Publicadas" value={gestor.knowledge.published} />
              <Row label="Em revisão" value={gestor.knowledge.inReview} />
              <Row label="Pendentes" value={gestor.knowledge.pending} />
              <Row label="Desatualizadas" value={gestor.knowledge.outdated} />
            </Block>
            <Block title="§7 Atenção">
              <Row label="Impedimentos abertos" value={gestor.openImpediments} />
              <Row label="Planos p/ acompanhamento" value={gestor.plansNeedingFollowUp} />
              <Row label="Knowledge p/ aprovação" value={gestor.knowledgePendingApproval} />
              <Row label="Próximos prazos" value={gestor.upcomingDeadlines.length} />
              <Row label="Prioritárias" value={gestor.priorityTasks.length} />
            </Block>
          </div>
        )}

        {admin && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12, marginBottom: 12 }}>
            <Block title="§8 Tasks">
              <Row label="Total" value={admin.tasks.total} />
              <Row label="Concluídas" value={admin.tasks.completed} />
              <Row label="Atrasadas" value={admin.tasks.overdue} />
              <Row label="Bloqueadas" value={admin.tasks.blocked} />
              <Row label="Taxa de conclusão" value={`${admin.tasks.completionRatePercent}%`} />
            </Block>
            <Block title="§8 PDI">
              <Row label="Planos" value={admin.pdi.totalPlans} />
              <Row label="Concluídos" value={admin.pdi.completed} />
              <Row label="Taxa" value={`${admin.pdi.completionRatePercent}%`} />
            </Block>
            <Block title="§8 Knowledge">
              <Row label="Total" value={admin.knowledge.total} />
              <Row label="Publicados" value={admin.knowledge.published} />
              <Row label="Em revisão" value={admin.knowledge.inReview} />
              <Row label="Rascunhos" value={admin.knowledge.draft} />
            </Block>
            {admin.byResponsible.length > 0 && (
              <Block title="§8 Por responsável">
                {admin.byResponsible.map((r, index) => (
                  <Row key={r.responsibleId ?? `sem-${index}`} label={r.responsibleName ?? "Sem responsável"} value={r.count} />
                ))}
              </Block>
            )}
          </div>
        )}
      </div>

      <div style={{ marginTop: 18 }}>
        <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>
          Relatórios por módulo
          <span style={{ fontWeight: 400, color: "#94a3b8", marginLeft: 8, fontSize: 12 }}>§9–§12</span>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 12 }}>
          <Button variant="ghost" onClick={() => void loadReport("tasks")} disabled={repLoading === "tasks"} style={{ padding: "6px 12px" }}>
            <i className="pi pi-list" /> {repLoading === "tasks" ? "Gerando…" : "Tasks"}
          </Button>
          <Button variant="ghost" onClick={() => void loadReport("pdi")} disabled={repLoading === "pdi"} style={{ padding: "6px 12px" }}>
            <i className="pi pi-briefcase" /> {repLoading === "pdi" ? "Gerando…" : "PDI"}
          </Button>
          <Button variant="ghost" onClick={() => void loadReport("knowledge")} disabled={repLoading === "knowledge"} style={{ padding: "6px 12px" }}>
            <i className="pi pi-book" /> {repLoading === "knowledge" ? "Gerando…" : "Knowledge"}
          </Button>
          <Button variant="ghost" onClick={() => void loadReport("gerencial")} disabled={repLoading === "gerencial"} style={{ padding: "6px 12px" }}>
            <i className="pi pi-file" /> {repLoading === "gerencial" ? "Gerando…" : "Gerencial"}
          </Button>
        </div>

        {tasksRep && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12, marginBottom: 12 }}>
            <Block title="§9 Tasks — Resumo">
              <Row label="Quantidade" value={tasksRep.summary.quantity} />
              <Row label="Concluídas" value={tasksRep.summary.completed} />
              <Row label="Atrasadas" value={tasksRep.summary.overdue} />
              <Row label="Bloqueadas" value={tasksRep.summary.blocked} />
              <Row label="Taxa de conclusão" value={`${tasksRep.summary.completionRatePercent}%`} />
            </Block>
            <Block title="§9 Tasks — Execução">
              <Row label="Dificuldades" value={tasksRep.execution.difficulties} />
              <Row label="Replanejamentos" value={tasksRep.execution.replannings} />
              <Row label="Com resultado" value={tasksRep.execution.withResult} />
              <Row label="Responsáveis" value={tasksRep.execution.responsibles.length} />
            </Block>
            <Block title="§9 Tasks — Melhorias">
              <Row label="Implementações" value={tasksRep.improvements.implementations} />
              <Row label="Preventivas" value={tasksRep.improvements.preventives} />
              <Row label="Automações" value={tasksRep.improvements.automations} />
              <Row label="Total" value={tasksRep.improvements.total} />
            </Block>
          </div>
        )}

        {pdiRep && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12, marginBottom: 12 }}>
            <Block title="§10 PDI">
              <Row label="Colaborador" value={pdiRep.colaborador ?? "—"} />
              <Row label="Objetivos" value={pdiRep.objectives} />
              <Row label="Estratégias" value={pdiRep.strategies} />
              <Row label="Fases" value={pdiRep.phases} />
              <Row label="Evolução" value={`${pdiRep.progressPercent}%`} />
              <Row label="Check-ins" value={pdiRep.checkIns} />
              <Row label="Dificuldades" value={pdiRep.difficulties} />
              <Row label="Evidências" value={pdiRep.evidences} />
              <Row label="Feedbacks" value={pdiRep.feedbacks} />
            </Block>
          </div>
        )}

        {knowRep && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12, marginBottom: 12 }}>
            <Block title="§11 Knowledge">
              <Row label="Criados" value={knowRep.created} />
              <Row label="Publicados" value={knowRep.published} />
              <Row label="Vínculos de uso" value={knowRep.usageLinks} />
              <Row label="Desatualizados" value={knowRep.outdated} />
              <Row label="Versões" value={knowRep.versions} />
              <Row label="Mais usados" value={knowRep.mostUsed.length} />
              <Row
                label="Origem"
                value={
                  knowRep.byOrigin.length === 0
                    ? "—"
                    : knowRep.byOrigin.map((o) => `${o.name}(${o.count})`).join(", ")
                }
              />
            </Block>
          </div>
        )}

        {gerRep && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12, marginBottom: 12 }}>
            <Block title="§12 Gerencial — Tasks">
              <Row label="Realizadas" value={gerRep.tasks.completed} />
              <Row label="Pendentes/atrasadas" value={gerRep.tasks.overdue} />
              <Row label="Melhorias" value={gerRep.improvements} />
            </Block>
            <Block title="§12 Gerencial — PDI/Knowledge">
              <Row label="Planos" value={gerRep.pdi.plans} />
              <Row label="Concluídos" value={gerRep.pdi.completed} />
              <Row label="Evolução" value={`${gerRep.pdi.progressPercent}%`} />
              <Row label="Conhecimentos criados" value={gerRep.knowledge.created} />
              <Row label="Conhecimentos usados" value={gerRep.knowledge.used} />
            </Block>
            <Block title="§12 Dificuldades / Atenção">
              {gerRep.principaisImpedimentos.length === 0 && <Row label="Impedimentos" value="—" />}
              {gerRep.principaisImpedimentos.map((imp, i) => (
                <Row key={`imp-${i}`} label="Impedimento" value={imp} />
              ))}
              {gerRep.pontosAtencao.map((p, i) => (
                <Row key={`aten-${i}`} label="Atenção" value={p} />
              ))}
            </Block>
          </div>
        )}
      </div>

      <div style={{ marginTop: 18 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: 8,
            marginBottom: 10,
          }}
        >
          <div style={{ fontSize: 14, fontWeight: 600 }}>
            Histórico de indicadores
            <span style={{ fontWeight: 400, color: "#94a3b8", marginLeft: 8, fontSize: 12 }}>§13–§14</span>
          </div>
          <div style={{ display: "flex", gap: 6 }}>
            <Button variant="ghost" onClick={() => void loadHistory()} disabled={histLoading} style={{ padding: "6px 12px" }}>
              <i className="pi pi-refresh" /> {histLoading ? "Carregando…" : "Carregar série"}
            </Button>
            {history && (
              <>
                <Button variant="ghost" onClick={() => void exportReportHistory(from || null, to || null, "csv")} style={{ padding: "6px 10px" }}>
                  CSV
                </Button>
                <Button variant="ghost" onClick={() => void exportReportHistory(from || null, to || null, "pdf")} style={{ padding: "6px 10px" }}>
                  PDF
                </Button>
                <Button variant="ghost" onClick={() => void exportReportHistory(from || null, to || null, "xlsx")} style={{ padding: "6px 10px" }}>
                  XLSX
                </Button>
              </>
            )}
          </div>
        </div>

        {history && (
          <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, overflow: "hidden" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
              <thead>
                <tr style={{ background: "#f8fafc" }}>
                  {["Mês", "Tasks", "Concluídas", "Knowledge", "PDIs"].map((h) => (
                    <th key={h} style={{ textAlign: "left", padding: "8px 10px", borderBottom: "1px solid #e5e7eb", color: "#64748b", fontWeight: 600 }}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {history.months.length === 0 && (
                  <tr>
                    <td colSpan={5} style={{ padding: "10px", color: "#94a3b8" }}>
                      Sem dados no período.
                    </td>
                  </tr>
                )}
                {history.months.map((m) => (
                  <tr key={m.month}>
                    <td style={{ padding: "8px 10px", borderBottom: "1px solid #f1f5f9", fontWeight: 600 }}>{m.month}</td>
                    <td style={{ padding: "8px 10px", borderBottom: "1px solid #f1f5f9" }}>{m.tasks}</td>
                    <td style={{ padding: "8px 10px", borderBottom: "1px solid #f1f5f9" }}>{m.completedTasks}</td>
                    <td style={{ padding: "8px 10px", borderBottom: "1px solid #f1f5f9" }}>{m.knowledge}</td>
                    <td style={{ padding: "8px 10px", borderBottom: "1px solid #f1f5f9" }}>{m.pdis}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div style={{ marginTop: 18 }}>
        <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>
          Relatórios automáticos e IA
          <span style={{ fontWeight: 400, color: "#94a3b8", marginLeft: 8, fontSize: 12 }}>§15–§16</span>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center", marginBottom: 12 }}>
          <label style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 12, color: "#475569" }}>
            Cadência
            <select
              value={cadence}
              onChange={(event) => setCadence(event.target.value as ReportCadence)}
              style={{ padding: "6px 8px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            >
              <option value="mensal">Mensal</option>
              <option value="trimestral">Trimestral</option>
              <option value="semestral">Semestral</option>
              <option value="anual">Anual</option>
            </select>
          </label>
          <Button variant="ghost" onClick={() => void loadAutomatic()} disabled={autoLoading} style={{ padding: "6px 12px", alignSelf: "flex-end" }}>
            <i className="pi pi-calendar" /> {autoLoading ? "Gerando…" : "Gerar automático"}
          </Button>
          <Button variant="ghost" onClick={() => void loadGerencialAi()} disabled={gerAiLoading} style={{ padding: "6px 12px", alignSelf: "flex-end" }} title="Interpreta o relatório gerencial com IA (LEVEL 0) — usa só dados reais">
            <i className="pi pi-sparkles" /> {gerAiLoading ? "Interpretando…" : "Interpretar com IA"}
          </Button>
        </div>

        {automatic && (
          <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 14, marginBottom: 12 }}>
            <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>
              Automático · {automatic.cadence} · {automatic.from} → {automatic.to}
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: 12 }}>
              <Block title="Tasks">
                <Row label="Realizadas" value={automatic.report.tasks.completed} />
                <Row label="Atrasadas" value={automatic.report.tasks.overdue} />
                <Row label="Melhorias" value={automatic.report.improvements} />
              </Block>
              <Block title="PDI / Knowledge">
                <Row label="Planos" value={automatic.report.pdi.plans} />
                <Row label="Conhecimentos criados" value={automatic.report.knowledge.created} />
                <Row label="Usados" value={automatic.report.knowledge.used} />
              </Block>
            </div>
          </div>
        )}

        {gerAi && (
          <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
            <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Interpretação com IA (gerencial)</div>
            <div style={{ fontSize: 13, lineHeight: 1.6, whiteSpace: "pre-wrap" }}>{gerAi}</div>
          </div>
        )}
      </div>

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
