import { dpiApps, trafficByClient, speedTestHistory } from "@/data/dashboardData";

export function InsightsPage() {
  const maxDpi = Math.max(...dpiApps.map(a => a.percentage));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div>
        <h1 style={{ fontSize: 20, fontWeight: 700 }}>Insights</h1>
        <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>Análises de tráfego, DPI e performance</p>
      </div>

      {/* DPI Apps */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Aplicações por Tráfego (DPI)</h2>
        </div>
        <div className="uni-panel-body">
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {dpiApps.map((app) => (
              <div key={app.name} style={{ display: "flex", alignItems: "center", gap: 12 }}>
                <span style={{ fontSize: 12, fontWeight: 500, width: 100 }}>{app.name}</span>
                <div style={{ flex: 1, height: 20, background: "#f3f4f6", borderRadius: 4, overflow: "hidden" }}>
                  <div style={{ height: "100%", width: `${(app.percentage / maxDpi) * 100}%`, background: app.color, borderRadius: 4, transition: "width 0.3s" }} />
                </div>
                <span style={{ fontSize: 11, color: "#6b7280", width: 60, textAlign: "right" }}>{app.bytes}</span>
                <span style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", width: 30, textAlign: "right" }}>{app.percentage}%</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="uni-grid-2">
        {/* Traffic by Client */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Tráfego por Cliente</h2>
          </div>
          <div className="uni-panel-body">
            <table className="uni-clients-table">
              <thead>
                <tr>
                  <th>Cliente</th>
                  <th>Download</th>
                  <th>Upload</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {trafficByClient.map((client) => (
                  <tr key={client.name}>
                    <td style={{ fontWeight: 500, fontSize: 12 }}>{client.name}</td>
                    <td style={{ fontSize: 12, color: "#22c55e" }}>↓ {client.download}</td>
                    <td style={{ fontSize: 12, color: "#3b82f6" }}>↑ {client.upload}</td>
                    <td style={{ fontSize: 12, fontWeight: 600 }}>{client.total}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Speed Test History */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Histórico de Speed Test</h2>
          </div>
          <div className="uni-panel-body">
            <table className="uni-clients-table">
              <thead>
                <tr>
                  <th>Data</th>
                  <th>Download</th>
                  <th>Upload</th>
                  <th>Ping</th>
                  <th>Servidor</th>
                </tr>
              </thead>
              <tbody>
                {speedTestHistory.map((test, i) => (
                  <tr key={i}>
                    <td style={{ fontFamily: "monospace", fontSize: 11, color: "#6b7280" }}>{test.date}</td>
                    <td style={{ fontSize: 12, color: "#22c55e", fontWeight: 500 }}>{test.download}</td>
                    <td style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500 }}>{test.upload}</td>
                    <td style={{ fontSize: 12 }}>{test.ping}</td>
                    <td style={{ fontSize: 11, color: "#6b7280" }}>{test.server}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
