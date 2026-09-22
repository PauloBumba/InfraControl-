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

  const allNavItems = [...navigation, ...secondaryNavigation];
  const getNavItem = (label: string) => allNavItems.find((item) => item.label === label);

  const sidebarClassName = `uni-sidebar ${sidebarCollapsed ? "collapsed" : ""} ${mobileOpen ? "mobile-open" : ""}`;

  return (
    <div className="uni-shell">
      <aside className={sidebarClassName}>
        <div className="uni-sidebar-logo">
          <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
            <circle cx="12" cy="12" r="11" stroke="#0088ff" strokeWidth="1.5"/>
            <path d="M8 9 C8 7.5 9.5 6 12 6 C14.5 6 16 7.5 16 9 C16 10.5 14.5 12 12 12 L12 18" stroke="#0088ff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <circle cx="12" cy="18" r="1" fill="#0088ff"/>
          </svg>
          <span>InfraControl</span>
          <button
            className="uni-sidebar-toggle"
            onClick={handleToggleSidebar}
            aria-label={sidebarCollapsed ? "Expandir" : "Recolher"}
          >
            <i className={`pi ${sidebarCollapsed ? "pi-angle-right" : "pi-angle-left"}`} />
          </button>
        </div>

        <nav className="uni-nav" aria-label="Navegação principal">
          <ul className="uni-nav-list">
            {navigation.map((item) => (
              <li key={item.label}>
                <button
                  className={`uni-nav-item ${activeItem === item.label ? "active" : ""}`}
                  onClick={() => navigate(item.label)}
                  title={item.label}
                >
                  <i className={item.icon} />
                  <span>{item.label}</span>
                  {item.badge && <span className="uni-nav-badge">{item.badge}</span>}
                </button>
              </li>
            ))}
          </ul>

          <div className="uni-nav-divider" />

          <ul className="uni-nav-list">
            {secondaryNavigation.map((item) => (
              <li key={item.label}>
                <button
                  className={`uni-nav-item ${activeItem === item.label ? "active" : ""}`}
                  onClick={() => navigate(item.label)}
                  title={item.label}
                >
                  <i className={item.icon} />
                  <span>{item.label}</span>
                  {item.badge && <span className="uni-nav-badge">{item.badge}</span>}
                </button>
              </li>
            ))}
          </ul>
        </nav>

        <div className="uni-sidebar-footer">
          <div className="uni-user-card">
            <div className="uni-user-avatar">PB</div>
            <div className="uni-user-info">
              <span className="uni-user-name">Paulo Bumba</span>
              <span className="uni-user-role">Administrador</span>
            </div>
          </div>
        </div>
      </aside>

      {mobileOpen && (
        <button
          className="uni-sidebar-overlay"
          onClick={() => dispatch(setMobileOpen(false))}
          aria-label="Fechar menu"
        />
      )}

      <main className="uni-main">
        <header className="uni-topbar">
          <button
            className="uni-mobile-menu"
            onClick={() => dispatch(setMobileOpen(true))}
            aria-label="Abrir menu"
          >
            <i className="pi pi-bars" />
          </button>

          <div className="uni-topbar-breadcrumb">
            InfraControl <i className="pi pi-angle-right" style={{ fontSize: 8 }} /> <span>{activeItem}</span>
          </div>

          <div className="uni-topbar-right">
            <div className="uni-topbar-search">
              <i className="pi pi-search" />
              <input type="text" placeholder="Pesquisar..." />
            </div>
            <button className="uni-topbar-icon" title="Adicionar">
              <i className="pi pi-plus" />
            </button>
            <button className="uni-topbar-icon" title="Atualizar">
              <i className="pi pi-refresh" />
            </button>
            <button className="uni-topbar-icon" title="Notificações">
              <i className="pi pi-bell" />
              <span className="uni-notif-badge">3</span>
            </button>
            <div className="uni-topbar-avatar" title="Paulo Bumba">PB</div>
          </div>
        </header>

        {recentTabs.length > 0 && (
          <div className="uni-tabs-bar">
            {recentTabs.map((tab: string) => {
              const navItem = getNavItem(tab);
              return (
                <button
                  key={tab}
                  className={`uni-tab ${activeItem === tab ? "active" : ""}`}
                  onClick={() => navigate(tab)}
                >
                  <i className={navItem?.icon || "pi pi-circle"} />
                  <span>{tab}</span>
                  <button
                    className="uni-tab-close"
                    onClick={(e) => { e.stopPropagation(); removeRecentTab(tab); }}
                    aria-label={`Fechar ${tab}`}
                  >
                    <i className="pi pi-times" />
                  </button>
                </button>
              );
            })}
          </div>
        )}

        <div className="uni-content">
          {children}
        </div>
      </main>
    </div>
  );
}
