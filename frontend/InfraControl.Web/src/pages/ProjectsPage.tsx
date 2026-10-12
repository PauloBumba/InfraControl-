import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Column } from "primereact/column";
import type { DataTablePageEvent } from "primereact/datatable";
import { Button } from "@/components/ui/Button";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { MetricChart } from "@/components/ui/MetricChart";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { listProjects, type ProjectItem } from "@/services/api/projects";

const PAGE_SIZE = 20;

const PROJECT_STATUS_LABEL: Record<string, string> = {
  Draft: "Rascunho",
  Planned: "Planejado",
  InProgress: "Em andamento",
  PendingFinalApproval: "Aguardando homologação",
  Completed: "Concluído",
  Cancelled: "Cancelado",
  OnHold: "Em espera",
};

const PROJECT_STATUS_COLOR: Record<string, { background: string; color: string }> = {
  Draft: { background: "#f1f5f9", color: "#475569" },
  Planned: { background: "#e0e7ff", color: "#4338ca" },
  InProgress: { background: "#dbeafe", color: "#1d4ed8" },
  PendingFinalApproval: { background: "#fef3c7", color: "#b45309" },
  Completed: { background: "#dcfce7", color: "#15803d" },
  Cancelled: { background: "#fee2e2", color: "#b91c1c" },
  OnHold: { background: "#f3e8ff", color: "#7c3aed" },
};

const PROJECT_PRIORITY_LABEL: Record<string, string> = {
  Low: "Baixa",
  Medium: "Média",
  High: "Alta",
  Critical: "Crítica",
};

const PROJECT_PRIORITY_COLOR: Record<string, { background: string; color: string }> = {
  Low: { background: "#ecfdf5", color: "#047857" },
  Medium: { background: "#fff7ed", color: "#c2410c" },
  High: { background: "#fef2f2", color: "#b91c1c" },
  Critical: { background: "#fee2e2", color: "#7f1d1d" },
};

/** Colunas do quadro — os mesmos 7 status do filtro e do gráfico. */
const PROJECT_STATUS_COLUMNS = Object.entries(PROJECT_STATUS_LABEL).map(([key, label]) => ({
  key,
  label,
}));

function formatDate(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  if (year && month && day) return `${day}/${month}/${year}`;
  return value;
}

export function ProjectsPage() {
  const navigate = useNavigate();
  const [term, setTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("All");
  const [priorityFilter, setPriorityFilter] = useState("All");
  const [page, setPage] = useState(1);
  const [projects, setProjects] = useState<ProjectItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Adoção do padrão de visões (backlog Operations UX 2026-10-11): era só
  // tabela; o quadro agrupa por status (os mesmos 7 do filtro/gráfico).
  // Tabela = padrão.
  const [view, setView] = useEntityViewMode("projects", "table");

  const loadProjects = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const result = await listProjects({
        term: term.trim() || undefined,
        status: statusFilter === "All" ? undefined : statusFilter,
        priority: priorityFilter === "All" ? undefined : priorityFilter,
        page,
        pageSize: PAGE_SIZE,
      });
      setProjects(result.items);
      setTotalCount(result.totalCount);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar os projetos.");
    } finally {
      setLoading(false);
    }
  }, [page, priorityFilter, statusFilter, term]);

  useEffect(() => {
    void loadProjects();
  }, [loadProjects]);

  const statusOptions = useMemo(
    () => ["All", "Draft", "Planned", "InProgress", "PendingFinalApproval", "Completed", "Cancelled", "OnHold"],
    [],
  );

  const priorityOptions = useMemo(() => ["All", "Low", "Medium", "High", "Critical"], []);

  const summary = useMemo(() => {
    const byStatus = Object.fromEntries(Object.keys(PROJECT_STATUS_LABEL).map((status) => [status, 0]));
    for (const project of projects) {
      byStatus[project.status] = (byStatus[project.status] ?? 0) + 1;
    }
    const inProgress = byStatus.InProgress ?? 0;
    const pendingApproval = byStatus.PendingFinalApproval ?? 0;
    const completed = byStatus.Completed ?? 0;

    return { byStatus, inProgress, pendingApproval, completed };
  }, [projects]);

  const chartStatuses = Object.keys(PROJECT_STATUS_LABEL);
  const chartLabels = chartStatuses.map((status) => PROJECT_STATUS_LABEL[status] ?? status);
  const chartValues = chartStatuses.map((status) => summary.byStatus[status] ?? 0);

  function handlePageChange(event: DataTablePageEvent) {
    setPage(Math.floor(event.first / PAGE_SIZE) + 1);
  }

  function changeFilter(setFilter: (value: string) => void, value: string) {
    setPage(1);
    setFilter(value);
  }

  /** Card de projeto — usado nas visões grid E quadro (mesmos dados da tabela). */
  function renderProjectCard(project: ProjectItem) {
    const statusColor = PROJECT_STATUS_COLOR[project.status] ?? { background: "#f1f5f9", color: "#475569" };
    const priorityColor = PROJECT_PRIORITY_COLOR[project.priority] ?? { background: "#f8fafc", color: "#334155" };
    const memberCount = project.members?.length ?? 0;
    return (
      <div
        style={{
          background: "#fff",
          border: "1px solid #e2e8f0",
          borderRadius: 12,
          padding: 14,
          display: "flex",
          flexDirection: "column",
          gap: 6,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
          <span style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 700 }}>{project.code}</span>
          <StatusChip
            label={PROJECT_STATUS_LABEL[project.status] ?? project.status}
            background={statusColor.background}
            color={statusColor.color}
            fontSize={10}
            fontWeight={600}
          />
        </div>
        <div style={{ fontSize: 13, fontWeight: 600 }}>{project.title}</div>
        <div style={{ fontSize: 12, color: "#64748b" }}>
          {project.description || "Sem descrição registrada."}
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <StatusChip
            label={PROJECT_PRIORITY_LABEL[project.priority] ?? project.priority}
            background={priorityColor.background}
            color={priorityColor.color}
            fontSize={10}
            fontWeight={600}
          />
          <span style={{ fontSize: 11, color: "#64748b" }}>
            {formatDate(project.plannedStartDate)} → {formatDate(project.plannedEndDate)}
          </span>
        </div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>
          {memberCount} membro{memberCount === 1 ? "" : "s"}
        </div>
        <Button
          variant="ghost"
          icon="pi pi-arrow-right"
          aria-label={`Ver projeto ${project.title}`}
          onClick={() => navigate(`/projects/${project.id}`)}
        >
          Detalhes
        </Button>
      </div>
    );
  }

  return (
    <div style={{ display: "grid", gap: 16 }}>
      <PageHeader
        title="Projetos"
        subtitle="Visão geral de iniciativas, fases, priorização e aprovação final."
        actions={
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
            <ViewSwitcher mode={view} onChange={setView} />
            <Button variant="primary" icon="pi pi-refresh" onClick={() => void loadProjects()}>
              Atualizar
            </Button>
          </div>
        }
      />

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 12 }}>
        <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 16 }}>
          <div style={{ color: "#64748b", fontSize: 12, textTransform: "uppercase" }}>Total encontrado</div>
          <div style={{ fontSize: 28, fontWeight: 700 }}>{totalCount}</div>
        </div>
        <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 16 }}>
          <div style={{ color: "#64748b", fontSize: 12, textTransform: "uppercase" }}>Em andamento</div>
          <div style={{ fontSize: 28, fontWeight: 700 }}>{summary.inProgress}</div>
          <div style={{ color: "#64748b", fontSize: 11 }}>Nesta página</div>
        </div>
        <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 16 }}>
          <div style={{ color: "#64748b", fontSize: 12, textTransform: "uppercase" }}>Aguardando aprovação</div>
          <div style={{ fontSize: 28, fontWeight: 700 }}>{summary.pendingApproval}</div>
          <div style={{ color: "#64748b", fontSize: 11 }}>Nesta página</div>
        </div>
        <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 16 }}>
          <div style={{ color: "#64748b", fontSize: 12, textTransform: "uppercase" }}>Concluídos</div>
          <div style={{ fontSize: 28, fontWeight: 700 }}>{summary.completed}</div>
          <div style={{ color: "#64748b", fontSize: 11 }}>Nesta página</div>
        </div>
      </div>

      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 16 }}>
        <div style={{ fontWeight: 700, marginBottom: 8 }}>Projetos por status — página atual</div>
        <MetricChart
          type="bar"
          labels={chartLabels}
          values={chartValues}
          label="Projetos"
          ariaLabel="Quantidade de projetos por status na página atual"
          height={240}
        />
      </div>

      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: 12,
          padding: 16,
          background: "#fff",
          border: "1px solid #e2e8f0",
          borderRadius: 12,
        }}
      >
        <div style={{ flex: 1, minWidth: 220 }}>
          <label style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>Busca</label>
          <input
            value={term}
            onChange={(event) => changeFilter(setTerm, event.target.value)}
            placeholder="Código, título ou justificativa"
            style={{ width: "100%", padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
          />
        </div>

        <div style={{ minWidth: 180 }}>
          <label style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>Status</label>
          <select
            value={statusFilter}
            onChange={(event) => changeFilter(setStatusFilter, event.target.value)}
            style={{ width: "100%", padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
          >
            {statusOptions.map((option) => (
              <option key={option} value={option}>
                {option === "All" ? "Todos" : PROJECT_STATUS_LABEL[option] ?? option}
              </option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: 180 }}>
          <label style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>Prioridade</label>
          <select
            value={priorityFilter}
            onChange={(event) => changeFilter(setPriorityFilter, event.target.value)}
            style={{ width: "100%", padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
          >
            {priorityOptions.map((option) => (
              <option key={option} value={option}>
                {option === "All" ? "Todas" : PROJECT_PRIORITY_LABEL[option] ?? option}
              </option>
            ))}
          </select>
        </div>
      </div>

      {error && <ErrorBanner message={error} onRetry={() => void loadProjects()} />}

      {loading ? (
        <div style={{ padding: 24, textAlign: "center", color: "#64748b" }}>Carregando projetos…</div>
      ) : projects.length === 0 ? (
        <div style={{ padding: 24, background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12 }}>
          Nenhum projeto encontrado para os filtros atuais.
        </div>
      ) : (
        <EntityView<ProjectItem>
          mode={view}
          items={projects}
          getKey={(project) => project.id}
          emptyMessage={error ? "—" : "Nenhum projeto encontrado"}
          kanban={{
            groupOf: (project) => project.status,
            columns: PROJECT_STATUS_COLUMNS,
          }}
          renderCard={renderProjectCard}
          table={
        <DataTablePanel
          value={projects}
          dataKey="id"
          loading={loading}
          paginator
          lazy
          rows={PAGE_SIZE}
          first={(page - 1) * PAGE_SIZE}
          totalRecords={totalCount}
          onPage={handlePageChange}
          alwaysShowPaginator={totalCount > PAGE_SIZE}
          paginatorTemplate="CurrentPageReport FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink"
          currentPageReportTemplate="Página {currentPage} de {totalPages} ({totalRecords} projetos)"
          emptyMessage={error ? "—" : "Nenhum projeto encontrado"}
          responsiveLayout="scroll"
          stripedRows
        >
          <Column field="code" header="Código" />
          <Column
            field="title"
            header="Projeto"
            body={(project: ProjectItem) => (
              <div style={{ minWidth: 180 }}>
                <div style={{ fontWeight: 700 }}>{project.title}</div>
                <div style={{ color: "#64748b", fontSize: 12, marginTop: 4 }}>
                  {project.description || "Sem descrição registrada."}
                </div>
              </div>
            )}
          />
          <Column
            field="status"
            header="Status"
            body={(project: ProjectItem) => {
              const color = PROJECT_STATUS_COLOR[project.status] ?? { background: "#f1f5f9", color: "#475569" };
              return (
                <StatusChip
                  label={PROJECT_STATUS_LABEL[project.status] ?? project.status}
                  background={color.background}
                  color={color.color}
                />
              );
            }}
          />
          <Column
            field="priority"
            header="Prioridade"
            body={(project: ProjectItem) => {
              const color = PROJECT_PRIORITY_COLOR[project.priority] ?? { background: "#f8fafc", color: "#334155" };
              return (
                <StatusChip
                  label={PROJECT_PRIORITY_LABEL[project.priority] ?? project.priority}
                  background={color.background}
                  color={color.color}
                />
              );
            }}
          />
          <Column
            header="Prazo planejado"
            body={(project: ProjectItem) => `${formatDate(project.plannedStartDate)} → ${formatDate(project.plannedEndDate)}`}
          />
          <Column
            header="Equipe"
            body={(project: ProjectItem) => {
              const memberCount = project.members?.length ?? 0;
              return `${memberCount} membro${memberCount === 1 ? "" : "s"}`;
            }}
          />
          <Column
            header="Ações"
            body={(project: ProjectItem) => (
              <Button
                variant="ghost"
                icon="pi pi-arrow-right"
                aria-label={`Ver projeto ${project.title}`}
                onClick={() => navigate(`/projects/${project.id}`)}
              >
                Detalhes
              </Button>
            )}
          />
        </DataTablePanel>
          }
        />
      )}
    </div>
  );
}
