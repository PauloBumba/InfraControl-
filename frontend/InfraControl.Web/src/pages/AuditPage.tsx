import { useState } from "react";
import { auditEntries, auditActionOptions, auditOutcomeOptions } from "@/data/dashboardData";

export function AuditPage() {
  const [search, setSearch] = useState("");
  const [actionFilter, setActionFilter] = useState("all");
  const [outcomeFilter, setOutcomeFilter] = useState("all");

  const filtered = auditEntries.filter((entry) => {
    const matchesSearch =
      entry.action.toLowerCase().includes(search.toLowerCase()) ||
      entry.resource.toLowerCase().includes(search.toLowerCase()) ||
      (entry.actorId ?? "").toLowerCase().includes(search.toLowerCase());
    const matchesAction = actionFilter === "all" || entry.action === actionFilter;
    const matchesOutcome = outcomeFilter === "all" || entry.outcome === outcomeFilter;
    return matchesSearch && matchesAction && matchesOutcome;
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Auditoria</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {filtered.length} de {auditEntries.length} eventos
          </p>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#f1f5f9", borderRadius: 6, padding: "6px 12px" }}>
            <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
            <input
              type="text"
              placeholder="Pesquisar auditoria..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ border: "none", background: "none", outline: "none", fontSize: 13, width: 180 }}
            />
          </div>
          <select
            value={actionFilter}
            onChange={(e) => setActionFilter(e.target.value)}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            {auditActionOptions.map((opt) => (
              <option key={opt} value={opt}>
                {opt === "all" ? "Todas as ações" : opt}
              </option>
            ))}
          </select>
          <select
            value={outcomeFilter}
            onChange={(e) => setOutcomeFilter(e.target.value)}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            {auditOutcomeOptions.map((opt) => (
              <option key={opt} value={opt}>
                {opt === "all" ? "Todos os resultados" : opt === "Success" ? "Sucesso" : "Falha"}
              </option>
            ))}
          </select>
          <button
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              padding: "6px 12px",
              borderRadius: 6,
              border: "1px solid #e2e8f0",
              fontSize: 13,
              color: "#64748b",
              background: "white",
            }}
          >
            <i className="pi pi-download" style={{ fontSize: 12 }} /> Exportar
          </button>
        </div>
      </div>

      <div className="uni-panel">
        <table className="uni-logs-table">
          <thead>
            <tr>
              <th style={{ width: 150 }}>Data/Hora</th>
              <th style={{ width: 200 }}>Ação</th>
              <th style={{ width: 180 }}>Recurso</th>
              <th style={{ width: 90 }}>Resultado</th>
              <th>Ator</th>
              <th style={{ width: 130 }}>Correlation</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((entry) => (
              <tr key={entry.id}>
                <td style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>{entry.occurredAt}</td>
                <td style={{ fontFamily: "monospace", fontSize: 12 }}>{entry.action}</td>
                <td style={{ fontSize: 12, color: "#64748b" }}>{entry.resource}</td>
                <td>
                  <span
                    style={{
                      fontSize: 11,
                      fontWeight: 500,
                      padding: "2px 8px",
                      borderRadius: 4,
                      background: entry.outcome === "Success" ? "#dcfce7" : "#fee2e2",
                      color: entry.outcome === "Success" ? "#16a34a" : "#dc2626",
                    }}
                  >
                    {entry.outcome === "Success" ? "Sucesso" : "Falha"}
                  </span>
                </td>
                <td style={{ fontSize: 12 }}>
                  {entry.actorId ?? <span style={{ color: "#94a3b8" }}>sistema</span>}
                  {entry.error && (
                    <div style={{ fontSize: 11, color: "#dc2626", marginTop: 2 }}>{entry.error}</div>
                  )}
                </td>
                <td style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8" }}>{entry.correlationId}</td>
              </tr>
            ))}
            {filtered.length === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  Nenhum evento encontrado
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
