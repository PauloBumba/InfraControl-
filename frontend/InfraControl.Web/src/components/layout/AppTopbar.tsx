import { useCallback, useEffect, useRef, useState, forwardRef, useImperativeHandle } from "react";
import { useNavigate } from "react-router-dom";
import { Menu } from "primereact/menu";
import type { MenuItem } from "primereact/menuitem";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { signedOut } from "@/app/store/slices/authSlice";
import { logout } from "@/services/api/auth";
import { globalSearch, listDevices, type GlobalSearchItem } from "@/services/api/inventory";
import { listAlerts } from "@/services/api/alerts";
import { listIncidents } from "@/services/api/incidents";
import { listTasks } from "@/services/api/tasks";
import { listFeed } from "@/services/api/notifications";
import { readStoredTheme, toggleTheme, type Theme } from "@/services/theme";
import { labelForRoute } from "@/app/router/routeLabels";

export interface AppTopbarRef {
  menubutton: HTMLButtonElement | null;
  topbarmenu: HTMLDivElement | null;
  topbarmenubutton: HTMLButtonElement | null;
}

interface AppTopbarProps {
  onMenuButtonClick: () => void;
  activeRoute: string;
  mobileMenuOpen: boolean;
}

const SEARCH_TYPE_LABEL: Record<GlobalSearchItem["type"], string> = {
  location: "Local",
  rack: "Rack",
  device: "Device",
  interface: "Interface",
  mac: "MAC",
  ip: "IP",
};

export const AppTopbar = forwardRef<AppTopbarRef, AppTopbarProps>(
  ({ onMenuButtonClick, activeRoute, mobileMenuOpen }, ref) => {
    const dispatch = useAppDispatch();
    const routerNavigate = useNavigate();
    const user = useAppSelector((s) => s.auth.user);
    const displayName = user?.fullName?.trim() || user?.displayName || user?.email || "—";
    const initials = displayName
      .split(" ")
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? "")
      .join("") || "IC";

    const [signingOut, setSigningOut] = useState(false);
    const [theme, setTheme] = useState<Theme>(readStoredTheme);
    const [sidebarBadges, setSidebarBadges] = useState<{
      devices?: number;
      alerts?: number;
      incidents?: number;
      tasks?: number;
      notifications?: number;
    }>({});
    const [addMenuOpen, setAddMenuOpen] = useState(false);
    const [refreshing, setRefreshing] = useState(false);

    // Search state
    const [searchTerm, setSearchTerm] = useState("");
    const [searchResults, setSearchResults] = useState<GlobalSearchItem[]>([]);
    const [searchTotal, setSearchTotal] = useState(0);
    const [searching, setSearching] = useState(false);
    const [searchOpen, setSearchOpen] = useState(false);
    const [searchError, setSearchError] = useState<string | null>(null);
    const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const searchSeq = useRef(0);

    // Refs
    const menubuttonRef = useRef<HTMLButtonElement>(null);
    const topbarmenuRef = useRef<HTMLDivElement>(null);
    const topbarmenubuttonRef = useRef<HTMLButtonElement>(null);
    const topbarUserMenuRef = useRef<Menu>(null);
    const [topbarMenuOpen, setTopbarMenuOpen] = useState(false);

    useImperativeHandle(ref, () => ({
      menubutton: menubuttonRef.current,
      topbarmenu: topbarmenuRef.current,
      topbarmenubutton: topbarmenubuttonRef.current,
    }));

    const handleToggleTheme = useCallback(() => {
      setTheme((current) => toggleTheme(current));
    }, []);

    const handleLogout = useCallback(async () => {
      setSigningOut(true);
      try {
        await logout();
        dispatch(signedOut());
        routerNavigate("/login", { replace: true });
      } finally {
        setSigningOut(false);
      }
    }, [dispatch, routerNavigate]);

    const userMenuItems: MenuItem[] = [
      {
        label: "Ver Meus Dados",
        icon: "pi pi-id-card",
        command: () => routerNavigate("/account"),
      },
      {
        label: "Meu perfil",
        icon: "pi pi-user",
        command: () => routerNavigate("/account"),
      },
      {
        label: "Alterar senha",
        icon: "pi pi-key",
        command: () => routerNavigate("/account"),
      },
      {
        label: theme === "dark" ? "Tema claro" : "Tema escuro",
        icon: theme === "dark" ? "pi pi-sun" : "pi pi-moon",
        command: handleToggleTheme,
      },
      {
        label: signingOut ? "Saindo…" : "Sair",
        icon: "pi pi-sign-out",
        disabled: signingOut,
        command: handleLogout,
      },
    ];

    const loadBadges = useCallback(async () => {
      try {
        const [devs, alrts, incs, tsks, feed] = await Promise.all([
          listDevices({ pageSize: 1 }).catch(() => null),
          listAlerts({ status: "open", pageSize: 1 }).catch(() => null),
          listIncidents({ status: "open", pageSize: 1 }).catch(() => null),
          listTasks({ pageSize: 100 }).catch(() => null),
          listFeed(1, 1).catch(() => null),
        ]);
        const openTasksCount = tsks
          ? tsks.items.filter((t) => !["Resolved", "Cancelled", "Closed"].includes(t.state)).length
          : undefined;
        setSidebarBadges({
          devices: devs?.totalCount,
          alerts: alrts?.totalCount,
          incidents: incs?.totalCount,
          tasks: openTasksCount,
          notifications: feed?.totalCount,
        });
      } catch {
        // ignore
      }
    }, []);

    const handleRefresh = useCallback(() => {
      setRefreshing(true);
      loadBadges().finally(() => {
        setTimeout(() => setRefreshing(false), 400);
      });
      window.dispatchEvent(new Event("infracontrol:refresh"));
    }, [loadBadges]);

    const goToSearchResult = useCallback((item: GlobalSearchItem) => {
      setSearchOpen(false);
      setSearchTerm("");
      if (item.type === "device") routerNavigate(`/devices/${item.id}`);
      else if (item.type === "location" || item.type === "rack") routerNavigate("/locations");
    }, [routerNavigate]);

    // Load badges on mount
    useEffect(() => {
      loadBadges();
    }, [loadBadges]);

    // Search effect
    // biome-ignore lint/correctness/useExhaustiveDependencies: searchSeq ref needed
    useEffect(() => {
      if (searchTimer.current) clearTimeout(searchTimer.current);
      const trimmed = searchTerm.trim();
      if (!trimmed) {
        searchSeq.current += 1;
        setSearchResults([]);
        setSearchTotal(0);
        setSearchOpen(false);
        setSearchError(null);
        setSearching(false);
        return;
      }
      searchTimer.current = setTimeout(() => {
        const seq = ++searchSeq.current;
        setSearching(true);
        setSearchError(null);
        globalSearch(trimmed)
          .then((result) => {
            if (seq !== searchSeq.current) return;
            setSearchResults(result.items);
            setSearchTotal(result.totalCount);
            setSearchOpen(true);
          })
          .catch((caught: unknown) => {
            if (seq !== searchSeq.current) return;
            setSearchError(
              caught instanceof Error
                ? caught.message
                : "Não foi possível buscar agora.",
            );
            setSearchResults([]);
            setSearchTotal(0);
            setSearchOpen(true);
          })
          .finally(() => {
            if (seq === searchSeq.current) setSearching(false);
          });
      }, 300);
      return () => {
        if (searchTimer.current) clearTimeout(searchTimer.current);
      };
    }, [searchTerm]);

    return (
      <header className="layout-topbar">
        <button
          ref={menubuttonRef}
          type="button"
          className="p-link layout-menu-button"
          onClick={onMenuButtonClick}
          aria-label="Abrir menu"
          aria-expanded={mobileMenuOpen}
          aria-controls="app-sidebar"
        >
          <i className="pi pi-bars" />
        </button>

        <div className="layout-topbar-breadcrumb">
          InfraControl <i className="pi pi-angle-right" style={{ fontSize: 8 }} />{" "}
          <span>{labelForRoute(activeRoute)}</span>
        </div>

        <div className="layout-topbar-actions">
          <div className="layout-topbar-search" style={{ position: "relative" }}>
            <i className="pi pi-search" />
            <input
              type="text"
              placeholder="Pesquisar…"
              aria-label="Busca global do inventário"
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              onFocus={() => {
                if (searchResults.length > 0 || searchError) setSearchOpen(true);
              }}
              onKeyDown={(event) => {
                if (event.key === "Escape") setSearchOpen(false);
              }}
              onBlur={() => setSearchOpen(false)}
            />
            {searchOpen && searchTerm.trim() !== "" && (
              <div
                onMouseDown={(event) => event.preventDefault()}
                style={{
                  position: "absolute",
                  top: "calc(100% + 6px)",
                  right: 0,
                  width: 340,
                  maxHeight: 380,
                  overflowY: "auto",
                  background: "var(--uni-dropdown-bg)",
                  border: "1px solid var(--uni-border)",
                  borderRadius: 8,
                  boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
                  zIndex: 60,
                  padding: 6,
                }}
              >
                {searching && (
                  <div style={{ padding: "10px 10px", fontSize: 12, color: "var(--uni-text-muted)" }}>
                    Buscando…
                  </div>
                )}
                {!searching && searchError && (
                  <div style={{ padding: "10px 10px", fontSize: 12, color: "var(--uni-red)" }}>
                    {searchError}
                  </div>
                )}
                {!searching && !searchError && searchResults.length === 0 && (
                  <div style={{ padding: "10px 10px", fontSize: 12, color: "var(--uni-text-muted)" }}>
                    Nada encontrado para "{searchTerm.trim()}".
                  </div>
                )}
                {!searching && !searchError && searchResults.map((item) => {
                  const navigable = item.type === "device" || item.type === "location" || item.type === "rack";
                  return (
                    <button
                      key={`${item.type}-${item.id}`}
                      type="button"
                      onClick={() => { if (navigable) goToSearchResult(item); }}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                        width: "100%",
                        padding: "7px 8px",
                        border: "none",
                        borderRadius: 6,
                        background: "transparent",
                        textAlign: "left",
                        cursor: navigable ? "pointer" : "default",
                      }}
                      onMouseEnter={(event) => {
                        if (navigable) event.currentTarget.style.background = "var(--uni-dropdown-hover)";
                      }}
                      onMouseLeave={(event) => { event.currentTarget.style.background = "transparent"; }}
                    >
                      <span style={{ fontSize: 9, fontWeight: 700, padding: "2px 5px", borderRadius: 4, background: "var(--uni-hover)", color: "var(--uni-text-secondary)", flexShrink: 0 }}>
                        {SEARCH_TYPE_LABEL[item.type] ?? item.type}
                      </span>
                      <span style={{ fontSize: 12, fontWeight: 500, color: "var(--uni-text)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                        {item.name}
                      </span>
                      {item.subtitle && (
                        <span style={{ fontSize: 11, color: "var(--uni-text-muted)", marginLeft: "auto", flexShrink: 0 }}>
                          {item.subtitle}
                        </span>
                      )}
                    </button>
                  );
                })}
                {!searching && !searchError && searchTotal > searchResults.length && (
                  <div style={{ padding: "6px 8px", fontSize: 10, color: "var(--uni-text-muted)", borderTop: "1px solid var(--uni-divider)", marginTop: 4 }}>
                    {searchResults.length} de {searchTotal} resultados — refine a busca
                  </div>
                )}
              </div>
            )}
          </div>

          <div style={{ position: "relative" }}>
            <button
              className="layout-topbar-icon"
              title="Adicionar"
              onClick={() => setAddMenuOpen((prev) => !prev)}
            >
              <i className="pi pi-plus" />
            </button>
            {addMenuOpen && (
              <div
                onMouseDown={(e) => e.preventDefault()}
                style={{
                  position: "absolute",
                  top: "calc(100% + 6px)",
                  right: 0,
                  width: 180,
                  background: "var(--uni-dropdown-bg)",
                  border: "1px solid var(--uni-border)",
                  borderRadius: 8,
                  boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
                  zIndex: 60,
                  padding: 4,
                }}
              >
                <button
                  type="button"
                  onClick={() => {
                    setAddMenuOpen(false);
                    routerNavigate("/devices");
                  }}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    width: "100%",
                    padding: "8px 10px",
                    border: "none",
                    borderRadius: 6,
                    background: "transparent",
                    fontSize: 12,
                    cursor: "pointer",
                    textAlign: "left",
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "var(--uni-dropdown-hover)")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                >
                  <i className="pi pi-server" style={{ color: "#3b82f6" }} />
                  <span>Novo Dispositivo</span>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setAddMenuOpen(false);
                    routerNavigate("/incidents");
                  }}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    width: "100%",
                    padding: "8px 10px",
                    border: "none",
                    borderRadius: 6,
                    background: "transparent",
                    fontSize: 12,
                    cursor: "pointer",
                    textAlign: "left",
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "var(--uni-dropdown-hover)")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                >
                  <i className="pi pi-exclamation-triangle" style={{ color: "#ef4444" }} />
                  <span>Novo Incidente</span>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setAddMenuOpen(false);
                    routerNavigate("/tasks");
                  }}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    width: "100%",
                    padding: "8px 10px",
                    border: "none",
                    borderRadius: 6,
                    background: "transparent",
                    fontSize: 12,
                    cursor: "pointer",
                    textAlign: "left",
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "var(--uni-dropdown-hover)")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                >
                  <i className="pi pi-check-square" style={{ color: "#06b6d4" }} />
                  <span>Nova Tarefa</span>
                </button>
              </div>
            )}
          </div>

          <button className="layout-topbar-icon" title="Atualizar" onClick={handleRefresh}>
            <i className={`pi ${refreshing ? "pi-spin pi-refresh" : "pi-refresh"}`} />
          </button>

          <button
            className="layout-topbar-icon"
            title="Notificações"
            onClick={() => routerNavigate("/notifications")}
          >
            <i className="pi pi-bell" />
            {sidebarBadges.notifications !== undefined && sidebarBadges.notifications > 0 && (
              <span className="layout-topbar-badge">{sidebarBadges.notifications}</span>
            )}
          </button>

          <button
            ref={topbarmenubuttonRef}
            className="layout-topbar-profile"
            title={displayName}
            aria-label={`Opções de ${displayName}`}
            aria-haspopup="menu"
            aria-expanded={topbarMenuOpen}
            onClick={(event) => topbarUserMenuRef.current?.toggle(event)}
          >
            {initials}
          </button>

          <Menu
            ref={topbarUserMenuRef}
            model={userMenuItems}
            popup
            aria-label="Opções da conta"
            onShow={() => setTopbarMenuOpen(true)}
            onHide={() => setTopbarMenuOpen(false)}
          />
        </div>
      </header>
    );
  },
);

AppTopbar.displayName = "AppTopbar";
