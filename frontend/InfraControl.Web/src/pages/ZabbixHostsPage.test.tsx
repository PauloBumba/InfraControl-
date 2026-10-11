import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { ZabbixHostsPage } from "./ZabbixHostsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { getZabbixHosts, type ZabbixHostView } from "@/services/api/zabbix";

/**
 * Adoção do padrão de visões (backlog Operations UX, pedido 2026-10-11).
 *
 * A F27 Fase 3.3 entregou a lista como tabela fixa; aqui ela ganha os 3
 * modos, com o quadro agrupado por ESTADO do host (Ausente / Ativo /
 * Desativado) — o mesmo recorte dos filtros "Estado" e "Presença no
 * Zabbix" que a página já expõe.
 */
vi.mock("@/services/api/zabbix", () => ({
  getZabbixHosts: vi.fn(),
  syncZabbixInventory: vi.fn(),
}));

const getZabbixHostsMock = vi.mocked(getZabbixHosts);

function makeHost(overrides: Partial<ZabbixHostView> = {}): ZabbixHostView {
  return {
    hostId: "10084",
    host: "zabbix-server",
    name: "Zabbix server",
    ip: "127.0.0.1",
    port: 10052,
    enabled: true,
    discovered: false,
    missing: false,
    missingSinceUtc: null,
    groups: ["Zabbix servers"],
    templates: ["Template OS Linux"],
    hasAsset: false,
    lastSyncedAtUtc: "2026-10-11T10:00:00Z",
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <ZabbixHostsPage />
      </QueryClientProvider>
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  // a preferência de visão vive no localStorage — zera p/ default determinístico
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Operator"],
    }),
  );
  getZabbixHostsMock.mockResolvedValue({
    items: [
      makeHost(),
      makeHost({
        hostId: "10683",
        host: "switch-rd-01",
        name: "Switch sala de reunião",
        enabled: false,
        missing: true,
        missingSinceUtc: "2026-10-05T09:00:00Z",
      }),
    ],
    totalCount: 2,
    page: 1,
    pageSize: 20,
  });
});

describe("ZabbixHostsPage — visões tabela/grid/quadro", () => {
  it("should keep the table as the default view", async () => {
    const { container } = renderPage();

    expect(await screen.findByText("zabbix-server")).toBeInTheDocument();
    expect(container.querySelector("table")).not.toBeNull();
  });

  it("should switch to a grid of cards", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();
    await screen.findByText("zabbix-server");

    await user.click(screen.getByRole("tab", { name: "Grid" }));

    expect(container.querySelector("table")).toBeNull();
    expect(screen.getByText("Zabbix server")).toBeInTheDocument();
    expect(screen.getByText("switch-rd-01")).toBeInTheDocument();
  });

  it("should switch to a kanban grouped by host state, with every state column", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();
    await screen.findByText("zabbix-server");

    await user.click(screen.getByRole("tab", { name: "Kanban" }));

    expect(container.querySelector("table")).toBeNull();
    // "Desativado" não tem nenhum item no fixture — só o quadro renderiza
    // as 3 colunas de estado, vazias ou não. (o rótulo do card e o da
    // coluna coincidem, por isso All)
    expect(screen.getByText("Desativado")).toBeInTheDocument();
    expect(screen.getAllByText("Ausente").length).toBeGreaterThan(1);
  });
});
