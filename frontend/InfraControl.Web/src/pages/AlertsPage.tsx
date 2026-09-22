import { useState, useMemo } from "react";
import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { Tag } from "primereact/tag";
import { Dropdown } from "primereact/dropdown";
import { Button } from "primereact/button";
import { InputText } from "primereact/inputtext";

interface Alert {
  id: string;
  severity: "critical" | "warning" | "info";
  title: string;
  message: string;
  source: string;
  time: string;
  acknowledged: boolean;
}

const alerts: Alert[] = [
  { id: "1", severity: "critical", title: "Port 24 Link Down", message: "Port 24 on US-16-150W has lost connectivity", source: "US-16-150W", time: "2 min atrás", acknowledged: false },
  { id: "2", severity: "critical", title: "Device Offline", message: "US-16-150W is unreachable since 14:32", source: "Controller", time: "5 min atrás", acknowledged: false },
  { id: "3", severity: "warning", title: "High CPU Usage", message: "UDM Pro Max CPU at 87% for 10 minutes", source: "UDM Pro Max", time: "12 min atrás", acknowledged: false },
  { id: "4", severity: "warning", title: "Rogue AP Detected", message: "Unknown AP detected on channel 6 (2.4GHz)", source: "IDS/IPS", time: "18 min atrás", acknowledged: true },
  { id: "5", severity: "warning", title: "Memory Usage High", message: "AP-AC-PRO memory usage at 82%", source: "AP-AC-PRO", time: "25 min atrás", acknowledged: false },
  { id: "6", severity: "info", title: "Firmware Update Available", message: "New firmware 6.6.56 available for switches", source: "Controller", time: "1h atrás", acknowledged: true },
  { id: "7", severity: "info", title: "Client Roamed", message: "iPhone-15-Pro roamed from AP-AC-PRO to AP-AC-LR", source: "Controller", time: "1h atrás", acknowledged: true },
  { id: "8", severity: "info", title: "Backup Completed", message: "Automatic backup completed successfully", source: "Controller", time: "2h atrás", acknowledged: true },
  { id: "9", severity: "warning", title: "DHCP Pool Low", message: "VLAN 10 DHCP pool at 85% capacity", source: "UDM Pro Max", time: "3h atrás", acknowledged: false },
  { id: "10", severity: "critical", title: "VPN Tunnel Down", message: "Site-to-site VPN to Branch Office disconnected", source: "UDM Pro Max", time: "4h atrás", acknowledged: true },
  { id: "11", severity: "info", title: "Speed Test Completed", message: "WAN speed test: 920 Mbps down / 450 Mbps up", source: "UDM Pro Max", time: "6h atrás", acknowledged: true },
  { id: "12", severity: "warning", title: "Spanning Tree Change", message: "Root bridge changed on VLAN 20", source: "US-24-250W", time: "8h atrás", acknowledged: true },
];

const severityOptions = [
  { label: "All Severities", value: null },
  { label: "Critical", value: "critical" },
  { label: "Warning", value: "warning" },
  { label: "Info", value: "info" },
];

function severityBody(rowData: Alert) {
  const config = {
    critical: { severity: "danger" as const, icon: "pi pi-exclamation-triangle" },
    warning: { severity: "warning" as const, icon: "pi pi-exclamation-circle" },
    info: { severity: "info" as const, icon: "pi pi-info-circle" },
  };
  const { severity, icon } = config[rowData.severity];
  return <Tag icon={icon} severity={severity} />;
}

function statusBody(rowData: Alert) {
  return rowData.acknowledged
    ? <Tag value="Acknowledged" severity="success" />
    : <Tag value="New" severity="danger" />;
}

export function AlertsPage() {
  const [search, setSearch] = useState("");
  const [severityFilter, setSeverityFilter] = useState<string | null>(null);
  const [showAcked, setShowAcked] = useState(true);

  const filtered = useMemo(() => {
    return alerts.filter((a) => {
      const matchSearch = !search || a.title.toLowerCase().includes(search.toLowerCase()) || a.source.toLowerCase().includes(search.toLowerCase());
      const matchSeverity = !severityFilter || a.severity === severityFilter;
      const matchAcked = showAcked || !a.acknowledged;
      return matchSearch && matchSeverity && matchAcked;
    });
  }, [search, severityFilter, showAcked]);

  const criticalCount = alerts.filter((a) => a.severity === "critical" && !a.acknowledged).length;
  const warningCount = alerts.filter((a) => a.severity === "warning" && !a.acknowledged).length;

  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">ALERTAS</p>
          <h1>Alerts</h1>
          <p className="heading-subtitle">{criticalCount} critical, {warningCount} warning unacknowledged</p>
        </div>
        <div style={{ display: "flex", gap: "var(--space-2)" }}>
          <Button label="Acknowledge All" icon="pi pi-check" severity="success" />
          <Button label="Mark All Read" icon="pi pi-eye" severity="secondary" />
        </div>
      </section>

      <div className="panel">
        <div className="panel-heading" style={{ flexWrap: "wrap", gap: "var(--space-3)" }}>
          <div style={{ display: "flex", gap: "var(--space-3)", flexWrap: "wrap", flex: 1 }}>
            <span className="p-input-icon-left">
              <i className="pi pi-search" />
              <InputText value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search alerts..." />
            </span>
            <Dropdown value={severityFilter} onChange={(e) => setSeverityFilter(e.value)} options={severityOptions} optionLabel="label" optionValue="value" />
            <Button
              label={showAcked ? "All Alerts" : "Unacknowledged"}
              icon={showAcked ? "pi pi-eye" : "pi pi-eye-slash"}
              severity={showAcked ? "secondary" : "warning"}
              onClick={() => setShowAcked(!showAcked)}
            />
          </div>
        </div>

        <DataTable value={filtered} rows={10} rowsPerPageOptions={[10, 25]} paginator stripedRows tableStyle={{ minWidth: "100%" }} emptyMessage="No alerts found">
          <Column body={severityBody} style={{ width: "80px" }} />
          <Column field="title" header="Title" />
          <Column field="message" header="Message" />
          <Column field="source" header="Source" />
          <Column field="time" header="Time" />
          <Column body={statusBody} header="Status" />
        </DataTable>
      </div>
    </div>
  );
}
