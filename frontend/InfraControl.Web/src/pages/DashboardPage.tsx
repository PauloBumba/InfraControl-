import { subsystems, gatewayData, trafficData, topClients, recentEvents, alertsData, devices, tasks, speedTestHistory } from "@/data/dashboardData";

function MiniStat({ icon, label, value, color, sub }: { icon: string; label: string; value: string; color: string; sub?: string }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "14px 16px", background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
      <div style={{ width: 40, height: 40, borderRadius: 8, background: `${color}12`, color, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 16, flexShrink: 0 }}>
        <i className={`pi ${icon}`} />
      </div>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 10, color: "#9ca3af", textTransform: "uppercase", letterSpacing: 0.5 }}>{label}</div>
        <div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1.2 }}>{value}</div>
        {sub && <div style={{ fontSize: 10, color: "#9ca3af" }}>{sub}</div>}
      </div>
    </div>
  );
}

function Progress({ value, color = "#3b82f6", height = 6 }: { value: number; color?: string; height?: number }) {
  return (
    <div style={{ width: "100%", height, background: "#f3f4f6", borderRadius: height, overflow: "hidden" }}>
      <div style={{ height: "100%", width: `${Math.min(value, 100)}%`, background: color, borderRadius: height, transition: "width 0.3s" }} />
    </div>
  );
}

export function DashboardPage() {
  const maxTraffic = Math.max(...trafficData.rx, ...trafficData.tx);
  const onlineDevices = devices.filter(d => d.status === "online").length;
  const totalClients = 84;
  const criticalAlerts = alertsData.filter(a => a.severity === "critical").length;
  const todoTasks = tasks.filter(t => t.status === "todo").length;
  const inProgressTasks = tasks.filter(t => t.status === "in_progress").length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>

      {/* ============ QUICK STATS ============ */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 12 }}>
        <MiniStat icon="pi-server" label="Dispositivos" value={`${onlineDevices}/${devices.length}`} color="#3b82f6" sub="online" />
        <MiniStat icon="pi-users" label="Clientes" value={`${totalClients}`} color="#22c55e" sub="conectados" />
        <MiniStat icon="pi-download" label="Download" value={gatewayData.rx} color="#8b5cf6" sub={`↑ ${gatewayData.tx}`} />
        <MiniStat icon="pi-bell" label="Alertas" value={`${alertsData.length}`} color="#f59e0b" sub={`${criticalAlerts} críticos`} />
        <MiniStat icon="pi-check-square" label="Tarefas" value={`${todoTasks + inProgressTasks}`} color="#06b6d4" sub={`${inProgressTasks} em progresso`} />
        <MiniStat icon="pi-wifi" label="WiFi" value="59" color="#22c55e" sub="clientes WiFi" />
      </div>

      {/* ============ SUBSYSTEMS ============ */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Status do Sistema</h2>
          <span style={{ fontSize: 11, color: "#22c55e", fontWeight: 500 }}>Todos operacionais</span>
        </div>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 8, padding: "16px 20px", flexWrap: "wrap" }}>
          {subsystems.map((sub, i) => (
            <div key={sub.name} style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 14px", borderRadius: 8, background: sub.status === "online" ? "#f0fdf4" : sub.status === "warning" ? "#fefce8" : "#fef2f2" }}>
                <i className={sub.icon} style={{ fontSize: 14, color: sub.status === "online" ? "#16a34a" : sub.status === "warning" ? "#ca8a04" : "#dc2626" }} />
                <span style={{ fontSize: 12, fontWeight: 600 }}>{sub.name}</span>
                <span style={{ width: 7, height: 7, borderRadius: "50%", background: sub.status === "online" ? "#22c55e" : sub.status === "warning" ? "#f59e0b" : "#ef4444" }} />
              </div>
              {i < subsystems.length - 1 && <div style={{ width: 24, height: 1, background: "#e5e7eb" }} />}
            </div>
          ))}
        </div>
      </div>

      {/* ============ GATEWAY + TRAFFIC ============ */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1.6fr", gap: 12 }}>
        {/* Gateway */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Gateway</h2>
            <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f0fdf4", color: "#16a34a", fontWeight: 500 }}>Online</span>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <div style={{ width: 44, height: 44, borderRadius: 10, background: "#3b82f615", color: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 20 }}>
                <i className="pi pi-th-large" />
              </div>
              <div>
                <div style={{ fontSize: 14, fontWeight: 700 }}>{gatewayData.name}</div>
                <div style={{ fontSize: 11, color: "#9ca3af" }}>{gatewayData.model} · v{gatewayData.version}</div>
              </div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
              <div style={{ padding: 10, background: "#f9fafb", borderRadius: 6, fontSize: 11 }}>
                <div style={{ color: "#9ca3af", marginBottom: 4 }}>CPU</div>
                <div style={{ fontSize: 18, fontWeight: 700 }}>{gatewayData.cpu}%</div>
                <Progress value={gatewayData.cpu} color={gatewayData.cpu > 80 ? "#ef4444" : "#3b82f6"} />
              </div>
              <div style={{ padding: 10, background: "#f9fafb", borderRadius: 6, fontSize: 11 }}>
                <div style={{ color: "#9ca3af", marginBottom: 4 }}>Memória</div>
                <div style={{ fontSize: 18, fontWeight: 700 }}>{gatewayData.mem}%</div>
                <Progress value={gatewayData.mem} color={gatewayData.mem > 80 ? "#ef4444" : "#22c55e"} />
              </div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
              <div style={{ textAlign: "center", padding: 10, background: "#f0fdf4", borderRadius: 6 }}>
                <div style={{ fontSize: 10, color: "#9ca3af" }}>↓ Download</div>
                <div style={{ fontSize: 16, fontWeight: 700, color: "#16a34a" }}>{gatewayData.rx}</div>
              </div>
              <div style={{ textAlign: "center", padding: 10, background: "#eff6ff", borderRadius: 6 }}>
                <div style={{ fontSize: 10, color: "#9ca3af" }}>↑ Upload</div>
                <div style={{ fontSize: 16, fontWeight: 700, color: "#3b82f6" }}>{gatewayData.tx}</div>
              </div>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 6, fontSize: 11, color: "#6b7280" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>Uptime</span><span style={{ fontWeight: 600 }}>{gatewayData.uptime}</span></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>WAN IP</span><span style={{ fontFamily: "monospace", fontWeight: 500 }}>{gatewayData.wanIp}</span></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>ISP</span><span style={{ fontWeight: 500 }}>{gatewayData.isp}</span></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>Velocidade</span><span style={{ fontWeight: 500 }}>{gatewayData.speedDown} ↓ / {gatewayData.speedUp} ↑</span></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>DNS</span><span style={{ fontFamily: "monospace", fontSize: 10 }}>{gatewayData.dns}</span></div>
            </div>
          </div>
        </div>

        {/* Traffic Chart */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Tráfego WAN — Última Hora</h2>
            <div style={{ display: "flex", gap: 4 }}>
              {["1H", "6H", "24H", "7D"].map((t, i) => (
                <button key={t} style={{ padding: "3px 8px", borderRadius: 4, fontSize: 11, fontWeight: i === 0 ? 600 : 400, background: i === 0 ? "#3b82f615" : "transparent", color: i === 0 ? "#3b82f6" : "#9ca3af", border: "none", cursor: "pointer" }}>{t}</button>
              ))}
            </div>
          </div>
          <div className="uni-panel-body">
            {/* Summary row */}
            <div style={{ display: "flex", gap: 20, marginBottom: 16, fontSize: 12 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <span style={{ width: 8, height: 8, borderRadius: 2, background: "#22c55e" }} />
                <span style={{ color: "#9ca3af" }}>Download:</span>
                <span style={{ fontWeight: 600 }}>Média {Math.round(trafficData.rx.reduce((a, b) => a + b, 0) / trafficData.rx.length)} Mbps</span>
                <span style={{ color: "#9ca3af" }}>Pico {Math.max(...trafficData.rx)} Mbps</span>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <span style={{ width: 8, height: 8, borderRadius: 2, background: "#3b82f6" }} />
                <span style={{ color: "#9ca3af" }}>Upload:</span>
                <span style={{ fontWeight: 600 }}>Média {Math.round(trafficData.tx.reduce((a, b) => a + b, 0) / trafficData.tx.length)} Mbps</span>
                <span style={{ color: "#9ca3af" }}>Pico {Math.max(...trafficData.tx)} Mbps</span>
              </div>
            </div>

            <svg style={{ width: "100%", height: 200 }} viewBox="0 0 600 200" preserveAspectRatio="none">
              <defs>
                <linearGradient id="rxGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#22c55e" stopOpacity="0.25" />
                  <stop offset="100%" stopColor="#22c55e" stopOpacity="0" />
                </linearGradient>
                <linearGradient id="txGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#3b82f6" stopOpacity="0.25" />
                  <stop offset="100%" stopColor="#3b82f6" stopOpacity="0" />
                </linearGradient>
              </defs>
              {/* Grid lines */}
              {[0, 1, 2, 3, 4].map(i => (
                <g key={i}>
                  <line x1="0" y1={i * 40 + 10} x2="600" y2={i * 40 + 10} stroke="#f3f4f6" strokeWidth="1" />
                  <text x="-2" y={i * 40 + 14} fill="#d1d5db" fontSize="9" textAnchor="end">{Math.round(maxTraffic - (maxTraffic / 5) * i)}</text>
                </g>
              ))}
              {/* RX */}
              <path
                d={`M0,${190 - ((trafficData.rx[0] ?? 0) / maxTraffic) * 170} ${trafficData.rx.map((v, i) => `L${(i / (trafficData.rx.length - 1)) * 600},${190 - (v / maxTraffic) * 170}`).join(" ")} L600,190 L0,190 Z`}
                fill="url(#rxGrad)"
              />
              <path
                d={`M0,${190 - ((trafficData.rx[0] ?? 0) / maxTraffic) * 170} ${trafficData.rx.map((v, i) => `L${(i / (trafficData.rx.length - 1)) * 600},${190 - (v / maxTraffic) * 170}`).join(" ")}`}
                fill="none" stroke="#22c55e" strokeWidth="2" strokeLinejoin="round"
              />
              {/* TX */}
              <path
                d={`M0,${190 - ((trafficData.tx[0] ?? 0) / maxTraffic) * 170} ${trafficData.tx.map((v, i) => `L${(i / (trafficData.tx.length - 1)) * 600},${190 - (v / maxTraffic) * 170}`).join(" ")} L600,190 L0,190 Z`}
                fill="url(#txGrad)"
              />
              <path
                d={`M0,${190 - ((trafficData.tx[0] ?? 0) / maxTraffic) * 170} ${trafficData.tx.map((v, i) => `L${(i / (trafficData.tx.length - 1)) * 600},${190 - (v / maxTraffic) * 170}`).join(" ")}`}
                fill="none" stroke="#3b82f6" strokeWidth="2" strokeLinejoin="round"
              />
            </svg>
          </div>
        </div>
      </div>

      {/* ============ DEVICES + ALERTS + TASKS ============ */}
      <div style={{ display: "grid", gridTemplateColumns: "1.2fr 1fr 1fr", gap: 12 }}>
        {/* Devices Overview */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Dispositivos</h2>
            <span style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500, cursor: "pointer" }}>Ver todos</span>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {devices.map((device) => (
              <div key={device.id} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 10px", borderRadius: 6, background: "#f9fafb", border: "1px solid #f3f4f6" }}>
                <span style={{ width: 7, height: 7, borderRadius: "50%", background: device.status === "online" ? "#22c55e" : device.status === "warning" ? "#f59e0b" : "#ef4444", flexShrink: 0 }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{device.name}</div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>{device.model}</div>
                </div>
                {device.cpu !== undefined && (
                  <div style={{ fontSize: 10, color: "#6b7280" }}>
                    <span style={{ fontWeight: 600 }}>{device.cpu}%</span> CPU
                  </div>
                )}
                {device.clients !== undefined && device.clients > 0 && (
                  <div style={{ fontSize: 10, color: "#6b7280" }}>
                    <i className="pi pi-users" style={{ fontSize: 9, marginRight: 2 }} />{device.clients}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Alerts */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Alertas Recentes</h2>
            <span style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500, cursor: "pointer" }}>Ver todos</span>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {alertsData.map((alert) => (
              <div key={alert.id} style={{ padding: "10px 12px", borderRadius: 6, border: "1px solid #f3f4f6", borderLeft: `3px solid ${alert.severity === "critical" ? "#ef4444" : alert.severity === "warning" ? "#f59e0b" : "#3b82f6"}` }}>
                <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 4 }}>
                  <i className={`pi ${alert.severity === "critical" ? "pi-exclamation-triangle" : alert.severity === "warning" ? "pi-exclamation-circle" : "pi-info-circle"}`} style={{ fontSize: 11, color: alert.severity === "critical" ? "#ef4444" : alert.severity === "warning" ? "#f59e0b" : "#3b82f6" }} />
                  <span style={{ fontSize: 12, fontWeight: 600 }}>{alert.title}</span>
                </div>
                <div style={{ fontSize: 11, color: "#6b7280" }}>{alert.detail}</div>
              </div>
            ))}
          </div>
        </div>

        {/* Tasks */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Tarefas</h2>
            <span style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500, cursor: "pointer" }}>Ver todas</span>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {tasks.filter(t => t.status !== "done").slice(0, 6).map((task) => (
              <div key={task.id} style={{ padding: "10px 12px", borderRadius: 6, border: "1px solid #f3f4f6" }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 4 }}>
                  <span style={{ fontSize: 12, fontWeight: 500 }}>{task.title}</span>
                  <span style={{ fontSize: 9, padding: "2px 6px", borderRadius: 4, background: task.priority === "high" ? "#fef2f2" : task.priority === "medium" ? "#fefce8" : "#f3f4f6", color: task.priority === "high" ? "#dc2626" : task.priority === "medium" ? "#ca8a04" : "#6b7280", fontWeight: 500 }}>
                    {task.priority === "high" ? "Alta" : task.priority === "medium" ? "Média" : "Baixa"}
                  </span>
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 10, color: "#9ca3af" }}>
                  <span><i className="pi pi-calendar" style={{ marginRight: 3 }} />{task.dueDate}</span>
                  <span><i className="pi pi-tag" style={{ marginRight: 3 }} />{task.device}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ============ TOP CLIENTS + EVENTS + SPEED TEST ============ */}
      <div style={{ display: "grid", gridTemplateColumns: "1.2fr 1fr 1fr", gap: 12 }}>
        {/* Top Clients */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Top Clientes</h2>
            <span style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500, cursor: "pointer" }}>Ver todos</span>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {topClients.map((client, i) => (
              <div key={client.mac} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 0", borderBottom: i < topClients.length - 1 ? "1px solid #f3f4f6" : "none" }}>
                <div style={{ width: 28, height: 28, borderRadius: 6, background: client.type === "WiFi" ? "#f0fdf4" : "#eff6ff", color: client.type === "WiFi" ? "#16a34a" : "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 12, flexShrink: 0 }}>
                  <i className={`pi ${client.type === "WiFi" ? "pi-wifi" : "pi-desktop"}`} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{client.name}</div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>{client.ip}</div>
                </div>
                <div style={{ textAlign: "right" }}>
                  <div style={{ fontSize: 12, fontWeight: 600 }}>{client.usage}</div>
                  <div style={{ fontSize: 9, color: "#9ca3af" }}>{client.type}</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Recent Events */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Eventos Recentes</h2>
            <span style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500, cursor: "pointer" }}>Ver todos</span>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {recentEvents.map((event, i) => (
              <div key={i} style={{ display: "flex", alignItems: "flex-start", gap: 8, padding: "8px 0", borderBottom: i < recentEvents.length - 1 ? "1px solid #f3f4f6" : "none" }}>
                <span style={{ width: 7, height: 7, borderRadius: "50%", background: event.type === "success" ? "#22c55e" : event.type === "warning" ? "#f59e0b" : "#3b82f6", marginTop: 4, flexShrink: 0 }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12 }}>{event.message}</div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>{event.source}</div>
                </div>
                <span style={{ fontSize: 10, color: "#9ca3af", whiteSpace: "nowrap" }}>{event.time}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Speed Test */}
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Speed Test</h2>
            <span style={{ fontSize: 11, color: "#22c55e", fontWeight: 500 }}>Último: 485/89 Mbps</span>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {speedTestHistory.map((test, i) => (
              <div key={i} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 0", borderBottom: i < speedTestHistory.length - 1 ? "1px solid #f3f4f6" : "none" }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 11, color: "#6b7280" }}>{test.date}</div>
                </div>
                <div style={{ textAlign: "right" }}>
                  <div style={{ fontSize: 12, fontWeight: 600, color: "#16a34a" }}>↓ {test.download}</div>
                  <div style={{ fontSize: 11, color: "#3b82f6" }}>↑ {test.upload}</div>
                </div>
                <span style={{ fontSize: 10, color: "#9ca3af", whiteSpace: "nowrap" }}>{test.ping}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
