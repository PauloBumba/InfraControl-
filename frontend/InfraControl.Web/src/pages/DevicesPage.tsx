import { devices } from "@/data/dashboardData";

export function DevicesPage() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Dispositivos</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>{devices.length} dispositivos gerenciados</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 12px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}>
            <i className="pi pi-filter" style={{ fontSize: 12 }} /> Filtrar
          </button>
          <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 13, fontWeight: 500 }}>
            <i className="pi pi-plus" style={{ fontSize: 12 }} /> Adicionar
          </button>
        </div>
      </div>

      <div className="uni-devices-grid">
        {devices.map((device) => (
          <div className="uni-device-card" key={device.id}>
            <div className="uni-device-top">
              <div className={`uni-device-icon ${device.type}`}>
                <i className={device.type === "gateway" ? "pi pi-th-large" : device.type === "switch" ? "pi pi-server" : "pi pi-wifi"} />
              </div>
              <div>
                <div className="uni-device-name">{device.name}</div>
                <div className="uni-device-model">{device.model}</div>
              </div>
              <div className={`uni-device-status ${device.status}`}>
                <span className={`uni-device-status-dot ${device.status}`} />
                {device.status === "online" ? "Online" : device.status === "warning" ? "Atenção" : "Offline"}
              </div>
            </div>
            <div className="uni-device-details">
              <div className="uni-device-detail">
                <i className="pi pi-desktop" />
                {device.ip}
              </div>
              <div className="uni-device-detail">
                <i className="pi pi-tag" />
                v{device.firmware}
              </div>
              <div className="uni-device-detail">
                <i className="pi pi-clock" />
                {device.uptime}
              </div>
            </div>
            <div className="uni-device-bottom">
              {device.clients !== undefined && (
                <div className="uni-device-stat">
                  <i className="pi pi-users" />
                  {device.clients} clientes
                </div>
              )}
              {device.ports && (
                <div className="uni-device-stat">
                  <i className="pi pi-arrow-right" />
                  {device.ports} portas
                </div>
              )}
              {device.poe !== undefined && (
                <div className="uni-device-stat">
                  <i className="pi pi-bolt" style={{ color: "#f59e0b" }} />
                  {device.poe} PoE
                </div>
              )}
              {device.channels && (
                <div className="uni-device-stat">
                  <i className="pi pi-wifi" />
                  {device.channels.join(", ")}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
