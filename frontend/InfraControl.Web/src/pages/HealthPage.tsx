import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { MeterGroup } from "primereact/metergroup";

export function HealthPage() {
  const checks = [
    { name: "WAN Connectivity", status: "online", latency: "12ms", uptime: "99.99%", detail: "Google DNS (8.8.8.8)" },
    { name: "DNS Resolution", status: "online", latency: "3ms", uptime: "100%", detail: "Cloudflare (1.1.1.1)" },
    { name: "DHCP Server", status: "online", latency: "1ms", uptime: "100%", detail: "UDM-Pro" },
    { name: "NTP Sync", status: "online", latency: "8ms", uptime: "100%", detail: "pool.ntp.org" },
    { name: "VPN Tunnel", status: "warning", latency: "45ms", uptime: "98.5%", detail: "Site-to-Site BR-US" },
    { name: "Controller DB", status: "online", latency: "2ms", uptime: "100%", detail: "MongoDB local" },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader title="Health Checks" />

      <div className="uni-health-grid">
        {checks.map((check) => (
          <div className="uni-health-card" key={check.name}>
            <div className="uni-health-header">
              <h3>{check.name}</h3>
              <StatusChip
                label={check.status === "online" ? "Saudável" : "Atenção"}
                background={check.status === "online" ? "#dcfce7" : "#fef3c7"}
                color={check.status === "online" ? "#16a34a" : "#d97706"}
                fontWeight={600}
              />
            </div>
            <div className="uni-health-metrics">
              <div className="uni-health-metric">
                <span>Latência</span>
                <strong>{check.latency}</strong>
              </div>
              <div className="uni-health-metric">
                <span>Uptime</span>
                <strong>{check.uptime}</strong>
                <MeterGroup className="uni-meter-group uni-health-meter" values={[{ label: "Uptime", value: Number.parseFloat(check.uptime), color: check.status === "online" ? "#22c55e" : "#f59e0b" }]} max={100} />
              </div>
              <div className="uni-health-metric">
                <span>Destino</span>
                <strong style={{ fontSize: 11 }}>{check.detail}</strong>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
