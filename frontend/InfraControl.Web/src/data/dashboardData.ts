export type NavItem = { label: string; icon: string; badge?: string; route: string };

export const navigation: NavItem[] = [
  { label: "Visão Geral", icon: "pi pi-th-large", route: "/" },
  { label: "Topologia", icon: "pi pi-directions", route: "/topology" },
  { label: "WiFi", icon: "pi pi-wifi", route: "/wifi" },
  { label: "Dispositivos", icon: "pi pi-server", badge: "128", route: "/devices" },
  { label: "Switches", icon: "pi pi-credit-card", route: "/switches" },
  { label: "Clientes", icon: "pi pi-users", route: "/clients" },
  { label: "Rede", icon: "pi pi-share-alt", route: "/network" },
  { label: "Locations", icon: "pi pi-map-marker", route: "/locations" },
];

export const secondaryNavigation: NavItem[] = [
  { label: "Insights", icon: "pi pi-chart-bar", route: "/insights" },
  { label: "Alertas", icon: "pi pi-bell", badge: "7", route: "/alerts" },
  { label: "Incidentes", icon: "pi pi-exclamation-triangle", badge: "3", route: "/incidents" },
  { label: "Tarefas", icon: "pi pi-check-square", badge: "12", route: "/tasks" },
  { label: "SLA/SLO", icon: "pi pi-chart-line", route: "/sla" },
  { label: "Notificações", icon: "pi pi-envelope", route: "/notifications" },
  { label: "Acesso Remoto", icon: "pi pi-external-link", route: "/remote" },
  { label: "Conhecimento", icon: "pi pi-book", route: "/knowledge" },
  { label: "Logs", icon: "pi pi-list", route: "/logs" },
  { label: "Health", icon: "pi pi-heart", route: "/health" },
  { label: "Usuários", icon: "pi pi-user", route: "/users" },
  { label: "Auditoria", icon: "pi pi-history", route: "/audit" },
  { label: "Configurações", icon: "pi pi-cog", route: "/settings" },
];

// ============ SUBSYSTEMS ============
export const subsystems = [
  { name: "WAN", icon: "pi pi-globe", status: "online" as const },
  { name: "WWW", icon: "pi pi-external-link", status: "online" as const },
  { name: "WLAN", icon: "pi pi-wifi", status: "online" as const },
  { name: "LAN", icon: "pi pi-sitemap", status: "online" as const },
  { name: "VPN", icon: "pi pi-lock", status: "warning" as const },
];

// ============ GATEWAY ============
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
  wanIp: "200.150.82.34",
  dns: "8.8.8.8, 8.8.4.4",
  isp: "Vivo Fibra",
  speedDown: "500 Mbps",
  speedUp: "250 Mbps",
};

// ============ TRAFFIC ============
export const trafficData = {
  rx: [120, 180, 220, 195, 310, 280, 340, 290, 250, 320, 380, 350, 290, 260, 310, 370, 420, 390, 340, 300, 280, 350, 400, 380],
  tx: [40, 65, 80, 70, 120, 95, 140, 110, 90, 130, 160, 145, 110, 85, 125, 150, 180, 160, 135, 110, 95, 140, 170, 155],
};

// ============ TOP CLIENTS ============
export const topClients = [
  { name: "iPhone de Maria", ip: "192.168.1.45", usage: "2.4 GB", mac: "AA:BB:CC:11:22:33", type: "WiFi" },
  { name: "MacBook-Pro", ip: "192.168.1.102", usage: "1.8 GB", mac: "DD:EE:FF:44:55:66", type: "WiFi" },
  { name: "Smart-TV-LG", ip: "192.168.1.78", usage: "5.2 GB", mac: "11:22:33:AA:BB:CC", type: "WiFi" },
  { name: "Desktop-Gaming", ip: "192.168.1.15", usage: "3.1 GB", mac: "44:55:66:DD:EE:FF", type: "Ethernet" },
  { name: "iPad-Sala", ip: "192.168.1.67", usage: "0.9 GB", mac: "77:88:99:AA:BB:CC", type: "WiFi" },
];

// ============ RECENT EVENTS ============
export const recentEvents = [
  { message: "Novo cliente conectado", source: "WLAN · iPhone de Maria", time: "2 min", type: "info" as const },
  { message: "Porta 8 ativada", source: "Switch-Core-01", time: "15 min", type: "info" as const },
  { message: "Atualização disponível", source: "UDM-Pro · v4.1.14", time: "1 h", type: "warning" as const },
  { message: "Cliente desconectado", source: "WLAN · iPad-Sala", time: "2 h", type: "info" as const },
  { message: "Backup concluído", source: "Sistema · Rotina diária", time: "3 h", type: "success" as const },
];

// ============ DEVICES ============
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

// ============ CLIENTS ============
export const clients = [
  { id: "1", name: "iPhone de Maria", ip: "192.168.1.45", mac: "AA:BB:CC:11:22:33", type: "WiFi", ap: "AP-Sala", signal: -42, speed: "866 Mbps", usage: "2.4 GB", os: "iOS 18.1", activity: ["Safari", "YouTube", "WhatsApp"] },
  { id: "2", name: "MacBook-Pro", ip: "192.168.1.102", mac: "DD:EE:FF:44:55:66", type: "WiFi", ap: "AP-Escritório", signal: -38, speed: "1200 Mbps", usage: "1.8 GB", os: "macOS Sequoia", activity: ["Chrome", "VS Code", "Slack"] },
  { id: "3", name: "Smart-TV-LG", ip: "192.168.1.78", mac: "11:22:33:AA:BB:CC", type: "WiFi", ap: "AP-Sala", signal: -55, speed: "433 Mbps", usage: "5.2 GB", os: "webOS 24", activity: ["Netflix", "YouTube"] },
  { id: "4", name: "Desktop-Gaming", ip: "192.168.1.15", mac: "44:55:66:DD:EE:FF", type: "Ethernet", port: "Switch-Core-01:Porta 1", speed: "1000 Mbps", usage: "3.1 GB", os: "Windows 11", activity: ["Steam", "Discord"] },
  { id: "5", name: "iPad-Sala", ip: "192.168.1.67", mac: "77:88:99:AA:BB:CC", type: "WiFi", ap: "AP-Sala", signal: -50, speed: "866 Mbps", usage: "0.9 GB", os: "iPadOS 18.1", activity: ["Safari", "Apple Music"] },
  { id: "6", name: "Alexa-Echo", ip: "192.168.1.80", mac: "BB:CC:DD:EE:FF:00", type: "WiFi", ap: "AP-Sala", signal: -48, speed: "72 Mbps", usage: "0.3 GB", os: "FireOS", activity: ["Spotify"] },
  { id: "7", name: "Printer-HP", ip: "192.168.1.50", mac: "1A:2B:3C:4D:5E:6F", type: "Ethernet", port: "Switch-Core-01:Porta 3", speed: "100 Mbps", usage: "12 MB", os: "HP Firmware", activity: [] },
];

// ============ ALERTS ============
export const alertsData = [
  { id: "1", title: "AP-Pátio com instabilidade", detail: "Alta taxa de reassociação de clientes · Atualizado há 5 min", severity: "warning" as const },
  { id: "2", title: "Switch-Rede offline", detail: "Não responde desde 14/09 08:32 · Verificar conexão física", severity: "critical" as const },
  { id: "3", title: "Atualização disponível", detail: "UDM-Pro v4.1.14 disponível para instalação", severity: "info" as const },
  { id: "4", title: "Certificado SSL expira em 14 dias", detail: "Portal externo · Renovar antes de 28/09", severity: "warning" as const },
  { id: "5", title: "Porta 12 com erros CRC", detail: "Switch-Core-01 · 247 erros nas últimas 24h", severity: "warning" as const },
];

// ============ LOGS ============
export const logsData = [
  { time: "14/09 15:42:12", level: "info", message: "Cliente conectado: iPhone de Maria", source: "WLAN" },
  { time: "14/09 15:41:58", level: "info", message: "DHCP lease atribuído: 192.168.1.45", source: "DHCP" },
  { time: "14/09 15:40:33", level: "warning", message: "AP-Pátio: Associação de cliente falhou", source: "WLAN" },
  { time: "14/09 15:39:17", level: "info", message: "Porta 8: Link up - 1 Gbps", source: "Switch" },
  { time: "14/09 15:38:45", level: "info", message: "Cliente desconectado: iPad-Sala", source: "WLAN" },
  { time: "14/09 15:37:22", level: "error", message: "Switch-Rede: Heartbeat perdido", source: "Sistema" },
  { time: "14/09 15:36:10", level: "info", message: "Speed test concluído: 485/89 Mbps", source: "WAN" },
  { time: "14/09 15:35:01", level: "info", message: "Firewall: Bloqueio de IP externo", source: "Firewall" },
  { time: "14/09 15:34:22", level: "info", message: "Rota estática adicionada: 10.0.0.0/8", source: "Rota" },
  { time: "14/09 15:33:10", level: "warning", message: "DNS externo lento: 45ms resposta", source: "DNS" },
  { time: "14/09 15:32:05", level: "info", message: "VLAN 10 criada: Administração", source: "Rede" },
  { time: "14/09 15:31:00", level: "info", message: "NTP sincronizado: pool.ntp.org", source: "Sistema" },
];

// ============ TOPOLOGY ============
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

// ============ NETWORK / VLANs ============
export const vlans = [
  { id: 1, name: "Administração", subnet: "192.168.1.0/24", gateway: "192.168.1.1", dhcp: "192.168.1.100-200", devices: 42, vlanId: 10, color: "#3b82f6" },
  { id: 2, name: "Produção", subnet: "192.168.2.0/24", gateway: "192.168.2.1", dhcp: "192.168.2.100-200", devices: 35, vlanId: 20, color: "#22c55e" },
  { id: 3, name: "Convidados", subnet: "192.168.3.0/24", gateway: "192.168.3.1", dhcp: "192.168.3.100-250", devices: 12, vlanId: 30, color: "#f59e0b" },
  { id: 4, name: "IoT", subnet: "192.168.4.0/24", gateway: "192.168.4.1", dhcp: "192.168.4.100-200", devices: 8, vlanId: 40, color: "#8b5cf6" },
  { id: 5, name: "VoIP", subnet: "192.168.5.0/24", gateway: "192.168.5.1", dhcp: "192.168.5.10-50", devices: 15, vlanId: 50, color: "#ec4899" },
];

export const dhcpLeases = [
  { ip: "192.168.1.45", mac: "AA:BB:CC:11:22:33", hostname: "iPhone-de-Maria", expires: "14/09 18:42", vlan: "Administração" },
  { ip: "192.168.1.102", mac: "DD:EE:FF:44:55:66", hostname: "MacBook-Pro", expires: "14/09 20:15", vlan: "Administração" },
  { ip: "192.168.1.78", mac: "11:22:33:AA:BB:CC", hostname: "Smart-TV-LG", expires: "14/09 19:30", vlan: "Administração" },
  { ip: "192.168.1.15", mac: "44:55:66:DD:EE:FF", hostname: "Desktop-Gaming", expires: "14/09 22:00", vlan: "Administração" },
  { ip: "192.168.1.67", mac: "77:88:99:AA:BB:CC", hostname: "iPad-Sala", expires: "14/09 17:45", vlan: "Administração" },
  { ip: "192.168.2.50", mac: "AA:11:BB:22:CC:33", hostname: "Server-DB-01", expires: "Reservado", vlan: "Produção" },
  { ip: "192.168.2.51", mac: "DD:44:EE:55:FF:66", hostname: "Server-APP-01", expires: "Reservado", vlan: "Produção" },
  { ip: "192.168.3.120", mac: "12:34:56:78:9A:BC", hostname: "Guest-iPhone", expires: "14/09 16:30", vlan: "Convidados" },
];

export const routes = [
  { destination: "0.0.0.0/0", gateway: "200.150.82.1", interface: "WAN1", metric: 1, type: "Estática" },
  { destination: "192.168.0.0/16", gateway: "192.168.1.1", interface: "LAN", metric: 0, type: "Conectada" },
  { destination: "10.0.0.0/8", gateway: "192.168.1.254", interface: "LAN", metric: 10, type: "Estática" },
  { destination: "172.16.0.0/12", gateway: "192.168.1.254", interface: "LAN", metric: 10, type: "Estática" },
];

export const firewallRules = [
  { id: 1, name: "Bloquear tráfego externo", action: "Bloquear", protocol: "TCP/UDP", source: "0.0.0.0/0", destination: "192.168.0.0/16", port: "Todo", enabled: true },
  { id: 2, name: "Permitir HTTPS externo", action: "Permitir", protocol: "TCP", source: "0.0.0.0/0", destination: "192.168.1.20", port: "443", enabled: true },
  { id: 3, name: "Permitir SSH interno", action: "Permitir", protocol: "TCP", source: "192.168.1.0/24", destination: "192.168.1.0/24", port: "22", enabled: true },
  { id: 4, name: "Bloquear IoT acesso LAN", action: "Bloquear", protocol: "Todo", source: "192.168.4.0/24", destination: "192.168.1.0/24", port: "Todo", enabled: true },
  { id: 5, name: "Permitir DNS", action: "Permitir", protocol: "UDP", source: "192.168.0.0/16", destination: "192.168.1.1", port: "53", enabled: true },
];

// ============ WiFi ============
export const wifiNetworks = [
  { id: 1, ssid: "Corporativo-5G", band: "5 GHz", channel: 36, width: "80 MHz", security: "WPA3-Enterprise", vlan: 10, clients: 42, status: "active" as const, aps: ["AP-Sala", "AP-Escritório", "AP-Pátio"] },
  { id: 2, ssid: "Convidados", band: "2.4 GHz + 5 GHz", channel: "Auto", width: "40 MHz", security: "WPA2-Personal", vlan: 30, clients: 12, status: "active" as const, aps: ["AP-Sala", "AP-Escritório"] },
  { id: 3, ssid: "IoT-Devices", band: "2.4 GHz", channel: 6, width: "20 MHz", security: "WPA2-Personal", vlan: 40, clients: 8, status: "active" as const, aps: ["AP-Sala", "AP-Pátio"] },
  { id: 4, ssid: "VoIP-WiFi", band: "5 GHz", channel: 149, width: "40 MHz", security: "WPA2-Enterprise", vlan: 50, clients: 15, status: "active" as const, aps: ["AP-Sala", "AP-Escritório"] },
];

export const apData = [
  { id: "1", name: "AP-Sala", model: "U6 Pro", ip: "192.168.1.10", status: "online" as const, clients: 24, cpu: 15, mem: 32, channels: ["2.4 GHz (ch 1)", "5 GHz (ch 36)"], txPower: "High", rxBytes: "2.4 GB", txBytes: "1.8 GB", uptime: "45d 12h", firmware: "6.6.7" },
  { id: "2", name: "AP-Escritório", model: "U6 Pro", ip: "192.168.1.11", status: "online" as const, clients: 18, cpu: 12, mem: 28, channels: ["2.4 GHz (ch 11)", "5 GHz (ch 149)"], txPower: "Medium", rxBytes: "1.8 GB", txBytes: "1.2 GB", uptime: "45d 12h", firmware: "6.6.7" },
  { id: "3", name: "AP-Pátio", model: "U6 Mesh", ip: "192.168.1.12", status: "warning" as const, clients: 5, cpu: 45, mem: 58, channels: ["5 GHz (ch 44)"], txPower: "High", rxBytes: "0.4 GB", txBytes: "0.2 GB", uptime: "12d 8h", firmware: "6.6.7" },
];

// ============ REMOTE ACCESS ============
export const remoteSessions = [
  { id: "1", device: "Server-DB-01", ip: "192.168.2.50", type: "SSH", user: "admin", status: "active" as const, startTime: "14/09 14:30", duration: "1h 12m", bytes: "2.4 MB" },
  { id: "2", device: "Server-APP-01", ip: "192.168.2.51", type: "RDP", user: "paulo.bumba", status: "active" as const, startTime: "14/09 15:00", duration: "42m", bytes: "15.8 MB" },
  { id: "3", device: "UDM-Pro", ip: "192.168.1.1", type: "SSH", user: "root", status: "closed" as const, startTime: "14/09 10:15", duration: "5m", bytes: "12 KB" },
  { id: "4", device: "Switch-Core-01", ip: "192.168.1.2", type: "SSH", user: "admin", status: "closed" as const, startTime: "14/09 09:00", duration: "12m", bytes: "48 KB" },
];

export const remoteDevices = [
  { name: "Server-DB-01", ip: "192.168.2.50", os: "Ubuntu 22.04", type: "SSH", status: "online" as const, lastAccess: "14/09 14:30" },
  { name: "Server-APP-01", ip: "192.168.2.51", os: "Windows Server 2022", type: "RDP", status: "online" as const, lastAccess: "14/09 15:00" },
  { name: "UDM-Pro", ip: "192.168.1.1", os: "UniFi OS", type: "SSH", status: "online" as const, lastAccess: "14/09 10:15" },
  { name: "Switch-Core-01", ip: "192.168.1.2", os: "UniFi OS", type: "SSH", status: "online" as const, lastAccess: "14/09 09:00" },
  { name: "NAS-Synology", ip: "192.168.1.30", os: "DSM 7.2", type: "SSH", status: "online" as const, lastAccess: "13/09 22:00" },
  { name: "Printer-HP", ip: "192.168.1.50", os: "HP Firmware", type: "—", status: "offline" as const, lastAccess: "12/09 18:00" },
];

// ============ TASKS ============
export const tasks = [
  { id: "1", title: "Atualizar firmware UDM-Pro", priority: "high" as const, status: "in_progress" as const, assignee: "Paulo Bumba", device: "UDM-Pro", dueDate: "15/09/2026", tags: ["firmware", "urgente"] },
  { id: "2", title: "Investigar instabilidade AP-Pátio", priority: "high" as const, status: "todo" as const, assignee: "Paulo Bumba", device: "AP-Pátio", dueDate: "14/09/2026", tags: ["wifi", "estabilidade"] },
  { id: "3", title: "Configurar VLAN VoIP", priority: "medium" as const, status: "done" as const, assignee: "Paulo Bumba", device: "Switch-Core-01", dueDate: "12/09/2026", tags: ["rede", "vlan"] },
  { id: "4", title: "Renovar certificado SSL", priority: "high" as const, status: "todo" as const, assignee: "Paulo Bumba", device: "Portal externo", dueDate: "28/09/2026", tags: ["segurança", "certificado"] },
  { id: "5", title: "Resolver erros CRC porta 12", priority: "medium" as const, status: "in_progress" as const, assignee: "Paulo Bumba", device: "Switch-Core-01", dueDate: "16/09/2026", tags: ["switch", "cabo"] },
  { id: "6", title: "Documentar topologia de rede", priority: "low" as const, status: "todo" as const, assignee: "Paulo Bumba", device: "Geral", dueDate: "20/09/2026", tags: ["documentação"] },
  { id: "7", title: "Configurar backup automático", priority: "medium" as const, status: "done" as const, assignee: "Paulo Bumba", device: "UDM-Pro", dueDate: "10/09/2026", tags: ["backup"] },
  { id: "8", title: "Testar VPN site-a-site", priority: "medium" as const, status: "todo" as const, assignee: "Paulo Bumba", device: "UDM-Pro", dueDate: "18/09/2026", tags: ["vpn"] },
  { id: "9", title: "Instalar AP novo no estoque", priority: "low" as const, status: "todo" as const, assignee: "Paulo Bumba", device: "Novo AP", dueDate: "22/09/2026", tags: ["wifi", "instalação"] },
  { id: "10", title: "Migrar clientes para WPA3", priority: "medium" as const, status: "todo" as const, assignee: "Paulo Bumba", device: "Corporativo-5G", dueDate: "30/09/2026", tags: ["wifi", "segurança"] },
  { id: "11", title: "Configurar alertas de email", priority: "low" as const, status: "done" as const, assignee: "Paulo Bumba", device: "Sistema", dueDate: "08/09/2026", tags: ["notificações"] },
  { id: "12", title: "Auditar regras de firewall", priority: "medium" as const, status: "todo" as const, assignee: "Paulo Bumba", device: "UDM-Pro", dueDate: "25/09/2026", tags: ["segurança", "firewall"] },
];

// ============ KNOWLEDGE BASE ============
export const knowledgeArticles = [
  { id: "1", title: "Como conectar nova VLAN", category: "Rede", content: "Acesse Configurações > Rede > Adicionar VLAN. Defina ID, subnet e gateway.", tags: ["vlan", "rede"], lastUpdate: "10/09/2026" },
  { id: "2", title: "Troubleshooting WiFi lento", category: "WiFi", content: "Verifique interferência, canal, potência TX e número de clientes por AP.", tags: ["wifi", "performance"], lastUpdate: "12/09/2026" },
  { id: "3", title: "Como fazer backup do UDM-Pro", category: "Sistema", content: "Settings > Sistema > Backup > Backup Agora. Copie o arquivo para local seguro.", tags: ["backup", "udm"], lastUpdate: "08/09/2026" },
  { id: "4", title: "Configurar SSH no switch", category: "Switch", content: "Device > Configurações > Services > SSH. Defina senha e porta.", tags: ["ssh", "switch"], lastUpdate: "05/09/2026" },
  { id: "5", title: "Regras de firewall para IoT", category: "Segurança", content: "Crie regra bloqueando IoT (VLAN 40) de acessar LAN (VLAN 10). Permita apenas internet.", tags: ["firewall", "iot", "segurança"], lastUpdate: "11/09/2026" },
  { id: "6", title: "Como renovar certificado SSL", category: "Segurança", content: "Use Let's Encrypt ou compre de CA. Substitua cert.key e cert.pem no controller.", tags: ["ssl", "certificado"], lastUpdate: "01/09/2026" },
];

// ============ INSIGHTS ============
export const dpiApps = [
  { name: "YouTube", category: "Vídeo", bytes: "5.2 GB", percentage: 28, color: "#ef4444" },
  { name: "Netflix", category: "Streaming", bytes: "4.8 GB", percentage: 26, color: "#dc2626" },
  { name: "Chrome", category: "Navegador", bytes: "2.1 GB", percentage: 11, color: "#3b82f6" },
  { name: "Slack", category: "Comunicação", bytes: "1.2 GB", percentage: 6, color: "#8b5cf6" },
  { name: "Teams", category: "Comunicação", bytes: "0.9 GB", percentage: 5, color: "#7c3aed" },
  { name: "Steam", category: "Jogos", bytes: "1.8 GB", percentage: 10, color: "#1d4ed8" },
  { name: "WhatsApp", category: "Mensagens", bytes: "0.4 GB", percentage: 2, color: "#22c55e" },
  { name: "Outros", category: "Diversos", bytes: "2.1 GB", percentage: 12, color: "#94a3b8" },
];

export const trafficByClient = [
  { name: "Smart-TV-LG", download: "5.2 GB", upload: "0.1 GB", total: "5.3 GB" },
  { name: "Desktop-Gaming", download: "3.1 GB", upload: "0.8 GB", total: "3.9 GB" },
  { name: "iPhone de Maria", download: "2.4 GB", upload: "0.3 GB", total: "2.7 GB" },
  { name: "MacBook-Pro", download: "1.8 GB", upload: "0.4 GB", total: "2.2 GB" },
  { name: "iPad-Sala", download: "0.9 GB", upload: "0.1 GB", total: "1.0 GB" },
];

export const speedTestHistory = [
  { date: "14/09 15:36", download: "485 Mbps", upload: "89 Mbps", ping: "12ms", server: "São Paulo" },
  { date: "13/09 15:30", download: "478 Mbps", upload: "92 Mbps", ping: "11ms", server: "São Paulo" },
  { date: "12/09 15:25", download: "492 Mbps", upload: "85 Mbps", ping: "13ms", server: "São Paulo" },
  { date: "11/09 15:20", download: "465 Mbps", upload: "78 Mbps", ping: "15ms", server: "Rio de Janeiro" },
  { date: "10/09 15:15", download: "488 Mbps", upload: "91 Mbps", ping: "12ms", server: "São Paulo" },
];

// ============ USERS & ROLES ============
export const users = [
  { id: "1", name: "Paulo Bumba", email: "paulo.bumba@empresa.com", role: "Admin" as const, status: "active" as const, lastLogin: "14/09/2026 15:40", createdAt: "01/09/2026", sessions: 2 },
  { id: "2", name: "Ana Souza", email: "ana.souza@empresa.com", role: "Operator" as const, status: "active" as const, lastLogin: "14/09/2026 14:12", createdAt: "03/09/2026", sessions: 1 },
  { id: "3", name: "Carlos Lima", email: "carlos.lima@empresa.com", role: "Operator" as const, status: "active" as const, lastLogin: "13/09/2026 09:30", createdAt: "05/09/2026", sessions: 0 },
  { id: "4", name: "Mariana Reis", email: "mariana.reis@empresa.com", role: "Viewer" as const, status: "active" as const, lastLogin: "12/09/2026 17:45", createdAt: "07/09/2026", sessions: 1 },
  { id: "5", name: "Pedro Alves", email: "pedro.alves@empresa.com", role: "Viewer" as const, status: "inactive" as const, lastLogin: "08/09/2026 11:20", createdAt: "09/09/2026", sessions: 0 },
  { id: "6", name: "Fernanda Costa", email: "fernanda.costa@empresa.com", role: "Operator" as const, status: "pending" as const, lastLogin: "—", createdAt: "14/09/2026", sessions: 0 },
];

// ADR-016: a role é a única unidade de acesso — não existe matriz de
// permissões. Cada card mostra o nível canônico que a role alcança
// (RoleAccess no backend: src/InfraControl.Domain/Enums/RoleAccess.cs).
export const roles = [
  { id: "admin", name: "Admin", level: "administer", description: "Acesso total à plataforma", users: 1, system: true },
  { id: "operator", name: "Operator", level: "operate", description: "Operação e tarefas do dia a dia", users: 3, system: true },
  { id: "viewer", name: "Viewer", level: "read", description: "Somente leitura de dashboards e relatórios", users: 2, system: true },
] as const;

// ============ AUDIT (alinhado a AuditEntrySummary / GET /api/v1/admin/audit) ============
export const auditEntries = [
  { id: "a1", action: "Identity.Login", resource: "ApplicationRequest", outcome: "Success" as const, actorId: "paulo.bumba@empresa.com", correlationId: "corr-8f2a19c4", error: null as string | null, occurredAt: "14/09/2026 15:40:12" },
  { id: "a2", action: "IdentityRoles.Update", resource: "Role:Admin", outcome: "Success" as const, actorId: "paulo.bumba@empresa.com", correlationId: "corr-3b7e02d1", error: null as string | null, occurredAt: "14/09/2026 14:22:05" },
  { id: "a3", action: "Identity.Register", resource: "User", outcome: "Success" as const, actorId: "ana.souza@empresa.com", correlationId: "corr-9c1f44a8", error: null as string | null, occurredAt: "14/09/2026 11:08:47" },
  { id: "a4", action: "Identity.PasswordChange", resource: "User", outcome: "Failure" as const, actorId: "carlos.lima@empresa.com", correlationId: "corr-5d8a73b2", error: "Current password is incorrect", occurredAt: "13/09/2026 17:55:30" },
  { id: "a5", action: "Identity.Login", resource: "ApplicationRequest", outcome: "Failure" as const, actorId: "pedro.alves@empresa.com", correlationId: "corr-2e6c91f0", error: "Invalid credentials", occurredAt: "13/09/2026 09:14:22" },
  { id: "a6", action: "IdentityRoles.Update", resource: "Role:Viewer", outcome: "Success" as const, actorId: "paulo.bumba@empresa.com", correlationId: "corr-7a4b18e5", error: null as string | null, occurredAt: "12/09/2026 16:41:18" },
  { id: "a7", action: "ApplicationRequest", resource: "SearchAuditEntriesQuery", outcome: "Success" as const, actorId: "paulo.bumba@empresa.com", correlationId: "corr-1f9d63c7", error: null as string | null, occurredAt: "12/09/2026 16:40:02" },
  { id: "a8", action: "BootstrapAdmin.Run", resource: "ApplicationRequest", outcome: "Success" as const, actorId: null, correlationId: "corr-0a2b55d9", error: null as string | null, occurredAt: "01/09/2026 10:00:00" },
  { id: "a9", action: "Identity.PasswordReset", resource: "ApplicationRequest", outcome: "Success" as const, actorId: "mariana.reis@empresa.com", correlationId: "corr-4c8e10a3", error: null as string | null, occurredAt: "11/09/2026 13:27:55" },
  { id: "a10", action: "Identity.Login", resource: "ApplicationRequest", outcome: "Success" as const, actorId: "fernanda.costa@empresa.com", correlationId: "corr-6b3f82e1", error: null as string | null, occurredAt: "14/09/2026 08:33:41" },
  { id: "a11", action: "ApplicationRequest", resource: "UpdateRoleCommand", outcome: "Failure" as const, actorId: "ana.souza@empresa.com", correlationId: "corr-8d5a24f6", error: "Forbidden: missing permission IdentityRolesManage", occurredAt: "12/09/2026 10:19:07" },
  { id: "a12", action: "Identity.Logout", resource: "ApplicationRequest", outcome: "Success" as const, actorId: "paulo.bumba@empresa.com", correlationId: "corr-9e7c35b0", error: null as string | null, occurredAt: "14/09/2026 15:52:10" },
];

export const auditActionOptions = ["all", "Identity.Login", "Identity.Register", "Identity.Logout", "Identity.PasswordChange", "Identity.PasswordReset", "IdentityRoles.Update", "BootstrapAdmin.Run", "ApplicationRequest"];
export const auditOutcomeOptions = ["all", "Success", "Failure"];

// ============ INCIDENTS ============
export const incidents = [
  { id: "INC-001", title: "Switch-Rede offline", severity: "critical" as const, status: "investigating" as const, assignee: "Paulo Bumba", device: "Switch-Rede", openedAt: "14/09/2026 08:32", slaBreached: true, relatedAlerts: 2 },
  { id: "INC-002", title: "Instabilidade AP-Pátio", severity: "high" as const, status: "identified" as const, assignee: "Paulo Bumba", device: "AP-Pátio", openedAt: "14/09/2026 10:15", slaBreached: false, relatedAlerts: 1 },
  { id: "INC-003", title: "Erros CRC porta 12", severity: "medium" as const, status: "monitoring" as const, assignee: "Ana Souza", device: "Switch-Core-01", openedAt: "13/09/2026 14:40", slaBreached: false, relatedAlerts: 1 },
  { id: "INC-004", title: "Certificado SSL expira em 14 dias", severity: "low" as const, status: "resolved" as const, assignee: "Carlos Lima", device: "Portal externo", openedAt: "10/09/2026 09:00", slaBreached: false, relatedAlerts: 1 },
  { id: "INC-005", title: "Lentidão WAN intermitente", severity: "high" as const, status: "resolved" as const, assignee: "Paulo Bumba", device: "UDM-Pro", openedAt: "12/09/2026 16:20", slaBreached: true, relatedAlerts: 3 },
  { id: "INC-006", title: "DHCP sem leases na VLAN 40", severity: "medium" as const, status: "investigating" as const, assignee: "Ana Souza", device: "UDM-Pro", openedAt: "14/09/2026 11:50", slaBreached: false, relatedAlerts: 1 },
];

// ============ NOTIFICATIONS ============
export const notifications = [
  { id: "n1", title: "Incidente INC-001 aberto", body: "Switch-Rede offline desde 08:32", channel: "Email" as const, status: "sent" as const, sentAt: "14/09/2026 08:33", recipient: "paulo.bumba@empresa.com", read: false },
  { id: "n2", title: "Alerta: AP-Pátio instável", body: "Alta taxa de reassociação", channel: "Telegram" as const, status: "sent" as const, sentAt: "14/09/2026 10:16", recipient: "@infra_ops", read: false },
  { id: "n3", title: "SLA breach INC-005", body: "Lentidão WAN excedeu tempo de resposta", channel: "Email" as const, status: "sent" as const, sentAt: "12/09/2026 17:00", recipient: "paulo.bumba@empresa.com", read: true },
  { id: "n4", title: "Backup concluído", body: "Rotina diária 03:00 OK", channel: "Webhook" as const, status: "sent" as const, sentAt: "14/09/2026 03:01", recipient: "https://hooks.local/backup", read: true },
  { id: "n5", title: "Novo usuário pendente", body: "Fernanda Costa aguarda ativação", channel: "SignalR" as const, status: "sent" as const, sentAt: "14/09/2026 14:05", recipient: "dashboard", read: false },
  { id: "n6", title: "Tentativa de login falhou", body: "pedro.alves: conta inativa", channel: "Email" as const, status: "failed" as const, sentAt: "13/09/2026 09:15", recipient: "security@empresa.com", read: true },
  { id: "n7", title: "WhatsApp teste", body: "Mensagem de teste do canal", channel: "WhatsApp" as const, status: "queued" as const, sentAt: "14/09/2026 15:00", recipient: "+5511999999999", read: true },
];

export const notificationPreferences = [
  { channel: "Email", enabled: true, events: "Incidentes, Alertas críticos, SLA" },
  { channel: "Telegram", enabled: true, events: "Alertas" },
  { channel: "WhatsApp", enabled: false, events: "Incidentes" },
  { channel: "Webhook", enabled: true, events: "Todos" },
  { channel: "SignalR", enabled: true, events: "Tempo real (dashboard)" },
];

// ============ LOCATIONS & RACKS ============
export const locations = [
  { id: "loc-1", name: "Datacenter", type: "Datacenter", racks: 2, devices: 6, description: "Sala técnica principal", floors: 1 },
  { id: "loc-2", name: "Rack Principal", type: "Sala de equipamentos", racks: 1, devices: 5, description: "Core network e gateway", floors: 1 },
  { id: "loc-3", name: "Rack Escritório", type: "Sala de equipamentos", racks: 1, devices: 3, description: "Switch de acesso do escritório", floors: 1 },
  { id: "loc-4", name: "Escritório", type: "Área administrativa", racks: 0, devices: 4, description: "Estações e impressoras", floors: 1 },
  { id: "loc-5", name: "Salas", type: "Área comum", racks: 0, devices: 3, description: "Salas de reunião e estar", floors: 1 },
  { id: "loc-6", name: "Garagem", type: "Externo", racks: 0, devices: 1, description: "Câmera e acesso", floors: 1 },
];

export const racks = [
  { id: "rack-a", name: "Rack-A", location: "Rack Principal", units: 42, usedUnits: 18, devices: ["UDM-Pro (U1)", "Switch-Core-01 (U2)", "Switch-Rede (U3)", "UPS-Rack-01 (U10)"], power: "1.2 kW", temp: "24°C" },
  { id: "rack-b", name: "Rack-B", location: "Rack Escritório", units: 24, usedUnits: 6, devices: ["Switch-Office-01 (U1)"], power: "0.4 kW", temp: "26°C" },
  { id: "rack-c", name: "Rack-C", location: "Datacenter", units: 42, usedUnits: 24, devices: ["Server-DB-01 (U4)", "Server-APP-01 (U6)", "Storage-Backup (U12)"], power: "3.8 kW", temp: "22°C" },
];

// ============ SLA/SLO & SERVICES ============
export const services = [
  { id: "svc-1", name: "Rede Corporativa", status: "operational" as const, uptime: 99.97, slaTarget: 99.9, sloTarget: 99.5, openIncidents: 1, owner: "TI" },
  { id: "svc-2", name: "WiFi Corporativo", status: "degraded" as const, uptime: 99.82, slaTarget: 99.5, sloTarget: 99.0, openIncidents: 1, owner: "TI" },
  { id: "svc-3", name: "Acesso Remoto", status: "operational" as const, uptime: 99.99, slaTarget: 99.5, sloTarget: 99.0, openIncidents: 0, owner: "TI" },
  { id: "svc-4", name: "Portal Externo", status: "operational" as const, uptime: 99.95, slaTarget: 99.5, sloTarget: 99.0, openIncidents: 0, owner: "Marketing" },
  { id: "svc-5", name: "Email Interno", status: "operational" as const, uptime: 99.98, slaTarget: 99.9, sloTarget: 99.5, openIncidents: 0, owner: "TI" },
];

export const sloBudgets = [
  { service: "Rede Corporativa", budgetRemaining: 87, window: "30 dias", burnRate: "normal" as const },
  { service: "WiFi Corporativo", budgetRemaining: 42, window: "30 dias", burnRate: "fast" as const },
  { service: "Acesso Remoto", budgetRemaining: 95, window: "30 dias", burnRate: "normal" as const },
  { service: "Portal Externo", budgetRemaining: 78, window: "30 dias", burnRate: "normal" as const },
  { service: "Email Interno", budgetRemaining: 91, window: "30 dias", burnRate: "normal" as const },
];

// ============ SWITCHES & PORTS ============
export const switches = [
  { id: "sw-1", name: "Switch-Core-01", model: "Switch Pro 24 PoE", ip: "192.168.1.2", status: "online" as const, totalPorts: 24, activePorts: 12, poePorts: 6, firmware: "7.0.6", uptime: "45d 12h", location: "Rack-A / U2", vlans: [10, 20, 40, 50] },
  { id: "sw-2", name: "Switch-Office-01", model: "Switch Lite 16 PoE", ip: "192.168.1.3", status: "online" as const, totalPorts: 16, activePorts: 5, poePorts: 2, firmware: "7.0.6", uptime: "30d 5h", location: "Rack-B / U1", vlans: [10, 30] },
  { id: "sw-3", name: "Switch-Rede", model: "Switch Lite 8", ip: "192.168.1.4", status: "offline" as const, totalPorts: 8, activePorts: 0, poePorts: 0, firmware: "7.0.6", uptime: "0d 0h", location: "Rack-A / U3", vlans: [10] },
];

export const switchPortDetail = [
  { switch: "Switch-Core-01", port: 1, device: "UDM-Pro", type: "gateway", speed: "10 Gbps", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 2, device: "Desktop-Gaming", type: "client", speed: "1 Gbps", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 3, device: "Printer-HP-01", type: "printer", speed: "100 Mbps", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 4, device: "AP-Sala", type: "ap", speed: "1 Gbps", poe: true, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 5, device: "AP-Escritório", type: "ap", speed: "1 Gbps", poe: true, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 6, device: "AP-Pátio", type: "ap", speed: "1 Gbps", poe: true, vlan: 40, status: "active" },
  { switch: "Switch-Core-01", port: 7, device: "Camera-Garagem", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 8, device: "Camera-Escritório", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 9, device: "Telefone-01", type: "phone", speed: "100 Mbps", poe: true, vlan: 50, status: "active" },
  { switch: "Switch-Core-01", port: 10, device: "Telefone-02", type: "phone", speed: "100 Mbps", poe: false, vlan: 50, status: "active" },
  { switch: "Switch-Core-01", port: 11, device: "NAS-Synology", type: "nas", speed: "1 Gbps", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 12, device: "UPS-Rack-01", type: "ups", speed: "USB", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Core-01", port: 13, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "inactive" },
  { switch: "Switch-Core-01", port: 14, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "inactive" },
  { switch: "Switch-Core-01", port: 15, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "inactive" },
  { switch: "Switch-Core-01", port: 16, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "inactive" },
  { switch: "Switch-Core-01", port: 17, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "inactive" },
  { switch: "Switch-Core-01", port: 18, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "inactive" },
  { switch: "Switch-Core-01", port: 19, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled" },
  { switch: "Switch-Core-01", port: 20, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled" },
  { switch: "Switch-Core-01", port: 21, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled" },
  { switch: "Switch-Core-01", port: 22, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled" },
  { switch: "Switch-Core-01", port: 23, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled" },
  { switch: "Switch-Core-01", port: 24, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled" },
  { switch: "Switch-Office-01", port: 1, device: "Camera-Escritório", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
  { switch: "Switch-Office-01", port: 2, device: "Camera-Entrada", type: "camera", speed: "100 Mbps", poe: true, vlan: 10, status: "active" },
  { switch: "Switch-Office-01", port: 3, device: "Printer-Epson-01", type: "printer", speed: "100 Mbps", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Office-01", port: 4, device: "Printer-HP-Cor", type: "printer", speed: "100 Mbps", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Office-01", port: 5, device: "Storage-Backup", type: "storage", speed: "10 Gbps", poe: false, vlan: 10, status: "active" },
  { switch: "Switch-Office-01", port: 6, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "inactive" },
  { switch: "Switch-Rede", port: 1, device: "—", type: "empty", speed: "—", poe: false, vlan: 0, status: "disabled" },
];
