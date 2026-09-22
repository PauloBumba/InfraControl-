import { subsystems, gatewayData, trafficData, topClients, recentEvents } from "@/data/dashboardData";

export function DashboardPage() {
  const maxTraffic = Math.max(...trafficData.rx, ...trafficData.tx);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Subsystem Status Bar */}
      <div className="uni-panel">
        <div className="uni-subsystems">
          {subsystems.map((sub, i) => (
            <div key={sub.name} style={{ display: "flex", alignItems: "center", gap: 24 }}>
              <div className="uni-subsystem">
                <div className={`uni-subsystem-icon ${sub.status}`}>
                  <i className={sub.icon} />
                  <span className={`uni-subsystem-dot ${sub.status}`} />
                </div>
                <span className="uni-subsystem-label">{sub.name}</span>
              </div>
              {i < subsystems.length - 1 && <div className="uni-connector" />}
            </div>
          ))}
        </div>
      </div>

      {/* Gateway + Traffic Chart */}
      <div className="uni-grid-2">
        {/* Gateway */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Gateway</h2>
            <span style={{ fontSize: 11, color: "#64748b" }}>Uptime: {gatewayData.uptime}</span>
          </div>
          <div className="uni-gateway">
            <div className="uni-gateway-header">
              <span className={`uni-gateway-dot ${gatewayData.status}`} />
              <div>
                <div className="uni-gateway-name">{gatewayData.name}</div>
                <div className="uni-gateway-model">{gatewayData.model} · v{gatewayData.version}</div>
              </div>
            </div>
            <div className="uni-gateway-ip">
              <i className="pi pi-globe" style={{ fontSize: 11 }} />
              {gatewayData.ip}
            </div>
            <div className="uni-gateway-stats">
              <div className="uni-stat-block">
                <span className="uni-stat-label">CPU</span>
                <span className="uni-stat-value">{gatewayData.cpu}%</span>
                <div className="uni-progress-bar">
                  <div className={`uni-progress-fill ${gatewayData.cpu > 80 ? "red" : gatewayData.cpu > 50 ? "orange" : "blue"}`} style={{ width: `${gatewayData.cpu}%` }} />
                </div>
              </div>
              <div className="uni-stat-block">
                <span className="uni-stat-label">Memória</span>
                <span className="uni-stat-value">{gatewayData.mem}%</span>
                <div className="uni-progress-bar">
                  <div className={`uni-progress-fill ${gatewayData.mem > 80 ? "red" : gatewayData.mem > 50 ? "orange" : "green"}`} style={{ width: `${gatewayData.mem}%` }} />
                </div>
              </div>
            </div>
            <div className="uni-throughput">
              <div className="uni-throughput-item">
                <i className="pi pi-arrow-down rx" />
                <span className="uni-throughput-value">{gatewayData.rx}</span>
                <span className="uni-throughput-label">Download</span>
              </div>
              <div className="uni-throughput-sep" />
              <div className="uni-throughput-item">
                <i className="pi pi-arrow-up tx" />
                <span className="uni-throughput-value">{gatewayData.tx}</span>
                <span className="uni-throughput-label">Upload</span>
              </div>
            </div>
          </div>
        </div>

        {/* Traffic Chart */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Tráfego WAN</h2>
            <div className="uni-chart-tabs">
              <button className="uni-chart-tab active">1H</button>
              <button className="uni-chart-tab">6H</button>
              <button className="uni-chart-tab">24H</button>
              <button className="uni-chart-tab">7D</button>
            </div>
          </div>
          <div className="uni-panel-body">
            <svg className="uni-chart-svg" viewBox="0 0 600 180" preserveAspectRatio="none">
              <defs>
                <linearGradient id="rxGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#22c55e" stopOpacity="0.3" />
                  <stop offset="100%" stopColor="#22c55e" stopOpacity="0" />
                </linearGradient>
                <linearGradient id="txGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#3b82f6" stopOpacity="0.3" />
                  <stop offset="100%" stopColor="#3b82f6" stopOpacity="0" />
                </linearGradient>
              </defs>
              {/* RX area */}
              <path
                d={`M0,${180 - ((trafficData.rx[0] ?? 0) / maxTraffic) * 160} ${trafficData.rx.map((v, i) => `L${(i / (trafficData.rx.length - 1)) * 600},${180 - (v / maxTraffic) * 160}`).join(" ")} L600,180 L0,180 Z`}
                fill="url(#rxGrad)"
              />
              <path
                d={`M0,${180 - ((trafficData.rx[0] ?? 0) / maxTraffic) * 160} ${trafficData.rx.map((v, i) => `L${(i / (trafficData.rx.length - 1)) * 600},${180 - (v / maxTraffic) * 160}`).join(" ")}`}
                fill="none"
                stroke="#22c55e"
                strokeWidth="2"
              />
              {/* TX area */}
              <path
                d={`M0,${180 - ((trafficData.tx[0] ?? 0) / maxTraffic) * 160} ${trafficData.tx.map((v, i) => `L${(i / (trafficData.tx.length - 1)) * 600},${180 - (v / maxTraffic) * 160}`).join(" ")} L600,180 L0,180 Z`}
                fill="url(#txGrad)"
              />
              <path
                d={`M0,${180 - ((trafficData.tx[0] ?? 0) / maxTraffic) * 160} ${trafficData.tx.map((v, i) => `L${(i / (trafficData.tx.length - 1)) * 600},${180 - (v / maxTraffic) * 160}`).join(" ")}`}
                fill="none"
                stroke="#3b82f6"
                strokeWidth="2"
              />
            </svg>
            <div className="uni-chart-legend">
              <span><span className="uni-legend-dot" style={{ background: "#22c55e" }} /> Download</span>
              <span><span className="uni-legend-dot" style={{ background: "#3b82f6" }} /> Upload</span>
            </div>
          </div>
        </div>
      </div>

      {/* Top Clients + Recent Events */}
      <div className="uni-grid-2">
        {/* Top Clients */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Top Clientes</h2>
            <button style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500 }}>Ver todos</button>
          </div>
          <div className="uni-panel-body">
            <div className="uni-clients-list">
              {topClients.map((client) => (
                <div className="uni-client-row" key={client.mac}>
                  <div className="uni-client-icon">
                    <i className={client.type === "WiFi" ? "pi pi-wifi" : "pi pi-desktop"} />
                  </div>
                  <div className="uni-client-info">
                    <div className="uni-client-name">{client.name}</div>
                    <div className="uni-client-ip">{client.ip} · {client.mac}</div>
                  </div>
                  <span className="uni-client-type">{client.type}</span>
                  <span className="uni-client-usage">{client.usage}</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Recent Events */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Eventos Recentes</h2>
            <button style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500 }}>Ver todos</button>
          </div>
          <div className="uni-panel-body">
            <div className="uni-events-list">
              {recentEvents.map((event, i) => (
                <div className="uni-event-row" key={i}>
                  <span className={`uni-event-dot ${event.type}`} />
                  <div className="uni-event-body">
                    <div className="uni-event-msg">{event.message}</div>
                    <div className="uni-event-src">{event.source}</div>
                  </div>
                  <span className="uni-event-time">{event.time}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
