import { useState } from "react";
import { Tag } from "primereact/tag";

interface Port {
  id: number;
  name: string;
  status: "active" | "inactive" | "disabled" | "blocked";
  speed: "1G" | "2.5G" | "5G" | "10G" | "—";
  poe: boolean;
  poeActive?: boolean;
  poeClass?: string;
  device?: string;
  client?: string;
  mac?: string;
  ip?: string;
  vlan?: string;
  rx?: string;
  tx?: string;
}

interface PortManagerProps {
  ports: Port[];
  title?: string;
  subtitle?: string;
  onPortClick?: (port: Port) => void;
}

function generateMockPorts(count: number): Port[] {
  const devices = [
    "AP-AC-PRO", "AP-AC-LR", "AP-U6-Lite", "Work Laptop", "Desktop PC",
    "IP Phone", "Printer", "NVR", "Smart TV", "Conference Room",
  ];
  const statuses: Array<"active" | "inactive" | "disabled" | "blocked"> = ["active", "active", "active", "active", "inactive", "inactive", "disabled"];
  const speeds: Array<"1G" | "2.5G" | "5G" | "10G"> = ["1G", "1G", "1G", "2.5G", "10G"];

  return Array.from({ length: count }, (_, i) => {
    const status = statuses[Math.floor(Math.random() * statuses.length)];
    const hasDevice = status === "active";
    const hasPoe = Math.random() > 0.5;
    const deviceIdx = Math.floor(Math.random() * devices.length);
    return {
      id: i + 1,
      name: `Port ${i + 1}`,
      status,
      speed: status === "active" ? speeds[Math.floor(Math.random() * speeds.length)] : "—",
      poe: hasPoe,
      poeActive: hasPoe && status === "active",
      poeClass: hasPoe && status === "active" ? `Class ${Math.floor(Math.random() * 4) + 1}` : undefined,
      device: hasDevice ? devices[deviceIdx] : undefined,
      client: hasDevice ? `Client-${100 + i}` : undefined,
      ip: hasDevice ? `192.168.1.${10 + i}` : undefined,
      vlan: hasDevice ? "VLAN 10" : undefined,
      rx: hasDevice ? `${(Math.random() * 100).toFixed(1)} Mbps` : undefined,
      tx: hasDevice ? `${(Math.random() * 50).toFixed(1)} Mbps` : undefined,
    } as Port;
  });
}

function PortTile({ port, onClick }: { port: Port; onClick?: () => void }) {
  const [hovered, setHovered] = useState(false);

  const statusColors = {
    active: "var(--status-green)",
    inactive: "var(--text-muted)",
    disabled: "var(--status-yellow)",
    blocked: "var(--status-red)",
  };

  return (
    <div
      className={`port-tile ${port.status} ${port.poe && port.poeActive ? "poe-active" : ""}`}
      style={{ borderColor: hovered ? "var(--accent)" : statusColors[port.status] }}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onClick={onClick}
      role="button"
      tabIndex={0}
    >
      <div className="port-header">
        <span className="port-number">{port.id}</span>
        {port.poe && (
          <span className={`port-poe ${port.poeActive ? "active" : ""}`}>PoE</span>
        )}
      </div>

      <div className="port-status-indicator" style={{ background: statusColors[port.status] }} />

      {port.device && (
        <div className="port-device">
          <span className="port-device-name">{port.device}</span>
          {port.ip && <span className="port-device-ip">{port.ip}</span>}
        </div>
      )}

      {port.speed !== "—" && (
        <div className="port-speed">
          <Tag value={port.speed} severity={port.speed === "10G" ? "danger" : port.speed === "2.5G" ? "warning" : "success"} />
        </div>
      )}

      {hovered && port.device && (
        <div className="port-tooltip">
          <div className="port-tooltip-row">
            <span>Device:</span> <strong>{port.device}</strong>
          </div>
          {port.ip && <div className="port-tooltip-row"><span>IP:</span> <strong>{port.ip}</strong></div>}
          {port.vlan && <div className="port-tooltip-row"><span>VLAN:</span> <strong>{port.vlan}</strong></div>}
          {port.rx && <div className="port-tooltip-row"><span>RX:</span> <strong>{port.rx}</strong></div>}
          {port.tx && <div className="port-tooltip-row"><span>TX:</span> <strong>{port.tx}</strong></div>}
        </div>
      )}
    </div>
  );
}

export function PortManager({ ports, title = "Port Manager", subtitle, onPortClick }: PortManagerProps) {
  const [selectedPort, setSelectedPort] = useState<Port | null>(null);

  const activeCount = ports.filter((p) => p.status === "active").length;
  const poeCount = ports.filter((p) => p.poe && p.poeActive).length;

  const handlePortClick = (port: Port) => {
    setSelectedPort(port);
    onPortClick?.(port);
  };

  return (
    <article className="panel port-manager">
      <div className="panel-heading">
        <div>
          <h2>{title}</h2>
          <p>{subtitle || `${activeCount}/${ports.length} ports active`}</p>
        </div>
        <div className="port-manager-stats">
          <span className="port-stat">
            <span className="port-stat-dot" style={{ background: "var(--status-green)" }} />
            {activeCount} Active
          </span>
          <span className="port-stat">
            <span className="port-stat-dot" style={{ background: "var(--text-muted)" }} />
            {ports.length - activeCount} Inactive
          </span>
          <span className="port-stat poe-stat">
            <i className="pi pi-bolt" />
            {poeCount} PoE
          </span>
        </div>
      </div>

      <div className="ports-grid">
        {ports.map((port) => (
          <PortTile
            key={port.id}
            port={port}
            onClick={() => handlePortClick(port)}
          />
        ))}
      </div>

      {selectedPort && (
        <div className="port-detail-panel">
          <div className="port-detail-header">
            <h3>Port {selectedPort.id} Details</h3>
            <button className="text-button" onClick={() => setSelectedPort(null)}>
              <i className="pi pi-times" />
            </button>
          </div>
          <div className="port-detail-body">
            <div className="port-detail-row">
              <span>Status:</span>
              <Tag value={selectedPort.status} severity={selectedPort.status === "active" ? "success" : selectedPort.status === "blocked" ? "danger" : "secondary"} />
            </div>
            <div className="port-detail-row">
              <span>Speed:</span>
              <strong>{selectedPort.speed}</strong>
            </div>
            {selectedPort.device && (
              <div className="port-detail-row">
                <span>Device:</span>
                <strong>{selectedPort.device}</strong>
              </div>
            )}
            {selectedPort.ip && (
              <div className="port-detail-row">
                <span>IP:</span>
                <strong>{selectedPort.ip}</strong>
              </div>
            )}
            {selectedPort.vlan && (
              <div className="port-detail-row">
                <span>VLAN:</span>
                <strong>{selectedPort.vlan}</strong>
              </div>
            )}
            {selectedPort.poe && (
              <div className="port-detail-row">
                <span>PoE:</span>
                <strong>{selectedPort.poeActive ? `Active (${selectedPort.poeClass})` : "Available"}</strong>
              </div>
            )}
            {selectedPort.rx && (
              <div className="port-detail-row">
                <span>RX:</span>
                <strong>{selectedPort.rx}</strong>
              </div>
            )}
            {selectedPort.tx && (
              <div className="port-detail-row">
                <span>TX:</span>
                <strong>{selectedPort.tx}</strong>
              </div>
            )}
          </div>
        </div>
      )}
    </article>
  );
}

export { generateMockPorts };
export type { Port };
