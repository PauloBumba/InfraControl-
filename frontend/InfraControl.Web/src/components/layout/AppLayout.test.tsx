import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { Provider } from "react-redux";
import { PrimeReactProvider } from "primereact/api";
import { AppLayout } from "./AppLayout";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { removeTab } from "@/app/store/slices/navigationSlice";
import { logout } from "@/services/api/auth";

vi.mock("@/services/api/auth", () => ({ logout: vi.fn() }));
vi.mock("@/services/api/inventory", () => ({
  globalSearch: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
  listDevices: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 1 }),
}));
vi.mock("@/services/api/alerts", () => ({
  listAlerts: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 1 }),
}));
vi.mock("@/services/api/incidents", () => ({
  listIncidents: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 1 }),
}));
vi.mock("@/services/api/tasks", () => ({
  listTasks: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 1 }),
}));
vi.mock("@/services/api/notifications", () => ({
  listFeed: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
}));
vi.mock("@/services/signalr/notifications", () => ({
  onNotificationPush: vi.fn(() => () => {}),
  startNotificationsConnection: vi.fn(() => Promise.resolve()),
  stopNotificationsConnection: vi.fn(),
}));

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="route-probe">{location.pathname}</div>;
}

function renderLayout(activeRoute: string) {
  const onNavigate = vi.fn();
  const { container } = render(
    <PrimeReactProvider>
      <Provider store={store}>
        <MemoryRouter>
          <LocationProbe />
          <AppLayout activeRoute={activeRoute} onNavigate={onNavigate}>
            <div>conteúdo</div>
          </AppLayout>
        </MemoryRouter>
      </Provider>
    </PrimeReactProvider>,
  );
  return { onNavigate, container };
}

function nav() {
  return screen.getByRole("navigation", { name: "Navegação principal" });
}

describe("AppLayout — grupos do sidebar (F38-T001)", () => {
  beforeEach(() => {
    store.getState().navigation.recentTabs.forEach((tab) => store.dispatch(removeTab(tab)));
    localStorage.clear();
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "ana@exemplo.com",
        displayName: "Ana",
        roles: ["Admin"],
      }),
    );
  });

  it("should render all nav groups", () => {
    renderLayout("/");
    expect(within(nav()).getByRole("button", { name: /Infraestrutura/ })).toBeInTheDocument();
    expect(within(nav()).getByRole("button", { name: /Rede/ })).toBeInTheDocument();
    expect(within(nav()).getByRole("button", { name: /Monitoramento/ })).toBeInTheDocument();
    expect(within(nav()).getByRole("button", { name: /Operação/ })).toBeInTheDocument();
    expect(within(nav()).getByRole("button", { name: /Administração/ })).toBeInTheDocument();
  });

  it("should open a group dropdown and navigate to an item", async () => {
    const { onNavigate } = renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: /Infraestrutura/ }));
    const menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Dispositivos" }));
    expect(onNavigate).toHaveBeenLastCalledWith("/devices");
  });

  it("should mark the group as active when a child route is current", () => {
    renderLayout("/devices");
    expect(document.querySelectorAll(".uni-nav-item.active").length).toBeGreaterThan(0);
  });

  it("should expand sub-items inside a group (UniFi)", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: /Monitoramento/ }));
    const menu = await screen.findByRole("menu");
    // UniFi é um sub-item com filhos — deve estar visível no menu
    expect(within(menu).getByText("UniFi")).toBeInTheDocument();
  });
});

describe("AppLayout — submenu popup dentro de grupos", () => {
  beforeEach(() => {
    store.getState().navigation.recentTabs.forEach((tab) => store.dispatch(removeTab(tab)));
    localStorage.clear();
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "ana@exemplo.com",
        displayName: "Ana",
        roles: ["Admin"],
      }),
    );
  });

  it("should keep children out of the document until the group is clicked", () => {
    renderLayout("/");
    expect(within(nav()).queryByRole("button", { name: "UniFi Ao Vivo" })).toBeNull();
    expect(screen.queryByRole("menu")).toBeNull();
  });

  it("should open group and show child items", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: /Monitoramento/ }));
    const menu = await screen.findByRole("menu");
    expect(within(menu).getByText("UniFi")).toBeInTheDocument();
    expect(within(menu).getByText("Zabbix")).toBeInTheDocument();
  });

  it("should navigate to a simple child item", async () => {
    const { onNavigate } = renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: /Infraestrutura/ }));
    const menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Servidores" }));
    expect(onNavigate).toHaveBeenLastCalledWith("/servers");
  });

  it("should mark group as active when child route is current", () => {
    renderLayout("/unifi/dashboard");
    expect(document.querySelectorAll(".uni-nav-item.active").length).toBeGreaterThan(0);
  });
});

describe("AppLayout — avatar do header abre as opções da conta", () => {
  beforeEach(() => {
    store.getState().navigation.recentTabs.forEach((tab) => store.dispatch(removeTab(tab)));
    localStorage.clear();
    document.documentElement.removeAttribute("data-theme");
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "ana@exemplo.com",
        displayName: "Ana",
        roles: ["Admin"],
      }),
    );
  });

  it("should open the account options from the topbar avatar", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    const avatar = screen.getByRole("button", { name: "Opções de Ana" });
    expect(avatar).toHaveAttribute("aria-haspopup", "menu");
    expect(avatar).toHaveAttribute("aria-expanded", "false");

    await user.click(avatar);

    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Ver Meus Dados" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Meu perfil" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Alterar senha" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Tema escuro" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Sair" })).toBeInTheDocument();
    expect(avatar).toHaveAttribute("aria-expanded", "true");
  });

  it("should close again when the avatar is clicked twice", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    const avatar = screen.getByRole("button", { name: "Opções de Ana" });
    await user.click(avatar);
    await screen.findByRole("menu");

    await user.click(avatar);
    await waitFor(() => expect(screen.queryByRole("menu")).toBeNull());
    expect(avatar).toHaveAttribute("aria-expanded", "false");
  });

  it("should navigate to /account on Meu perfil and on Alterar senha", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Opções de Ana" }));
    let menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Meu perfil" }));
    await waitFor(() => expect(screen.getByTestId("route-probe")).toHaveTextContent("/account"));
    await waitFor(() => expect(screen.queryByRole("menu")).toBeNull());

    await user.click(screen.getByRole("button", { name: "Opções de Ana" }));
    menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Alterar senha" }));
    await waitFor(() => expect(screen.getByTestId("route-probe")).toHaveTextContent("/account"));
  });

  it("should toggle the dark theme and persist the choice", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Opções de Ana" }));
    let menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Tema escuro" }));

    await waitFor(() =>
      expect(document.documentElement.getAttribute("data-theme")).toBe("dark"),
    );
    expect(localStorage.getItem("infracontrol.theme")).toBe("dark");

    await user.click(screen.getByRole("button", { name: "Opções de Ana" }));
    menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Tema claro" })).toBeInTheDocument();

    await user.click(within(menu).getByRole("menuitem", { name: "Tema claro" }));
    await waitFor(() =>
      expect(document.documentElement.hasAttribute("data-theme")).toBe(false),
    );
    expect(localStorage.getItem("infracontrol.theme")).toBe("light");
  });

  it("should sign out through the header menu", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Opções de Ana" }));
    const menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Sair" }));

    await waitFor(() => expect(logout).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.queryByRole("menu")).toBeNull());
    expect(store.getState().auth.status).toBe("anonymous");
  });
});

describe("AppLayout — card do usuário no sidebar", () => {
  beforeEach(() => {
    store.getState().navigation.recentTabs.forEach((tab) => store.dispatch(removeTab(tab)));
    localStorage.clear();
    document.documentElement.removeAttribute("data-theme");
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "ana@exemplo.com",
        displayName: "Ana",
        roles: ["Admin"],
      }),
    );
  });

  it("should open the same account options as the header", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    await user.click(screen.getByTitle("Opções do usuário"));

    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Meu perfil" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Alterar senha" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Tema escuro" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Sair" })).toBeInTheDocument();
  });
});
