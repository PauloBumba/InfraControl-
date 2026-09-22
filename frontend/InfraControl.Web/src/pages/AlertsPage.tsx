import { alertsData } from "@/data/dashboardData";

export function AlertsPage() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Alertas</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>{alertsData.length} alertas ativos</p>
        </div>
        <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 12px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}>
          <i className="pi pi-filter" style={{ fontSize: 12 }} /> Filtrar
        </button>
      </div>

      <div className="uni-panel">
        <div className="uni-alerts-list">
          {alertsData.map((alert) => (
            <div className="uni-alert-row" key={alert.id}>
              <div className={`uni-alert-icon ${alert.severity}`}>
                <i className={alert.severity === "critical" ? "pi pi-exclamation-circle" : alert.severity === "warning" ? "pi pi-exclamation-triangle" : "pi pi-info-circle"} />
              </div>
              <div className="uni-alert-body">
                <div className="uni-alert-title">{alert.title}</div>
                <div className="uni-alert-detail">{alert.detail}</div>
              </div>
              <span className={`uni-alert-severity ${alert.severity}`}>
                {alert.severity === "critical" ? "Crítico" : alert.severity === "warning" ? "Atenção" : "Info"}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
