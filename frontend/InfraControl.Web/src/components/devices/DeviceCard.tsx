import { Tag } from "primereact/tag";

export type DeviceType = "gateway" | "switch" | "ap";
export type DeviceStatus = "online" | "offline" | "adopting" | "upgrading" | "failed";

interface DeviceCardProps {
  name: string;
  model: string;
  type: DeviceType;
  status: DeviceStatus;
  ip: string;
  mac: string;
  uptime?: string;
  clients?: number;
  ports?: { total: number; active: number };
  cpu?: number;
  memory?: number;
  firmware?: string;
  location?: string;
  onClick?: () => void;
}

const statusConfig: Record<DeviceStatus, { severity: "success" | "danger" | "warning" | "info" | "secondary"; label: string }> = {
  online: { severity: "success", label: "Online" },
  offline: { severity: "danger", label: "Offline" },
  adopting: { severity: "info", label: "Adopting" },
  upgrading: { severity: "warning", label: "Upgrading" },
  failed: { severity: "danger", label: "Failed" },
};

function getDeviceIcon(type: DeviceType) {
  switch (type) {
    case "gateway":
      return (
        <svg viewBox="0 0 48 48" width="48" height="48" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="8" y="14" width="32" height="22" rx="3" />
          <circle cx="16" cy="25" r="2" fill="currentColor" />
          <circle cx="24" cy="25" r="2" fill="currentColor" />
          <circle cx="32" cy="25" r="2" fill="currentColor" />
          <line x1="24" y1="4" x2="24" y2="14" />
          <circle cx="24" cy="4" r="2" fill="currentColor" />
        </svg>
      );
    case "switch":
      return (
        <svg viewBox="0 0 48 48" width="48" height="48" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="4" y="16" width="40" height="16" rx="2" />
          {[10, 16, 22, 28, 34].map((x) => (
            <g key={x}>
              <rect x={x} y="20" width="4" height="4" rx="0.5" fill="currentColor" opacity="0.3" />
              <rect x={x} y="26" width="4" height="4" rx="0.5" fill="currentColor" opacity="0.3" />
            </g>
          ))}
        </svg>
      );
    case "ap":
      return (
        <svg viewBox="0 0 48 48" width="48" height="48" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="24" cy="28" r="4" fill="currentColor" />
          <path d="M16 22a11.3 11.3 0 0 1 16 0" strokeLinecap="round" />
          <path d="M11 17a18 18 0 0 1 26 0" strokeLinecap="round" />
          <path d="M6 12a25 25 0 0 1 36 0" strokeLinecap="round" />
        </svg>
      );
  }
}

export function DeviceCard({
  name,
  model,
  type,
  status,
  ip,
  uptime,
  clients,
  ports,
  cpu,
  memory,
  firmware,
  location,
  onClick,
}: DeviceCardProps) {
  const { severity, label } = statusConfig[status];

  return (
    <article className="device-card" onClick={onClick} role="button" tabIndex={0}>
      <div className="device-card-header">
        <div className={`device-card-icon ${type}`}>
          {getDeviceIcon(type)}
        </div>
        <div className="device-card-info">
          <h3>{name}</h3>
          <p>{model}</p>
        </div>
        <Tag value={label} severity={severity} />
      </div>

      <div className="device-card-details">
        <div className="device-detail">
          <i className="pi pi-globe" />
          <span>{ip}</span>
        </div>
        {location && (
          <div className="device-detail">
            <i className="pi pi-map-marker" />
            <span>{location}</span>
          </div>
        )}
        {uptime && (
          <div className="device-detail">
            <i className="pi pi-clock" />
            <span>{uptime}</span>
          </div>
        )}
        {firmware && (
          <div className="device-detail">
            <i className="pi pi-code" />
            <span>{firmware}</span>
          </div>
        )}
      </div>

      <div className="device-card-stats">
        {ports && (
          <div className="device-stat">
            <i className="pi pi-link" />
            <span>{ports.active}/{ports.total} Ports</span>
          </div>
        )}
        {clients !== undefined && (
          <div className="device-stat">
            <i className="pi pi-users" />
            <span>{clients} Clients</span>
          </div>
        )}
        {cpu !== undefined && (
          <div className="device-stat">
            <i className="pi pi-microchip" />
            <span>{cpu}% CPU</span>
          </div>
        )}
        {memory !== undefined && (
          <div className="device-stat">
            <i className="pi pi-database" />
            <span>{memory}% RAM</span>
          </div>
        )}
      </div>
    </article>
  );
}
