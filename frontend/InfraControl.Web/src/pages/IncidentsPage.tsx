import { useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import type { DataTablePageEvent } from "primereact/datatable";
import { Can } from "@/components/auth/Can";
import { formatDateTime } from "@/utils/format";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { PageHeader } from "@/components/ui/PageHeader";
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
  mutationErrorMessage,
} from "@/hooks/queries";
import {
  acknowledgeIncident,
  changeIncidentStatus,
  createIncident,
  listIncidents,
  type IncidentItem,
} from "@/services/api/incidents";

/**
 * F17-T003 + ADR-030 Fase 5 — IncidentsPage com React Query
 * (`usePagedQuery` + mutations com invalidação por prefixo `["incidents"]`)
 * e visões tabela/grid/kanban (preferência persistida). Kanban agrupa por
 * status (5 colunas do ciclo Open→Resolved); a transição usa
 * `PUT /incidents/{id}/status` (mapa de estados no backend — 409/400 pt-BR
 * vira banner) e `POST /{id}/acknowledge` sai de Open. Sem exclusão
 * (ADR-030: ciclo por estado). Escrita atrás de `Can operate` (exibição;
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
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

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

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) {
      setFormError("Informe o título do incidente.");
      return;
    }
    setFormError(null);
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
      setFormError(mutationErrorMessage(caught as Error));
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
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Incidentes"
        subtitle={incidents.isPending ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "incidente" : "incidentes"}`}
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
                  onClick={() => { setCreating((value) => !value); setFormError(null); }}
                >
                  Novo Incidente
                </Button>
              </Can>
            </div></FilterBar>
          </div>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {(actionError || listError) && (
        <ErrorBanner
          message={actionError ?? listError ?? "Não foi possível falar com o servidor. Tente novamente."}
          onRetry={() => {
            setActionError(null);
            void incidents.refetch();
          }}
        />
      )}

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo incidente</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 180px", gap: 10 }}>
            <FormField label="Título" error={formError}>
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
          <FormError message={formError ?? ""} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setCreating(false); setFormError(null); }}
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
                style={{ width: 190 }}
                body={(incident: IncidentItem) => (
                  <Can level="operate">
                    <div className="flex gap-1 align-items-center">
                      {statusSelect(incident, 150)}
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
    </div>
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
