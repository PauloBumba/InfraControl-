import { useState, useMemo } from "react";
import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { Tag } from "primereact/tag";
import { Dropdown } from "primereact/dropdown";
import { InputText } from "primereact/inputtext";

interface LogEntry {
  id: string;
  timestamp: string;
  level: "error" | "warn" | "info" | "debug";
  source: string;
  message: string;
}

const logs: LogEntry[] = [
  { id: "1", timestamp: "14:32:15", level: "error", source: "US-16-150W", message: "Port 24 link state changed to DOWN" },
  { id: "2", timestamp: "14:32:12", level: "error", source: "Controller", message: "Device US-16-150W became unreachable" },
  { id: "3", timestamp: "14:28:03", level: "warn", source: "UDM Pro Max", message: "CPU usage exceeded 80% threshold (87%)" },
  { id: "4", timestamp: "14:25:41", level: "info", source: "AP-AC-PRO", message: "Client iPhone-15-Pro connected via 5GHz" },
  { id: "5", timestamp: "14:22:18", level: "warn", source: "IDS/IPS", message: "Rogue AP detected on channel 6 (2.4GHz)" },
  { id: "6", timestamp: "14:18:55", level: "info", source: "AP-AC-LR", message: "Client MacBook-Pro roamed from AP-AC-PRO" },
  { id: "7", timestamp: "14:15:30", level: "info", source: "UDM Pro Max", message: "DHCP lease renewed for 192.168.1.104" },
  { id: "8", timestamp: "14:10:22", level: "info", source: "Controller", message: "Backup completed successfully (45MB)" },
  { id: "9", timestamp: "14:05:11", level: "warn", source: "AP-AC-PRO", message: "Memory usage at 82% (threshold: 80%)" },
  { id: "10", timestamp: "14:00:00", level: "info", source: "UDM Pro Max", message: "WAN speed test: 920 Mbps down / 450 Mbps up" },
  { id: "11", timestamp: "13:55:44", level: "info", source: "US-24-250W", message: "Port 12 speed negotiated at 2.5Gbps" },
  { id: "12", timestamp: "13:50:33", level: "debug", source: "Controller", message: "SNMP poll completed for 7 devices" },
  { id: "13", timestamp: "13:45:22", level: "info", source: "AP-U6-Lite", message: "Device adoption in progress" },
  { id: "14", timestamp: "13:40:11", level: "info", source: "UDM Pro Max", message: "Site-to-site VPN tunnel established" },
  { id: "15", timestamp: "13:35:00", level: "info", source: "Controller", message: "Scheduled speed test started" },
];

const levelOptions = [
  { label: "All Levels", value: null },
  { label: "Error", value: "error" },
  { label: "Warning", value: "warn" },
  { label: "Info", value: "info" },
  { label: "Debug", value: "debug" },
];

function levelBody(rowData: LogEntry) {
  const config = {
    error: { severity: "danger" as const, label: "ERROR" },
    warn: { severity: "warning" as const, label: "WARN" },
    info: { severity: "info" as const, label: "INFO" },
    debug: { severity: "secondary" as const, label: "DEBUG" },
  };
  const { severity, label } = config[rowData.level];
  return <Tag value={label} severity={severity} />;
}

export function LogsPage() {
  const [search, setSearch] = useState("");
  const [levelFilter, setLevelFilter] = useState<string | null>(null);

  const filtered = useMemo(() => {
    return logs.filter((l) => {
      const matchSearch = !search || l.message.toLowerCase().includes(search.toLowerCase()) || l.source.toLowerCase().includes(search.toLowerCase());
      const matchLevel = !levelFilter || l.level === levelFilter;
      return matchSearch && matchLevel;
    });
  }, [search, levelFilter]);

  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">LOGS</p>
          <h1>System Logs</h1>
          <p className="heading-subtitle">{logs.length} log entries</p>
        </div>
      </section>

      <div className="panel">
        <div className="panel-heading" style={{ flexWrap: "wrap", gap: "var(--space-3)" }}>
          <div style={{ display: "flex", gap: "var(--space-3)", flexWrap: "wrap", flex: 1 }}>
            <span className="p-input-icon-left">
              <i className="pi pi-search" />
              <InputText value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search logs..." />
            </span>
            <Dropdown value={levelFilter} onChange={(e) => setLevelFilter(e.value)} options={levelOptions} optionLabel="label" optionValue="value" />
          </div>
        </div>

        <DataTable value={filtered} rows={15} rowsPerPageOptions={[15, 30, 50]} paginator stripedRows tableStyle={{ minWidth: "100%" }} emptyMessage="No logs found">
          <Column field="timestamp" header="Time" style={{ width: "100px" }} />
          <Column body={levelBody} header="Level" style={{ width: "80px" }} />
          <Column field="source" header="Source" style={{ width: "150px" }} />
          <Column field="message" header="Message" />
        </DataTable>
      </div>
    </div>
  );
}
