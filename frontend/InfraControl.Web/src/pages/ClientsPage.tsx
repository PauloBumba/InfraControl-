import { useState, useMemo } from "react";
import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { InputText } from "primereact/inputtext";
import { Dropdown } from "primereact/dropdown";
import { Tag } from "primereact/tag";
import { Button } from "primereact/button";

interface Client {
  id: string;
  hostname: string;
  mac: string;
  ip: string;
  ap: string;
  vlan: string;
  usage: { rx: number; tx: number };
  signal: "strong" | "medium" | "weak";
  os: string;
  lastSeen: string;
  isOnline: boolean;
}

const clients: Client[] = [
  { id: "1", hostname: "iPhone-15-Pro", mac: "AA:BB:CC:DD:EE:01", ip: "192.168.1.101", ap: "AP-AC-PRO", vlan: "VLAN 10", usage: { rx: 2400, tx: 800 }, signal: "strong", os: "iOS 18.1", lastSeen: "Agora", isOnline: true },
  { id: "2", hostname: "MacBook-Pro", mac: "AA:BB:CC:DD:EE:02", ip: "192.168.1.102", ap: "AP-AC-PRO", vlan: "VLAN 10", usage: { rx: 5100, tx: 1200 }, signal: "strong", os: "macOS Sequoia", lastSeen: "Agora", isOnline: true },
  { id: "3", hostname: "Samsung-QN90", mac: "AA:BB:CC:DD:EE:03", ip: "192.168.1.103", ap: "AP-AC-PRO", vlan: "VLAN 20", usage: { rx: 8700, tx: 200 }, signal: "medium", os: "Tizen 8.0", lastSeen: "Agora", isOnline: true },
  { id: "4", hostname: "PS5-Slim", mac: "AA:BB:CC:DD:EE:04", ip: "192.168.1.104", ap: "AP-AC-PRO", vlan: "VLAN 20", usage: { rx: 12300, tx: 3400 }, signal: "strong", os: "PS5 OS", lastSeen: "Agora", isOnline: true },
  { id: "5", hostname: "Work-Laptop", mac: "AA:BB:CC:DD:EE:05", ip: "192.168.1.105", ap: "AP-AC-LR", vlan: "VLAN 30", usage: { rx: 1800, tx: 900 }, signal: "strong", os: "Windows 11", lastSeen: "Agora", isOnline: true },
  { id: "6", hostname: "iPad-Air", mac: "AA:BB:CC:DD:EE:06", ip: "192.168.1.106", ap: "AP-AC-LR", vlan: "VLAN 10", usage: { rx: 900, tx: 150 }, signal: "medium", os: "iPadOS 18.1", lastSeen: "Agora", isOnline: true },
  { id: "7", hostname: "Ecobee-Thermostat", mac: "AA:BB:CC:DD:EE:07", ip: "192.168.1.107", ap: "AP-U6-Lite", vlan: "VLAN 40", usage: { rx: 100, tx: 20 }, signal: "weak", os: "Embedded", lastSeen: "5 min atrás", isOnline: true },
  { id: "8", hostname: "Ring-Doorbell", mac: "AA:BB:CC:DD:EE:08", ip: "192.168.1.108", ap: "AP-AC-PRO", vlan: "VLAN 40", usage: { rx: 15200, tx: 8900 }, signal: "strong", os: "RingOS", lastSeen: "Agora", isOnline: true },
  { id: "9", hostname: "Printer-HP", mac: "AA:BB:CC:DD:EE:09", ip: "192.168.1.109", ap: "US-24-250W", vlan: "VLAN 30", usage: { rx: 200, tx: 50 }, signal: "strong", os: "HP Firmware", lastSeen: "1h atrás", isOnline: false },
  { id: "10", hostname: "Desktop-PC", mac: "AA:BB:CC:DD:EE:10", ip: "192.168.1.110", ap: "US-24-250W", vlan: "VLAN 10", usage: { rx: 3200, tx: 1100 }, signal: "strong", os: "Windows 11", lastSeen: "Agora", isOnline: true },
  { id: "11", hostname: "Nest-Hub", mac: "AA:BB:CC:DD:EE:11", ip: "192.168.1.111", ap: "AP-AC-LR", vlan: "VLAN 40", usage: { rx: 500, tx: 30 }, signal: "medium", os: "Cast OS", lastSeen: "Agora", isOnline: true },
  { id: "12", hostname: "Smart-Lock", mac: "AA:BB:CC:DD:EE:12", ip: "192.168.1.112", ap: "AP-U6-Lite", vlan: "VLAN 40", usage: { rx: 50, tx: 10 }, signal: "weak", os: "Embedded", lastSeen: "2h atrás", isOnline: false },
];

const apOptions = [
  { label: "All Access Points", value: null },
  { label: "AP-AC-PRO", value: "AP-AC-PRO" },
  { label: "AP-AC-LR", value: "AP-AC-LR" },
  { label: "AP-U6-Lite", value: "AP-U6-Lite" },
  { label: "US-24-250W", value: "US-24-250W" },
];

const vlanOptions = [
  { label: "All VLANs", value: null },
  { label: "VLAN 10 - Staff", value: "VLAN 10" },
  { label: "VLAN 20 - Guest", value: "VLAN 20" },
  { label: "VLAN 30 - IoT", value: "VLAN 30" },
  { label: "VLAN 40 - IoT", value: "VLAN 40" },
];

function formatBytes(bytes: number): string {
  if (bytes >= 1000) return `${(bytes / 1000).toFixed(1)} GB`;
  return `${bytes} MB`;
}

function signalBody(rowData: Client) {
  const config = {
    strong: { severity: "success" as const, label: "Strong" },
    medium: { severity: "warning" as const, label: "Medium" },
    weak: { severity: "danger" as const, label: "Weak" },
  };
  const { severity, label } = config[rowData.signal];
  return <Tag value={label} severity={severity} />;
}

function usageBody(rowData: Client) {
  return (
    <div>
      <span style={{ color: "var(--status-green)" }}>↓ {formatBytes(rowData.usage.rx)}</span>
      <br />
      <span style={{ color: "var(--accent)" }}>↑ {formatBytes(rowData.usage.tx)}</span>
    </div>
  );
}

function statusBody(rowData: Client) {
  return (
    <span style={{ display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
      <span
        style={{
          width: "8px",
          height: "8px",
          borderRadius: "50%",
          background: rowData.isOnline ? "var(--status-green)" : "var(--text-muted)",
        }}
      />
      {rowData.isOnline ? "Online" : "Offline"}
    </span>
  );
}

export function ClientsPage() {
  const [search, setSearch] = useState("");
  const [apFilter, setApFilter] = useState<string | null>(null);
  const [vlanFilter, setVlanFilter] = useState<string | null>(null);
  const [onlineOnly, setOnlineOnly] = useState(false);

  const filteredClients = useMemo(() => {
    return clients.filter((c) => {
      const matchSearch = !search || c.hostname.toLowerCase().includes(search.toLowerCase()) || c.ip.includes(search) || c.mac.toLowerCase().includes(search.toLowerCase());
      const matchAp = !apFilter || c.ap === apFilter;
      const matchVlan = !vlanFilter || c.vlan === vlanFilter;
      const matchOnline = !onlineOnly || c.isOnline;
      return matchSearch && matchAp && matchVlan && matchOnline;
    });
  }, [search, apFilter, vlanFilter, onlineOnly]);

  const onlineCount = clients.filter((c) => c.isOnline).length;
  const totalRx = clients.reduce((acc, c) => acc + c.usage.rx, 0);

  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">CLIENTES</p>
          <h1>Clients</h1>
          <p className="heading-subtitle">{onlineCount} online / {clients.length} total — {formatBytes(totalRx)} total usage</p>
        </div>
        <Button label="Export CSV" icon="pi pi-download" severity="secondary" />
      </section>

      <div className="panel">
        <div className="panel-heading" style={{ flexWrap: "wrap", gap: "var(--space-3)" }}>
          <div style={{ display: "flex", gap: "var(--space-3)", flexWrap: "wrap", flex: 1 }}>
            <span className="p-input-icon-left">
              <i className="pi pi-search" />
              <InputText value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by name, IP or MAC..." />
            </span>
            <Dropdown value={apFilter} onChange={(e) => setApFilter(e.value)} options={apOptions} optionLabel="label" optionValue="value" />
            <Dropdown value={vlanFilter} onChange={(e) => setVlanFilter(e.value)} options={vlanOptions} optionLabel="label" optionValue="value" />
            <Button
              label={onlineOnly ? "Online Only" : "All Clients"}
              icon={onlineOnly ? "pi pi-check-circle" : "pi pi-circle"}
              severity={onlineOnly ? "success" : "secondary"}
              onClick={() => setOnlineOnly(!onlineOnly)}
            />
          </div>
        </div>

        <DataTable
          value={filteredClients}
          rows={10}
          rowsPerPageOptions={[10, 25, 50]}
          paginator
          stripedRows
          selectionMode="single"
          tableStyle={{ minWidth: "100%" }}
          emptyMessage="No clients found"
        >
          <Column body={statusBody} style={{ width: "60px" }} />
          <Column field="hostname" header="Hostname" sortable />
          <Column field="ip" header="IP Address" sortable />
          <Column field="mac" header="MAC Address" />
          <Column field="ap" header="Access Point" sortable />
          <Column field="vlan" header="VLAN" />
          <Column body={signalBody} header="Signal" sortable sortField="signal" />
          <Column body={usageBody} header="Usage" sortable sortField="usage.rx" />
          <Column field="os" header="OS" />
          <Column field="lastSeen" header="Last Seen" />
        </DataTable>
      </div>
    </div>
  );
}
