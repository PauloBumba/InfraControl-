import { useCallback, useEffect, useRef, type ReactNode } from "react";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { setMobileOpen } from "@/app/store/slices/uiSlice";
import { removeTab } from "@/app/store/slices/navigationSlice";
import { labelForRoute } from "@/app/router/routeLabels";
import { LayoutProvider, useLayoutContext } from "./LayoutContext";
import { AppTopbar, type AppTopbarRef } from "./AppTopbar";
import { AppSidebar } from "./AppSidebar";
import { AppFooter } from "./AppFooter";

type AppLayoutProps = {
  children: ReactNode;
  activeRoute: string;
  onNavigate: (route: string) => void;
};

function AppLayoutContent({ children, activeRoute, onNavigate }: AppLayoutProps) {
  const { layoutConfig, layoutState, setLayoutState } = useLayoutContext();
  const dispatch = useAppDispatch();
  const topbarRef = useRef<AppTopbarRef>(null);
  const sidebarRef = useRef<HTMLDivElement>(null);
  const recentTabs = useAppSelector((s) => s.navigation.recentTabs);

  const handleMenuButtonClick = useCallback(() => {
    if (layoutConfig.menuMode === "overlay") {
      setLayoutState((prev) => ({
        ...prev,
        overlayMenuActive: !prev.overlayMenuActive,
      }));
    } else {
      dispatch(setMobileOpen(true));
    }
  }, [layoutConfig.menuMode, dispatch, setLayoutState]);

  const hideMenu = useCallback(() => {
    setLayoutState((prev) => ({
      ...prev,
      overlayMenuActive: false,
      staticMenuMobileActive: false,
    }));
  }, [setLayoutState]);

  const handleOutsideClick = useCallback((event: MouseEvent) => {
    const isOutsideClicked = !(
      sidebarRef.current?.isSameNode(event.target as Node) ||
      sidebarRef.current?.contains(event.target as Node) ||
      topbarRef.current?.menubutton?.isSameNode(event.target as Node) ||
      topbarRef.current?.menubutton?.contains(event.target as Node)
    );

    if (isOutsideClicked) {
      hideMenu();
    }
  }, [hideMenu]);

  useEffect(() => {
    if (layoutState.overlayMenuActive || layoutState.staticMenuMobileActive) {
      document.addEventListener("click", handleOutsideClick);
    }
    return () => {
      document.removeEventListener("click", handleOutsideClick);
    };
  }, [layoutState.overlayMenuActive, layoutState.staticMenuMobileActive, handleOutsideClick]);

  const removeRecentTab = (tab: string) => {
    dispatch(removeTab(tab));
  };

  const canSee = () => {
    // Simple permission check - can be enhanced
    return true;
  };

  const visibleRecentTabs = recentTabs.filter(() => canSee());

  const getNavItem = (route: string) => {
    // This is a simplified version - would need proper implementation
    return { icon: "pi pi-circle", label: labelForRoute(route) };
  };

  const containerClass = `layout-wrapper ${layoutConfig.menuMode === "overlay" ? "layout-overlay" : "layout-static"} ${
    layoutState.overlayMenuActive ? "layout-overlay-active" : ""
  } ${layoutState.staticMenuMobileActive ? "layout-mobile-active" : ""}`;

  return (
    <div className={containerClass}>
      <AppTopbar ref={topbarRef} onMenuButtonClick={handleMenuButtonClick} activeRoute={activeRoute} />
      <div ref={sidebarRef} className="layout-sidebar">
        <AppSidebar activeRoute={activeRoute} onNavigate={onNavigate} />
      </div>
      <div className="layout-main-container">
        <div className="layout-main">
          {visibleRecentTabs.length > 0 && (
            <div className="layout-tabs-bar">
              {visibleRecentTabs.map((route: string) => {
                const navItem = getNavItem(route);
                const tabLabel = labelForRoute(route);
                return (
                  <button
                    key={route}
                    className={`layout-tab ${activeRoute === route ? "active" : ""}`}
                    aria-current={activeRoute === route ? "page" : undefined}
                    onClick={() => onNavigate(route)}
                  >
                    <i className={navItem?.icon || "pi pi-circle"} />
                    <span>{tabLabel}</span>
                    <button
                      className="layout-tab-close"
                      onClick={(e) => { e.stopPropagation(); removeRecentTab(route); }}
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
      {layoutState.staticMenuMobileActive && (
        <div
          className="layout-sidebar-overlay"
          onClick={() => dispatch(setMobileOpen(false))}
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
