import { useRef, useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import type { DataTablePageEvent } from "primereact/datatable";
import { Can } from "@/components/auth/Can";
import { formatDateTime } from "@/utils/format";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { StatusChip } from "@/components/ui/StatusChip";
import { FormField, FormError } from "@/components/ui/FormField";
import { SelectField } from "@/components/ui/SelectField";
import { Button } from "@/components/ui/Button";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { FilterBar } from "@/components/ui/FilterBar";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import {
  useCreateMutation,
  usePagedQuery,
  useUpdateMutation,
  errorMessage,
  mutationErrorMessage,
} from "@/hooks/queries";
import { useFormErrors } from "@/hooks/useFormErrors";
import {
  acknowledgeIncident,
  changeIncidentStatus,
  createIncident,
  generateIncidentKnowledge,
  investigateIncident,
  listIncidents,
  updateIncident,
  type IncidentItem,
  type InvestigationResult,
} from "@/services/api/incidents";

/**
 * F17-T003 + ADR-030 Fase 5 — IncidentsPage com React Query
 * (`usePagedQuery` + mutations com invalidação por prefixo `["incidents"]`)
 * e visões tabela/grid/kanban (preferência persistida). Kanban agrupa por
 * status (5 colunas do ciclo Open→Resolved); a transição usa
 * `PUT /incidents/{id}/status` (mapa de estados no backend — 409/400 pt-BR
 * vira banner) e `POST /{id}/acknowledge` sai de Open. **CRUD-Incidentes
 * (2026-10-12)**: edição dos campos descritivos (título/descrição/severidade)
 * via `PUT /incidents/{id}`; a exclusão não existe (ADR-030: ciclo por
 * estado). Escrita atrás de `Can operate` (exibição;
 * policy Operate + antiforgery continuam sendo a autoridade).
 */

const PAGE_SIZE = 20;

const severityConfig: Record<string, { label: string; bg: string; color: string }> = {
  Critical: { label: "Crítico", bg: "#fee2e2", color: "#dc2626" },
  High: { label: "Alta", bg: "#ffedd5", color: "#ea580c" },
  Medium: { label: "Média", bg: "#fef3c7", color: "#d97706" },
  Low: { label: "Baixa", bg: "#dbeafe", color: "#2563eb" },
};

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  Open: { label: "Aberto", bg: "#f1f5f9", color: "#475569" },
  Investigating: { label: "Investigando", bg: "#fee2e2", color: "#dc2626" },
  Identified: { label: "Identificado", bg: "#fef3c7", color: "#d97706" },
  Monitoring: { label: "Monitorando", bg: "#dbeafe", color: "#2563eb" },
  Resolved: { label: "Resolvido", bg: "#dcfce7", color: "#16a34a" },
};

const severityOptions = ["Low", "Medium", "High", "Critical"];
const statusFilterOptions = ["", "Open", "Investigating", "Identified", "Monitoring", "Resolved"];

/** Colunas do kanban = ciclo de estados do incidente. */
const statusColumns = statusFilterOptions
  .filter((value) => value !== "")
  .map((value) => ({ key: value, label: statusConfig[value]?.label ?? value }));

const shortId = (id: string): string => id.slice(0, 8);

export function IncidentsPage() {
  const [view, setView] = useEntityViewMode("incidents", "table");
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("");

  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState("");
  const [severity, setSeverity] = useState("Medium");
  const [description, setDescription] = useState("");
  // F29-T001B — o erro do formulário agora declara o campo que falhou; antes
  // ele era pendurado fixo em "Título" mesmo quando o problema era outro.
  const {
    error: formError,
    field,
    reset: resetFormError,
    fail: failForm,
    errorFor,
  } = useFormErrors();
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  // CRUD-Incidentes (2026-10-12) — edição dos campos descritivos (título,
  // descrição, severidade). Status continua no select de ciclo por estado.
  const [editing, setEditing] = useState<IncidentItem | null>(null);
  const [editTitle, setEditTitle] = useState("");
  const [editSeverity, setEditSeverity] = useState("Medium");
  const [editDescription, setEditDescription] = useState("");

  // AI Investigation (ADR-053 · Fase C) — painel de análise LEVEL 0
  // aberto por incidente; resultado não é persistido no incidente.
  const [investigating, setInvestigating] = useState<IncidentItem | null>(null);
  const [investigation, setInvestigation] = useState<InvestigationResult | null>(null);
  const [investigationError, setInvestigationError] = useState<string | null>(null);

  const incidents = usePagedQuery<IncidentItem>(["incidents", page, statusFilter], () =>
    listIncidents({
      status: statusFilter || undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
  );

  const createMutation = useCreateMutation({
    invalidate: ["incidents"],
    mutationFn: (input: { title: string; severity: string; description?: string }) =>
      createIncident(input),
    onSuccess: () => setNotice("Incidente criado."),
  });
  const statusMutation = useUpdateMutation({
    invalidate: ["incidents"],
    mutationFn: (input: { id: string; status: string }) =>
      changeIncidentStatus(input.id, input.status),
    onSuccess: (_data, vars) => {
      setActionError(null);
      setNotice(`Incidente movido para ${statusConfig[vars.status]?.label ?? vars.status}.`);
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });
  const acknowledgeMutation = useUpdateMutation({
    invalidate: ["incidents"],
    mutationFn: (id: string) => acknowledgeIncident(id),
    onSuccess: () => {
      setActionError(null);
      setNotice("Incidente reconhecido — agora em Investigando.");
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });
  const updateMutation = useUpdateMutation({
    invalidate: ["incidents"],
    mutationFn: (input: { id: string; title: string; description?: string | null; severity: string }) =>
      updateIncident(input.id, {
        title: input.title,
        description: input.description,
        severity: input.severity,
      }),
    onSuccess: () => {
      setActionError(null);
      setNotice("Incidente atualizado.");
      setEditing(null);
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });
  // Fase C (ADR-053) — investigação não altera estado do servidor, só
  // devolve análise; sem invalidação de lista.
  // ADR-061 §3 (H7): mutation única compartilhada — o guard de id (REF
  // síncrono; o estado do closure atrasa um render e deixava B ser
  // ignorado/ A sobrescrever) impede que a resposta LENTA do incidente A
  // sobrescreva o painel aberto do B (cabeçalho = investigating, corpo =
  // investigation).
  const investigatingIdRef = useRef<string | null>(null);
  const investigationMutation = useUpdateMutation({
    invalidate: [],
    mutationFn: (id: string) => investigateIncident(id),
    onSuccess: (data, id) => {
      if (investigatingIdRef.current !== id) return;
      setInvestigation(data);
      setInvestigationError(null);
    },
    onError: (error, id) => {
      if (investigatingIdRef.current !== id) return;
      setInvestigation(null);
      setInvestigationError(mutationErrorMessage(error));
    },
  });

  function openInvestigation(incident: IncidentItem) {
    investigatingIdRef.current = incident.id;
    setInvestigating(incident);
    setInvestigation(null);
    setInvestigationError(null);
    investigationMutation.mutate(incident.id);
  }

  // Fase D (ADR-054) — rascunho de knowledge com IA nasce Draft; o
  // aviso aponta a revisão/publicação na página Knowledge (ADR-034).
  const draftMutation = useUpdateMutation({
    invalidate: [],
    mutationFn: (id: string) => generateIncidentKnowledge(id),
    onSuccess: (data) => {
      setActionError(null);
      setNotice(
        `Rascunho de knowledge criado: "${data.title}" — revise e publique na página Knowledge.`,
      );
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

  function openEdit(incident: IncidentItem) {
    setEditing(incident);
    setEditTitle(incident.title);
    setEditSeverity(incident.severity);
    setEditDescription(incident.description ?? "");
    resetFormError();
  }

  async function submitEdit(event: FormEvent) {
    event.preventDefault();
    if (!editing) return;
    const trimmed = editTitle.trim();
    if (!trimmed) {
      failForm("Informe o título do incidente.", "title");
      return;
    }
    resetFormError();
    try {
      await updateMutation.mutateAsync({
        id: editing.id,
        title: trimmed,
        description: editDescription.trim() || null,
        severity: editSeverity,
      });
    } catch (caught) {
      failForm(errorMessage(caught));
    }
  }

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) {
      failForm("Informe o título do incidente.", "title");
      return;
    }
    resetFormError();
    try {
      await createMutation.mutateAsync({
        title: trimmed,
        severity,
        description: description.trim() || undefined,
      });
      setCreating(false);
      setTitle("");
      setDescription("");
      setSeverity("Medium");
    } catch (caught) {
      // Sem campo: o servidor recusou o conjunto inteiro, não um campo.
      failForm(errorMessage(caught));
    }
  }

  function handlePageChange(event: DataTablePageEvent) {
    setPage(Math.floor(event.first / PAGE_SIZE) + 1);
  }

  const incidentsList = incidents.items;
  const totalCount = incidents.totalCount;
  const listError = mutationErrorMessage(incidents.error as Error | null);

  function statusSelect(incident: IncidentItem, width: string | number = "100%") {
    return (
      <SelectField
        value={incident.status}
        aria-label={`Mover "${incident.title}" para outro status`}
        options={statusColumns.map((column) => ({
          value: column.key,
          label: column.label,
        }))}
        style={{ width }}
        onChange={(target) => {
          if (target === incident.status) return;
          statusMutation.mutate({ id: incident.id, status: target });
        }}
      />
    );
  }

  return (
    <ListPageShell
      title="Incidentes"
      singular="incidente"
      plural="incidentes"
      total={totalCount}
      loading={incidents.isPending}
      notice={notice ? <NoticeBanner message={notice} onDismiss={() => setNotice(null)} /> : undefined}
      error={actionError || listError ? actionError ?? listError ?? "Não foi possível falar com o servidor. Tente novamente." : null}
      onRetry={() => {
        setActionError(null);
        void incidents.refetch();
      }}
      actions={
        <div className="flex align-items-center gap-2 flex-wrap">
          <FilterBar><div style={{ display: "flex", gap: 8 }}>
            <SelectField
              value={statusFilter}
              onChange={(value) => { setStatusFilter(value); setPage(1); }}
              options={statusFilterOptions.map((value) => ({
                value,
                label: value === "" ? "Todos os status" : statusConfig[value]?.label ?? value,
              }))}
              style={{ minWidth: 160 }}
            />
            <ViewSwitcher mode={view} onChange={setView} />
            <Can level="operate">
              <Button
                icon="pi pi-plus"
                onClick={() => { setCreating((value) => !value); resetFormError(); }}
              >
                Novo Incidente
              </Button>
            </Can>
          </div></FilterBar>
        </div>
      }
    >

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo incidente</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 180px", gap: 10 }}>
            <FormField label="Título" error={errorFor("title")}>
              <input
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                placeholder="Título *"
                autoFocus
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
              />
            </FormField>
            <FormField label="Severidade">
              <SelectField
                value={severity}
                onChange={setSeverity}
                options={severityOptions.map((value) => ({
                  value,
                  label: severityConfig[value]?.label ?? value,
                }))}
                style={{ width: "100%" }}
              />
            </FormField>
          </div>
          <FormField label="Descrição (opcional)">
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Descrição (opcional)"
              rows={3}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
            />
          </FormField>
          {/* Erro de campo já aparece embaixo do campo; aqui só o do formulário
              inteiro (ex.: recusa do servidor), senão a mesma frase sairia duas vezes. */}
          <FormError message={field ? "" : (formError ?? "")} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setCreating(false); resetFormError(); }}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={createMutation.isPending}
              style={{ padding: "6px 14px" }}
            >
              {createMutation.isPending ? "Salvando…" : "Criar incidente"}
            </Button>
          </div>
        </form>
      )}

      {editing && (
        <form onSubmit={submitEdit} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Editar incidente</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 180px", gap: 10 }}>
            <FormField label="Título" error={errorFor("title")}>
              <input
                value={editTitle}
                onChange={(event) => setEditTitle(event.target.value)}
                placeholder="Título *"
                autoFocus
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
              />
            </FormField>
            <FormField label="Severidade">
              <SelectField
                value={editSeverity}
                onChange={setEditSeverity}
                options={severityOptions.map((value) => ({
                  value,
                  label: severityConfig[value]?.label ?? value,
                }))}
                style={{ width: "100%" }}
              />
            </FormField>
          </div>
          <FormField label="Descrição (opcional)">
            <textarea
              value={editDescription}
              onChange={(event) => setEditDescription(event.target.value)}
              placeholder="Descrição (opcional)"
              rows={3}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
            />
          </FormField>
          <div style={{ fontSize: 11, color: "#94a3b8" }}>
            O status é alterado pelo ciclo por estado (select na listagem); a exclusão não existe por decisão de arquitetura (ADR-030).
          </div>
          <FormError message={field ? "" : (formError ?? "")} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setEditing(null); resetFormError(); }}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={updateMutation.isPending}
              style={{ padding: "6px 14px" }}
            >
              {updateMutation.isPending ? "Salvando…" : "Salvar alterações"}
            </Button>
          </div>
        </form>
      )}

      {investigating && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8 }}>
            <div style={{ fontSize: 13, fontWeight: 600 }}>
              Investigação com IA — {investigating.title}
            </div>
            <span style={{ fontSize: 11, color: "#94a3b8", whiteSpace: "nowrap" }}>
              LEVEL 0 · somente análise
            </span>
          </div>

          {investigationMutation.isPending && (
            <div style={{ fontSize: 13, color: "#64748b" }}>
              <i className="pi pi-spin pi-spinner" style={{ marginRight: 8 }} />
              Investigando (dados do incidente + knowledge)…
            </div>
          )}

          {!investigationMutation.isPending && investigation && (
            <>
              <div style={{ fontSize: 13, lineHeight: 1.6, whiteSpace: "pre-wrap" }}>
                {investigation.analysis}
              </div>
              {investigation.evidences.length > 0 && (
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <div style={{ fontSize: 12, fontWeight: 600 }}>Evidências</div>
                  {investigation.evidences.map((evidence, index) => (
                    <div
                      key={`${evidence.source}-${index}`}
                      style={{ fontSize: 12, color: "#475569", background: "#f8fafc", border: "1px solid #eef2f7", borderRadius: 6, padding: "6px 8px" }}
                    >
                      <span style={{ fontFamily: "monospace", fontWeight: 600 }}>{evidence.citation}</span>
                      {" — "}
                      {evidence.sourceType} · {evidence.source} · {evidence.confidence}
                      {" · "}
                      {formatDateTime(new Date(evidence.timestampUtc))}
                    </div>
                  ))}
                </div>
              )}
              <div style={{ fontSize: 11, color: "#94a3b8" }}>
                {investigation.provider} · {investigation.model}
                {investigation.usedFallback ? " (fallback)" : ""}
              </div>
            </>
          )}

          {!investigationMutation.isPending && !investigation && investigationError && (
            <FormError message={investigationError} />
          )}

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => setInvestigating(null)}
              style={{ padding: "6px 14px" }}
            >
              Fechar
            </Button>
          </div>
        </div>
      )}

      <EntityView<IncidentItem>
        mode={view}
        items={incidentsList}
        getKey={(incident) => incident.id}
        emptyMessage={listError ? "—" : "Nenhum incidente encontrado"}
        kanban={{ groupOf: (incident) => incident.status, columns: statusColumns }}
        table={
          <DataTablePanel value={incidentsList} dataKey="id" loading={incidents.isPending} paginator lazy rows={PAGE_SIZE} first={(page - 1) * PAGE_SIZE} totalRecords={totalCount} onPage={handlePageChange} alwaysShowPaginator={totalCount > PAGE_SIZE} paginatorTemplate="CurrentPageReport FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink" currentPageReportTemplate="Página {currentPage} de {totalPages} ({totalRecords} incidentes)" emptyMessage={listError ? "—" : "Nenhum incidente encontrado"}>
              <Column header="ID" body={(incident: IncidentItem) => <span style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }} title={incident.id}>{shortId(incident.id)}</span>} style={{ width: 90 }} />
              <Column header="Título" body={(incident: IncidentItem) => <IncidentSummary incident={incident} />} />
              <Column header="Severidade" body={(incident: IncidentItem) => <IncidentSeverity incident={incident} />} style={{ width: 100 }} />
              <Column header="Status" body={(incident: IncidentItem) => <IncidentStatus incident={incident} />} style={{ width: 125 }} />
              <Column header="Aberto em" body={(incident: IncidentItem) => formatDateTime(new Date(incident.openedAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
              <Column header="Resolvido em" body={(incident: IncidentItem) => incident.resolvedAt ? formatDateTime(new Date(incident.resolvedAt)) : "—"} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
              <Column
                header=""
                style={{ width: 320 }}
                body={(incident: IncidentItem) => (
                  <Can level="operate">
                    <div className="flex gap-1 align-items-center">
                      {statusSelect(incident, 130)}
                      <Button
                        variant="ghost"
                        title="Editar campos do incidente (título, descrição, severidade)"
                        onClick={() => openEdit(incident)}
                        style={{ padding: "4px 8px" }}
                      >
                        <i className="pi pi-pencil" />
                      </Button>
                      <Button
                        variant="ghost"
                        title="Investigar incidente com IA (análise LEVEL 0)"
                        onClick={() => openInvestigation(incident)}
                        style={{ padding: "4px 8px" }}
                      >
                        <i className="pi pi-sparkles" />
                      </Button>
                      <Button
                        variant="ghost"
                        title="Gerar rascunho de knowledge com IA (Draft)"
                        disabled={draftMutation.isPending}
                        onClick={() => draftMutation.mutate(incident.id)}
                        style={{ padding: "4px 8px" }}
                      >
                        <i className="pi pi-file-edit" />
                      </Button>
                      {incident.status === "Open" && (
                        <Button
                          variant="ghost"
                          title="Reconhecer incidente (Open → Investigando)"
                          onClick={() => acknowledgeMutation.mutate(incident.id)}
                          style={{ padding: "4px 8px" }}
                        >
                          <i className="pi pi-check" />
                        </Button>
                      )}
                    </div>
                  </Can>
                )}
              />
          </DataTablePanel>
        }
        renderCard={(incident) => (
          <div className="uni-panel" style={{ padding: 12 }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 6 }}>
              <div className="flex gap-1" style={{ flexWrap: "wrap" }}>
                <IncidentSeverity incident={incident} />
                <IncidentStatus incident={incident} />
              </div>
              <span style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8" }} title={incident.id}>
                {shortId(incident.id)}
              </span>
            </div>
            <IncidentSummary incident={incident} />
            <div style={{ fontSize: 11, color: "#64748b", marginTop: 6 }}>
              Aberto {formatDateTime(new Date(incident.openedAt))}
              {incident.resolvedAt && ` · Resolvido ${formatDateTime(new Date(incident.resolvedAt))}`}
            </div>
            <Can level="operate">
              <div className="flex gap-1 align-items-center" style={{ marginTop: 8, borderTop: "1px solid #f3f4f6", paddingTop: 8 }}>
                {statusSelect(incident)}
                <Button
                  variant="ghost"
                  title="Editar campos do incidente"
                  onClick={() => openEdit(incident)}
                  style={{ padding: "4px 8px", whiteSpace: "nowrap" }}
                >
                  <i className="pi pi-pencil" /> Editar
                </Button>
                <Button
                  variant="ghost"
                  title="Investigar incidente com IA (análise LEVEL 0)"
                  onClick={() => openInvestigation(incident)}
                  style={{ padding: "4px 8px", whiteSpace: "nowrap" }}
                >
                  <i className="pi pi-sparkles" /> Investigar
                </Button>
                <Button
                  variant="ghost"
                  title="Gerar rascunho de knowledge com IA (Draft)"
                  disabled={draftMutation.isPending}
                  onClick={() => draftMutation.mutate(incident.id)}
                  style={{ padding: "4px 8px", whiteSpace: "nowrap" }}
                >
                  <i className="pi pi-file-edit" /> Artigo (IA)
                </Button>
                {incident.status === "Open" && (
                  <Button
                    variant="ghost"
                    title="Reconhecer incidente (Open → Investigando)"
                    onClick={() => acknowledgeMutation.mutate(incident.id)}
                    style={{ padding: "4px 8px", whiteSpace: "nowrap" }}
                  >
                    <i className="pi pi-check" /> Reconhecer
                  </Button>
                )}
              </div>
            </Can>
          </div>
        )}
      />
    </ListPageShell>
  );
}

function IncidentSummary({ incident }: { incident: IncidentItem }) {
  const meta: string[] = [];
  if (incident.alertCount > 0) meta.push(`${incident.alertCount} alerta(s)`);
  if (incident.deviceId) meta.push(`dispositivo ${shortId(incident.deviceId)}`);

  return (
      <>
        <div style={{ fontWeight: 500, fontSize: 13 }}>{incident.title}</div>
        {incident.description && (
          <div style={{ fontSize: 11, color: "#94a3b8" }}>{incident.description}</div>
        )}
        {meta.length > 0 && (
          <div style={{ fontSize: 11, color: "#94a3b8" }}>{meta.join(" · ")}</div>
        )}
      </>
  );
}

function IncidentSeverity({ incident }: { incident: IncidentItem }) {
  const value = severityConfig[incident.severity] ?? { label: incident.severity, bg: "#f1f5f9", color: "#64748b" };
  return <StatusChip label={value.label} background={value.bg} color={value.color} />;
}

function IncidentStatus({ incident }: { incident: IncidentItem }) {
  const value = statusConfig[incident.status] ?? { label: incident.status, bg: "#f1f5f9", color: "#64748b" };
  return <StatusChip label={value.label} background={value.bg} color={value.color} />;
}
