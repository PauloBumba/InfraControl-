import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import { type DataTablePageEvent } from "primereact/datatable";
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
import {
  createIncident,
  listIncidents,
  type IncidentItem,
  type PagedIncidents,
} from "@/services/api/incidents";

/**
 * F17-T003 — IncidentsPage conectada ao backend (substitui o mock de
 * `data/dashboardData`). Mudanças de contrato: os enums são PascalCase e o
 * status **Open** (auto-abertura do avaliador) não existia no protótipo;
 * `assignee` e `slaBreached` não existem no `IncidentDto` (coluna de
 * responsável saiu; SLA de incidente não é exposto — a tela mostra
 * `resolvedAt`); `device`/`relatedAlerts` viram `deviceId`/`alertCount`.
 * Escrita atrás de `Can operate` (exibição; policy Operate + antiforgery
 * continuam sendo a autoridade).
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

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

const shortId = (id: string): string => id.slice(0, 8);

export function IncidentsPage() {
  const [pageData, setPageData] = useState<PagedIncidents | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("");

  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState("");
  const [severity, setSeverity] = useState("Medium");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setPageData(
        await listIncidents({
          status: statusFilter || undefined,
          page,
          pageSize: PAGE_SIZE,
        }),
      );
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [statusFilter, page]);

  useEffect(() => {
    void load();
  }, [load]);

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) {
      setFormError("Informe o título do incidente.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      await createIncident({
        title: trimmed,
        severity,
        description: description.trim() || undefined,
      });
      setCreating(false);
      setTitle("");
      setDescription("");
      setSeverity("Medium");
      setNotice("Incidente criado.");
      await load();
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  }

  const incidents = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;
  function handlePageChange(event: DataTablePageEvent) {
    setPage(Math.floor(event.first / PAGE_SIZE) + 1);
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Incidentes"
        subtitle={loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "incidente" : "incidentes"}`}
        actions={
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
            <Can level="operate">
              <Button
                icon="pi pi-plus"
                onClick={() => { setCreating((value) => !value); setFormError(null); }}
              >
                Novo Incidente
              </Button>
            </Can>
          </div></FilterBar>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

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
            disabled={saving}
            style={{ padding: "6px 14px" }}
          >
            {saving ? "Salvando…" : "Criar incidente"}
          </Button>
          </div>
        </form>
      )}

      <DataTablePanel value={incidents} dataKey="id" loading={loading} paginator lazy rows={PAGE_SIZE} first={(page - 1) * PAGE_SIZE} totalRecords={totalCount} onPage={handlePageChange} alwaysShowPaginator={totalCount > PAGE_SIZE} paginatorTemplate="CurrentPageReport FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink" currentPageReportTemplate="Página {currentPage} de {totalPages} ({totalRecords} incidentes)" emptyMessage={error ? "—" : "Nenhum incidente encontrado"}>
          <Column header="ID" body={(incident: IncidentItem) => <span style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }} title={incident.id}>{shortId(incident.id)}</span>} style={{ width: 90 }} />
          <Column header="Título" body={(incident: IncidentItem) => <IncidentSummary incident={incident} />} />
          <Column header="Severidade" body={(incident: IncidentItem) => <IncidentSeverity incident={incident} />} style={{ width: 100 }} />
          <Column header="Status" body={(incident: IncidentItem) => <IncidentStatus incident={incident} />} style={{ width: 125 }} />
          <Column header="Aberto em" body={(incident: IncidentItem) => formatDateTime(new Date(incident.openedAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
          <Column header="Resolvido em" body={(incident: IncidentItem) => incident.resolvedAt ? formatDateTime(new Date(incident.resolvedAt)) : "—"} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
      </DataTablePanel>
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
