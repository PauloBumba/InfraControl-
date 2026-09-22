import { useEffect, useCallback, type ReactNode } from "react";
import { navigation, secondaryNavigation } from "@/data/dashboardData";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { toggleSidebar, setMobileOpen } from "@/app/store/slices/uiSlice";
import { addToRecentTabs, removeTab } from "@/app/store/slices/navigationSlice";

type AppLayoutProps = { children: ReactNode; activeItem: string; onNavigate: (item: string) => void };

export function AppLayout({ children, activeItem, onNavigate }: AppLayoutProps) {
  const dispatch = useAppDispatch();
  const sidebarCollapsed = useAppSelector((s) => s.ui.sidebarCollapsed);
  const mobileOpen = useAppSelector((s) => s.ui.mobileOpen);
  const recentTabs = useAppSelector((s) => s.navigation.recentTabs);

  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === "Escape") dispatch(setMobileOpen(false));
    };
    document.addEventListener("keydown", handleEscape);
    return () => document.removeEventListener("keydown", handleEscape);
  }, [dispatch]);

  const handleToggleSidebar = useCallback(() => {
    dispatch(toggleSidebar());
  }, [dispatch]);

  const navigate = useCallback((item: string) => {
    onNavigate(item);
    dispatch(addToRecentTabs(item));
    dispatch(setMobileOpen(false));
  }, [onNavigate, dispatch]);

  const removeRecentTab = useCallback((tab: string) => {
    dispatch(removeTab(tab));
  }, [dispatch]);

  const sidebarClassName = `sidebar ${sidebarCollapsed ? "collapsed" : ""} ${mobileOpen ? "mobile-open" : ""}`;

  const allNavItems = [...navigation, ...secondaryNavigation];
  const getNavIcon = (label: string) => allNavItems.find((item) => item.label === label)?.icon || "pi pi-circle";

  return (
    <div className="app-shell">
      <aside className={sidebarClassName}>
        <div className="brand">
          <div className="brand-mark">
            <i className="pi pi-sitemap" />
          </div>
          <div className="brand-text">
            <strong>InfraControl</strong>
            <span>IT Operations Platform</span>
          </div>
          <button
            className="sidebar-toggle"
            onClick={handleToggleSidebar}
            aria-label={sidebarCollapsed ? "Expandir menu" : "Recolher menu"}
          >
            <i className={`pi ${sidebarCollapsed ? "pi-angle-right" : "pi-angle-left"}`} />
          </button>
        </div>

        <div className="workspace-select">
          <div className="workspace-avatar">IT</div>
          <div className="workspace-info">
            <span>Workspace</span>
            <strong>Operações TI</strong>
          </div>
          <i className="pi pi-chevron-down" />
        </div>

        <nav className="main-nav" aria-label="Navegação principal">
          <div className="nav-section">
            <p className="nav-label">PLATAFORMA</p>
            <ul className="nav-items">
              {navigation.map((item) => (
                <li key={item.label}>
                  <button
                    className={`nav-item ${activeItem === item.label ? "active" : ""}`}
                    onClick={() => navigate(item.label)}
                    title={sidebarCollapsed ? item.label : undefined}
                  >
                    <i className={item.icon} />
                    {!sidebarCollapsed && <span>{item.label}</span>}
                    {item.badge && !sidebarCollapsed && <b className="badge">{item.badge}</b>}
                  </button>
                </li>
              ))}
            </ul>
          </div>

          <div className="nav-section">
            <p className="nav-label">GESTÃO</p>
            <ul className="nav-items">
              {secondaryNavigation.map((item) => (
                <li key={item.label}>
                  <button
                    className={`nav-item ${activeItem === item.label ? "active" : ""}`}
                    onClick={() => navigate(item.label)}
                    title={sidebarCollapsed ? item.label : undefined}
                  >
                    <i className={item.icon} />
                    {!sidebarCollapsed && <span>{item.label}</span>}
                  </button>
                </li>
              ))}
            </ul>
          </div>
        </nav>

        <div className="sidebar-footer">
          <div className="support-card">
            <i className="pi pi-question-circle" />
            <div className="support-info">
              <strong>Precisa de ajuda?</strong>
              <span>Consulte a base de conhecimento</span>
            </div>
            <i className="pi pi-arrow-up-right" />
          </div>
          <div className="user-card">
            <div className="user-avatar">PB</div>
            <div className="user-info">
              <strong>Paulo Bumba</strong>
              <span>Administrador</span>
            </div>
            <i className="pi pi-ellipsis-v" />
          </div>
        </div>
      </aside>

      {mobileOpen && (
        <button
          className="sidebar-overlay"
          onClick={() => dispatch(setMobileOpen(false))}
          aria-label="Fechar menu"
        />
      )}

      <main className="main-content">
        <header className="topbar">
          <button
            className="mobile-menu"
            onClick={() => dispatch(setMobileOpen(true))}
            aria-label="Abrir menu"
          >
            <i className="pi pi-bars" />
          </button>
          <div className="breadcrumbs">
            <span>InfraControl</span>
            <i className="pi pi-angle-right" />
            <strong>{activeItem}</strong>
          </div>
          <div className="topbar-actions">
            <button className="icon-button" aria-label="Pesquisar">
              <i className="pi pi-search" />
            </button>
            <button className="icon-button notification-button" aria-label="Notificações">
              <i className="pi pi-bell" />
              <span className="notification-badge">3</span>
            </button>
            <div className="topbar-avatar">PB</div>
          </div>
        </header>

        {recentTabs.length > 0 && (
          <div className="side-panel-tabs">
            {recentTabs.map((tab: string) => (
              <button
                key={tab}
                className={`side-panel-tab ${activeItem === tab ? "active" : ""}`}
                onClick={() => navigate(tab)}
              >
                <i className={getNavIcon(tab)} />
                <span>{tab}</span>
                <button
                  className="tab-close"
                  onClick={(e) => {
                    e.stopPropagation();
                    removeRecentTab(tab);
                  }}
                  aria-label={`Fechar ${tab}`}
                >
                  <i className="pi pi-times" />
                </button>
              </button>
            ))}
          </div>
        )}

        {children}
      </main>
    </div>
  );
}
