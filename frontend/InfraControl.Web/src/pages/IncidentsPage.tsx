import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Can } from "@/components/auth/Can";
import { formatDateTime } from "@/utils/format";
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
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Incidentes</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "incidente" : "incidentes"}`}
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <select
            value={statusFilter}
            onChange={(event) => { setStatusFilter(event.target.value); setPage(1); }}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            {statusFilterOptions.map((value) => (
              <option key={value || "all"} value={value}>
                {value === "" ? "Todos os status" : statusConfig[value]?.label ?? value}
              </option>
            ))}
          </select>
          <Can level="operate">
            <button
              onClick={() => { setCreating((value) => !value); setFormError(null); }}
              style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 13, fontWeight: 500, border: "none" }}
            >
              <i className="pi pi-plus" style={{ fontSize: 12 }} /> Novo Incidente
            </button>
          </Can>
        </div>
      </div>

      {notice && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#f0fdf4", color: "#15803d" }}>
          <i className="pi pi-check-circle" />
          <span>{notice}</span>
          <button onClick={() => setNotice(null)} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer" }}>
            <i className="pi pi-times" />
          </button>
        </div>
      )}

      {error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
          <button onClick={() => void load()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
            Tentar de novo
          </button>
        </div>
      )}

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo incidente</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 180px", gap: 10 }}>
            <input
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder="Título *"
              autoFocus
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
            <select
              value={severity}
              onChange={(event) => setSeverity(event.target.value)}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, background: "white" }}
            >
              {severityOptions.map((value) => (
                <option key={value} value={value}>{severityConfig[value]?.label ?? value}</option>
              ))}
            </select>
          </div>
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Descrição (opcional)"
            rows={3}
            style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
          />
          {formError && <div style={{ fontSize: 12, color: "#b91c1c" }}>{formError}</div>}
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <button
              type="button"
              onClick={() => { setCreating(false); setFormError(null); }}
              style={{ padding: "6px 14px", borderRadius: 6, background: "white", border: "1px solid #e5e7eb", fontSize: 12, color: "#374151" }}
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={saving}
              style={{ padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500, opacity: saving ? 0.6 : 1 }}
            >
              {saving ? "Salvando…" : "Criar incidente"}
            </button>
          </div>
        </form>
      )}

      <div className="uni-panel">
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th style={{ width: 90 }}>ID</th>
              <th>Título</th>
              <th style={{ width: 90 }}>Severidade</th>
              <th style={{ width: 120 }}>Status</th>
              <th style={{ width: 140 }}>Aberto em</th>
              <th style={{ width: 140 }}>Resolvido em</th>
            </tr>
          </thead>
          <tbody>
            {incidents.map((incident) => (
              <IncidentRow key={incident.id} incident={incident} />
            ))}
            {!loading && incidents.length === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  {error ? "—" : "Nenhum incidente encontrado"}
                </td>
              </tr>
            )}
            {loading && incidents.length === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  Carregando…
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {totalCount > PAGE_SIZE && (
        <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 10, fontSize: 12, color: "#64748b" }}>
          <span>Página {page} de {totalPages}</span>
          <button
            onClick={() => setPage((value) => Math.max(1, value - 1))}
            disabled={page <= 1 || loading}
            style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: page <= 1 ? "default" : "pointer", opacity: page <= 1 ? 0.5 : 1 }}
          >
            Anterior
          </button>
          <button
            onClick={() => setPage((value) => Math.min(totalPages, value + 1))}
            disabled={page >= totalPages || loading}
            style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: page >= totalPages ? "default" : "pointer", opacity: page >= totalPages ? 0.5 : 1 }}
          >
            Próxima
          </button>
        </div>
      )}
    </div>
  );
}

function IncidentRow({ incident }: { incident: IncidentItem }) {
  const sev = severityConfig[incident.severity] ?? {
    label: incident.severity, bg: "#f1f5f9", color: "#64748b",
  };
  const st = statusConfig[incident.status] ?? {
    label: incident.status, bg: "#f1f5f9", color: "#64748b",
  };
  const meta: string[] = [];
  if (incident.alertCount > 0) meta.push(`${incident.alertCount} alerta(s)`);
  if (incident.deviceId) meta.push(`dispositivo ${shortId(incident.deviceId)}`);

  return (
    <tr>
      <td style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }} title={incident.id}>
        {shortId(incident.id)}
      </td>
      <td>
        <div style={{ fontWeight: 500, fontSize: 13 }}>{incident.title}</div>
        {incident.description && (
          <div style={{ fontSize: 11, color: "#94a3b8" }}>{incident.description}</div>
        )}
        {meta.length > 0 && (
          <div style={{ fontSize: 11, color: "#94a3b8" }}>{meta.join(" · ")}</div>
        )}
      </td>
      <td>
        <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4, background: sev.bg, color: sev.color }}>
          {sev.label}
        </span>
      </td>
      <td>
        <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4, background: st.bg, color: st.color }}>
          {st.label}
        </span>
      </td>
      <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(incident.openedAt))}</td>
      <td style={{ fontSize: 12, color: "#64748b" }}>
        {incident.resolvedAt ? formatDateTime(new Date(incident.resolvedAt)) : "—"}
      </td>
    </tr>
  );
}
