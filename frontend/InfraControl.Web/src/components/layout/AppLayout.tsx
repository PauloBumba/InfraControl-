import { useCallback, useEffect, useRef, type ReactNode } from "react";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { setMobileOpen } from "@/app/store/slices/uiSlice";
import { removeTab } from "@/app/store/slices/navigationSlice";
import { labelForRoute } from "@/app/router/routeLabels";
import { navigation, secondaryNavigation, type NavItem } from "@/data/dashboardData";
import { canReach, routeLevel } from "@/app/router/access";
import { LayoutProvider, useLayoutContext } from "./LayoutContext";
import { AppTopbar, type AppTopbarRef } from "./AppTopbar";
import { AppSidebar } from "./AppSidebar";
import { AppFooter } from "./AppFooter";

type AppLayoutProps = {
  children: ReactNode;
  activeRoute: string;
  onNavigate: (route: string) => void;
};

function findNavItem(route: string): NavItem | undefined {
  for (const group of [...navigation, ...secondaryNavigation]) {
    for (const item of group.items) {
      if (item.route === route) return item;
      const child = item.items?.find((candidate) => candidate.route === route);
      if (child) return child;
    }
  }
  return undefined;
}

function AppLayoutContent({ children, activeRoute, onNavigate }: AppLayoutProps) {
  const { layoutConfig } = useLayoutContext();
  const dispatch = useAppDispatch();
  const topbarRef = useRef<AppTopbarRef>(null);
  const sidebarRef = useRef<HTMLDivElement>(null);
  const recentTabs = useAppSelector((s) => s.navigation.recentTabs);
  const sidebarCollapsed = useAppSelector((s) => s.ui.sidebarCollapsed);
  const mobileOpen = useAppSelector((s) => s.ui.mobileOpen);
  const roles = useAppSelector((s) => s.auth.user?.roles);

  const handleMenuButtonClick = useCallback(() => {
    dispatch(setMobileOpen(true));
  }, [dispatch]);

  const closeMobileMenu = useCallback(() => {
    dispatch(setMobileOpen(false));
  }, [dispatch]);

  const handleNavigate = useCallback(
    (route: string) => {
      onNavigate(route);
      closeMobileMenu();
    },
    [closeMobileMenu, onNavigate],
  );

  // Sakai: clique fora fecha o drawer; Escape também. O botão do topbar fica
  // fora do listener para o mesmo clique que abre não fechar em seguida.
  useEffect(() => {
    if (!mobileOpen) return;

    const handleOutsideClick = (event: MouseEvent) => {
      const isOutsideClicked = !(
        sidebarRef.current?.isSameNode(event.target as Node) ||
        sidebarRef.current?.contains(event.target as Node) ||
        topbarRef.current?.menubutton?.isSameNode(event.target as Node) ||
        topbarRef.current?.menubutton?.contains(event.target as Node)
      );

      if (isOutsideClicked) closeMobileMenu();
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeMobileMenu();
    };

    document.addEventListener("click", handleOutsideClick);
    document.addEventListener("keydown", handleEscape);
    return () => {
      document.removeEventListener("click", handleOutsideClick);
      document.removeEventListener("keydown", handleEscape);
    };
  }, [closeMobileMenu, mobileOpen]);

  // Trava o scroll do body apenas no mobile, como o `blocked-scroll` do Sakai.
  useEffect(() => {
    if (!mobileOpen) return;
    const desktop = window.matchMedia?.("(min-width: 992px)");
    if (desktop?.matches) return;
    document.body.classList.add("blocked-scroll");
    return () => document.body.classList.remove("blocked-scroll");
  }, [mobileOpen]);

  // Se a viewport crescer para desktop, o drawer não pode ficar "aberto".
  useEffect(() => {
    const desktop = window.matchMedia?.("(min-width: 992px)");
    if (!desktop) return;
    const handleChange = (event: MediaQueryListEvent) => {
      if (event.matches) closeMobileMenu();
    };
    desktop.addEventListener("change", handleChange);
    return () => desktop.removeEventListener("change", handleChange);
  }, [closeMobileMenu]);

  // Navegar por qualquer origem (aba, menu, link) fecha o drawer.
  useEffect(() => {
    closeMobileMenu();
  }, [activeRoute, closeMobileMenu]);

  const removeRecentTab = (tab: string) => {
    dispatch(removeTab(tab));
  };

  const visibleRecentTabs = recentTabs.filter((route) =>
    canReach(roles, routeLevel(route)),
  );

  const containerClass = `layout-wrapper ${
    layoutConfig.menuMode === "overlay" ? "layout-overlay" : "layout-static"
  } ${mobileOpen ? "layout-mobile-active" : ""}`;

  const sidebarClass = `layout-sidebar ${sidebarCollapsed ? "collapsed" : ""} ${
    mobileOpen ? "mobile-open" : ""
  }`;

  return (
    <div className={containerClass}>
      <AppTopbar
        ref={topbarRef}
        onMenuButtonClick={handleMenuButtonClick}
        activeRoute={activeRoute}
        mobileMenuOpen={mobileOpen}
      />
      <div ref={sidebarRef} id="app-sidebar" className={sidebarClass}>
        <AppSidebar activeRoute={activeRoute} onNavigate={handleNavigate} />
      </div>
      <div className="layout-main-container">
        <div className="layout-main">
          {visibleRecentTabs.length > 0 && (
            <div className="layout-tabs-bar">
              {visibleRecentTabs.map((route: string) => {
                const navItem = findNavItem(route);
                const tabLabel = labelForRoute(route);
                return (
                  <button
                    key={route}
                    className={`layout-tab ${activeRoute === route ? "active" : ""}`}
                    aria-current={activeRoute === route ? "page" : undefined}
                    onClick={() => handleNavigate(route)}
                  >
                    <i className={navItem?.icon || "pi pi-circle"} />
                    <span>{tabLabel}</span>
                    <button
                      className="layout-tab-close"
                      onClick={(event) => {
                        event.stopPropagation();
                        removeRecentTab(route);
                      }}
                      aria-label={`Fechar ${tabLabel}`}
                    >
                      <i className="pi pi-times" />
                    </button>
                  </button>
                );
              })}
            </div>
          )}
          {children}
        </div>
        <AppFooter />
      </div>
      {mobileOpen && (
        <button
          type="button"
          className="layout-sidebar-overlay"
          onClick={closeMobileMenu}
          aria-label="Fechar menu"
        />
      )}
    </div>
  );
}

export function AppLayout(props: AppLayoutProps) {
  return (
    <LayoutProvider>
      <AppLayoutContent {...props} />
    </LayoutProvider>
  );
}
