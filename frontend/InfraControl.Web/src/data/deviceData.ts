// ============ DEVICE TYPES ============
export type DeviceType = "gateway" | "switch" | "ap" | "printer" | "camera" | "phone" | "server" | "vm" | "nas" | "ups" | "storage";

// ============ ALL DEVICES ============
export const allDevices = [
  // GATEWAYS
  { id: "gw-1", name: "UDM-Pro", model: "Dream Machine Pro", type: "gateway" as DeviceType, ip: "192.168.1.1", mac: "FC:EC:DA:12:34:56", status: "online" as const, firmware: "4.1.13", uptime: "45d 12h", location: "Rack Principal", sector: "Rack Principal", department: "TI", serial: "UDM-2024-FC12", vlan: 10 },

  // SWITCHES
  { id: "sw-1", name: "Switch-Core-01", model: "Switch Pro 24 PoE", type: "switch" as DeviceType, ip: "192.168.1.2", mac: "FC:EC:DA:22:33:44", status: "online" as const, firmware: "7.0.6", uptime: "45d 12h", location: "Rack Principal", sector: "Rack Principal", department: "TI", serial: "SW-PRO24-AB12", vlan: 10, totalPorts: 24, activePorts: 12, poePorts: 6 },
  { id: "sw-2", name: "Switch-Office-01", model: "Switch Lite 16 PoE", type: "switch" as DeviceType, ip: "192.168.1.3", mac: "FC:EC:DA:33:44:55", status: "online" as const, firmware: "7.0.6", uptime: "30d 5h", location: "Rack Escritório", sector: "Rack Escritório", department: "TI", serial: "SW-LITE16-CD34", vlan: 10, totalPorts: 16, activePorts: 5, poePorts: 2 },
  { id: "sw-3", name: "Switch-Rede", model: "Switch Lite 8", type: "switch" as DeviceType, ip: "192.168.1.4", mac: "FC:EC:DA:44:55:66", status: "offline" as const, firmware: "7.0.6", uptime: "0d 0h", location: "Rack Principal", sector: "Rack Principal", department: "TI", serial: "SW-LITE8-EF56", vlan: 10, totalPorts: 8, activePorts: 0, poePorts: 0 },

  // ACCESS POINTS
  { id: "ap-1", name: "AP-Sala", model: "U6 Pro", type: "ap" as DeviceType, ip: "192.168.1.10", mac: "FC:EC:DA:55:66:77", status: "online" as const, firmware: "6.6.7", uptime: "45d 12h", location: "Sala", sector: "Sala", department: "TI", serial: "AP-U6P-GH78", vlan: 10, clients: 24, switchName: "Switch-Core-01", switchPort: 4 },
  { id: "ap-2", name: "AP-Escritório", model: "U6 Pro", type: "ap" as DeviceType, ip: "192.168.1.11", mac: "FC:EC:DA:66:77:88", status: "online" as const, firmware: "6.6.7", uptime: "45d 12h", location: "Escritório", sector: "Escritório", department: "TI", serial: "AP-U6P-IJ90", vlan: 10, clients: 18, switchName: "Switch-Core-01", switchPort: 5 },
  { id: "ap-3", name: "AP-Pátio", model: "U6 Mesh", type: "ap" as DeviceType, ip: "192.168.1.12", mac: "FC:EC:DA:77:88:99", status: "warning" as const, firmware: "6.6.7", uptime: "12d 8h", location: "Pátio", sector: "Pátio", department: "TI", serial: "AP-MESH-KL12", vlan: 40, clients: 5, switchName: "Switch-Core-01", switchPort: 6 },

  // PRINTERS
  { id: "pr-1", name: "Printer-HP-01", model: "HP LaserJet Pro M404dn", type: "printer" as DeviceType, ip: "192.168.1.50", mac: "1A:2B:3C:4D:5E:6F", status: "online" as const, firmware: "20230901", uptime: "120d 5h", location: "Escritório", sector: "Escritório", department: "Administrativo", serial: "HP-M404-1234", vlan: 10, switchName: "Switch-Core-01", switchPort: 3 },
  { id: "pr-2", name: "Printer-Epson-01", model: "Epson EcoTank L3250", type: "printer" as DeviceType, ip: "192.168.1.51", mac: "2B:3C:4D:5E:6F:7A", status: "online" as const, firmware: "1.04", uptime: "60d 10h", location: "Recepção", sector: "Recepção", department: "Administrativo", serial: "EP-L325-5678", vlan: 10, switchName: "Switch-Office-01", switchPort: 3 },
  { id: "pr-3", name: "Printer-HP-Cor", model: "HP Color LaserJet Pro M255dw", type: "printer" as DeviceType, ip: "192.168.1.52", mac: "3C:4D:5E:6F:7A:8B", status: "warning" as const, firmware: "20231201", uptime: "90d 3h", location: "Marketing", sector: "Marketing", department: "Marketing", serial: "HP-M255-9012", vlan: 10, switchName: "Switch-Office-01", switchPort: 4 },

  // CAMERAS
  { id: "cam-1", name: "Camera-Garagem", model: "G4 Bullet", type: "camera" as DeviceType, ip: "192.168.1.20", mac: "4D:5E:6F:7A:8B:9C", status: "online" as const, firmware: "4.72.1", uptime: "45d 12h", location: "Garagem", sector: "Garagem", department: "Segurança", serial: "CAM-G4B-3456", vlan: 10, switchName: "Switch-Core-01", switchPort: 7, storageName: "NAS-Synology" },
  { id: "cam-2", name: "Camera-Escritório", model: "G4 Pro", type: "camera" as DeviceType, ip: "192.168.1.21", mac: "5E:6F:7A:8B:9C:AD", status: "online" as const, firmware: "4.72.1", uptime: "45d 12h", location: "Escritório", sector: "Escritório", department: "Segurança", serial: "CAM-G4P-7890", vlan: 10, switchName: "Switch-Core-01", switchPort: 8, storageName: "NAS-Synology" },
  { id: "cam-3", name: "Camera-Entrada", model: "G3 Instant", type: "camera" as DeviceType, ip: "192.168.1.22", mac: "6F:7A:8B:9C:AD:BE", status: "online" as const, firmware: "4.68.20", uptime: "30d 8h", location: "Entrada", sector: "Entrada", department: "Segurança", serial: "CAM-G3I-2345", vlan: 10, switchName: "Switch-Office-01", switchPort: 2, storageName: "NAS-Synology" },

  // PHONES
  { id: "ph-1", name: "Telefone-01", model: "UniFi Talk Touch", type: "phone" as DeviceType, ip: "192.168.1.60", mac: "7A:8B:9C:AD:BE:CF", status: "online" as const, firmware: "2.4.0", uptime: "20d 6h", location: "Recepção", sector: "Recepção", department: "Administrativo", serial: "PH-UT-6789", vlan: 50, switchName: "Switch-Core-01", switchPort: 9 },
  { id: "ph-2", name: "Telefone-02", model: "UniFi Talk Touch", type: "phone" as DeviceType, ip: "192.168.1.61", mac: "8B:9C:AD:BE:CF:DA", status: "online" as const, firmware: "2.4.0", uptime: "20d 6h", location: "Diretoria", sector: "Diretoria", department: "Diretoria", serial: "PH-UT-0123", vlan: 50, switchName: "Switch-Core-01", switchPort: 10 },

  // SERVERS
  { id: "srv-1", name: "Server-DB-01", model: "Dell PowerEdge R750", type: "server" as DeviceType, ip: "192.168.2.50", mac: "9C:AD:BE:CF:DA:EB", status: "online" as const, firmware: "iDRAC 7.10", uptime: "180d 12h", location: "Datacenter", sector: "Datacenter", department: "TI", serial: "DELL-R750-4567", vlan: 20, storageName: "RAID-10 3.8TB" },
  { id: "srv-2", name: "Server-APP-01", model: "Dell PowerEdge R650", type: "server" as DeviceType, ip: "192.168.2.51", mac: "AD:BE:CF:DA:EB:FC", status: "online" as const, firmware: "iDRAC 7.10", uptime: "180d 12h", location: "Datacenter", sector: "Datacenter", department: "TI", serial: "DELL-R650-8901", vlan: 20, storageName: "RAID-5 2.8TB" },

  // VMS
  { id: "vm-1", name: "VM-Web-01", model: "Ubuntu 22.04 LTS", type: "vm" as DeviceType, ip: "192.168.2.100", mac: "BE:CF:DA:EB:FC:01", status: "online" as const, firmware: "Kernel 5.15", uptime: "90d 8h", location: "VMware ESXi", sector: "Datacenter", department: "TI", serial: "VM-WEB-1111", vlan: 20, host: "192.168.2.200" },
  { id: "vm-2", name: "VM-DB-01", model: "Ubuntu 22.04 LTS", type: "vm" as DeviceType, ip: "192.168.2.101", mac: "CF:DA:EB:FC:01:23", status: "online" as const, firmware: "Kernel 5.15", uptime: "90d 8h", location: "VMware ESXi", sector: "Datacenter", department: "TI", serial: "VM-DB-2222", vlan: 20, host: "192.168.2.200" },
  { id: "vm-3", name: "VM-Win-01", model: "Windows Server 2022", type: "vm" as DeviceType, ip: "192.168.2.102", mac: "DA:EB:FC:01:23:45", status: "online" as const, firmware: "Build 20348", uptime: "45d 3h", location: "VMware ESXi", sector: "Datacenter", department: "TI", serial: "VM-WIN-3333", vlan: 20, host: "192.168.2.200" },

  // NAS
  { id: "nas-1", name: "NAS-Synology", model: "DS920+", type: "nas" as DeviceType, ip: "192.168.1.30", mac: "EB:FC:01:23:45:67", status: "online" as const, firmware: "DSM 7.2.1", uptime: "120d 5h", location: "Escritório", sector: "Escritório", department: "TI", serial: "SYN-DS92-4444", vlan: 10, storageUsed: 8300, storageTotal: 16000 },

  // UPS
  { id: "ups-1", name: "UPS-Rack-01", model: "APC Smart-UPS 1500", type: "ups" as DeviceType, ip: "192.168.1.40", mac: "FC:01:23:45:67:89", status: "online" as const, firmware: "15.0", uptime: "365d 0h", location: "Rack Principal", sector: "Rack Principal", department: "TI", serial: "APC-SU15-5555", vlan: 10 },

  // STORAGE
  { id: "stg-1", name: "Storage-Backup", model: "Synology DS1621+", type: "storage" as DeviceType, ip: "192.168.1.31", mac: "01:23:45:67:89:AB", status: "online" as const, firmware: "DSM 7.2.1", uptime: "120d 5h", location: "Datacenter", sector: "Datacenter", department: "TI", serial: "SYN-DS16-6666", vlan: 10, storageUsed: 28000, storageTotal: 48000 },
];

// ============ SWITCH PORT MAP ============
export const switchPortMap: Record<string, {
  name: string; model: string; location: string; rack: string;
  totalPorts: number; activePorts: number; poePorts: number;
  ports: { port: number; device: string; type: string; speed: string; poe: boolean; vlan: number; status: string }[];
}> = {
  "sw-1": {
    name: "Switch-Core-01", model: "Switch Pro 24 PoE", location: "Rack Principal", rack: "Rack-A / U2",
    totalPorts: 24, activePorts: 12, poePorts: 6,
    ports: [
      { port: 1, device: "UDM-Pro", type: "gateway", speed: "10 Gbps", poe: false, vlan: 10, status: "active" },
      { port: 2, device: "Desktop-Gaming", type: "client", speed: "1 Gbps", poe: false, vlan: 10, status: "active" },
      { port: 3, device: "Printer-HP-01", type: "printer", speed: "100 Mbps", poe: false, vlan: 10, status: "active" },
      { port: 4, device: "AP-Sala", type: "ap", speed: "1 Gbps", poe: true, vlan: 10, status: "active" },
      { port: 5, device: "AP-Escritório", type: "ap", speed: "1 Gbps", poe: true, vlan: 10, status: "active" },
      { port: 6, device: "AP-Pátio", type: "ap", speed: "1 Gbps", poe: true, vlan: 40, status: "active" },
      { port: 7, device: "Camera-Garagem", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
      { port: 8, device: "Camera-Escritório", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
      { port: 9, device: "Telefone-01", type: "phone", speed: "100 Mbps", poe: true, vlan: 50, status: "active" },
      { port: 10, device: "Telefone-02", type: "phone", speed: "100 Mbps", poe: false, vlan: 50, status: "active" },
      { port: 11, device: "NAS-Synology", type: "nas", speed: "1 Gbps", poe: false, vlan: 10, status: "active" },
      { port: 12, device: "UPS-Rack-01", type: "ups", speed: "USB", poe: false, vlan: 10, status: "active" },
      ...Array.from({ length: 12 }, (_, i) => ({
        port: i + 13, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: i < 8 ? "inactive" : "disabled",
      })),
    ],
  },
  "sw-2": {
    name: "Switch-Office-01", model: "Switch Lite 16 PoE", location: "Rack Escritório", rack: "Rack-B / U1",
    totalPorts: 16, activePorts: 5, poePorts: 2,
    ports: [
      { port: 1, device: "Camera-Escritório", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
      { port: 2, device: "Camera-Entrada", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
      { port: 3, device: "Printer-Epson-01", type: "printer", speed: "100 Mbps", poe: false, vlan: 10, status: "active" },
      { port: 4, device: "Printer-HP-Cor", type: "printer", speed: "100 Mbps", poe: false, vlan: 10, status: "active" },
      { port: 5, device: "Storage-Backup", type: "storage", speed: "10 Gbps", poe: false, vlan: 10, status: "active" },
      ...Array.from({ length: 11 }, (_, i) => ({
        port: i + 6, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: i < 5 ? "inactive" : "disabled",
      })),
    ],
  },
  "sw-3": {
    name: "Switch-Rede", model: "Switch Lite 8", location: "Rack Principal", rack: "Rack-A / U3",
    totalPorts: 8, activePorts: 0, poePorts: 0,
    ports: Array.from({ length: 8 }, (_, i) => ({
      port: i + 1, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled",
    })),
  },
};

// ============ SECTOR MAP ============
export const sectorMap: Record<string, {
  icon: string; color: string; description: string;
  devices: { id: string; name: string; type: DeviceType; status: string; ip: string }[];
  clients: { name: string; ip: string; connection: string; signal?: number; port?: string }[];
  online: number; warning: number; offline: number; totalClients: number;
}> = {
  "Sala": {
    icon: "pi pi-home", color: "#f59e0b", description: "Área principal de estar",
    devices: [
      { id: "ap-1", name: "AP-Sala", type: "ap", status: "online", ip: "192.168.1.10" },
    ],
    clients: [
      { name: "iPhone de Maria", ip: "192.168.1.45", connection: "WiFi", signal: -42 },
      { name: "Smart-TV-LG", ip: "192.168.1.78", connection: "WiFi", signal: -55 },
      { name: "iPad-Sala", ip: "192.168.1.67", connection: "WiFi", signal: -50 },
      { name: "Alexa-Echo", ip: "192.168.1.80", connection: "WiFi", signal: -48 },
    ],
    online: 1, warning: 0, offline: 0, totalClients: 4,
  },
  "Escritório": {
    icon: "pi pi-building", color: "#3b82f6", description: "Área de trabalho",
    devices: [
      { id: "ap-2", name: "AP-Escritório", type: "ap", status: "online", ip: "192.168.1.11" },
      { id: "pr-1", name: "Printer-HP-01", type: "printer", status: "online", ip: "192.168.1.50" },
      { id: "nas-1", name: "NAS-Synology", type: "nas", status: "online", ip: "192.168.1.30" },
      { id: "cam-2", name: "Camera-Escritório", type: "camera", status: "online", ip: "192.168.1.21" },
    ],
    clients: [
      { name: "MacBook-Pro", ip: "192.168.1.102", connection: "WiFi", signal: -38 },
      { name: "Desktop-Gaming", ip: "192.168.1.15", connection: "Ethernet", port: "Sw-Core P2" },
    ],
    online: 4, warning: 0, offline: 0, totalClients: 2,
  },
  "Pátio": {
    icon: "pi pi-external-link", color: "#f97316", description: "Área externa",
    devices: [
      { id: "ap-3", name: "AP-Pátio", type: "ap", status: "warning", ip: "192.168.1.12" },
    ],
    clients: [],
    online: 0, warning: 1, offline: 0, totalClients: 5,
  },
  "Recepção": {
    icon: "pi pi-users", color: "#06b6d4", description: "Recepção e atendimento",
    devices: [
      { id: "ph-1", name: "Telefone-01", type: "phone", status: "online", ip: "192.168.1.60" },
      { id: "pr-2", name: "Printer-Epson-01", type: "printer", status: "online", ip: "192.168.1.51" },
    ],
    clients: [],
    online: 2, warning: 0, offline: 0, totalClients: 0,
  },
  "Diretoria": {
    icon: "pi pi-briefcase", color: "#14b8a6", description: "Escritório da diretoria",
    devices: [
      { id: "ph-2", name: "Telefone-02", type: "phone", status: "online", ip: "192.168.1.61" },
    ],
    clients: [],
    online: 1, warning: 0, offline: 0, totalClients: 0,
  },
  "Marketing": {
    icon: "pi pi-megaphone", color: "#a855f7", description: "Departamento de marketing",
    devices: [
      { id: "pr-3", name: "Printer-HP-Cor", type: "printer", status: "warning", ip: "192.168.1.52" },
    ],
    clients: [],
    online: 0, warning: 1, offline: 0, totalClients: 0,
  },
  "Garagem": {
    icon: "pi pi-video", color: "#ec4899", description: "Estacionamento e garagem",
    devices: [
      { id: "cam-1", name: "Camera-Garagem", type: "camera", status: "online", ip: "192.168.1.20" },
    ],
    clients: [],
    online: 1, warning: 0, offline: 0, totalClients: 0,
  },
  "Entrada": {
    icon: "pi pi-sign-in", color: "#64748b", description: "Entrada principal",
    devices: [
      { id: "cam-3", name: "Camera-Entrada", type: "camera", status: "online", ip: "192.168.1.22" },
    ],
    clients: [],
    online: 1, warning: 0, offline: 0, totalClients: 0,
  },
  "Datacenter": {
    icon: "pi pi-server", color: "#22c55e", description: "Sala de servidores",
    devices: [
      { id: "srv-1", name: "Server-DB-01", type: "server", status: "online", ip: "192.168.2.50" },
      { id: "srv-2", name: "Server-APP-01", type: "server", status: "online", ip: "192.168.2.51" },
      { id: "stg-1", name: "Storage-Backup", type: "storage", status: "online", ip: "192.168.1.31" },
      { id: "vm-1", name: "VM-Web-01", type: "vm", status: "online", ip: "192.168.2.100" },
      { id: "vm-2", name: "VM-DB-01", type: "vm", status: "online", ip: "192.168.2.101" },
      { id: "vm-3", name: "VM-Win-01", type: "vm", status: "online", ip: "192.168.2.102" },
    ],
    clients: [],
    online: 6, warning: 0, offline: 0, totalClients: 0,
  },
  "Rack Principal": {
    icon: "pi pi-th-large", color: "#3b82f6", description: "Rack-A — infraestrutura core",
    devices: [
      { id: "gw-1", name: "UDM-Pro", type: "gateway", status: "online", ip: "192.168.1.1" },
      { id: "sw-1", name: "Switch-Core-01", type: "switch", status: "online", ip: "192.168.1.2" },
      { id: "sw-3", name: "Switch-Rede", type: "switch", status: "offline", ip: "192.168.1.4" },
      { id: "ups-1", name: "UPS-Rack-01", type: "ups", status: "online", ip: "192.168.1.40" },
    ],
    clients: [],
    online: 3, warning: 0, offline: 1, totalClients: 0,
  },
  "Rack Escritório": {
    icon: "pi pi-th-large", color: "#8b5cf6", description: "Rack-B — switches e storage",
    devices: [
      { id: "sw-2", name: "Switch-Office-01", type: "switch", status: "online", ip: "192.168.1.3" },
    ],
    clients: [],
    online: 1, warning: 0, offline: 0, totalClients: 0,
  },
};

// ============ PRINTER DETAILS ============
export const printerDetails: Record<string, {
  tonerBlack: number; tonerCyan: number; tonerMagenta: number; tonerYellow: number;
  drumBlack: number; drumCyan: number; drumMagenta: number; drumYellow: number;
  totalPages: number; pagesToday: number; paperJams: number; warnings: string[];
  model_info: string; printSpeed: string; resolution: string; paperTray: string;
}> = {
  "pr-1": {
    tonerBlack: 72, tonerCyan: 0, tonerMagenta: 0, tonerYellow: 0,
    drumBlack: 45, drumCyan: 0, drumMagenta: 0, drumYellow: 0,
    totalPages: 24567, pagesToday: 45, paperJams: 0, warnings: [],
    model_info: "Mono Laser", printSpeed: "38 ppm", resolution: "1200x1200 dpi", paperTray: "550 folhas"
  },
  "pr-2": {
    tonerBlack: 85, tonerCyan: 62, tonerMagenta: 58, tonerYellow: 71,
    drumBlack: 90, drumCyan: 88, drumMagenta: 87, drumYellow: 89,
    totalPages: 8234, pagesToday: 12, paperJams: 0, warnings: [],
    model_info: "Color Inkjet", printSpeed: "10 ppm", resolution: "5760x1440 dpi", paperTray: "100 folhas"
  },
  "pr-3": {
    tonerBlack: 15, tonerCyan: 34, tonerMagenta: 22, tonerYellow: 41,
    drumBlack: 30, drumCyan: 35, drumMagenta: 32, drumYellow: 38,
    totalPages: 15890, pagesToday: 28, paperJams: 2, warnings: ["Toner preto baixo", "Cilindro preto próximo do fim"],
    model_info: "Color Laser", printSpeed: "28 ppm", resolution: "600x600 dpi", paperTray: "250 folhas"
  },
};

// ============ CAMERA DETAILS ============
export const cameraDetails: Record<string, {
  resolution: string; fps: number; nightVision: boolean; motionDetection: boolean;
  recordingMode: string; storageUsed: number; storageTotal: number; storagePath: string;
  ptz: boolean; micEnabled: boolean; speakerEnabled: boolean; irDistance: string;
  events24h: number; motionEvents: number; aiDetection: boolean;
}> = {
  "cam-1": {
    resolution: "4MP (2688x1520)", fps: 30, nightVision: true, motionDetection: true,
    recordingMode: "Continuo + Motion", storageUsed: 847, storageTotal: 2048, storagePath: "NAS-Synology",
    ptz: false, micEnabled: true, speakerEnabled: false, irDistance: "25m",
    events24h: 156, motionEvents: 23, aiDetection: true
  },
  "cam-2": {
    resolution: "4K (3840x2160)", fps: 30, nightVision: true, motionDetection: true,
    recordingMode: "Continuo + Motion", storageUsed: 1240, storageTotal: 2048, storagePath: "NAS-Synology",
    ptz: true, micEnabled: true, speakerEnabled: true, irDistance: "25m",
    events24h: 89, motionEvents: 15, aiDetection: true
  },
  "cam-3": {
    resolution: "1080p (1920x1080)", fps: 30, nightVision: true, motionDetection: true,
    recordingMode: "Motion Only", storageUsed: 234, storageTotal: 512, storagePath: "NAS-Synology",
    ptz: false, micEnabled: true, speakerEnabled: true, irDistance: "10m",
    events24h: 234, motionEvents: 67, aiDetection: false
  },
};

// ============ PHONE DETAILS ============
export const phoneDetails: Record<string, {
  lines: { number: string; label: string; status: string }[];
  callsToday: number; callsMissed: number; avgCallDuration: string;
  firmware: string; provisioningServer: string; directorySync: boolean;
  recentCalls: { number: string; name: string; type: string; duration: string; time: string }[];
}> = {
  "ph-1": {
    lines: [
      { number: "(11) 3456-7890", label: "Recepção", status: "active" },
      { number: "(11) 3456-7891", label: "Suporte", status: "active" },
      { number: "100", label: "Ramal 100", status: "active" },
    ],
    callsToday: 24, callsMissed: 2, avgCallDuration: "3m 45s",
    firmware: "2.4.0", provisioningServer: "192.168.2.102", directorySync: true,
    recentCalls: [
      { number: "(11) 99876-5432", name: "João Silva", type: "Recebida", duration: "5m 12s", time: "15:30" },
      { number: "(11) 3321-8765", name: "Maria Santos", type: "Realizada", duration: "2m 05s", time: "14:45" },
      { number: "(11) 4432-1098", name: "Pedro Costa", type: "Perdida", duration: "—", time: "14:20" },
      { number: "101", name: "Ramal 101", type: "Recebida", duration: "1m 30s", time: "13:55" },
    ]
  },
  "ph-2": {
    lines: [
      { number: "(11) 3456-7800", label: "Diretoria", status: "active" },
      { number: "200", label: "Ramal 200", status: "active" },
    ],
    callsToday: 8, callsMissed: 0, avgCallDuration: "4m 20s",
    firmware: "2.4.0", provisioningServer: "192.168.2.102", directorySync: true,
    recentCalls: [
      { number: "(11) 98765-4321", name: "Carlos Mendes", type: "Recebida", duration: "8m 30s", time: "15:00" },
      { number: "(11) 2233-4455", name: "Ana Oliveira", type: "Realizada", duration: "3m 15s", time: "14:30" },
    ]
  },
};

// ============ SERVER DETAILS ============
export const serverDetails: Record<string, {
  cpu: { model: string; cores: number; usage: number; temp: number };
  ram: { total: number; used: number; free: number };
  disks: { name: string; total: number; used: number; type: string }[];
  network: { interface: string; ip: string; speed: string; rx: string; tx: string }[];
  services: { name: string; status: string; port: string }[];
  os: string; hostname: string; kernel: string;
}> = {
  "srv-1": {
    cpu: { model: "Intel Xeon Silver 4314", cores: 32, usage: 34, temp: 52 },
    ram: { total: 128, used: 84, free: 44 },
    disks: [
      { name: "RAID-1 OS", total: 480, used: 210, type: "SSD" },
      { name: "RAID-10 Data", total: 3840, used: 2100, type: "SSD NVMe" },
      { name: "RAID-1 Logs", total: 960, used: 340, type: "SSD" },
    ],
    network: [
      { interface: "bond0", ip: "192.168.2.50", speed: "10 Gbps", rx: "1.2 TB", tx: "890 GB" },
      { interface: "bond1", ip: "10.0.0.50", speed: "10 Gbps", rx: "450 GB", tx: "320 GB" },
    ],
    services: [
      { name: "PostgreSQL 15", status: "running", port: "5432" },
      { name: "Redis 7", status: "running", port: "6379" },
      { name: "RabbitMQ", status: "running", port: "5672" },
      { name: "SSH", status: "running", port: "22" },
    ],
    os: "Ubuntu 22.04 LTS", hostname: "srv-db-01", kernel: "5.15.0-91-generic"
  },
  "srv-2": {
    cpu: { model: "Intel Xeon Silver 4310", cores: 24, usage: 56, temp: 58 },
    ram: { total: 64, used: 52, free: 12 },
    disks: [
      { name: "RAID-1 OS", total: 480, used: 180, type: "SSD" },
      { name: "RAID-5 Apps", total: 2880, used: 1600, type: "SSD" },
    ],
    network: [
      { interface: "bond0", ip: "192.168.2.51", speed: "10 Gbps", rx: "890 GB", tx: "650 GB" },
    ],
    services: [
      { name: "ASP.NET Core", status: "running", port: "5000" },
      { name: "Nginx", status: "running", port: "80/443" },
      { name: "Docker", status: "running", port: "2375" },
      { name: "SSH", status: "running", port: "22" },
    ],
    os: "Ubuntu 22.04 LTS", hostname: "srv-app-01", kernel: "5.15.0-91-generic"
  },
};

// ============ VM DETAILS ============
export const vmDetails: Record<string, {
  host: string; cpuCores: number; cpuUsage: number; ramTotal: number; ramUsed: number;
  diskTotal: number; diskUsed: number; networkRx: string; networkTx: string;
  status: string; os: string; uptime: string; snapshots: number;
  services: { name: string; status: string }[];
}> = {
  "vm-1": {
    host: "192.168.2.200 (ESXi)", cpuCores: 4, cpuUsage: 28, ramTotal: 8, ramUsed: 5.2,
    diskTotal: 100, diskUsed: 45, networkRx: "2.4 GB", networkTx: "1.8 GB",
    status: "running", os: "Ubuntu 22.04 LTS", uptime: "90d 8h", snapshots: 3,
    services: [
      { name: "Nginx", status: "running" },
      { name: "Node.js", status: "running" },
      { name: "PM2", status: "running" },
    ]
  },
  "vm-2": {
    host: "192.168.2.200 (ESXi)", cpuCores: 8, cpuUsage: 67, ramTotal: 32, ramUsed: 28.4,
    diskTotal: 500, diskUsed: 320, networkRx: "5.8 GB", networkTx: "4.2 GB",
    status: "running", os: "Ubuntu 22.04 LTS", uptime: "90d 8h", snapshots: 2,
    services: [
      { name: "PostgreSQL 15", status: "running" },
      { name: "pgAdmin", status: "running" },
    ]
  },
  "vm-3": {
    host: "192.168.2.200 (ESXi)", cpuCores: 4, cpuUsage: 42, ramTotal: 16, ramUsed: 11.2,
    diskTotal: 200, diskUsed: 85, networkRx: "1.2 GB", networkTx: "0.8 GB",
    status: "running", os: "Windows Server 2022", uptime: "45d 3h", snapshots: 1,
    services: [
      { name: "IIS", status: "running" },
      { name: "Active Directory", status: "running" },
      { name: "DNS", status: "running" },
    ]
  },
};

// ============ SWITCH PORTS (legacy) ============
export const switchPorts = switchPortMap;

// ============ NAS DETAILS ============
export const nasDetails: Record<string, {
  model: string; cpu: string; ram: number; disks: { slot: number; model: string; size: number; status: string; temp: number }[];
  volumes: { name: string; size: number; used: number; raid: string; filesystem: string }[];
  services: { name: string; status: string }[];
  storageUsed: number; storageTotal: number;
}> = {
  "nas-1": {
    model: "DS920+", cpu: "Intel Celeron J4125", ram: 8,
    disks: [
      { slot: 1, model: "WD Red Plus 4TB", size: 4000, status: "healthy", temp: 32 },
      { slot: 2, model: "WD Red Plus 4TB", size: 4000, status: "healthy", temp: 33 },
      { slot: 3, model: "WD Red Plus 4TB", size: 4000, status: "healthy", temp: 31 },
      { slot: 4, model: "WD Red Plus 4TB", size: 4000, status: "healthy", temp: 34 },
    ],
    volumes: [
      { name: "volume1", size: 8000, used: 5200, raid: "RAID 1", filesystem: "ext4" },
      { name: "volume2", size: 8000, used: 3100, raid: "RAID 1", filesystem: "ext4" },
    ],
    services: [
      { name: "File Station", status: "running" },
      { name: "Surveillance Station", status: "running" },
      { name: "Hyper Backup", status: "running" },
      { name: "SSH", status: "running" },
    ],
    storageUsed: 8300, storageTotal: 16000
  },
};

// ============ UPS DETAILS ============
export const upsDetails: Record<string, {
  model: string; capacity: number; batteryLevel: number; batteryTimeLeft: string;
  inputVoltage: number; outputVoltage: number; loadPercent: number;
  temperature: number; lastSelfTest: string; replaceBattery: string;
  events: string[];
}> = {
  "ups-1": {
    model: "Smart-UPS 1500", capacity: 1500, batteryLevel: 100, batteryTimeLeft: "25m 30s",
    inputVoltage: 220, outputVoltage: 220, loadPercent: 35,
    temperature: 28, lastSelfTest: "01/09/2026", replaceBattery: "01/03/2028",
    events: ["Power restored", "On battery", "Power restored", "Self-test passed"],
  },
};
