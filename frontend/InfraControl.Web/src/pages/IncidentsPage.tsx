import { useState } from "react";
import { incidents } from "@/data/dashboardData";
import { Can } from "@/components/auth/Can";

const severityConfig: Record<string, { label: string; bg: string; color: string }> = {
  critical: { label: "Crítico", bg: "#fee2e2", color: "#dc2626" },
  high: { label: "Alta", bg: "#ffedd5", color: "#ea580c" },
  medium: { label: "Média", bg: "#fef3c7", color: "#d97706" },
  low: { label: "Baixa", bg: "#dbeafe", color: "#2563eb" },
};

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  investigating: { label: "Investigando", bg: "#fee2e2", color: "#dc2626" },
  identified: { label: "Identificado", bg: "#fef3c7", color: "#d97706" },
  monitoring: { label: "Monitorando", bg: "#dbeafe", color: "#2563eb" },
  resolved: { label: "Resolvido", bg: "#dcfce7", color: "#16a34a" },
};

export function IncidentsPage() {
  const [statusFilter, setStatusFilter] = useState("all");

  const filtered = incidents.filter((i) => statusFilter === "all" || i.status === statusFilter);
  const open = incidents.filter((i) => i.status !== "resolved").length;
  const breached = incidents.filter((i) => i.slaBreached).length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Incidentes</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {open} abertos · {incidents.length} total · {breached} com SLA breach
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            <option value="all">Todos os status</option>
            <option value="investigating">Investigando</option>
            <option value="identified">Identificado</option>
            <option value="monitoring">Monitorando</option>
            <option value="resolved">Resolvido</option>
          </select>
          <Can level="operate">
            <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 13, fontWeight: 500, border: "none" }}>
              <i className="pi pi-plus" style={{ fontSize: 12 }} /> Novo Incidente
            </button>
          </Can>
        </div>
      </div>

      <div className="uni-panel">
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th style={{ width: 90 }}>ID</th>
              <th>Título</th>
              <th style={{ width: 90 }}>Severidade</th>
              <th style={{ width: 120 }}>Status</th>
              <th style={{ width: 130 }}>Responsável</th>
              <th style={{ width: 140 }}>Aberto em</th>
              <th style={{ width: 70 }}>SLA</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((inc) => {
              const sev = severityConfig[inc.severity] ?? { label: inc.severity, bg: "#f1f5f9", color: "#64748b" };
              const st = statusConfig[inc.status] ?? { label: inc.status, bg: "#f1f5f9", color: "#64748b" };
              return (
                <tr key={inc.id}>
                  <td style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }}>{inc.id}</td>
                  <td>
                    <div style={{ fontWeight: 500, fontSize: 13 }}>{inc.title}</div>
                    <div style={{ fontSize: 11, color: "#94a3b8" }}>{inc.device} · {inc.relatedAlerts} alerta(s)</div>
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
                  <td style={{ fontSize: 12, color: "#64748b" }}>{inc.assignee}</td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{inc.openedAt}</td>
                  <td>
                    {inc.slaBreached ? (
                      <span style={{ fontSize: 11, fontWeight: 600, color: "#dc2626" }}>Breach</span>
                    ) : (
                      <span style={{ fontSize: 11, color: "#16a34a" }}>OK</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
