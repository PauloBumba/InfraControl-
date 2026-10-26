import { useEffect, useCallback, useRef, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { Badge } from "primereact/badge";
import { Menu } from "primereact/menu";
import type { MenuItem } from "primereact/menuitem";
import { navigation, secondaryNavigation, type NavItem, type NavGroup } from "@/data/dashboardData";
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
import { labelForRoute } from "@/app/router/routeLabels";
import { readStoredTheme, toggleTheme, type Theme } from "@/services/theme";

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
  const displayName = user?.fullName?.trim() || user?.displayName || user?.email || "—";
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

  // Tema claro/escuro (2026-10-19 — "alterar a cor" = alternar tema): a
  // preferência vive no localStorage (services/theme.ts) e o efeito é o
  // atributo data-theme no <html>, que redeclara as variáveis --uni-*.
  const [theme, setTheme] = useState<Theme>(readStoredTheme);

  const handleToggleTheme = useCallback(() => {
    setTheme((current) => toggleTheme(current));
  }, []);

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

  // Dropdown do usuário — as MESMAS opções nos DOIS dropdowns (footer do
  // sidebar e avatar do header): perfil (/account com nome), troca de senha
  // (/account, seção Segurança), tema claro/escuro e sair.
  const userMenuRef = useRef<Menu>(null);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
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

  // Avatar do header (2026-10-19): as MESMAS opções da conta num popup
  // próprio — o footer do sidebar não sai (decisão: adicionar, não mover).
  const topbarUserMenuRef = useRef<Menu>(null);
  const [topbarMenuOpen, setTopbarMenuOpen] = useState(false);

  // Acordeão inline (2026-10-22): os filhos abrem DENTRO da própria barra
  // (empurrando os grupos seguintes) — sem popup flutuante. O popup de
  // 2026-10-19 nascia mais estreito que a barra e deixava os ícones dos
  // itens de trás vazarem na borda (parecia quebrado); o motivo original
  // dele (submenu não cabia na barra RECOLHIDA de 60px) morre aqui: com a
  // barra recolhida, clicar no grupo reabre a barra e mostra o acordeão.
  const [openGroup, setOpenGroup] = useState<string | null>(null);

  // O grupo da rota ativa abre sozinho (item corrente sempre visível).
  useEffect(() => {
    const group = [...navigation, ...secondaryNavigation].find(
      (candidate) =>
        candidate.items.some(
          (item) =>
            item.route === activeRoute ||
            (item.items?.some((child) => child.route === activeRoute) ?? false),
        ),
    );
    if (group) setOpenGroup(group.label);
  }, [activeRoute]);

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
  const getNavItem = (route: string): NavItem | undefined => {
    for (const group of [...navigation, ...secondaryNavigation]) {
      const found = group.items.find((item) => item.route === route);
      if (found) return found;
      for (const item of group.items) {
        if (item.items) {
          const child = item.items.find((c) => c.route === route);
          if (child) return child;
        }
      }
    }
    return undefined;
  };

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

  /** Filtra um grupo de navegação pelo nível de acesso do utilizador. */
  const filterGroup = (group: NavGroup): NavGroup => ({
    ...group,
    items: group.items
      .filter((item) => canSee(item.route))
      .map((item) =>
        item.items ? { ...item, items: item.items.filter((child) => canSee(child.route)) } : item,
      )
      .filter((item) => !item.items || item.items.length > 0),
  });

  const visibleNavigation = navigation.map(filterGroup).filter((group) => group.items.length > 0);
  const visibleSecondaryNavigation = secondaryNavigation.map(filterGroup).filter((group) => group.items.length > 0);
  const visibleRecentTabs = recentTabs.filter((route) => canSee(route));

  /** Verifica se alguma rota do grupo (ou dos seus filhos) está ativa. */
  const groupHasActive = (group: NavGroup): boolean =>
    group.items.some(
      (item) =>
        item.route === activeRoute ||
        (item.items?.some((child) => child.route === activeRoute) ?? false),
    );

  /**
   * Item filho (ou neto, em UniFi/Zabbix) do acordeão — botão inline que
   * navega direto; o rota ativa ganha o mesmo realce `.active` do grupo.
   */
  const renderNavItem = (item: NavItem, childClass = "") => (
    <button
      className={`uni-nav-item uni-nav-child ${childClass} ${item.route === activeRoute ? "active" : ""}`}
      onClick={() => navigate(item.route)}
      title={item.label}
    >
      <i className={item.icon} />
      <span>{item.label}</span>
    </button>
  );

  /**
   * Grupo do sidebar — acordeão inline: o clique expande os itens DENTRO
   * da barra (um grupo por vez, identidade = ROTA em F29-T005). Recolhido
   * (60px), o clique reabre a barra já com o grupo aberto.
   */
  const renderNavGroup = (group: NavGroup) => {
    const expanded = openGroup === group.label;
    const active = groupHasActive(group);
    const badge = getItemBadge(group.label);

    return (
      <li key={group.label}>
        <button
          className={`uni-nav-item uni-nav-group ${active ? "active" : ""} ${expanded ? "expanded" : ""}`}
          aria-expanded={expanded}
          onClick={() => {
            if (sidebarCollapsed) {
              dispatch(toggleSidebar());
              setOpenGroup(group.label);
              return;
            }
            setOpenGroup((current) => (current === group.label ? null : group.label));
          }}
          title={group.label}
        >
          <i className={group.icon} />
          <span>{group.label}</span>
          <i className="pi pi-angle-down uni-nav-arrow" />
          {badge && <Badge value={badge} className="uni-nav-badge" />}
        </button>
        {expanded && (
          <ul className="uni-nav-submenu" aria-label={group.label}>
            {group.items.map((item) => (
              <li key={item.route}>
                {renderNavItem(item)}
                {item.items && item.items.length > 0 && (
                  <ul className="uni-nav-submenu uni-nav-submenu-2" aria-label={item.label}>
                    {item.items.map((child) => (
                      <li key={child.route}>{renderNavItem(child)}</li>
                    ))}
                  </ul>
                )}
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
            {visibleNavigation.map(renderNavGroup)}
          </ul>

          <div className="uni-nav-divider" />

          <ul className="uni-nav-list">
            {visibleSecondaryNavigation.map(renderNavGroup)}
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
              title={`${displayName} — ${roleLabel} (opções da conta)`}
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
              model={[
                {
                  label: "Ver Meus Dados",
                  icon: "pi pi-id-card",
                  command: () => routerNavigate("/account"),
                },
                ...userMenuItems.filter((item) => item.label !== "Ver Meus Dados"),
              ]}
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
                    background: "var(--uni-dropdown-bg)",
                    border: "1px solid var(--uni-border)",
                    borderRadius: 8, boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
                    zIndex: 60, padding: 6,
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
                          display: "flex", alignItems: "center", gap: 8, width: "100%",
                          padding: "7px 8px", border: "none", borderRadius: 6,
                          background: "transparent", textAlign: "left",
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
            <button
              className="uni-topbar-avatar"
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
