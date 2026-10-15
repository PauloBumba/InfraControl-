import { useEffect, useCallback, useRef, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { Badge } from "primereact/badge";
import { Menu } from "primereact/menu";
import { navigation, secondaryNavigation, type NavItem } from "@/data/dashboardData";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { toggleSidebar, setMobileOpen } from "@/app/store/slices/uiSlice";
import { addToRecentTabs, removeTab } from "@/app/store/slices/navigationSlice";
import { signedOut } from "@/app/store/slices/authSlice";
import { logout } from "@/services/api/auth";
import { globalSearch, listDevices, type GlobalSearchItem } from "@/services/api/inventory";
import { listAlerts } from "@/services/api/alerts";
import { listIncidents } from "@/services/api/incidents";
import { listTasks } from "@/services/api/tasks";
import { listFeed } from "@/services/api/notifications";
import {
  onNotificationPush,
  startNotificationsConnection,
  stopNotificationsConnection,
} from "@/services/signalr/notifications";
import { ACCESS_LEVEL_LABEL, highestAccessLevel } from "@/types/roles";
import { canReach, routeLevel } from "@/app/router/access";
import { allNavItems, labelForRoute } from "@/app/router/routeLabels";

/**
 * `activeRoute` é a **rota** atual (identidade), não o rótulo — F29-T005.
 * O rótulo da trilha é derivado dela, porque o menu tem dois "Zabbix" e só a
 * rota distingue um do outro.
 */
type AppLayoutProps = {
  children: ReactNode;
  activeRoute: string;
  onNavigate: (route: string) => void;
};

/** Rótulos pt-BR dos tipos de `GlobalSearchItemDto` (F2-T007). */
const SEARCH_TYPE_LABEL: Record<GlobalSearchItem["type"], string> = {
  location: "Local",
  rack: "Rack",
  device: "Device",
  interface: "Interface",
  mac: "MAC",
  ip: "IP",
};

export function AppLayout({ children, activeRoute, onNavigate }: AppLayoutProps) {
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

  const navigate = useCallback((route: string) => {
    onNavigate(route);
    dispatch(addToRecentTabs(route));
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

  // Dropdown do usuário no footer do sidebar
  const userMenuRef = useRef<Menu>(null);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const userMenuItems = [
    {
      label: "Minha Conta",
      icon: "pi pi-key",
      command: () => routerNavigate("/account"),
    },
    {
      label: signingOut ? "Saindo…" : "Sair",
      icon: "pi pi-sign-out",
      disabled: signingOut,
      command: handleLogout,
    },
  ];

  // Dropdown do sidebar — decisão 2026-10-14: o filho expande DENTRO do
  // sidebar, sob o pai; o `<Menu>` popup flutuante saiu. A identidade é a
  // ROTA (F29-T005), não o label: os dois "Zabbix" não podem dividir o
  // estado de expandido.
  const [expandedRoute, setExpandedRoute] = useState<string | null>(null);
  const toggleExpanded = useCallback((route: string) => {
    setExpandedRoute((current) => (current === route ? null : route));
  }, []);

  // Real dynamic badges (F18-T008 / F17-T009)
  const [sidebarBadges, setSidebarBadges] = useState<{
    devices?: number;
    alerts?: number;
    incidents?: number;
    tasks?: number;
    notifications?: number;
  }>({});
  const [addMenuOpen, setAddMenuOpen] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

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

  useEffect(() => {
    loadBadges();
  }, [loadBadges]);

  // Entrega 3 (docs/29 backlog): push SignalR em tempo real — conexão do
  // hub viva enquanto o layout logado estiver montado; cada evento novo
  // incrementa o contador do sino (o feed persistido continua fonte de
  // histórico; o badge é revalidado a cada refresh/reload).
  useEffect(() => {
    void startNotificationsConnection();
    const unsubscribe = onNotificationPush(() => {
      setSidebarBadges((current) => ({
        ...current,
        notifications: (current.notifications ?? 0) + 1,
      }));
    });
    return () => {
      unsubscribe();
      void stopNotificationsConnection();
    };
  }, []);

  const handleRefresh = useCallback(() => {
    setRefreshing(true);
    loadBadges().finally(() => {
      setTimeout(() => setRefreshing(false), 400);
    });
    window.dispatchEvent(new Event("infracontrol:refresh"));
  }, [loadBadges]);

  const getItemBadge = (label: string, defaultBadge?: string) => {
    if (label === "Dispositivos" && sidebarBadges.devices !== undefined) return String(sidebarBadges.devices);
    if (label === "Alertas" && sidebarBadges.alerts !== undefined) return String(sidebarBadges.alerts);
    if (label === "Incidentes" && sidebarBadges.incidents !== undefined) return String(sidebarBadges.incidents);
    if (label === "Tarefas" && sidebarBadges.tasks !== undefined) return String(sidebarBadges.tasks);
    return defaultBadge;
  };

  // F29-T005 — busca por ROTA (a lista vem de `routeLabels`, fonte única das
  // duas listas do menu): por rótulo, o "Zabbix" primário achava o item
  // secundário (ícone e aba errados).
  const getNavItem = (route: string) => allNavItems.find((item) => item.route === route);

  // Busca global (fecha a task aberta "Busca global no frontend", F2-T007):
  // o input do topbar era UI morta — agora com debounce de 300ms sobre
  // GET /inventory/search (6 fontes; term obrigatório, então só busca com
  // termo trimmed não vazio). Seq de requisição descarta resposta velha.
  const [searchTerm, setSearchTerm] = useState("");
  const [searchResults, setSearchResults] = useState<GlobalSearchItem[]>([]);
  const [searchTotal, setSearchTotal] = useState(0);
  const [searching, setSearching] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const searchSeq = useRef(0);

  useEffect(() => {
    if (searchTimer.current) clearTimeout(searchTimer.current);
    const trimmed = searchTerm.trim();
    if (!trimmed) {
      searchSeq.current += 1; // invalida resposta em voo
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

  const goToSearchResult = useCallback((item: GlobalSearchItem) => {
    setSearchOpen(false);
    setSearchTerm("");
    // Device vai pro detalhe; localização/rack caem na lista (não existe
    // rota de rack); interface/MAC/IP não têm deviceId no DTO — ficam só
    // listados (decisão da rodada: não inventar rota).
    if (item.type === "device") routerNavigate(`/devices/${item.id}`);
    else if (item.type === "location" || item.type === "rack") routerNavigate("/locations");
  }, [routerNavigate]);

  // F14-T009 — menu e abas acompanham o nível da rota (ADR-016): o Admin
  // (administer) vê tudo; Operator/Viewer não enxergam as rotas administer
  // nem as abas recentes delas. Exibição apenas — o backend autoriza de verdade.
  const canSee = (route: string) => canReach(user?.roles, routeLevel(route));
  const visibleNavigation = navigation.filter((item) => canSee(item.route));
  const visibleSecondaryNavigation = secondaryNavigation.filter((item) => canSee(item.route));
  const visibleRecentTabs = recentTabs.filter((route) => canSee(route));

  /** Um filho já aberto quando a rota corrente é dele — o grupo vira contexto. */
  const hasActiveChild = (item: NavItem): boolean =>
    (item.items ?? []).some((child) => child.route === activeRoute || hasActiveChild(child));

  /**
   * Linha do sidebar. Item com filho vira dropdown INLINE (decisão
   * 2026-10-14): o clique no pai navega para a própria rota E alterna a
   * expansão; os filhos aparecem logo abaixo, num `.uni-nav-sublist`.
   *
   * Com o sidebar recolhido (60px, só ícones) não cabe submenu — o pai
   * continua navegando e o usuário expande o sidebar para alcançar os filhos.
   */
  const renderNavItem = (item: NavItem) => {
    const children = (item.items ?? []).filter((child) => canSee(child.route));
    const hasSubmenu = children.length > 0;
    const expanded = hasSubmenu && (expandedRoute === item.route || hasActiveChild(item));
    const badge = getItemBadge(item.label, item.badge);

    return (
      <li key={item.route}>
        <button
          className={`uni-nav-item ${activeRoute === item.route ? "active" : ""} ${expanded ? "expanded" : ""}`}
          aria-current={activeRoute === item.route ? "page" : undefined}
          aria-expanded={hasSubmenu ? expanded : undefined}
          onClick={() => {
            // F29-T005: o pai navega para a própria rota; o submenu abre
            // como atalho para os filhos.
            navigate(item.route);
            if (hasSubmenu && !sidebarCollapsed) toggleExpanded(item.route);
          }}
          title={item.label}
        >
          <i className={item.icon} />
          <span>{item.label}</span>
          {hasSubmenu && (
            <i className={`pi ${expanded ? "pi-angle-up" : "pi-angle-down"} uni-nav-arrow`} />
          )}
          {badge && <Badge value={badge} className="uni-nav-badge" />}
        </button>
        {hasSubmenu && expanded && !sidebarCollapsed && (
          <ul className="uni-nav-sublist">
            {children.map((child) => (
              <li key={child.route}>
                <button
                  className={`uni-nav-subitem ${activeRoute === child.route ? "active" : ""}`}
                  aria-current={activeRoute === child.route ? "page" : undefined}
                  onClick={() => navigate(child.route)}
                  title={child.label}
                >
                  <i className={child.icon} />
                  <span>{child.label}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </li>
    );
  };

  const sidebarClassName = `uni-sidebar ${sidebarCollapsed ? "collapsed" : ""} ${mobileOpen ? "mobile-open" : ""}`;

  return (
    <div className="uni-shell">
      <aside className={sidebarClassName}>
        <div className="uni-sidebar-logo">
          <svg viewBox="0 0 64 64" width="22" height="22">
            <defs>
              <linearGradient id="infraSidebarGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                <stop offset="0%" style={{stopColor:'#3b82f6',stopOpacity:1}} />
                <stop offset="100%" style={{stopColor:'#2563eb',stopOpacity:1}} />
              </linearGradient>
            </defs>
            <rect width="64" height="64" rx="12" fill="url(#infraSidebarGrad)" />
            <g fill="white">
              <rect x="16" y="12" width="32" height="40" rx="4" fill="none" stroke="white" strokeWidth="2.5"/>
              <rect x="20" y="18" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
              <rect x="20" y="28" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
              <rect x="20" y="38" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
              <circle cx="38" cy="21" r="1.5" fill="#22c55e"/>
              <circle cx="38" cy="31" r="1.5" fill="#22c55e"/>
              <circle cx="38" cy="41" r="1.5" fill="#eab308"/>
            </g>
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
            {visibleNavigation.map(renderNavItem)}
          </ul>

          <div className="uni-nav-divider" />

          <ul className="uni-nav-list">
            {visibleSecondaryNavigation.map(renderNavItem)}
          </ul>
        </nav>

        <div className="uni-sidebar-footer">
          <div className="uni-user-card">
            <button
              className="uni-user-info"
              onClick={(e) => {
                if (userMenuRef.current) {
                  userMenuRef.current.toggle(e);
                }
                setUserMenuOpen(!userMenuOpen);
              }}
              title="Opções do usuário"
            >
              <div className="uni-user-avatar">{initials}</div>
              <div className="uni-user-details">
                <span className="uni-user-name">{displayName}</span>
                <span className="uni-user-role">{roleLabel}</span>
              </div>
              <i className={`pi pi-angle-down ${userMenuOpen ? "pi-angle-up" : ""}`} />
            </button>
            <Menu
              ref={userMenuRef}
              model={userMenuItems}
              popup
              onHide={() => setUserMenuOpen(false)}
            />
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
            InfraControl <i className="pi pi-angle-right" style={{ fontSize: 8 }} /> <span>{labelForRoute(activeRoute)}</span>
          </div>

          <div className="uni-topbar-right">
            <div className="uni-topbar-search" style={{ position: "relative" }}>
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
                  // onMouseDown.prevent: sem isso o blur fecha o dropdown
                  // antes do clique navegar.
                  onMouseDown={(event) => event.preventDefault()}
                  style={{
                    position: "absolute", top: "calc(100% + 6px)", right: 0,
                    width: 340, maxHeight: 380, overflowY: "auto",
                    background: "white", border: "1px solid #e5e7eb",
                    borderRadius: 8, boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
                    zIndex: 60, padding: 6,
                  }}
                >
                  {searching && (
                    <div style={{ padding: "10px 10px", fontSize: 12, color: "#6b7280" }}>
                      Buscando…
                    </div>
                  )}
                  {!searching && searchError && (
                    <div style={{ padding: "10px 10px", fontSize: 12, color: "#b91c1c" }}>
                      {searchError}
                    </div>
                  )}
                  {!searching && !searchError && searchResults.length === 0 && (
                    <div style={{ padding: "10px 10px", fontSize: 12, color: "#9ca3af" }}>
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
                          display: "flex", alignItems: "center", gap: 8, width: "100%",
                          padding: "7px 8px", border: "none", borderRadius: 6,
                          background: "transparent", textAlign: "left",
                          cursor: navigable ? "pointer" : "default",
                        }}
                        onMouseEnter={(event) => {
                          if (navigable) event.currentTarget.style.background = "#f3f4f6";
                        }}
                        onMouseLeave={(event) => { event.currentTarget.style.background = "transparent"; }}
                      >
                        <span style={{ fontSize: 9, fontWeight: 700, padding: "2px 5px", borderRadius: 4, background: "#f1f5f9", color: "#475569", flexShrink: 0 }}>
                          {SEARCH_TYPE_LABEL[item.type] ?? item.type}
                        </span>
                        <span style={{ fontSize: 12, fontWeight: 500, color: "#1f2937", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                          {item.name}
                        </span>
                        {item.subtitle && (
                          <span style={{ fontSize: 11, color: "#9ca3af", marginLeft: "auto", flexShrink: 0 }}>
                            {item.subtitle}
                          </span>
                        )}
                      </button>
                    );
                  })}
                  {!searching && !searchError && searchTotal > searchResults.length && (
                    <div style={{ padding: "6px 8px", fontSize: 10, color: "#9ca3af", borderTop: "1px solid #f3f4f6", marginTop: 4 }}>
                      {searchResults.length} de {searchTotal} resultados — refine a busca
                    </div>
                  )}
                </div>
              )}
            </div>
            <div style={{ position: "relative" }}>
              <button
                className="uni-topbar-icon"
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
                    background: "white",
                    border: "1px solid #e5e7eb",
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
                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f3f4f6")}
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
                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f3f4f6")}
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
                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f3f4f6")}
                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                  >
                    <i className="pi pi-check-square" style={{ color: "#06b6d4" }} />
                    <span>Nova Tarefa</span>
                  </button>
                </div>
              )}
            </div>
            <button className="uni-topbar-icon" title="Atualizar" onClick={handleRefresh}>
              <i className={`pi ${refreshing ? "pi-spin pi-refresh" : "pi-refresh"}`} />
            </button>
            <button
              className="uni-topbar-icon"
              title="Notificações"
              onClick={() => routerNavigate("/notifications")}
            >
              <i className="pi pi-bell" />
              {sidebarBadges.notifications !== undefined && sidebarBadges.notifications > 0 && (
                <span className="uni-notif-badge">{sidebarBadges.notifications}</span>
              )}
            </button>
            <div className="uni-topbar-avatar" title={displayName}>{initials}</div>
          </div>
        </header>

        {visibleRecentTabs.length > 0 && (
          <div className="uni-tabs-bar">
            {visibleRecentTabs.map((route: string) => {
              const navItem = getNavItem(route);
              const tabLabel = labelForRoute(route);
              return (
                <button
                  key={route}
                  className={`uni-tab ${activeRoute === route ? "active" : ""}`}
                  aria-current={activeRoute === route ? "page" : undefined}
                  onClick={() => navigate(route)}
                >
                  <i className={navItem?.icon || "pi pi-circle"} />
                  <span>{tabLabel}</span>
                  <button
                    className="uni-tab-close"
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

        <div className="uni-content">
          {children}
        </div>
      </main>
    </div>
  );
}
