import { useEffect, useCallback, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { navigation, secondaryNavigation } from "@/data/dashboardData";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { toggleSidebar, setMobileOpen } from "@/app/store/slices/uiSlice";
import { addToRecentTabs, removeTab } from "@/app/store/slices/navigationSlice";
import { signedOut } from "@/app/store/slices/authSlice";
import { logout } from "@/services/api/auth";
import { ACCESS_LEVEL_LABEL, highestAccessLevel } from "@/types/roles";
import { canReach, routeLevel } from "@/app/router/access";

type AppLayoutProps = { children: ReactNode; activeItem: string; onNavigate: (item: string) => void };

export function AppLayout({ children, activeItem, onNavigate }: AppLayoutProps) {
  const dispatch = useAppDispatch();
  const routerNavigate = useNavigate();
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

  // F14-T008: identidade exibida vem do /auth/me (cache de tela). As roles
  // daqui NÃO autorizam (quem autoriza é o backend); o F14-T009 usa-as só
  // para conformidade da UI — esconder menu/aba acima do nível do usuário.
  const user = useAppSelector((s) => s.auth.user);
  const displayName = user?.displayName || user?.email || "—";
  // ADR-016: "User" virou "Operator"; o rótulo vem do nível canônico
  const access = highestAccessLevel(user?.roles);
  const roleLabel = access ? ACCESS_LEVEL_LABEL[access] : "—";
  const initials = displayName
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("") || "IC";

  const [signingOut, setSigningOut] = useState(false);

  const handleLogout = useCallback(async () => {
    setSigningOut(true);
    try {
      // Revoga a sessão no servidor (cookie + sessões persistidas) antes de
      // limpar o estado local.
      await logout();
      dispatch(signedOut());
      routerNavigate("/login", { replace: true });
    } finally {
      setSigningOut(false);
    }
  }, [dispatch, routerNavigate]);

  const allNavItems = [...navigation, ...secondaryNavigation];
  const getNavItem = (label: string) => allNavItems.find((item) => item.label === label);

  // F14-T009 — menu e abas acompanham o nível da rota (ADR-016): o Admin
  // (administer) vê tudo; Operator/Viewer não enxergam as rotas administer
  // nem as abas recentes delas. Exibição apenas — o backend autoriza de verdade.
  const canSee = (route: string) => canReach(user?.roles, routeLevel(route));
  const visibleNavigation = navigation.filter((item) => canSee(item.route));
  const visibleSecondaryNavigation = secondaryNavigation.filter((item) => canSee(item.route));
  const visibleRecentTabs = recentTabs.filter((label) => {
    const item = getNavItem(label);
    return item ? canSee(item.route) : true;
  });

  const sidebarClassName = `uni-sidebar ${sidebarCollapsed ? "collapsed" : ""} ${mobileOpen ? "mobile-open" : ""}`;

  return (
    <div className="uni-shell">
      <aside className={sidebarClassName}>
        <div className="uni-sidebar-logo">
          <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
            <circle cx="12" cy="12" r="11" stroke="#3b82f6" strokeWidth="1.5"/>
            <path d="M8 9 C8 7.5 9.5 6 12 6 C14.5 6 16 7.5 16 9 C16 10.5 14.5 12 12 12 L12 18" stroke="#3b82f6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <circle cx="12" cy="18" r="1" fill="#3b82f6"/>
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
            {visibleNavigation.map((item) => (
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
            {visibleSecondaryNavigation.map((item) => (
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
            <div className="uni-user-avatar">{initials}</div>
            <div className="uni-user-info">
              <span className="uni-user-name">{displayName}</span>
              <span className="uni-user-role">{roleLabel}</span>
            </div>
            <button
              className="uni-user-logout"
              onClick={handleLogout}
              disabled={signingOut}
              title="Sair"
              aria-label="Sair"
            >
              <i className={`pi ${signingOut ? "pi-spin pi-spinner" : "pi-sign-out"}`} />
            </button>
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
            <div className="uni-topbar-avatar" title={displayName}>{initials}</div>
          </div>
        </header>

        {visibleRecentTabs.length > 0 && (
          <div className="uni-tabs-bar">
            {visibleRecentTabs.map((tab: string) => {
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
