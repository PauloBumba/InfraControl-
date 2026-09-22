// ============ DEVICE TYPES ============
export type DeviceType = "gateway" | "switch" | "ap" | "printer" | "camera" | "phone" | "server" | "vm" | "nas" | "ups" | "storage";

// ============ ALL DEVICES ============
export const allDevices = [
  // GATEWAYS
  { id: "gw-1", name: "UDM-Pro", model: "Dream Machine Pro", type: "gateway" as DeviceType, ip: "192.168.1.1", mac: "FC:EC:DA:12:34:56", status: "online" as const, firmware: "4.1.13", uptime: "45d 12h", location: "Rack Principal", department: "TI", serial: "UDM-2024-FC12" },

  // SWITCHES
  { id: "sw-1", name: "Switch-Core-01", model: "Switch Pro 24 PoE", type: "switch" as DeviceType, ip: "192.168.1.2", mac: "FC:EC:DA:22:33:44", status: "online" as const, firmware: "7.0.6", uptime: "45d 12h", location: "Rack Principal", department: "TI", serial: "SW-PRO24-AB12" },
  { id: "sw-2", name: "Switch-Office-01", model: "Switch Lite 16 PoE", type: "switch" as DeviceType, ip: "192.168.1.3", mac: "FC:EC:DA:33:44:55", status: "online" as const, firmware: "7.0.6", uptime: "30d 5h", location: "Rack Escritório", department: "TI", serial: "SW-LITE16-CD34" },
  { id: "sw-3", name: "Switch-Rede", model: "Switch Lite 8", type: "switch" as DeviceType, ip: "192.168.1.4", mac: "FC:EC:DA:44:55:66", status: "offline" as const, firmware: "7.0.6", uptime: "0d 0h", location: "Rack Principal", department: "TI", serial: "SW-LITE8-EF56" },

  // ACCESS POINTS
  { id: "ap-1", name: "AP-Sala", model: "U6 Pro", type: "ap" as DeviceType, ip: "192.168.1.10", mac: "FC:EC:DA:55:66:77", status: "online" as const, firmware: "6.6.7", uptime: "45d 12h", location: "Sala", department: "TI", serial: "AP-U6P-GH78" },
  { id: "ap-2", name: "AP-Escritório", model: "U6 Pro", type: "ap" as DeviceType, ip: "192.168.1.11", mac: "FC:EC:DA:66:77:88", status: "online" as const, firmware: "6.6.7", uptime: "45d 12h", location: "Escritório", department: "TI", serial: "AP-U6P-IJ90" },
  { id: "ap-3", name: "AP-Pátio", model: "U6 Mesh", type: "ap" as DeviceType, ip: "192.168.1.12", mac: "FC:EC:DA:77:88:99", status: "warning" as const, firmware: "6.6.7", uptime: "12d 8h", location: "Pátio", department: "TI", serial: "AP-MESH-KL12" },

  // PRINTERS
  { id: "pr-1", name: "Printer-HP-01", model: "HP LaserJet Pro M404dn", type: "printer" as DeviceType, ip: "192.168.1.50", mac: "1A:2B:3C:4D:5E:6F", status: "online" as const, firmware: "20230901", uptime: "120d 5h", location: "Escritório", department: "Administrativo", serial: "HP-M404-1234" },
  { id: "pr-2", name: "Printer-Epson-01", model: "Epson EcoTank L3250", type: "printer" as DeviceType, ip: "192.168.1.51", mac: "2B:3C:4D:5E:6F:7A", status: "online" as const, firmware: "1.04", uptime: "60d 10h", location: "Recepção", department: "Administrativo", serial: "EP-L325-5678" },
  { id: "pr-3", name: "Printer-HP-Cor", model: "HP Color LaserJet Pro M255dw", type: "printer" as DeviceType, ip: "192.168.1.52", mac: "3C:4D:5E:6F:7A:8B", status: "warning" as const, firmware: "20231201", uptime: "90d 3h", location: "Marketing", department: "Marketing", serial: "HP-M255-9012" },

  // CAMERAS
  { id: "cam-1", name: "Camera-Garagem", model: "G4 Bullet", type: "camera" as DeviceType, ip: "192.168.1.20", mac: "4D:5E:6F:7A:8B:9C", status: "online" as const, firmware: "4.72.1", uptime: "45d 12h", location: "Garagem", department: "Segurança", serial: "CAM-G4B-3456" },
  { id: "cam-2", name: "Camera-Escritório", model: "G4 Pro", type: "camera" as DeviceType, ip: "192.168.1.21", mac: "5E:6F:7A:8B:9C:AD", status: "online" as const, firmware: "4.72.1", uptime: "45d 12h", location: "Escritório", department: "Segurança", serial: "CAM-G4P-7890" },
  { id: "cam-3", name: "Camera-Entrada", model: "G3 Instant", type: "camera" as DeviceType, ip: "192.168.1.22", mac: "6F:7A:8B:9C:AD:BE", status: "online" as const, firmware: "4.68.20", uptime: "30d 8h", location: "Entrada", department: "Segurança", serial: "CAM-G3I-2345" },

  // PHONES
  { id: "ph-1", name: "Telefone-01", model: "UniFi Talk Touch", type: "phone" as DeviceType, ip: "192.168.1.60", mac: "7A:8B:9C:AD:BE:CF", status: "online" as const, firmware: "2.4.0", uptime: "20d 6h", location: "Recepção", department: "Administrativo", serial: "PH-UT-6789" },
  { id: "ph-2", name: "Telefone-02", model: "UniFi Talk Touch", type: "phone" as DeviceType, ip: "192.168.1.61", mac: "8B:9C:AD:BE:CF:DA", status: "online" as const, firmware: "2.4.0", uptime: "20d 6h", location: "Diretoria", department: "Diretoria", serial: "PH-UT-0123" },

  // SERVERS
  { id: "srv-1", name: "Server-DB-01", model: "Dell PowerEdge R750", type: "server" as DeviceType, ip: "192.168.2.50", mac: "9C:AD:BE:CF:DA:EB", status: "online" as const, firmware: "iDRAC 7.10", uptime: "180d 12h", location: "Datacenter", department: "TI", serial: "DELL-R750-4567" },
  { id: "srv-2", name: "Server-APP-01", model: "Dell PowerEdge R650", type: "server" as DeviceType, ip: "192.168.2.51", mac: "AD:BE:CF:DA:EB:FC", status: "online" as const, firmware: "iDRAC 7.10", uptime: "180d 12h", location: "Datacenter", department: "TI", serial: "DELL-R650-8901" },

  // VMS
  { id: "vm-1", name: "VM-Web-01", model: "Ubuntu 22.04 LTS", type: "vm" as DeviceType, ip: "192.168.2.100", mac: "BE:CF:DA:EB:FC:01", status: "online" as const, firmware: "Kernel 5.15", uptime: "90d 8h", location: "VMware ESXi", department: "TI", serial: "VM-WEB-1111" },
  { id: "vm-2", name: "VM-DB-01", model: "Ubuntu 22.04 LTS", type: "vm" as DeviceType, ip: "192.168.2.101", mac: "CF:DA:EB:FC:01:23", status: "online" as const, firmware: "Kernel 5.15", uptime: "90d 8h", location: "VMware ESXi", department: "TI", serial: "VM-DB-2222" },
  { id: "vm-3", name: "VM-Win-01", model: "Windows Server 2022", type: "vm" as DeviceType, ip: "192.168.2.102", mac: "DA:EB:FC:01:23:45", status: "online" as const, firmware: "Build 20348", uptime: "45d 3h", location: "VMware ESXi", department: "TI", serial: "VM-WIN-3333" },

  // NAS
  { id: "nas-1", name: "NAS-Synology", model: "DS920+", type: "nas" as DeviceType, ip: "192.168.1.30", mac: "EB:FC:01:23:45:67", status: "online" as const, firmware: "DSM 7.2.1", uptime: "120d 5h", location: "Escritório", department: "TI", serial: "SYN-DS92-4444" },

  // UPS
  { id: "ups-1", name: "UPS-Rack-01", model: "APC Smart-UPS 1500", type: "ups" as DeviceType, ip: "192.168.1.40", mac: "FC:01:23:45:67:89", status: "online" as const, firmware: "15.0", uptime: "365d 0h", location: "Rack Principal", department: "TI", serial: "APC-SU15-5555" },

  // STORAGE
  { id: "stg-1", name: "Storage-Backup", model: "Synology DS1621+", type: "storage" as DeviceType, ip: "192.168.1.31", mac: "01:23:45:67:89:AB", status: "online" as const, firmware: "DSM 7.2.1", uptime: "120d 5h", location: "Datacenter", department: "TI", serial: "SYN-DS16-6666" },
];

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

// ============ SWITCH PORTS ============
export const switchPorts: Record<string, { port: number; name: string; speed: string; poe: boolean; poeWatts: number; device: string; mac: string; vlan: number; status: string }[]> = {
  "sw-1": Array.from({ length: 24 }, (_, i) => ({
    port: i + 1,
    name: i === 0 ? "Uplink UDM" : i === 1 ? "Desktop-Gaming" : i === 2 ? "Printer-HP" : i === 3 ? "AP-Sala" : i === 4 ? "AP-Escritório" : i === 5 ? "AP-Pátio" : i === 6 ? "Camera-Garagem" : i === 7 ? "Camera-Escritório" : i === 8 ? "IP Phone 01" : i === 9 ? "IP Phone 02" : i === 10 ? "NAS-Synology" : i === 11 ? "UPS-APC" : `Porta ${i + 1}`,
    speed: i < 2 ? "10 Gbps" : i < 8 ? "1 Gbps" : i < 12 ? "100 Mbps" : "—",
    poe: i >= 3 && i <= 8,
    poeWatts: i === 3 ? 12.5 : i === 4 ? 11.8 : i === 5 ? 8.2 : i === 6 ? 6.4 : i === 7 ? 13.1 : i === 8 ? 4.2 : 0,
    device: i === 0 ? "UDM-Pro" : i === 1 ? "Desktop-Gaming" : i === 2 ? "Printer-HP-01" : i === 3 ? "AP-Sala" : i === 4 ? "AP-Escritório" : i === 5 ? "AP-Pátio" : i === 6 ? "Camera-Garagem" : i === 7 ? "Camera-Escritório" : i === 8 ? "Telefone-01" : i === 9 ? "Telefone-02" : i === 10 ? "NAS-Synology" : i === 11 ? "UPS-Rack-01" : "—",
    mac: i < 12 ? ["FC:EC:DA:12:34:56", "44:55:66:DD:EE:FF", "1A:2B:3C:4D:5E:6F", "FC:EC:DA:55:66:77", "FC:EC:DA:66:77:88", "FC:EC:DA:77:88:99", "4D:5E:6F:7A:8B:9C", "5E:6F:7A:8B:9C:AD", "7A:8B:9C:AD:BE:CF", "8B:9C:AD:BE:CF:DA", "EB:FC:01:23:45:67", "FC:01:23:45:67:89"][i]! : "—",
    vlan: i < 3 ? 10 : i < 9 ? 10 : i === 10 ? 10 : i === 11 ? 10 : 1,
    status: i < 12 ? "active" : i < 20 ? "inactive" : "disabled",
  })),
};

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
