import { logsData } from "@/data/dashboardData";

export function LogsPage() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Logs</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>{logsData.length} registros</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#f1f5f9", borderRadius: 6, padding: "6px 12px" }}>
            <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
            <input type="text" placeholder="Pesquisar logs..." style={{ border: "none", background: "none", outline: "none", fontSize: 13, width: 180 }} />
          </div>
        </div>
      </div>

      <div className="uni-panel">
        <table className="uni-logs-table">
          <thead>
            <tr>
              <th style={{ width: 140 }}>Data/Hora</th>
              <th style={{ width: 80 }}>Nível</th>
              <th>Mensagem</th>
              <th style={{ width: 100 }}>Fonte</th>
            </tr>
          </thead>
          <tbody>
            {logsData.map((log, i) => (
              <tr key={i}>
                <td style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>{log.time}</td>
                <td>
                  <span className={`uni-log-level ${log.level}`}>
                    {log.level === "info" ? "INFO" : log.level === "warning" ? "WARN" : "ERR"}
                  </span>
                </td>
                <td style={{ fontSize: 13 }}>{log.message}</td>
                <td style={{ fontSize: 12, color: "#64748b" }}>{log.source}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
