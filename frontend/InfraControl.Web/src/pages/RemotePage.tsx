import { remoteSessions, remoteDevices } from "@/data/dashboardData";

export function RemotePage() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Acesso Remoto</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{remoteSessions.filter(s => s.status === "active").length} sessões ativas · {remoteDevices.length} dispositivos</p>
        </div>
        <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          <i className="pi pi-external-link" style={{ fontSize: 11 }} /> Nova Conexão
        </button>
      </div>

      {/* Active Sessions */}
      <div>
        <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>Sessões Ativas</h2>
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Dispositivo</th>
                <th>IP</th>
                <th>Tipo</th>
                <th>Usuário</th>
                <th>Início</th>
                <th>Duração</th>
                <th>Dados</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              {remoteSessions.filter(s => s.status === "active").map((session) => (
                <tr key={session.id}>
                  <td style={{ fontWeight: 500 }}>{session.device}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{session.ip}</td>
                  <td>
                    <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: session.type === "SSH" ? "#eff6ff" : "#f5f3ff", color: session.type === "SSH" ? "#3b82f6" : "#8b5cf6" }}>
                      {session.type}
                    </span>
                  </td>
                  <td style={{ fontSize: 12 }}>{session.user}</td>
                  <td style={{ fontSize: 12, color: "#6b7280" }}>{session.startTime}</td>
                  <td style={{ fontSize: 12, fontWeight: 500 }}>{session.duration}</td>
                  <td style={{ fontSize: 12, color: "#6b7280" }}>{session.bytes}</td>
                  <td>
                    <button style={{ fontSize: 10, padding: "3px 8px", borderRadius: 4, background: "#fef2f2", color: "#dc2626", fontWeight: 500 }}>
                      <i className="pi pi-times" style={{ fontSize: 9, marginRight: 3 }} /> Encerrar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Session History */}
      <div>
        <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>Histórico de Sessões</h2>
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Dispositivo</th>
                <th>IP</th>
                <th>Tipo</th>
                <th>Usuário</th>
                <th>Início</th>
                <th>Duração</th>
                <th>Dados</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {remoteSessions.map((session) => (
                <tr key={session.id}>
                  <td style={{ fontWeight: 500 }}>{session.device}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{session.ip}</td>
                  <td>
                    <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: session.type === "SSH" ? "#eff6ff" : "#f5f3ff", color: session.type === "SSH" ? "#3b82f6" : "#8b5cf6" }}>
                      {session.type}
                    </span>
                  </td>
                  <td style={{ fontSize: 12 }}>{session.user}</td>
                  <td style={{ fontSize: 12, color: "#6b7280" }}>{session.startTime}</td>
                  <td style={{ fontSize: 12 }}>{session.duration}</td>
                  <td style={{ fontSize: 12, color: "#6b7280" }}>{session.bytes}</td>
                  <td>
                    <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: session.status === "active" ? "#f0fdf4" : "#f3f4f6", color: session.status === "active" ? "#16a34a" : "#9ca3af" }}>
                      {session.status === "active" ? "Ativa" : "Fechada"}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Available Devices */}
      <div>
        <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>Dispositivos Disponíveis</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 12 }}>
          {remoteDevices.map((device) => (
            <div key={device.name} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 14, cursor: "pointer", transition: "all 0.12s" }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <div style={{ width: 32, height: 32, borderRadius: 6, background: device.type === "SSH" ? "#eff6ff" : device.type === "RDP" ? "#f5f3ff" : "#f3f4f6", color: device.type === "SSH" ? "#3b82f6" : device.type === "RDP" ? "#8b5cf6" : "#9ca3af", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 13 }}>
                    <i className={device.type === "SSH" ? "pi pi-terminal" : device.type === "RDP" ? "pi pi-desktop" : "pi pi-question"} />
                  </div>
                  <div>
                    <div style={{ fontSize: 12, fontWeight: 600 }}>{device.name}</div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>{device.os}</div>
                  </div>
                </div>
                <span style={{ width: 6, height: 6, borderRadius: "50%", background: device.status === "online" ? "#22c55e" : "#d1d5db" }} />
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#6b7280" }}>
                <span style={{ fontFamily: "monospace" }}>{device.ip}</span>
                <span>{device.lastAccess}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
