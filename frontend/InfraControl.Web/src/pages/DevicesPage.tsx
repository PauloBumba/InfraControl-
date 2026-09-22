import { useNavigate } from "react-router-dom";
import { allDevices } from "@/data/deviceData";

export function DevicesPage() {
  const navigate = useNavigate();

  const typeIcons: Record<string, string> = {
    gateway: "pi pi-th-large", switch: "pi pi-server", ap: "pi pi-wifi",
    printer: "pi pi-print", camera: "pi pi-video", phone: "pi pi-phone",
    server: "pi pi-desktop", vm: "pi pi-box", nas: "pi pi-database",
    ups: "pi pi-bolt", storage: "pi pi-database"
  };
  const typeColors: Record<string, string> = {
    gateway: "#3b82f6", switch: "#8b5cf6", ap: "#22c55e",
    printer: "#f59e0b", camera: "#ec4899", phone: "#06b6d4",
    server: "#3b82f6", vm: "#8b5cf6", nas: "#22c55e",
    ups: "#f59e0b", storage: "#3b82f6"
  };
  const typeNames: Record<string, string> = {
    gateway: "Gateway", switch: "Switch", ap: "Access Point",
    printer: "Impressora", camera: "Câmera", phone: "Telefone",
    server: "Servidor", vm: "Máquina Virtual", nas: "NAS",
    ups: "UPS", storage: "Armazenamento"
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Dispositivos</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{allDevices.length} dispositivos gerenciados</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 12px", borderRadius: 6, border: "1px solid #e5e7eb", fontSize: 12, color: "#6b7280", background: "white" }}>
            <i className="pi pi-filter" style={{ fontSize: 11 }} /> Filtrar
          </button>
          <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#0088ff", color: "white", fontSize: 12, fontWeight: 500 }}>
            <i className="pi pi-plus" style={{ fontSize: 11 }} /> Adicionar
          </button>
        </div>
      </div>

      <div className="uni-devices-grid">
        {allDevices.map((device) => (
          <div
            key={device.id}
            className="uni-device-card"
            onClick={() => navigate(`/devices/${device.id}`)}
          >
            <div className="uni-device-top">
              <div className={`uni-device-icon ${device.type}`} style={{ background: `${typeColors[device.type]}15`, color: typeColors[device.type] }}>
                <i className={typeIcons[device.type] || "pi pi-circle"} />
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
                {typeNames[device.type]}
              </div>
              <div className="uni-device-detail">
                <i className="pi pi-map-marker" />
                {device.location}
              </div>
              <div className="uni-device-detail">
                <i className="pi pi-clock" />
                {device.uptime}
              </div>
            </div>
            <div className="uni-device-bottom">
              <div className="uni-device-stat">
                <i className="pi pi-code" />
                v{device.firmware}
              </div>
              <div className="uni-device-stat">
                <i className="pi pi-building" />
                {device.department}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
