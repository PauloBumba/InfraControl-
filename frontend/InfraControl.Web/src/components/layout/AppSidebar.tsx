import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge } from "primereact/badge";
import { Menu } from "primereact/menu";
import type { MenuItem } from "primereact/menuitem";
import { navigation, secondaryNavigation, type NavGroup } from "@/data/dashboardData";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { toggleSidebar } from "@/app/store/slices/uiSlice";
import { addToRecentTabs } from "@/app/store/slices/navigationSlice";
import { logout } from "@/services/api/auth";
import { listDevices } from "@/services/api/inventory";
import { listAlerts } from "@/services/api/alerts";
import { listIncidents } from "@/services/api/incidents";
import { listTasks } from "@/services/api/tasks";
import { listFeed } from "@/services/api/notifications";
import { readStoredTheme, toggleTheme, type Theme } from "@/services/theme";
import { canReach, routeLevel } from "@/app/router/access";
import { ACCESS_LEVEL_LABEL, highestAccessLevel } from "@/types/roles";

interface AppSidebarProps {
  activeRoute: string;
  onNavigate: (route: string) => void;
}

export function AppSidebar({ activeRoute, onNavigate }: AppSidebarProps) {
  const dispatch = useAppDispatch();
  const routerNavigate = useNavigate();
  const sidebarCollapsed = useAppSelector((s) => s.ui.sidebarCollapsed);
  const user = useAppSelector((s) => s.auth.user);

  const displayName = user?.fullName?.trim() || user?.displayName || user?.email || "—";
  const access = highestAccessLevel(user?.roles);
  const roleLabel = access ? ACCESS_LEVEL_LABEL[access] : "—";
  const initials = displayName
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("") || "IC";

  const [theme, setTheme] = useState<Theme>(readStoredTheme);
  const [sidebarBadges, setSidebarBadges] = useState<{
    devices?: number;
    alerts?: number;
    incidents?: number;
    tasks?: number;
    notifications?: number;
  }>({});

  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const userMenuRef = useRef<Menu>(null);
  const [openGroup, setOpenGroup] = useState<string | null>(null);

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

  const handleToggleSidebar = () => {
    dispatch(toggleSidebar());
  };

  const navigate = (route: string) => {
    onNavigate(route);
    dispatch(addToRecentTabs(route));
  };

  const handleToggleTheme = () => {
    setTheme((current) => toggleTheme(current));
  };

  const handleLogout = async () => {
    await logout();
    routerNavigate("/login", { replace: true });
  };

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
      label: "Sair",
      icon: "pi pi-sign-out",
      command: handleLogout,
    },
  ];

  // Auto-open group containing active route
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

  const canSee = (route: string) => canReach(user?.roles, routeLevel(route));

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

  const groupHasActive = (group: NavGroup): boolean =>
    group.items.some(
      (item) =>
        item.route === activeRoute ||
        (item.items?.some((child) => child.route === activeRoute) ?? false),
    );

  const getItemBadge = (label: string) => {
    if (label === "Dispositivos" && sidebarBadges.devices !== undefined) return String(sidebarBadges.devices);
    if (label === "Alertas" && sidebarBadges.alerts !== undefined) return String(sidebarBadges.alerts);
    if (label === "Incidentes" && sidebarBadges.incidents !== undefined) return String(sidebarBadges.incidents);
    if (label === "Tarefas" && sidebarBadges.tasks !== undefined) return String(sidebarBadges.tasks);
    return undefined;
  };

  const renderNavItem = (item: any, childClass = "") => (
    <button
      className={`layout-menu-item ${childClass} ${item.route === activeRoute ? "active" : ""}`}
      onClick={() => navigate(item.route)}
      title={item.label}
    >
      <i className={item.icon} />
      <span>{item.label}</span>
    </button>
  );

  const renderNavGroup = (group: NavGroup) => {
    const expanded = openGroup === group.label;
    const active = groupHasActive(group);
    const badge = getItemBadge(group.label);

    return (
      <div key={group.label} className="layout-menu-category">
        <button
          className={`layout-menu-item ${active ? "active" : ""} ${expanded ? "expanded" : ""}`}
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
          <i className="pi pi-angle-down layout-menu-arrow" />
          {badge && <Badge value={badge} className="layout-menu-badge" />}
        </button>
        {expanded && (
          <div className="layout-menu-submenu">
            {group.items.map((item) => (
              <div key={item.route}>
                {renderNavItem(item)}
                {item.items && item.items.length > 0 && (
                  <div className="layout-menu-submenu">
                    {item.items.map((child) => (
                      <div key={child.route}>{renderNavItem(child, "layout-menu-subitem")}</div>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className={`layout-sidebar ${sidebarCollapsed ? "collapsed" : ""}`}>
      <div className="layout-sidebar-logo">
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
          className="layout-sidebar-toggle"
          onClick={handleToggleSidebar}
          aria-label={sidebarCollapsed ? "Expandir" : "Recolher"}
        >
          <i className={`pi ${sidebarCollapsed ? "pi-angle-right" : "pi-angle-left"}`} />
        </button>
      </div>

      <nav className="layout-menu">
        {visibleNavigation.map(renderNavGroup)}

        <div className="layout-menu-divider" />

        {visibleSecondaryNavigation.map(renderNavGroup)}
      </nav>

      <div className="layout-sidebar-footer">
        <div className="layout-user-card">
          <button
            className="layout-user-info"
            onClick={(e) => {
              if (userMenuRef.current) {
                userMenuRef.current.toggle(e);
              }
              setUserMenuOpen(!userMenuOpen);
            }}
            title={`${displayName} — ${roleLabel} (opções da conta)`}
          >
            <div className="layout-user-avatar">{initials}</div>
            <div className="layout-user-details">
              <span className="layout-user-name">{displayName}</span>
              <span className="layout-user-role">{roleLabel}</span>
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
    </div>
  );
}
