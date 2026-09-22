import { useState, useEffect } from "react";

type ConnectionStatus = "online" | "warning" | "offline";

interface ConnectionDot {
  id: string;
  label: string;
  icon: string;
  status: ConnectionStatus;
}

const defaultConnections: ConnectionDot[] = [
  { id: "wan", label: "WAN", icon: "pi pi-globe", status: "online" },
  { id: "www", label: "WWW", icon: "pi pi-cloud", status: "online" },
  { id: "wlan", label: "WLAN", icon: "pi pi-wifi", status: "warning" },
  { id: "lan", label: "LAN", icon: "pi pi-server", status: "online" },
  { id: "vpn", label: "VPN", icon: "pi pi-lock", status: "online" },
];

function randomStatus(): ConnectionStatus {
  const r = Math.random();
  if (r < 0.33) return "online";
  if (r < 0.66) return "warning";
  return "offline";
}

function StatusDot({ status }: { status: ConnectionStatus }) {
  return (
    <span
      className={`status-dot ${status}`}
      aria-label={status === "online" ? "Online" : status === "warning" ? "Atenção" : "Offline"}
    />
  );
}

export function ConnectivityDots() {
  const [connections, setConnections] = useState<ConnectionDot[]>(defaultConnections);

  useEffect(() => {
    const interval = setInterval(() => {
      setConnections((prev) =>
        prev.map((conn) => (Math.random() > 0.95 ? { ...conn, status: randomStatus() } : conn))
      );
    }, 5000);
    return () => clearInterval(interval);
  }, []);

  return (
    <article className="panel connectivity-dots">
      <div className="panel-heading">
        <div>
          <h2>Connectivity</h2>
          <p>Network status overview</p>
        </div>
      </div>
      <div className="dots-container">
        <div className="dots-map">
          {connections.map((conn, index) => (
            <div key={conn.id} style={{ display: "contents" }}>
              <div className={`dot-item ${conn.status}`}>
                <div className="dot-icon">
                  <StatusDot status={conn.status} />
                  <i className={conn.icon} />
                </div>
                <span className="dot-label">{conn.label}</span>
              </div>
              {index < connections.length - 1 && (
                <div className="dot-connection">
                  <span className="connection-line" />
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </article>
  );
}
