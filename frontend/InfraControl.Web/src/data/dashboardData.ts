export type NavItem = { label: string; icon: string; badge?: string };

export const navigation: NavItem[] = [
  { label: "Visão geral", icon: "pi pi-th-large" },
  { label: "Inventário", icon: "pi pi-server" },
  { label: "Dispositivos", icon: "pi pi-desktop", badge: "128" },
  { label: "Rede", icon: "pi pi-share-alt" },
  { label: "Monitoramento", icon: "pi pi-chart-line" },
  { label: "Alertas", icon: "pi pi-bell", badge: "7" },
  { label: "Incidentes", icon: "pi pi-exclamation-circle" },
  { label: "Tarefas", icon: "pi pi-check-square", badge: "12" },
];

export const systems = [
  { name: "Servidores", detail: "42 ativos", value: "97,6%", tone: "blue", icon: "pi pi-server" },
  { name: "Rede", detail: "86 dispositivos", value: "99,2%", tone: "green", icon: "pi pi-share-alt" },
  { name: "Aplicações", detail: "18 serviços", value: "94,8%", tone: "purple", icon: "pi pi-box" },
];

export const activities = [
  { title: "Novo dispositivo detectado", detail: "Switch-Core-03 · há 12 min", icon: "pi pi-plus", tone: "blue" },
  { title: "Incidente resolvido", detail: "Indisponibilidade do link WAN · há 34 min", icon: "pi pi-check", tone: "green" },
  { title: "Backup concluído", detail: "Rotina diária · há 1 h", icon: "pi pi-cloud-upload", tone: "purple" },
  { title: "Regra de alerta atualizada", detail: "Monitoramento · há 2 h", icon: "pi pi-sliders-h", tone: "orange" },
];

export const alerts = [
  { title: "Servidor APP-02 está indisponível", detail: "Produção · há 8 min", icon: "pi pi-exclamation-triangle", tone: "high", label: "Crítico" },
  { title: "Uso de CPU acima de 85% — DB-01", detail: "Banco de dados · há 26 min", icon: "pi pi-exclamation-circle", tone: "medium", label: "Atenção" },
  { title: "Certificado SSL expira em 14 dias", detail: "Portal externo · há 1 h", icon: "pi pi-info-circle", tone: "low", label: "Informativo" },
];

export const metricCards = [
  { label: "Dispositivos monitorados", value: "128", change: "8,4%", icon: "pi pi-server", tone: "blue", direction: "up" },
  { label: "Disponibilidade média", value: "98,7%", change: "1,2%", icon: "pi pi-check-circle", tone: "green", direction: "up" },
  { label: "Alertas ativos", value: "07", change: "14,6%", icon: "pi pi-bell", tone: "orange", direction: "down" },
  { label: "Tarefas pendentes", value: "12", change: "0,0%", icon: "pi pi-list-check", tone: "purple", direction: "neutral" },
];

export const secondaryNavigation = [
  { label: "Conhecimento", icon: "pi pi-book" },
  { label: "Relatórios", icon: "pi pi-file-chart" },
  { label: "Configurações", icon: "pi pi-cog" },
];
