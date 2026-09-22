import { useEffect } from "react";
import { TabView, TabPanel } from "primereact/tabview";
import { Tag } from "primereact/tag";
import { ProgressBar } from "primereact/progressbar";
import { Button } from "primereact/button";

interface DeviceDetailProps {
  device: {
    id: string;
    name: string;
    model: string;
    type: "gateway" | "switch" | "ap";
    status: "online" | "offline" | "adopting" | "upgrading" | "failed";
    ip: string;
    mac: string;
    uptime?: string;
    firmware?: string;
    location?: string;
    cpu?: number;
    memory?: number;
    temperature?: number;
    ports?: { total: number; active: number };
    clients?: number;
    wan?: { up: number; down: number };
  };
  onClose: () => void;
}

function TabOverview({ device }: { device: DeviceDetailProps["device"] }) {
  return (
    <div className="detail-tab-content">
      <div className="detail-section">
        <h4>Device Information</h4>
        <div className="detail-grid">
          <div className="detail-field"><span>Name</span><strong>{device.name}</strong></div>
          <div className="detail-field"><span>Model</span><strong>{device.model}</strong></div>
          <div className="detail-field"><span>IP Address</span><strong>{device.ip}</strong></div>
          <div className="detail-field"><span>MAC Address</span><strong>{device.mac}</strong></div>
          <div className="detail-field"><span>Location</span><strong>{device.location || "—"}</strong></div>
          <div className="detail-field"><span>Firmware</span><strong>{device.firmware || "—"}</strong></div>
          <div className="detail-field"><span>Uptime</span><strong>{device.uptime || "—"}</strong></div>
          <div className="detail-field"><span>Status</span>
            <Tag value={device.status} severity={device.status === "online" ? "success" : device.status === "adopting" ? "info" : "danger"} />
          </div>
        </div>
      </div>

      {(device.cpu !== undefined || device.memory !== undefined) && (
        <div className="detail-section">
          <h4>System Resources</h4>
          {device.cpu !== undefined && (
            <div className="detail-progress">
              <div className="detail-progress-header">
                <span>CPU</span><strong>{device.cpu}%</strong>
              </div>
              <ProgressBar value={device.cpu} showValue={false} style={{ height: "6px" }} />
            </div>
          )}
          {device.memory !== undefined && (
            <div className="detail-progress">
              <div className="detail-progress-header">
                <span>Memory</span><strong>{device.memory}%</strong>
              </div>
              <ProgressBar value={device.memory} showValue={false} style={{ height: "6px" }} />
            </div>
          )}
          {device.temperature !== undefined && (
            <div className="detail-field" style={{ marginTop: "var(--space-3)" }}>
              <span>Temperature</span>
              <strong>{device.temperature}°C</strong>
            </div>
          )}
        </div>
      )}

      {device.wan && (
        <div className="detail-section">
          <h4>WAN Throughput</h4>
          <div className="detail-throughput">
            <div className="throughput-stat">
              <i className="pi pi-arrow-up" />
              <span>{device.wan.up} Mbps</span>
              <small>Upload</small>
            </div>
            <div className="throughput-stat">
              <i className="pi pi-arrow-down" />
              <span>{device.wan.down} Mbps</span>
              <small>Download</small>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function TabPerformance(_props: { device: DeviceDetailProps["device"] }) {
  const mockHistory = Array.from({ length: 24 }, (_, i) => ({
    time: `${String(i).padStart(2, "0")}:00`,
    cpu: Math.floor(Math.random() * 40 + 10),
    memory: Math.floor(Math.random() * 20 + 50),
    network: Math.floor(Math.random() * 500 + 100),
  }));

  return (
    <div className="detail-tab-content">
      <div className="detail-section">
        <h4>Performance Metrics (24h)</h4>
        <div className="detail-metrics-grid">
          <div className="detail-metric">
            <span>Avg CPU</span>
            <strong>{Math.floor(mockHistory.reduce((a, h) => a + h.cpu, 0) / 24)}%</strong>
          </div>
          <div className="detail-metric">
            <span>Peak CPU</span>
            <strong>{Math.max(...mockHistory.map((h) => h.cpu))}%</strong>
          </div>
          <div className="detail-metric">
            <span>Avg Memory</span>
            <strong>{Math.floor(mockHistory.reduce((a, h) => a + h.memory, 0) / 24)}%</strong>
          </div>
          <div className="detail-metric">
            <span>Avg Network</span>
            <strong>{Math.floor(mockHistory.reduce((a, h) => a + h.network, 0) / 24)} Mbps</strong>
          </div>
        </div>
      </div>

      <div className="detail-section">
        <h4>Uptime History</h4>
        <div className="uptime-chart">
          {mockHistory.map((h, i) => (
            <div key={i} className="uptime-bar" style={{ height: `${h.cpu}%` }} title={`${h.time}: ${h.cpu}%`} />
          ))}
        </div>
        <div className="uptime-labels">
          <span>00:00</span>
          <span>06:00</span>
          <span>12:00</span>
          <span>18:00</span>
          <span>23:00</span>
        </div>
      </div>
    </div>
  );
}

function TabPorts({ device }: { device: DeviceDetailProps["device"] }) {
  if (!device.ports) return <div className="detail-tab-content"><p>No port data available</p></div>;

  const ports = Array.from({ length: device.ports.total }, (_, i) => ({
    id: i + 1,
    active: i < device.ports!.active,
    speed: i < device.ports!.active ? "1G" : "—",
    poe: Math.random() > 0.5,
  }));

  return (
    <div className="detail-tab-content">
      <div className="detail-section">
        <h4>Port Status ({device.ports.active}/{device.ports.total} Active)</h4>
        <div className="detail-ports-grid">
          {ports.map((port) => (
            <div key={port.id} className={`detail-port ${port.active ? "active" : "inactive"}`}>
              <span className="port-num">{port.id}</span>
              {port.poe && port.active && <span className="port-poe-badge">PoE</span>}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function TabClients({ device }: { device: DeviceDetailProps["device"] }) {
  if (device.clients === undefined) return <div className="detail-tab-content"><p>No client data available</p></div>;

  const mockClients = Array.from({ length: Math.min(device.clients, 10) }, (_, i) => ({
    id: i + 1,
    name: `Client-${100 + i}`,
    ip: `192.168.1.${100 + i}`,
    usage: `${(Math.random() * 5).toFixed(1)} GB`,
    signal: Math.random() > 0.5 ? "strong" : "medium",
  }));

  return (
    <div className="detail-tab-content">
      <div className="detail-section">
        <h4>Connected Clients ({device.clients})</h4>
        <div className="detail-clients-list">
          {mockClients.map((c) => (
            <div key={c.id} className="detail-client-row">
              <div className="detail-client-info">
                <strong>{c.name}</strong>
                <span>{c.ip}</span>
              </div>
              <div className="detail-client-stats">
                <span>{c.usage}</span>
                <Tag value={c.signal} severity={c.signal === "strong" ? "success" : "warning"} />
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function TabConfig({ device }: { device: DeviceDetailProps["device"] }) {
  return (
    <div className="detail-tab-content">
      <div className="detail-section">
        <h4>Network Configuration</h4>
        <div className="detail-config-grid">
          <div className="detail-config-item">
            <label>Device Name</label>
            <input type="text" defaultValue={device.name} />
          </div>
          <div className="detail-config-item">
            <label>Location</label>
            <input type="text" defaultValue={device.location || ""} />
          </div>
          <div className="detail-config-item">
            <label>IP Address</label>
            <input type="text" defaultValue={device.ip} />
          </div>
        </div>
      </div>

      <div className="detail-section">
        <h4>Actions</h4>
        <div className="detail-actions">
          <Button label="Restart Device" icon="pi pi-refresh" severity="warning" />
          <Button label="Factory Reset" icon="pi pi-trash" severity="danger" />
          <Button label="Adopt" icon="pi pi-check" severity="success" />
        </div>
      </div>
    </div>
  );
}

export function DeviceDetailPanel({ device, onClose }: DeviceDetailProps) {
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", handleEscape);
    return () => document.removeEventListener("keydown", handleEscape);
  }, [onClose]);

  return (
    <>
      <div className="detail-backdrop" onClick={onClose} />
      <div className="detail-panel">
        <div className="detail-header">
          <div className="detail-header-info">
            <h2>{device.name}</h2>
            <p>{device.model}</p>
          </div>
          <button className="icon-button" onClick={onClose} aria-label="Close">
            <i className="pi pi-times" />
          </button>
        </div>

        <TabView>
          <TabPanel header="Overview">
            <TabOverview device={device} />
          </TabPanel>
          <TabPanel header="Performance">
            <TabPerformance device={device} />
          </TabPanel>
          <TabPanel header="Ports">
            <TabPorts device={device} />
          </TabPanel>
          <TabPanel header="Clients">
            <TabClients device={device} />
          </TabPanel>
          <TabPanel header="Config">
            <TabConfig device={device} />
          </TabPanel>
        </TabView>
      </div>
    </>
  );
}
