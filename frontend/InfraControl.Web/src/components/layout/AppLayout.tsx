import { useState, type ReactNode } from "react";
import { navigation, secondaryNavigation } from "@/data/dashboardData";

type AppLayoutProps = { children: ReactNode; activeItem: string; onNavigate: (item: string) => void };

export function AppLayout({ children, activeItem, onNavigate }: AppLayoutProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const navigate = (item: string) => { onNavigate(item); setSidebarOpen(false); };

  return <div className="app-shell">
    <aside className={`sidebar ${sidebarOpen ? "sidebar-open" : ""}`}>
      <div className="brand"><div className="brand-mark"><i className="pi pi-sitemap" /></div><div><strong>InfraControl</strong><span>IT Operations Platform</span></div><button className="close-sidebar" onClick={() => setSidebarOpen(false)} aria-label="Fechar menu"><i className="pi pi-times" /></button></div>
      <div className="workspace-select"><div className="workspace-avatar">IT</div><div><span>Workspace</span><strong>Operações TI</strong></div><i className="pi pi-chevron-down" /></div>
      <nav className="main-nav" aria-label="Navegação principal"><p className="nav-label">PLATAFORMA</p>{navigation.map((item) => <button key={item.label} className={`nav-item ${activeItem === item.label ? "active" : ""}`} onClick={() => navigate(item.label)}><i className={item.icon} /><span>{item.label}</span>{item.badge && <b>{item.badge}</b>}</button>)}<p className="nav-label nav-label-spaced">GESTÃO</p>{secondaryNavigation.map((item) => <button key={item.label} className={`nav-item ${activeItem === item.label ? "active" : ""}`} onClick={() => navigate(item.label)}><i className={item.icon} /><span>{item.label}</span></button>)}</nav>
      <div className="sidebar-footer"><div className="support-card"><i className="pi pi-question-circle" /><div><strong>Precisa de ajuda?</strong><span>Consulte a base de conhecimento</span></div><i className="pi pi-arrow-up-right" /></div><div className="user-card"><div className="user-avatar">PB</div><div><strong>Paulo Bumba</strong><span>Administrador</span></div><i className="pi pi-ellipsis-v" /></div></div>
    </aside>
    {sidebarOpen && <button className="sidebar-overlay" onClick={() => setSidebarOpen(false)} aria-label="Fechar menu" />}
    <main className="main-content"><header className="topbar"><button className="mobile-menu" onClick={() => setSidebarOpen(true)} aria-label="Abrir menu"><i className="pi pi-bars" /></button><div className="breadcrumbs"><span>InfraControl</span><i className="pi pi-angle-right" /><strong>{activeItem}</strong></div><div className="topbar-actions"><button className="icon-button" aria-label="Pesquisar"><i className="pi pi-search" /></button><button className="icon-button notification-button" aria-label="Notificações"><i className="pi pi-bell" /><span /></button><div className="topbar-avatar">PB</div></div></header>{children}</main>
  </div>;
}
