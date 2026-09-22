interface GatewayCardProps {
  name: string;
  model: string;
  ip: string;
  status: "online" | "warning" | "offline";
  uptime: string;
  cpu: number;
  memory: number;
  temperature: number;
  wan: { up: number; down: number };
}

export function GatewayCard({ name, model, ip, status, uptime, cpu, memory, temperature, wan }: GatewayCardProps) {
  return (
    <article className="panel gateway-card">
      <div className="panel-heading">
        <div className="gateway-header">
          <div className={`gateway-status-dot ${status}`} />
          <div>
            <h2>{name}</h2>
            <p>{model}</p>
          </div>
        </div>
        <div className="gateway-ip">
          <i className="pi pi-globe" />
          <span>{ip}</span>
        </div>
      </div>

      <div className="gateway-stats">
        <div className="gateway-stat">
          <span className="stat-label">Uptime</span>
          <span className="stat-value">{uptime}</span>
        </div>
        <div className="gateway-stat">
          <span className="stat-label">CPU</span>
          <div className="stat-bar">
            <div className="stat-bar-fill" style={{ width: `${cpu}%` }} />
          </div>
          <span className="stat-value">{cpu}%</span>
        </div>
        <div className="gateway-stat">
          <span className="stat-label">Memory</span>
          <div className="stat-bar">
            <div className="stat-bar-fill" style={{ width: `${memory}%` }} />
          </div>
          <span className="stat-value">{memory}%</span>
        </div>
        <div className="gateway-stat">
          <span className="stat-label">Temp</span>
          <span className="stat-value">{temperature}°C</span>
        </div>
      </div>

      <div className="gateway-throughput">
        <div className="throughput-item">
          <i className="pi pi-arrow-up" />
          <span className="throughput-value">{wan.up} Mbps</span>
          <span className="throughput-label">Upload</span>
        </div>
        <div className="throughput-divider" />
        <div className="throughput-item">
          <i className="pi pi-arrow-down" />
          <span className="throughput-value">{wan.down} Mbps</span>
          <span className="throughput-label">Download</span>
        </div>
      </div>

      <div className="gateway-actions">
        <button className="text-button">
          <i className="pi pi-cog" /> Configurar
        </button>
        <button className="text-button">
          <i className="pi pi-chart-bar" /> Métricas
        </button>
        <button className="text-button">
          <i className="pi pi-refresh" /> Reiniciar
        </button>
      </div>
    </article>
  );
}
