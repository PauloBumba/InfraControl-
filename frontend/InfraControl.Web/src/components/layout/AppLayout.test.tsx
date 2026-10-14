import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { Provider } from "react-redux";
import { AppLayout } from "./AppLayout";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { removeTab } from "@/app/store/slices/navigationSlice";

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
// onNotificationPush DEVOLVE o unsubscribe.
vi.mock("@/services/signalr/notifications", () => ({
  onNotificationPush: vi.fn(() => () => {}),
  startNotificationsConnection: vi.fn(() => Promise.resolve()),
  stopNotificationsConnection: vi.fn(() => Promise.resolve()),
}));

function renderLayout(activeRoute: string) {
  const onNavigate = vi.fn();
  const { container } = render(
    <Provider store={store}>
      <MemoryRouter>
        <AppLayout activeRoute={activeRoute} onNavigate={onNavigate}>
          <div>conteúdo</div>
        </AppLayout>
      </MemoryRouter>
    </Provider>,
  );
  return { onNavigate, container };
}

/**
 * F29-T005 — a navegação do sidebar era por LABEL, não por rota.
 *
 * O label "Zabbix" existe DUAS vezes (`navigation` → /zabbix e
 * `secondaryNavigation` → /integrations/zabbix), e porque a chave era o texto,
 * três coisas quebravam ao mesmo tempo:
 *  - `labelRouteMap` (`Object.fromEntries`) fica com o ÚLTIMO → clicar no
 *    "Zabbix" do menu primário levava para /integrations/zabbix;
 *  - `getNavItem` acha o PRIMEIRO → ícone/aba errados;
 *  - `activeItem === item.label` acendia OS DOIS itens ao mesmo tempo.
 *
 * A correção é estrutural: a identidade passa a ser a ROTA. Renomear o label
 * não conserta — o próximo duplicado repetiria tudo.
 */
describe("AppLayout — navegação por rota (F29-T005)", () => {
  beforeEach(() => {
    // o store é singleton: abas do teste anterior persistiriam.
    store.getState().navigation.recentTabs.forEach((tab) => store.dispatch(removeTab(tab)));
    localStorage.clear();
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "ana@exemplo.com",
        displayName: "Ana",
        // Admin: os dois "Zabbix" ficam visíveis, que é onde o bug mora.
        roles: ["Admin"],
      }),
    );
  });

  it("should navigate each Zabbix entry to its own route", async () => {
    const { onNavigate } = renderLayout("/zabbix");
    const user = userEvent.setup();

    const [primary, secondary] = screen.getAllByRole("button", { name: "Zabbix" });
    expect(primary).toBeDefined();
    expect(secondary).toBeDefined();

    await user.click(primary!);
    expect(onNavigate).toHaveBeenLastCalledWith("/zabbix");

    await user.click(secondary!);
    expect(onNavigate).toHaveBeenLastCalledWith("/integrations/zabbix");
  });

  it("should mark exactly one entry as current on /zabbix", () => {
    renderLayout("/zabbix");

    const entries = screen.getAllByRole("button", { name: "Zabbix" });
    const current = entries.filter((node) => node.getAttribute("aria-current") === "page");
    expect(current).toHaveLength(1);
    expect(current[0]).toHaveTextContent("Zabbix");
    // …e só UM item do menu aceso, não os dois.
    expect(document.querySelectorAll(".uni-nav-item.active")).toHaveLength(1);
  });

  it("should keep tabs labelled while navigating by route", async () => {
    const { onNavigate, container } = renderLayout("/");
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Dispositivos" }));
    expect(onNavigate).toHaveBeenLastCalledWith("/devices");

    const tabs = await waitFor(
      () => {
        const bar = container.querySelector(".uni-tabs-bar");
        expect(bar).not.toBeNull();
        return bar as HTMLElement;
      },
      { timeout: 3000 },
    );
    // O rótulo continua sendo o que o usuário lê…
    expect(tabs).toHaveTextContent("Dispositivos");
    expect(tabs).not.toHaveTextContent("/devices");

    // …mas clicar na aba navega por ROTA.
    await user.click(within(tabs).getByRole("button", { name: /^Dispositivos/ }));
    expect(onNavigate).toHaveBeenLastCalledWith("/devices");
  });
});

/**
 * Sidebar com dropdown — decisão do usuário (2026-10-14): manter o visual
 * atual, mas o filho passa a expandir DENTRO do sidebar, sob o pai. O
 * `<Menu>` POPUP flutuante do PrimeReact morre.
 *
 * O clique no pai continua navegando para a própria rota (contrato
 * F29-T005) E alternando a expansão. A identidade da expansão é a ROTA, não
 * o label — senão os dois "Zabbix" dividiriam o estado, que é exatamente o
 * bug que o F29-T005 corrigiu.
 */
describe("AppLayout — submenu do sidebar expande inline", () => {
  /**
   * As consultas ficam DENTRO do `<nav>`: a primeira aba criada na barra de
   * abas tem o mesmo rótulo do item-pai ("UniFi") e também é um `<button>`,
   * então `screen` acharia dois.
   */
  const nav = () => screen.getByRole("navigation", { name: "Navegação principal" });

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

  it("should expand the children under the parent, never in a floating popup", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    // Fechado: os filhos não estão no documento.
    expect(within(nav()).queryByRole("button", { name: "UniFi Ao Vivo" })).toBeNull();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));

    // Aberto: os filhos aparecem DENTRO do sidebar…
    expect(within(nav()).getByRole("button", { name: "UniFi Ao Vivo" })).toBeInTheDocument();
    expect(within(nav()).getByRole("button", { name: "Dashboard UniFi" })).toBeInTheDocument();
    // …e não houve menu flutuante do PrimeReact.
    expect(document.querySelector(".p-menu")).toBeNull();
    expect(document.querySelectorAll('[role="menu"]')).toHaveLength(0);
  });

  it("should navigate to the parent route AND expand on the same click", async () => {
    const { onNavigate } = renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));

    expect(onNavigate).toHaveBeenLastCalledWith("/unifi");
    expect(within(nav()).getByRole("button", { name: "UniFi Ao Vivo" })).toBeInTheDocument();
  });

  it("should collapse again on the second click", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));
    expect(within(nav()).getByRole("button", { name: "UniFi Ao Vivo" })).toBeInTheDocument();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));
    expect(within(nav()).queryByRole("button", { name: "UniFi Ao Vivo" })).toBeNull();
  });

  it("should be open already when the current route is a child", () => {
    renderLayout("/unifi/dashboard");

    // Aberto automaticamente…
    expect(within(nav()).getByRole("button", { name: "Dashboard UniFi" })).toBeInTheDocument();
    // …com o FILHO marcado como rota corrente, e não o pai junto.
    expect(document.querySelectorAll(".uni-nav-subitem.active")).toHaveLength(1);
    expect(document.querySelectorAll(".uni-nav-item.active")).toHaveLength(0);
  });

  it("should let the child navigate without collapsing the group", async () => {
    const { onNavigate } = renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));
    await user.click(within(nav()).getByRole("button", { name: "UniFi Ao Vivo" }));

    expect(onNavigate).toHaveBeenLastCalledWith("/unifi");
    // Continua aberto — o usuário ainda pode trocar de filho.
    expect(within(nav()).getByRole("button", { name: "UniFi Ao Vivo" })).toBeInTheDocument();
  });
});
