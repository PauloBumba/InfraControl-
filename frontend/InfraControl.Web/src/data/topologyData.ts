// ============ TOPOLOGY NODES (All Devices) ============
export type DeviceCategory = "network" | "wireless" | "infrastructure" | "endpoint" | "security" | "storage";

export const topologyNodes = [
  // WAN / INTERNET
  { id: "wan", label: "WAN", x: 500, y: 30, type: "wan" as const, status: "online" as const, ip: "200.150.82.34", location: "Externo", rack: "—" },

  // GATEWAY
  { id: "udm", label: "UDM-Pro", x: 500, y: 120, type: "gateway" as const, status: "online" as const, model: "Dream Machine Pro", ip: "192.168.1.1", location: "Rack Principal", rack: "Rack-A / U1", clients: 84 },

  // CORE SWITCHES
  { id: "sw1", label: "Switch-Core-01", x: 250, y: 240, type: "switch" as const, status: "online" as const, model: "Switch Pro 24 PoE", ip: "192.168.1.2", location: "Rack Principal", rack: "Rack-A / U2", ports: 24, poePorts: 12 },
  { id: "sw2", label: "Switch-Office-01", x: 750, y: 240, type: "switch" as const, status: "online" as const, model: "Switch Lite 16 PoE", ip: "192.168.1.3", location: "Rack Escritório", rack: "Rack-B / U1", ports: 16, poePorts: 6 },
  { id: "sw3", label: "Switch-Rede", x: 500, y: 240, type: "switch" as const, status: "offline" as const, model: "Switch Lite 8", ip: "192.168.1.4", location: "Rack Principal", rack: "Rack-A / U3", ports: 8, poePorts: 0 },

  // ACCESS POINTS
  { id: "ap1", label: "AP-Sala", x: 120, y: 370, type: "ap" as const, status: "online" as const, model: "U6 Pro", ip: "192.168.1.10", location: "Sala", rack: "Teto Sala", clients: 24 },
  { id: "ap2", label: "AP-Escritório", x: 300, y: 370, type: "ap" as const, status: "online" as const, model: "U6 Pro", ip: "192.168.1.11", location: "Escritório", rack: "Teto Escritório", clients: 18 },
  { id: "ap3", label: "AP-Pátio", x: 480, y: 370, type: "ap" as const, status: "warning" as const, model: "U6 Mesh", ip: "192.168.1.12", location: "Pátio", rack: "Parede Pátio", clients: 5 },

  // CAMERAS
  { id: "cam1", label: "Camera-Garagem", x: 660, y: 370, type: "camera" as const, status: "online" as const, model: "G4 Bullet", ip: "192.168.1.20", location: "Garagem", rack: "Parede Garagem", storage: "NAS-Synology" },
  { id: "cam2", label: "Camera-Escritório", x: 840, y: 370, type: "camera" as const, status: "online" as const, model: "G4 Pro", ip: "192.168.1.21", location: "Escritório", rack: "Teto Escritório", storage: "NAS-Synology" },
  { id: "cam3", label: "Camera-Entrada", x: 960, y: 370, type: "camera" as const, status: "online" as const, model: "G3 Instant", ip: "192.168.1.22", location: "Entrada", rack: "Parede Entrada", storage: "NAS-Synology" },

  // PHONES
  { id: "ph1", label: "Telefone-01", x: 120, y: 500, type: "phone" as const, status: "online" as const, model: "UniFi Talk Touch", ip: "192.168.1.60", location: "Recepção", rack: "Mesa Recepção" },
  { id: "ph2", label: "Telefone-02", x: 250, y: 500, type: "phone" as const, status: "online" as const, model: "UniFi Talk Touch", ip: "192.168.1.61", location: "Diretoria", rack: "Mesa Diretoria" },

  // SERVERS (Datacenter)
  { id: "srv1", label: "Server-DB-01", x: 380, y: 500, type: "server" as const, status: "online" as const, model: "Dell PowerEdge R750", ip: "192.168.2.50", location: "Datacenter", rack: "Rack-C / U1", storage: "RAID-10 3.8TB" },
  { id: "srv2", label: "Server-APP-01", x: 510, y: 500, type: "server" as const, status: "online" as const, model: "Dell PowerEdge R650", ip: "192.168.2.51", location: "Datacenter", rack: "Rack-C / U5", storage: "RAID-5 2.8TB" },

  // VMS
  { id: "vm1", label: "VM-Web-01", x: 380, y: 590, type: "vm" as const, status: "online" as const, model: "Ubuntu 22.04", ip: "192.168.2.100", location: "VMware ESXi", rack: "VMware", host: "192.168.2.200" },
  { id: "vm2", label: "VM-DB-01", x: 510, y: 590, type: "vm" as const, status: "online" as const, model: "Ubuntu 22.04", ip: "192.168.2.101", location: "VMware ESXi", rack: "VMware", host: "192.168.2.200" },
  { id: "vm3", label: "VM-Win-01", x: 640, y: 590, type: "vm" as const, status: "online" as const, model: "Windows Server 2022", ip: "192.168.2.102", location: "VMware ESXi", rack: "VMware", host: "192.168.2.200" },

  // NAS / STORAGE
  { id: "nas1", label: "NAS-Synology", x: 770, y: 500, type: "nas" as const, status: "online" as const, model: "DS920+", ip: "192.168.1.30", location: "Escritório", rack: "Rack-B / U2", storage: "4x4TB RAID1" },
  { id: "stg1", label: "Storage-Backup", x: 900, y: 500, type: "storage" as const, status: "online" as const, model: "Synology DS1621+", ip: "192.168.1.31", location: "Datacenter", rack: "Rack-C / U10", storage: "6x8TB RAID5" },

  // UPS
  { id: "ups1", label: "UPS-Rack-01", x: 120, y: 610, type: "ups" as const, status: "online" as const, model: "APC Smart-UPS 1500", ip: "192.168.1.40", location: "Rack Principal", rack: "Rack-A / U0", battery: "100%" },

  // PRINTERS
  { id: "pr1", label: "Printer-HP-01", x: 320, y: 610, type: "printer" as const, status: "online" as const, model: "HP LaserJet M404dn", ip: "192.168.1.50", location: "Escritório", rack: "Mesa Escritório" },
  { id: "pr2", label: "Printer-Epson", x: 500, y: 610, type: "printer" as const, status: "online" as const, model: "Epson EcoTank L3250", ip: "192.168.1.51", location: "Recepção", rack: "Mesa Recepção" },
  { id: "pr3", label: "Printer-HP-Cor", x: 680, y: 610, type: "printer" as const, status: "warning" as const, model: "HP Color M255dw", ip: "192.168.1.52", location: "Marketing", rack: "Mesa Marketing" },

  // CLIENTS (WiFi)
  { id: "c1", label: "iPhone de Maria", x: 120, y: 700, type: "client" as const, status: "online" as const, ip: "192.168.1.45", location: "Sala", connection: "WiFi" },
  { id: "c2", label: "MacBook-Pro", x: 220, y: 700, type: "client" as const, status: "online" as const, ip: "192.168.1.102", location: "Escritório", connection: "WiFi" },
  { id: "c3", label: "Smart-TV-LG", x: 320, y: 700, type: "client" as const, status: "online" as const, ip: "192.168.1.78", location: "Sala", connection: "WiFi" },
  { id: "c4", label: "Desktop-Gaming", x: 420, y: 700, type: "client" as const, status: "online" as const, ip: "192.168.1.15", location: "Escritório", connection: "Ethernet" },
  { id: "c5", label: "iPad-Sala", x: 520, y: 700, type: "client" as const, status: "online" as const, ip: "192.168.1.67", location: "Sala", connection: "WiFi" },
  { id: "c6", label: "Alexa-Echo", x: 620, y: 700, type: "client" as const, status: "online" as const, ip: "192.168.1.80", location: "Sala", connection: "WiFi" },
];

// ============ TOPOLOGY EDGES ============
export const topologyEdges = [
  { from: "wan", to: "udm", label: "500 Mbps", type: "wan" },
  { from: "udm", to: "sw1", label: "10 Gbps", type: "backbone" },
  { from: "udm", to: "sw2", label: "10 Gbps", type: "backbone" },
  { from: "udm", to: "sw3", label: "1 Gbps", type: "backbone" },
  { from: "sw1", to: "ap1", label: "1 Gbps PoE", type: "poe" },
  { from: "sw1", to: "ap2", label: "1 Gbps PoE", type: "poe" },
  { from: "sw1", to: "ap3", label: "1 Gbps PoE", type: "poe" },
  { from: "sw2", to: "cam1", label: "100 Mbps PoE", type: "poe" },
  { from: "sw2", to: "cam2", label: "100 Mbps PoE", type: "poe" },
  { from: "sw2", to: "cam3", label: "100 Mbps", type: "access" },
  { from: "sw1", to: "srv1", label: "10 Gbps", type: "server" },
  { from: "sw1", to: "srv2", label: "10 Gbps", type: "server" },
  { from: "srv1", to: "vm1", label: "vSwitch", type: "virtual" },
  { from: "srv1", to: "vm2", label: "vSwitch", type: "virtual" },
  { from: "srv2", to: "vm3", label: "vSwitch", type: "virtual" },
  { from: "sw1", to: "nas1", label: "1 Gbps", type: "access" },
  { from: "sw2", to: "stg1", label: "10 Gbps", type: "server" },
  { from: "sw1", to: "ups1", label: "USB", type: "access" },
  { from: "sw1", to: "pr1", label: "100 Mbps", type: "access" },
  { from: "sw2", to: "pr2", label: "100 Mbps", type: "access" },
  { from: "sw2", to: "pr3", label: "100 Mbps", type: "access" },
  { from: "sw1", to: "ph1", label: "100 Mbps PoE", type: "poe" },
  { from: "sw2", to: "ph2", label: "100 Mbps PoE", type: "poe" },
  { from: "ap1", to: "c1", label: "WiFi", type: "wifi" },
  { from: "ap2", to: "c2", label: "WiFi", type: "wifi" },
  { from: "ap1", to: "c3", label: "WiFi", type: "wifi" },
  { from: "sw1", to: "c4", label: "1 Gbps", type: "access" },
  { from: "ap1", to: "c5", label: "WiFi", type: "wifi" },
  { from: "ap1", to: "c6", label: "WiFi", type: "wifi" },
];

// ============ DEVICE SUMMARY ============
export const deviceSummary = {
  total: 25,
  byType: [
    { type: "gateway", label: "Gateways", count: 1, icon: "pi pi-th-large", color: "#3b82f6" },
    { type: "switch", label: "Switches", count: 3, icon: "pi pi-server", color: "#8b5cf6" },
    { type: "ap", label: "Access Points", count: 3, icon: "pi pi-wifi", color: "#22c55e" },
    { type: "camera", label: "Câmeras", count: 3, icon: "pi pi-video", color: "#ec4899" },
    { type: "phone", label: "Telefones", count: 2, icon: "pi pi-phone", color: "#06b6d4" },
    { type: "server", label: "Servidores", count: 2, icon: "pi pi-desktop", color: "#3b82f6" },
    { type: "vm", label: "Máquinas Virtuais", count: 3, icon: "pi pi-box", color: "#8b5cf6" },
    { type: "nas", label: "NAS", count: 1, icon: "pi pi-database", color: "#22c55e" },
    { type: "storage", label: "Armazenamento", count: 1, icon: "pi pi-database", color: "#f59e0b" },
    { type: "ups", label: "UPS", count: 1, icon: "pi pi-bolt", color: "#f59e0b" },
    { type: "printer", label: "Impressoras", count: 3, icon: "pi pi-print", color: "#f59e0b" },
  ],
  byLocation: [
    { location: "Rack Principal", label: "Rack-A", devices: ["UDM-Pro", "Switch-Core-01", "Switch-Rede", "UPS-Rack-01"], count: 4, color: "#3b82f6" },
    { location: "Rack Escritório", label: "Rack-B", devices: ["Switch-Office-01", "NAS-Synology"], count: 2, color: "#8b5cf6" },
    { location: "Datacenter", label: "Rack-C", devices: ["Server-DB-01", "Server-APP-01", "Storage-Backup"], count: 3, color: "#22c55e" },
    { location: "VMware ESXi", label: "Virtual", devices: ["VM-Web-01", "VM-DB-01", "VM-Win-01"], count: 3, color: "#8b5cf6" },
    { location: "Sala", label: "Área", devices: ["AP-Sala", "Camera-Garagem"], count: 2, color: "#f59e0b" },
    { location: "Escritório", label: "Área", devices: ["AP-Escritório", "Camera-Escritório", "Printer-HP-01"], count: 3, color: "#ec4899" },
    { location: "Pátio", label: "Externo", devices: ["AP-Pátio"], count: 1, color: "#f97316" },
    { location: "Recepção", label: "Área", devices: ["Telefone-01", "Printer-Epson"], count: 2, color: "#06b6d4" },
    { location: "Diretoria", label: "Área", devices: ["Telefone-02"], count: 1, color: "#14b8a6" },
    { location: "Marketing", label: "Área", devices: ["Printer-HP-Cor"], count: 1, color: "#a855f7" },
    { location: "Entrada", label: "Externo", devices: ["Camera-Entrada"], count: 1, color: "#64748b" },
  ],
  byStatus: {
    online: 22,
    warning: 2,
    offline: 1,
  },
};
