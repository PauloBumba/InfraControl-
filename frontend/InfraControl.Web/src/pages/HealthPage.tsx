import { Tag } from "primereact/tag";
import { ProgressBar } from "primereact/progressbar";

interface HealthCheck {
  name: string;
  status: "healthy" | "degraded" | "unhealthy";
  latency: number;
  uptime: string;
  lastCheck: string;
}

const healthChecks: HealthCheck[] = [
  { name: "PostgreSQL", status: "healthy", latency: 2, uptime: "99.99%", lastCheck: "Agora" },
  { name: "Redis", status: "healthy", latency: 1, uptime: "99.99%", lastCheck: "Agora" },
  { name: "RabbitMQ", status: "healthy", latency: 5, uptime: "99.95%", lastCheck: "Agora" },
  { name: "API Backend", status: "healthy", latency: 12, uptime: "99.90%", lastCheck: "Agora" },
  { name: "Frontend", status: "healthy", latency: 3, uptime: "99.99%", lastCheck: "Agora" },
  { name: "OpenTelemetry", status: "degraded", latency: 45, uptime: "98.50%", lastCheck: "Agora" },
  { name: "Serilog", status: "healthy", latency: 1, uptime: "99.99%", lastCheck: "Agora" },
  { name: "Health Check Service", status: "healthy", latency: 2, uptime: "99.99%", lastCheck: "Agora" },
];

function statusTag(status: HealthCheck["status"]) {
  const config = {
    healthy: { severity: "success" as const, label: "Healthy" },
    degraded: { severity: "warning" as const, label: "Degraded" },
    unhealthy: { severity: "danger" as const, label: "Unhealthy" },
  };
  const { severity, label } = config[status];
  return <Tag value={label} severity={severity} />;
}

function latencyColor(ms: number) {
  if (ms <= 5) return "var(--status-green)";
  if (ms <= 20) return "var(--accent)";
  if (ms <= 50) return "var(--status-yellow)";
  return "var(--status-red)";
}

export function HealthPage() {
  const healthyCount = healthChecks.filter((h) => h.status === "healthy").length;
  const totalCount = healthChecks.length;

  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">SAÚDE DO SISTEMA</p>
          <h1>Health Checks</h1>
          <p className="heading-subtitle">{healthyCount}/{totalCount} services healthy</p>
        </div>
      </section>

      <div className="health-grid">
        {healthChecks.map((hc) => (
          <article key={hc.name} className="panel health-card">
            <div className="health-card-header">
              <h3>{hc.name}</h3>
              {statusTag(hc.status)}
            </div>
            <div className="health-card-body">
              <div className="health-metric">
                <span>Latency</span>
                <strong style={{ color: latencyColor(hc.latency) }}>{hc.latency}ms</strong>
              </div>
              <div className="health-metric">
                <span>Uptime</span>
                <strong>{hc.uptime}</strong>
              </div>
              <div className="health-metric">
                <span>Last Check</span>
                <strong>{hc.lastCheck}</strong>
              </div>
            </div>
            <ProgressBar value={hc.status === "healthy" ? 100 : hc.status === "degraded" ? 70 : 20} showValue={false} style={{ height: "4px" }} />
          </article>
        ))}
      </div>
    </div>
  );
}
