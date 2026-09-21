import { useState } from "react";
import "./styles.css";

type NavItem = {
  label: string;
  icon: string;
  badge?: string;
};

const navigation: NavItem[] = [
  { label: "Visão geral", icon: "pi pi-th-large" },
  { label: "Inventário", icon: "pi pi-server" },
  { label: "Dispositivos", icon: "pi pi-desktop", badge: "128" },
  { label: "Rede", icon: "pi pi-share-alt" },
  { label: "Monitoramento", icon: "pi pi-chart-line" },
  { label: "Alertas", icon: "pi pi-bell", badge: "7" },
  { label: "Incidentes", icon: "pi pi-exclamation-circle" },
  { label: "Tarefas", icon: "pi pi-check-square", badge: "12" },
];

const systems = [
  { name: "Servidores", detail: "42 ativos", value: "97,6%", tone: "blue", icon: "pi pi-server" },
  { name: "Rede", detail: "86 dispositivos", value: "99,2%", tone: "green", icon: "pi pi-share-alt" },
  { name: "Aplicações", detail: "18 serviços", value: "94,8%", tone: "purple", icon: "pi pi-box" },
];

const activities = [
  { title: "Novo dispositivo detectado", detail: "Switch-Core-03 · há 12 min", icon: "pi pi-plus", tone: "blue" },
  { title: "Incidente resolvido", detail: "Indisponibilidade do link WAN · há 34 min", icon: "pi pi-check", tone: "green" },
  { title: "Backup concluído", detail: "Rotina diária · há 1 h", icon: "pi pi-cloud-upload", tone: "purple" },
  { title: "Regra de alerta atualizada", detail: "Monitoramento · há 2 h", icon: "pi pi-sliders-h", tone: "orange" },
];

export function App() {
  const [activeItem, setActiveItem] = useState("Visão geral");
  const [sidebarOpen, setSidebarOpen] = useState(false);

  return (
    <div className="app-shell">
      <aside className={`sidebar ${sidebarOpen ? "sidebar-open" : ""}`}>
        <div className="brand">
          <div className="brand-mark"><i className="pi pi-sitemap" /></div>
          <div><strong>InfraControl</strong><span>IT Operations Platform</span></div>
          <button className="close-sidebar" onClick={() => setSidebarOpen(false)} aria-label="Fechar menu"><i className="pi pi-times" /></button>
        </div>

        <div className="workspace-select">
          <div className="workspace-avatar">IT</div>
          <div><span>Workspace</span><strong>Operações TI</strong></div>
          <i className="pi pi-chevron-down" />
        </div>

        <nav className="main-nav" aria-label="Navegação principal">
          <p className="nav-label">PLATAFORMA</p>
          {navigation.map((item) => (
            <button key={item.label} className={`nav-item ${activeItem === item.label ? "active" : ""}`} onClick={() => { setActiveItem(item.label); setSidebarOpen(false); }}>
              <i className={item.icon} /><span>{item.label}</span>{item.badge && <b>{item.badge}</b>}
            </button>
          ))}
          <p className="nav-label nav-label-spaced">GESTÃO</p>
          <button className={`nav-item ${activeItem === "Conhecimento" ? "active" : ""}`} onClick={() => setActiveItem("Conhecimento")}><i className="pi pi-book" /><span>Conhecimento</span></button>
          <button className={`nav-item ${activeItem === "Relatórios" ? "active" : ""}`} onClick={() => setActiveItem("Relatórios")}><i className="pi pi-file-chart" /><span>Relatórios</span></button>
          <button className={`nav-item ${activeItem === "Configurações" ? "active" : ""}`} onClick={() => setActiveItem("Configurações")}><i className="pi pi-cog" /><span>Configurações</span></button>
        </nav>

        <div className="sidebar-footer"><div className="support-card"><i className="pi pi-question-circle" /><div><strong>Precisa de ajuda?</strong><span>Consulte a base de conhecimento</span></div><i className="pi pi-arrow-up-right" /></div><div className="user-card"><div className="user-avatar">PB</div><div><strong>Paulo Bumba</strong><span>Administrador</span></div><i className="pi pi-ellipsis-v" /></div></div>
      </aside>

      {sidebarOpen && <button className="sidebar-overlay" onClick={() => setSidebarOpen(false)} aria-label="Fechar menu" />}

      <main className="main-content">
        <header className="topbar"><button className="mobile-menu" onClick={() => setSidebarOpen(true)} aria-label="Abrir menu"><i className="pi pi-bars" /></button><div className="breadcrumbs"><span>InfraControl</span><i className="pi pi-angle-right" /><strong>{activeItem}</strong></div><div className="topbar-actions"><button className="icon-button" aria-label="Pesquisar"><i className="pi pi-search" /></button><button className="icon-button notification-button" aria-label="Notificações"><i className="pi pi-bell" /><span /></button><div className="topbar-avatar">PB</div></div></header>

        <div className="page-content">
          <section className="page-heading"><div><p className="eyebrow">SEGUNDA-FEIRA, 21 DE SETEMBRO DE 2026</p><h1>Bom dia, Paulo <span>👋</span></h1><p className="heading-subtitle">Aqui está o resumo da sua infraestrutura hoje.</p></div><button className="primary-button"><i className="pi pi-plus" /> Nova tarefa</button></section>

          <section className="metric-grid" aria-label="Indicadores principais">
            <article className="metric-card"><div className="metric-icon blue-icon"><i className="pi pi-server" /></div><div className="metric-copy"><span>Dispositivos monitorados</span><strong>128</strong><small className="positive"><i className="pi pi-arrow-up" /> 8,4% <em>vs. mês anterior</em></small></div><i className="pi pi-ellipsis-h card-menu" /></article>
            <article className="metric-card"><div className="metric-icon green-icon"><i className="pi pi-check-circle" /></div><div className="metric-copy"><span>Disponibilidade média</span><strong>98,7%</strong><small className="positive"><i className="pi pi-arrow-up" /> 1,2% <em>vs. mês anterior</em></small></div><i className="pi pi-ellipsis-h card-menu" /></article>
            <article className="metric-card"><div className="metric-icon orange-icon"><i className="pi pi-bell" /></div><div className="metric-copy"><span>Alertas ativos</span><strong>07</strong><small className="negative"><i className="pi pi-arrow-down" /> 14,6% <em>vs. mês anterior</em></small></div><i className="pi pi-ellipsis-h card-menu" /></article>
            <article className="metric-card"><div className="metric-icon purple-icon"><i className="pi pi-list-check" /></div><div className="metric-copy"><span>Tarefas pendentes</span><strong>12</strong><small className="neutral"><i className="pi pi-minus" /> 0,0% <em>vs. mês anterior</em></small></div><i className="pi pi-ellipsis-h card-menu" /></article>
          </section>

          <section className="content-grid"><article className="panel availability-panel"><div className="panel-heading"><div><h2>Disponibilidade da infraestrutura</h2><p>Visão consolidada dos últimos 30 dias</p></div><button className="select-button">Últimos 30 dias <i className="pi pi-chevron-down" /></button></div><div className="chart-area"><div className="chart-y-axis"><span>100%</span><span>75%</span><span>50%</span><span>25%</span><span>0%</span></div><div className="chart"><div className="chart-grid"><i /><i /><i /><i /><i /></div><svg viewBox="0 0 700 210" preserveAspectRatio="none" role="img" aria-label="Gráfico de disponibilidade"><defs><linearGradient id="chartFill" x1="0" x2="0" y1="0" y2="1"><stop offset="0%" stopColor="#4f8cff" stopOpacity=".24" /><stop offset="100%" stopColor="#4f8cff" stopOpacity="0" /></linearGradient></defs><path d="M0 144 C28 146, 38 124, 58 132 S93 113, 112 123 S145 95, 166 108 S194 83, 218 95 S247 90, 270 99 S294 73, 319 82 S346 63, 370 77 S398 71, 422 84 S452 56, 478 69 S505 48, 529 59 S554 38, 581 50 S610 42, 637 48 S672 30, 700 35 L700 210 L0 210 Z" fill="url(#chartFill)" /><path d="M0 144 C28 146, 38 124, 58 132 S93 113, 112 123 S145 95, 166 108 S194 83, 218 95 S247 90, 270 99 S294 73, 319 82 S346 63, 370 77 S398 71, 422 84 S452 56, 478 69 S505 48, 529 59 S554 38, 581 50 S610 42, 637 48 S672 30, 700 35" fill="none" stroke="#4f8cff" strokeWidth="3" strokeLinecap="round" /></svg><div className="chart-x-axis"><span>23 ago</span><span>30 ago</span><span>06 set</span><span>13 set</span><span>20 set</span></div></div></div><div className="chart-legend"><span><i className="legend-dot blue-dot" /> Disponibilidade geral</span><strong>98,7% <small>média</small></strong></div></article>

            <article className="panel systems-panel"><div className="panel-heading"><div><h2>Saúde dos sistemas</h2><p>Status atual por categoria</p></div><button className="more-button"><i className="pi pi-ellipsis-h" /></button></div><div className="systems-list">{systems.map((system) => <div className="system-row" key={system.name}><div className={`system-icon ${system.tone}-icon`}><i className={system.icon} /></div><div className="system-info"><strong>{system.name}</strong><span>{system.detail}</span></div><div className="system-status"><strong>{system.value}</strong><span><i className="pi pi-circle-fill" /> Operacional</span></div></div>)}</div><button className="text-button">Ver todos os sistemas <i className="pi pi-arrow-right" /></button></article></section>

          <section className="bottom-grid"><article className="panel alerts-panel"><div className="panel-heading"><div><h2>Alertas recentes</h2><p>Eventos que precisam da sua atenção</p></div><button className="link-button">Ver todos <i className="pi pi-arrow-right" /></button></div><div className="alert-list"><div className="alert-row"><div className="alert-severity high"><i className="pi pi-exclamation-triangle" /></div><div className="alert-copy"><strong>Servidor APP-02 está indisponível</strong><span>Produção · há 8 min</span></div><span className="status-pill high-pill">Crítico</span></div><div className="alert-row"><div className="alert-severity medium"><i className="pi pi-exclamation-circle" /></div><div className="alert-copy"><strong>Uso de CPU acima de 85% — DB-01</strong><span>Banco de dados · há 26 min</span></div><span className="status-pill medium-pill">Atenção</span></div><div className="alert-row"><div className="alert-severity low"><i className="pi pi-info-circle" /></div><div className="alert-copy"><strong>Certificado SSL expira em 14 dias</strong><span>Portal externo · há 1 h</span></div><span className="status-pill low-pill">Informativo</span></div></div></article><article className="panel activity-panel"><div className="panel-heading"><div><h2>Atividade recente</h2><p>Últimas movimentações na plataforma</p></div><button className="more-button"><i className="pi pi-ellipsis-h" /></button></div><div className="activity-list">{activities.map((activity) => <div className="activity-row" key={activity.title}><div className={`activity-icon ${activity.tone}-icon`}><i className={activity.icon} /></div><div><strong>{activity.title}</strong><span>{activity.detail}</span></div></div>)}</div></article></section>
        </div>
      </main>
    </div>
  );
}
