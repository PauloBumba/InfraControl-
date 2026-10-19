import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Column } from "primereact/column";
import type { DataTablePageEvent } from "primereact/datatable";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog, Dialog } from "@/components/ui/Dialog";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { MetricChart } from "@/components/ui/MetricChart";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { useAppSelector } from "@/app/store/hooks";
import {
  createProject,
  deleteProject,
  listProjects,
  updateProject,
  type ProjectItem,
} from "@/services/api/projects";

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

/** Campos do formulário criar/editar (datas em yyyy-mm-dd p/ input[type=date]). */
interface ProjectForm {
  code: string;
  title: string;
  description: string;
  priority: string;
  justification: string;
  plannedStartDate: string;
  plannedEndDate: string;
}

const EMPTY_FORM: ProjectForm = {
  code: "",
  title: "",
  description: "",
  priority: "Medium",
  justification: "",
  plannedStartDate: "",
  plannedEndDate: "",
};

const PRIORITY_OPTIONS = ["Low", "Medium", "High", "Critical"] as const;

function formOf(project: ProjectItem): ProjectForm {
  return {
    code: project.code,
    title: project.title,
    description: project.description,
    priority: project.priority,
    justification: project.justification ?? "",
    plannedStartDate: project.plannedStartDate ?? "",
    plannedEndDate: project.plannedEndDate ?? "",
  };
}

export function ProjectsPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
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

  const currentUserId = useAppSelector((state) => state.auth.user?.id) ?? "";

  const [dialogMode, setDialogMode] = useState<"create" | "edit" | null>(null);
  const [editingProject, setEditingProject] = useState<ProjectItem | null>(null);
  const [form, setForm] = useState<ProjectForm>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ProjectItem | null>(null);

  function setField<K extends keyof ProjectForm>(key: K, value: ProjectForm[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function openCreate() {
    setEditingProject(null);
    setForm(EMPTY_FORM);
    setActionError(null);
    setDialogMode("create");
  }

  function openEdit(project: ProjectItem) {
    setEditingProject(project);
    setForm(formOf(project));
    setActionError(null);
    setDialogMode("edit");
  }

  async function save() {
    if (dialogMode === "create" && (!form.code.trim() || !form.title.trim() || !form.description.trim())) {
      setActionError("Código, título e descrição são obrigatórios.");
      return;
    }
    if (dialogMode === "edit" && (!form.title.trim() || !form.description.trim())) {
      setActionError("Título e descrição são obrigatórios.");
      return;
    }

    setSaving(true);
    setActionError(null);
    try {
      if (dialogMode === "create") {
        if (!currentUserId) throw new Error("Não foi possível identificar o usuário atual.");
        await createProject({
          code: form.code.trim(),
          title: form.title.trim(),
          description: form.description.trim(),
          leaderId: currentUserId,
          priority: form.priority,
          justification: form.justification.trim() || null,
          plannedStartDate: form.plannedStartDate || null,
          plannedEndDate: form.plannedEndDate || null,
        });
        setNotice("Projeto criado.");
      } else if (dialogMode === "edit" && editingProject) {
        await updateProject(editingProject.id, {
          title: form.title.trim(),
          description: form.description.trim(),
          priority: form.priority,
          justification: form.justification.trim() || null,
          plannedStartDate: form.plannedStartDate || null,
          plannedEndDate: form.plannedEndDate || null,
        });
        setNotice("Projeto atualizado.");
      }
      setDialogMode(null);
      setEditingProject(null);
      await loadProjects();
    } catch (caught) {
      setActionError(caught instanceof Error ? caught.message : "Não foi possível salvar o projeto.");
    } finally {
      setSaving(false);
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    setSaving(true);
    setActionError(null);
    try {
      await deleteProject(deleteTarget.id);
      setNotice(`Projeto ${deleteTarget.code} removido.`);
      await loadProjects();
    } catch (caught) {
      setActionError(caught instanceof Error ? caught.message : "Não foi possível remover o projeto.");
    } finally {
      setSaving(false);
    }
  }

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

  // Deep-link do dashboard (`/projects?novo=1`): abre o diálogo de criação
  // uma única vez e limpa o parâmetro para não reabrir em navegações futuras.
  useEffect(() => {
    if (searchParams.get("novo") !== "1") return;
    openCreate();
    const next = new URLSearchParams(searchParams);
    next.delete("novo");
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

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
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          <Button
            variant="ghost"
            icon="pi pi-arrow-right"
            aria-label={`Ver projeto ${project.title}`}
            onClick={() => navigate(`/projects/${project.id}`)}
          >
            Detalhes
          </Button>
          <Button
            variant="ghost"
            icon="pi pi-pencil"
            aria-label={`Editar projeto ${project.title}`}
            onClick={() => openEdit(project)}
          >
            Editar
          </Button>
          <Button
            variant="danger"
            icon="pi pi-trash"
            aria-label={`Remover projeto ${project.title}`}
            onClick={() => setDeleteTarget(project)}
          >
            Remover
          </Button>
        </div>
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
            <Button variant="primary" icon="pi pi-plus" onClick={openCreate}>
              Novo projeto
            </Button>
            <Button variant="ghost" icon="pi pi-refresh" onClick={() => void loadProjects()}>
              Atualizar
            </Button>
          </div>
        }
      />

      {notice && (
        <div style={{ padding: 12, borderRadius: 10, background: "#f0fdf4", color: "#15803d" }}>{notice}</div>
      )}

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

      {actionError && (
        <div style={{ padding: 12, borderRadius: 10, background: "#fef2f2", color: "#991b1b" }}>{actionError}</div>
      )}

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
              <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                <Button
                  variant="ghost"
                  icon="pi pi-arrow-right"
                  aria-label={`Ver projeto ${project.title}`}
                  onClick={() => navigate(`/projects/${project.id}`)}
                >
                  Detalhes
                </Button>
                <Button
                  variant="ghost"
                  icon="pi pi-pencil"
                  aria-label={`Editar projeto ${project.title}`}
                  onClick={() => openEdit(project)}
                >
                  Editar
                </Button>
                <Button
                  variant="danger"
                  icon="pi pi-trash"
                  aria-label={`Remover projeto ${project.title}`}
                  onClick={() => setDeleteTarget(project)}
                >
                  Remover
                </Button>
              </div>
            )}
          />
        </DataTablePanel>
          }
        />
      )}

      <Dialog
        visible={dialogMode !== null}
        onHide={() => {
          setDialogMode(null);
          setEditingProject(null);
          setActionError(null);
        }}
        header={dialogMode === "create" ? "Novo projeto" : "Editar projeto"}
        width={520}
        footer={
          <div className="uni-modal-actions">
            <Button
              variant="ghost"
              onClick={() => {
                setDialogMode(null);
                setEditingProject(null);
                setActionError(null);
              }}
            >
              Cancelar
            </Button>
            <Button variant="primary" onClick={() => void save()} disabled={saving}>
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gap: 12 }}>
          {dialogMode === "create" && (
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Código *</span>
              <input
                value={form.code}
                onChange={(event) => setField("code", event.target.value)}
                placeholder="PRJ-001"
                style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              />
            </label>
          )}
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Título *</span>
            <input
              value={form.title}
              onChange={(event) => setField("title", event.target.value)}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
            />
          </label>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Descrição *</span>
            <textarea
              value={form.description}
              onChange={(event) => setField("description", event.target.value)}
              rows={3}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1", resize: "vertical" }}
            />
          </label>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Prioridade</span>
            <select
              value={form.priority}
              onChange={(event) => setField("priority", event.target.value)}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
            >
              {PRIORITY_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {PROJECT_PRIORITY_LABEL[option] ?? option}
                </option>
              ))}
            </select>
          </label>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Justificativa</span>
            <textarea
              value={form.justification}
              onChange={(event) => setField("justification", event.target.value)}
              rows={2}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1", resize: "vertical" }}
            />
          </label>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Início planejado</span>
              <input
                type="date"
                value={form.plannedStartDate}
                onChange={(event) => setField("plannedStartDate", event.target.value)}
                style={{ padding: "8px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              />
            </label>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Fim planejado</span>
              <input
                type="date"
                value={form.plannedEndDate}
                onChange={(event) => setField("plannedEndDate", event.target.value)}
                style={{ padding: "8px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              />
            </label>
          </div>
          {dialogMode === "create" && (
            <div style={{ fontSize: 11, color: "#94a3b8" }}>
              Líder: você (usuário autenticado).
            </div>
          )}
          {actionError && (
            <div style={{ padding: 10, borderRadius: 8, background: "#fef2f2", color: "#991b1b", fontSize: 12 }}>
              {actionError}
            </div>
          )}
        </div>
      </Dialog>

      <ConfirmDialog
        visible={deleteTarget !== null}
        onHide={() => setDeleteTarget(null)}
        onConfirm={() => void confirmDelete()}
        header="Remover projeto"
        message={`Remover o projeto ${deleteTarget?.code ?? ""} — ${deleteTarget?.title ?? ""}? Esta ação não pode ser desfeita.`}
        confirmLabel="Remover"
        severity="danger"
      />
    </div>
  );
}
