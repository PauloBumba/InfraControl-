import { useState } from "react";
import { InputText } from "primereact/inputtext";
import { Dropdown } from "primereact/dropdown";
import { DeviceCard, type DeviceType, type DeviceStatus } from "@/components/devices/DeviceCard";
import { DeviceDetailPanel } from "@/components/devices/DeviceDetailPanel";

interface Device {
  id: string;
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
  temperature?: number;
  firmware?: string;
  location?: string;
  wan?: { up: number; down: number };
}

const devices: Device[] = [
  {
    id: "1", name: "UDM Pro Max", model: "Dream Machine Pro Max", type: "gateway", status: "online",
    ip: "192.168.1.1", mac: "78:8A:20:AA:BB:01", uptime: "45d 12h 30m", cpu: 23, memory: 67,
    temperature: 42, firmware: "4.1.22", location: "Server Room", wan: { up: 450, down: 920 },
  },
  {
    id: "2", name: "US-24-250W", model: "UniFi Switch 24 PoE", type: "switch", status: "online",
    ip: "192.168.1.2", mac: "78:8A:20:AA:BB:02", uptime: "30d 8h 15m", ports: { total: 24, active: 18 },
    clients: 45, firmware: "6.6.55", location: "Server Room",
  },
  {
    id: "3", name: "US-8-150W", model: "UniFi Switch 8 PoE", type: "switch", status: "online",
    ip: "192.168.1.3", mac: "78:8A:20:AA:BB:03", uptime: "22d 5h 42m", ports: { total: 8, active: 6 },
    clients: 12, firmware: "6.6.55", location: "Office Floor 2",
  },
  {
    id: "4", name: "AP-AC-PRO", model: "UniFi AP AC Pro", type: "ap", status: "online",
    ip: "192.168.1.10", mac: "78:8A:20:AA:BB:04", uptime: "60d 3h 22m", clients: 28,
    firmware: "6.6.55", location: "Office Floor 1",
  },
  {
    id: "5", name: "AP-AC-LR", model: "UniFi AP AC Long Range", type: "ap", status: "online",
    ip: "192.168.1.11", mac: "78:8A:20:AA:BB:05", uptime: "60d 3h 22m", clients: 15,
    firmware: "6.6.55", location: "Warehouse",
  },
  {
    id: "6", name: "AP-U6-Lite", model: "UniFi AP 6 Lite", type: "ap", status: "adopting",
    ip: "192.168.1.12", mac: "78:8A:20:AA:BB:06", firmware: "6.6.55", location: "Parking Lot",
  },
  {
    id: "7", name: "US-16-150W", model: "UniFi Switch 16 PoE", type: "switch", status: "offline",
    ip: "192.168.1.4", mac: "78:8A:20:AA:BB:07", ports: { total: 16, active: 0 },
    firmware: "6.6.55", location: "Basement",
  },
];

const deviceTypes = [
  { label: "All Types", value: null },
  { label: "Gateway", value: "gateway" },
  { label: "Switch", value: "switch" },
  { label: "Access Point", value: "ap" },
];

const statusOptions = [
  { label: "All Status", value: null },
  { label: "Online", value: "online" },
  { label: "Offline", value: "offline" },
  { label: "Adopting", value: "adopting" },
];

export function DevicesPage() {
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState<DeviceType | null>(null);
  const [statusFilter, setStatusFilter] = useState<DeviceStatus | null>(null);
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null);

  const filteredDevices = devices.filter((d) => {
    const matchesSearch = !search || d.name.toLowerCase().includes(search.toLowerCase()) || d.ip.includes(search);
    const matchesType = !typeFilter || d.type === typeFilter;
    const matchesStatus = !statusFilter || d.status === statusFilter;
    return matchesSearch && matchesType && matchesStatus;
  });

  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">DISPOSITIVOS</p>
          <h1>Devices</h1>
          <p className="heading-subtitle">{devices.length} devices managed</p>
        </div>
        <button className="primary-button">
          <i className="pi pi-plus" /> Add Device
        </button>
      </section>

      <div style={{ display: "flex", gap: "var(--space-3)", flexWrap: "wrap" }}>
        <span className="p-input-icon-left">
          <i className="pi pi-search" />
          <InputText value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search devices..." />
        </span>
        <Dropdown value={typeFilter} onChange={(e) => setTypeFilter(e.value)} options={deviceTypes} optionLabel="label" optionValue="value" placeholder="All Types" />
        <Dropdown value={statusFilter} onChange={(e) => setStatusFilter(e.value)} options={statusOptions} optionLabel="label" optionValue="value" placeholder="All Status" />
      </div>

      <div className="devices-grid">
        {filteredDevices.map((device) => (
          <DeviceCard
            key={device.id}
            name={device.name}
            model={device.model}
            type={device.type}
            status={device.status}
            ip={device.ip}
            mac={device.mac}
            uptime={device.uptime}
            clients={device.clients}
            ports={device.ports}
            cpu={device.cpu}
            memory={device.memory}
            firmware={device.firmware}
            location={device.location}
            onClick={() => setSelectedDevice(device)}
          />
        ))}
      </div>

      {selectedDevice && (
        <DeviceDetailPanel device={selectedDevice} onClose={() => setSelectedDevice(null)} />
      )}
    </div>
  );
}
