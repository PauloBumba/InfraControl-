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
// onNotificationPush DEVOLVE o unsubscribe.
vi.mock("@/services/signalr/notifications", () => ({
  onNotificationPush: vi.fn(() => () => {}),
  startNotificationsConnection: vi.fn(() => Promise.resolve()),
  stopNotificationsConnection: vi.fn(() => Promise.resolve()),
}));

/**
 * Os itens da conta navegam pelo React Router (`useNavigate`), não pela prop
 * `onNavigate` do layout — o probe expõe o pathname atual para o teste
 * observar a navegação real.
 */
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
 * Sidebar com dropdown — decisão do usuário (2026-10-19): REVERTE a decisão
 * de 2026-10-14. O filho volta a abrir num `<Menu>` POPUP flutuante do
 * PrimeReact; a expansão INLINE (`.uni-nav-sublist`) morre.
 *
 * Um `<Menu popup>` POR PAI com filho (UniFi e Zabbix): cada instância tem o
 * próprio listener de outside-click apontando para o próprio gatilho — dois
 * gatilhos dividindo UMA instância deixariam o `target` do listener velho
 * (stale: o `useOverlayListener` só refaz o lookup quando `when` muda) e o
 * segundo clique fechava em vez de abrir o outro.
 *
 * Contratos preservados:
 *  - clique no pai navega para a própria rota E alterna o popup (F29-T005);
 *  - a identidade de abertura é a ROTA, não o label — os dois "Zabbix" não
 *    dividem estado (o teste "exactly one active" do bloco acima continua
 *    sendo a rede de proteção);
 *  - os filhos passam pelo mesmo filtro de role dos pais (`canSee`).
 *
 * Comportamentos novos, registrados aqui de propósito:
 *  - o popup NÃO abre sozinho ao entrar numa rota-filha; o pai acende como
 *    contexto do grupo (`.uni-nav-item.active`) para não perder o indicador;
 *  - com o sidebar recolhido (60px) o popup ABRE (flutua por cima — o inline
 *    não cabia);
 *  - clicar num filho navega e FECHA o popup (`onItemClick` do `Menu` popup
 *    chama `hide` — nativo do PrimeReact);
 *  - clique fora/Escape fecham e o `onHide` devolve o `aria-expanded` ao
 *    repouso.
 */
describe("AppLayout — submenu do sidebar abre em popup (PrimeReact)", () => {
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

  it("should keep the children out of the document until the parent is clicked, then show them in a floating menu", async () => {
    renderLayout("/");
    const user = userEvent.setup();

    // Fechado: os filhos não estão em lugar nenhum do documento.
    expect(within(nav()).queryByRole("button", { name: "UniFi Ao Vivo" })).toBeNull();
    expect(screen.queryByRole("menu")).toBeNull();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));

    // Aberto: menu flutuante do PrimeReact…
    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "UniFi Ao Vivo" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Dashboard UniFi" })).toBeInTheDocument();
    // …e o submenu INLINE não existe mais.
    expect(document.querySelector(".uni-nav-sublist")).toBeNull();
    expect(within(nav()).queryByRole("button", { name: "UniFi Ao Vivo" })).toBeNull();
  });

  it("should navigate to the parent route AND toggle the popup on the same click", async () => {
    const { onNavigate } = renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));
    expect(onNavigate).toHaveBeenLastCalledWith("/unifi");
    expect(await screen.findByRole("menu")).toBeInTheDocument();

    // Segundo clique no pai fecha — e navega de novo (F29-T005: o pai SEMPRE
    // navega para a própria rota; abrir/fechar é efeito colateral).
    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));
    expect(onNavigate).toHaveBeenLastCalledWith("/unifi");
    await waitFor(() => expect(screen.queryByRole("menu")).toBeNull());
    expect(within(nav()).getByRole("button", { name: "UniFi" })).toHaveAttribute("aria-expanded", "false");
  });

  it("should mark the parent as group context when the current route is a child", () => {
    renderLayout("/unifi/dashboard");

    // O popup NÃO abre sozinho (ele flutuaria sobre o conteúdo)…
    expect(screen.queryByRole("menu")).toBeNull();
    // …mas o pai acende como contexto do grupo — e só ele.
    expect(document.querySelectorAll(".uni-nav-item.active")).toHaveLength(1);
    const parent = within(nav()).getByRole("button", { name: "UniFi" });
    expect(parent).toHaveAttribute("aria-expanded", "false");
    // aria-current continua sendo da ROTA REAL (F29-T005): o pai é /unifi,
    // não /unifi/dashboard — ninguém no menu é a página atual.
    expect(parent).not.toHaveAttribute("aria-current");
  });

  it("should navigate on child click and close the popup", async () => {
    const { onNavigate } = renderLayout("/");
    const user = userEvent.setup();

    await user.click(within(nav()).getByRole("button", { name: "UniFi" }));
    const menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Dashboard UniFi" }));

    expect(onNavigate).toHaveBeenLastCalledWith("/unifi/dashboard");
    // Menu popup fecha ao acionar um item (nativo do PrimeReact)…
    await waitFor(() => expect(screen.queryByRole("menu")).toBeNull());
    // …e o pai reflete o repouso.
    expect(within(nav()).getByRole("button", { name: "UniFi" })).toHaveAttribute("aria-expanded", "false");
  });
});

/**
 * Avatar do header (2026-10-19): era uma `<div>` morta no canto do topbar.
 * Agora abre as opções da conta num `<Menu popup>` próprio — as MESMAS
 * opções do footer do sidebar (a decisão foi ADICIONAR ao header, não
 * mover). Opções desde 2026-10-19: Meu perfil / Alterar senha (ambos →
 * /account), Tema claro↔escuro (alterna de verdade) e Sair.
 */
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

    // O rótulo reflete o estado NOVO na reabertura (Tema escuro → Tema claro).
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

/**
 * Dropdown do footer do sidebar (o card do usuário): as MESMAS opções do
 * header — os dois dropdowns coexistem por decisão de 2026-10-19.
 */
describe("AppLayout — card do usuário no sidebar abre as mesmas opções", () => {
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
