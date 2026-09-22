export type NavItem = { label: string; icon: string; badge?: string; route: string };

export const navigation: NavItem[] = [
  { label: "Visão Geral", icon: "pi pi-th-large", route: "/" },
  { label: "Topologia", icon: "pi pi-directions", route: "/topology" },
  { label: "WiFi", icon: "pi pi-wifi", route: "/wifi" },
  { label: "Dispositivos", icon: "pi pi-server", badge: "128", route: "/devices" },
  { label: "Clientes", icon: "pi pi-users", route: "/clients" },
];

export const secondaryNavigation: NavItem[] = [
  { label: "Insights", icon: "pi pi-chart-bar", route: "/insights" },
  { label: "Alertas", icon: "pi pi-bell", badge: "7", route: "/alerts" },
  { label: "Logs", icon: "pi pi-list", route: "/logs" },
  { label: "Configurações", icon: "pi pi-cog", route: "/settings" },
];

export const subsystems = [
  { name: "WAN", icon: "pi pi-globe", status: "online" as const },
  { name: "WWW", icon: "pi pi-external-link", status: "online" as const },
  { name: "WLAN", icon: "pi pi-wifi", status: "online" as const },
  { name: "LAN", icon: "pi pi-sitemap", status: "online" as const },
  { name: "VPN", icon: "pi pi-lock", status: "warning" as const },
];

export const gatewayData = {
  name: "Dream Machine Pro",
  model: "UDM-Pro",
  ip: "192.168.1.1",
  version: "4.1.13",
  uptime: "45d 12h 34m",
  cpu: 23,
  mem: 41,
  rx: "245.8 Mbps",
  tx: "89.3 Mbps",
  status: "online" as const,
};

export const trafficData = {
  rx: [120, 180, 220, 195, 310, 280, 340, 290, 250, 320, 380, 350, 290, 260, 310, 370, 420, 390, 340, 300, 280, 350, 400, 380],
  tx: [40, 65, 80, 70, 120, 95, 140, 110, 90, 130, 160, 145, 110, 85, 125, 150, 180, 160, 135, 110, 95, 140, 170, 155],
  labels: ["00:00", "01:00", "02:00", "03:00", "04:00", "05:00", "06:00", "07:00", "08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00", "18:00", "19:00", "20:00", "21:00", "22:00", "23:00"],
};

export const topClients = [
  { name: "iPhone de Maria", ip: "192.168.1.45", usage: "2.4 GB", mac: "AA:BB:CC:11:22:33", type: "WiFi" },
  { name: "MacBook-Pro", ip: "192.168.1.102", usage: "1.8 GB", mac: "DD:EE:FF:44:55:66", type: "WiFi" },
  { name: "Smart-TV-LG", ip: "192.168.1.78", usage: "5.2 GB", mac: "11:22:33:AA:BB:CC", type: "WiFi" },
  { name: "Desktop-Gaming", ip: "192.168.1.15", usage: "3.1 GB", mac: "44:55:66:DD:EE:FF", type: "Ethernet" },
  { name: "iPad-Sala", ip: "192.168.1.67", usage: "0.9 GB", mac: "77:88:99:AA:BB:CC", type: "WiFi" },
];

export const recentEvents = [
  { message: "Novo cliente conectado", source: "WLAN · iPhone de Maria", time: "2 min", type: "info" as const },
  { message: "Porta 8 ativada", source: "Switch-Core-01", time: "15 min", type: "info" as const },
  { message: "Atualização disponível", source: "UDM-Pro · v4.1.14", time: "1 h", type: "warning" as const },
  { message: "Cliente desconectado", source: "WLAN · iPad-Sala", time: "2 h", type: "info" as const },
  { message: "Backup concluído", source: "Sistema · Rotina diária", time: "3 h", type: "success" as const },
];

export const devices = [
  { id: "1", name: "UDM-Pro", model: "Dream Machine Pro", type: "gateway" as const, ip: "192.168.1.1", status: "online" as const, firmware: "4.1.13", uptime: "45d 12h", clients: 84, cpu: 23, mem: 41 },
  { id: "2", name: "Switch-Core-01", model: "Switch Pro 24 PoE", type: "switch" as const, ip: "192.168.1.2", status: "online" as const, firmware: "7.0.6", uptime: "45d 12h", clients: 0, ports: 24, poe: 12 },
  { id: "3", name: "Switch-Office-01", model: "Switch Lite 16 PoE", type: "switch" as const, ip: "192.168.1.3", status: "online" as const, firmware: "7.0.6", uptime: "30d 5h", clients: 0, ports: 16, poe: 6 },
  { id: "4", name: "AP-Sala", model: "U6 Pro", type: "ap" as const, ip: "192.168.1.10", status: "online" as const, firmware: "6.6.7", uptime: "45d 12h", clients: 24, channels: ["2.4 GHz", "5 GHz"] },
  { id: "5", name: "AP-Escritório", model: "U6 Pro", type: "ap" as const, ip: "192.168.1.11", status: "online" as const, firmware: "6.6.7", uptime: "45d 12h", clients: 18, channels: ["2.4 GHz", "5 GHz"] },
  { id: "6", name: "AP-Pátio", model: "U6 Mesh", type: "ap" as const, ip: "192.168.1.12", status: "warning" as const, firmware: "6.6.7", uptime: "12d 8h", clients: 5, channels: ["5 GHz"] },
  { id: "7", name: "Camera-Garagem", model: "G4 Bullet", type: "ap" as const, ip: "192.168.1.20", status: "online" as const, firmware: "4.72.1", uptime: "45d 12h", clients: 0 },
  { id: "8", name: "Switch-Rede", model: "Switch Lite 8", type: "switch" as const, ip: "192.168.1.4", status: "offline" as const, firmware: "7.0.6", uptime: "0d 0h", clients: 0, ports: 8, poe: 0 },
];

export const clients = [
  { id: "1", name: "iPhone de Maria", ip: "192.168.1.45", mac: "AA:BB:CC:11:22:33", type: "WiFi", ap: "AP-Sala", signal: -42, speed: "866 Mbps", usage: "2.4 GB", os: "iOS 18.1", activity: ["Safari", "YouTube", "WhatsApp"] },
  { id: "2", name: "MacBook-Pro", ip: "192.168.1.102", mac: "DD:EE:FF:44:55:66", type: "WiFi", ap: "AP-Escritório", signal: -38, speed: "1200 Mbps", usage: "1.8 GB", os: "macOS Sequoia", activity: ["Chrome", "VS Code", "Slack"] },
  { id: "3", name: "Smart-TV-LG", ip: "192.168.1.78", mac: "11:22:33:AA:BB:CC", type: "WiFi", ap: "AP-Sala", signal: -55, speed: "433 Mbps", usage: "5.2 GB", os: "webOS 24", activity: ["Netflix", "YouTube"] },
  { id: "4", name: "Desktop-Gaming", ip: "192.168.1.15", mac: "44:55:66:DD:EE:FF", type: "Ethernet", port: "Switch-Core-01:Porta 1", speed: "1000 Mbps", usage: "3.1 GB", os: "Windows 11", activity: ["Steam", "Discord"] },
  { id: "5", name: "iPad-Sala", ip: "192.168.1.67", mac: "77:88:99:AA:BB:CC", type: "WiFi", ap: "AP-Sala", signal: -50, speed: "866 Mbps", usage: "0.9 GB", os: "iPadOS 18.1", activity: ["Safari", "Apple Music"] },
  { id: "6", name: "Alexa-Echo", ip: "192.168.1.80", mac: "BB:CC:DD:EE:FF:00", type: "WiFi", ap: "AP-Sala", signal: -48, speed: "72 Mbps", usage: "0.3 GB", os: "FireOS", activity: ["Spotify"] },
  { id: "7", name: "Printer-HP", ip: "192.168.1.50", mac: "1A:2B:3C:4D:5E:6F", type: "Ethernet", port: "Switch-Core-01:Porta 3", speed: "100 Mbps", usage: "12 MB", os: "HP Firmware", activity: [] },
];

export const alertsData = [
  { id: "1", title: "AP-Pátio com instabilidade", detail: "Alta taxa de reassociação de clientes · Atualizado há 5 min", severity: "warning" as const },
  { id: "2", title: "Switch-Rede offline", detail: "Não responde desde 14/09 08:32 · Verificar conexão física", severity: "critical" as const },
  { id: "3", title: "Atualização disponível", detail: "UDM-Pro v4.1.14 disponível para instalação", severity: "info" as const },
  { id: "4", title: "Certificado SSL expira em 14 dias", detail: "Portal externo · Renovar antes de 28/09", severity: "warning" as const },
  { id: "5", title: "Porta 12 com erros CRC", detail: "Switch-Core-01 · 247 erros nas últimas 24h", severity: "warning" as const },
];

export const logsData = [
  { time: "14/09 15:42:12", level: "info", message: "Cliente conectado: iPhone de Maria", source: "WLAN" },
  { time: "14/09 15:41:58", level: "info", message: "DHCP lease atribuído: 192.168.1.45", source: "DHCP" },
  { time: "14/09 15:40:33", level: "warning", message: "AP-Pátio: Associação de cliente falhou", source: "WLAN" },
  { time: "14/09 15:39:17", level: "info", message: "Porta 8: Link up - 1 Gbps", source: "Switch" },
  { time: "14/09 15:38:45", level: "info", message: "Cliente desconectado: iPad-Sala", source: "WLAN" },
  { time: "14/09 15:37:22", level: "error", message: "Switch-Rede: Heartbeat perdido", source: "Sistema" },
  { time: "14/09 15:36:10", level: "info", message: "Speed test concluído: 485/89 Mbps", source: "WAN" },
  { time: "14/09 15:35:01", level: "info", message: "Firewall: Bloqueio de IP externo", source: "Firewall" },
];

export const topologyNodes = [
  { id: "wan", label: "WAN", x: 400, y: 40, type: "wan" as const, status: "online" as const },
  { id: "udm", label: "UDM-Pro", x: 400, y: 140, type: "gateway" as const, status: "online" as const, model: "Dream Machine Pro" },
  { id: "sw1", label: "Switch-Core-01", x: 200, y: 280, type: "switch" as const, status: "online" as const, model: "Switch Pro 24 PoE" },
  { id: "sw2", label: "Switch-Office-01", x: 600, y: 280, type: "switch" as const, status: "online" as const, model: "Switch Lite 16 PoE" },
  { id: "ap1", label: "AP-Sala", x: 100, y: 420, type: "ap" as const, status: "online" as const, model: "U6 Pro" },
  { id: "ap2", label: "AP-Escritório", x: 300, y: 420, type: "ap" as const, status: "online" as const, model: "U6 Pro" },
  { id: "ap3", label: "AP-Pátio", x: 500, y: 420, type: "ap" as const, status: "warning" as const, model: "U6 Mesh" },
  { id: "cam1", label: "Camera-Garagem", x: 700, y: 420, type: "ap" as const, status: "online" as const, model: "G4 Bullet" },
  { id: "c1", label: "iPhone de Maria", x: 50, y: 540, type: "client" as const, status: "online" as const },
  { id: "c2", label: "MacBook-Pro", x: 170, y: 540, type: "client" as const, status: "online" as const },
  { id: "c3", label: "Smart-TV-LG", x: 260, y: 540, type: "client" as const, status: "online" as const },
  { id: "c4", label: "Desktop-Gaming", x: 350, y: 540, type: "client" as const, status: "online" as const },
  { id: "c5", label: "iPad-Sala", x: 440, y: 540, type: "client" as const, status: "online" as const },
  { id: "c6", label: "Alexa-Echo", x: 530, y: 540, type: "client" as const, status: "online" as const },
  { id: "c7", label: "Printer-HP", x: 620, y: 540, type: "client" as const, status: "online" as const },
];

export const topologyEdges = [
  { from: "wan", to: "udm" },
  { from: "udm", to: "sw1" },
  { from: "udm", to: "sw2" },
  { from: "sw1", to: "ap1" },
  { from: "sw1", to: "ap2" },
  { from: "sw2", to: "ap3" },
  { from: "sw2", to: "cam1" },
  { from: "ap1", to: "c1" },
  { from: "ap1", to: "c2" },
  { from: "ap2", to: "c3" },
  { from: "ap2", to: "c4" },
  { from: "ap3", to: "c5" },
  { from: "ap3", to: "c6" },
  { from: "sw2", to: "c7" },
];
