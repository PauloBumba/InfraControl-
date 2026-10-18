import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RoutersPage } from "./RoutersPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listLocations } from "@/services/api/inventory";
import { listRouters } from "@/services/api/routers";
import { getDeviceResults } from "@/services/api/monitoring";

/**
 * F30-T007d — RoutersPage: lista DeviceType.Router + Firewall + painel
 * Rotas & Sessões (SNMP universal) a partir do histórico de monitoramento.
 * Tranca: contagem da casca (Router+Firewall mergeados), o estado vazio
 * com dica e os blocos de rotas (defaultGateway) + sessões (established)
 * + saúde (CPU/mem).
 */
vi.mock("@/services/api/routers", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/routers")>();
  return { ...actual, listRouters: vi.fn() };
});

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return { ...actual, listLocations: vi.fn() };
});

vi.mock("@/services/api/monitoring", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/monitoring")>();
  return { ...actual, getDeviceResults: vi.fn() };
});

const router = {
  id: "rtr-1",
  locationId: "loc-1",
  rackId: null,
  type: "Router",
  name: "rtr-core-01",
  ip: "192.0.2.1",
  mac: null,
  serial: null,
  model: "CCR2004",
  vendor: "MikroTik",
  vlanId: null,
  description: null,
  monitoringChecks: ["Ping", "SnmpRouterRoutes", "SnmpRouterSessions"],
  profileIsCustom: false,
};

const firewall = {
  ...router,
  id: "fw-1",
  type: "Firewall",
  name: "fw-edge-01",
  ip: "192.0.2.2",
};

const listRoutersMock = vi.mocked(listRouters);
const listLocationsMock = vi.mocked(listLocations);
const getDeviceResultsMock = vi.mocked(getDeviceResults);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "admin@exemplo.com",
      displayName: "Admin",
      roles: ["Admin"],
    }),
  );
  listLocationsMock.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100 });
  listRoutersMock.mockResolvedValue({
    items: [router, firewall],
    totalCount: 2,
    page: 1,
    pageSize: 2,
  });
  getDeviceResultsMock.mockResolvedValue({
    deviceId: "rtr-1",
    deviceName: "rtr-core-01",
    results: [],
  });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <RoutersPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("RoutersPage — casca da lista (F30-T007d)", () => {
  it("should show heading and merged router/firewall count", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { name: "Roteadores & Firewalls" })).toBeInTheDocument();
    expect(await screen.findByText("2 routers/firewalls")).toBeInTheDocument();
  });

  it("should hint running checks when there is no SNMP result yet", async () => {
    renderPage();

    // Espera a auto-seleção do primeiro device — antes disso o hint
    // renderizaria de forma transitória (query ainda desabilitada).
    expect(
      await screen.findByText(/Rotas & Sessões — rtr-core-01/),
    ).toBeInTheDocument();
    expect(
      await screen.findByText(/Nenhum resultado de monitoramento ainda/),
    ).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Executar checks" })).toBeInTheDocument();
  });

  it("should render routes, sessions and health blocks from history", async () => {
    getDeviceResultsMock.mockResolvedValue({
      deviceId: "rtr-1",
      deviceName: "rtr-core-01",
      results: [
        {
          id: "r1",
          check: "SnmpRouterRoutes",
          succeeded: true,
          durationMs: 40,
          error: null,
          values: {
            routeCount: "12",
            defaultGateway: "10.0.0.254",
            staticCount: "4",
            dynamicCount: "8",
            routes: JSON.stringify([
              { dest: "0.0.0.0", nextHop: "10.0.0.254", proto: "local", type: "4" },
              { dest: "10.10.0.0", nextHop: "10.0.0.1", proto: "ospf", type: "4" },
            ]),
          },
          checkedAt: "2026-10-16T12:00:00Z",
        },
        {
          id: "r2",
          check: "SnmpRouterSessions",
          succeeded: true,
          durationMs: 55,
          error: null,
          values: {
            sessionCount: "340",
            establishedCount: "210",
            listenCount: "15",
            topRemoteIps: JSON.stringify([
              { ip: "203.0.113.5", connections: 18 },
              { ip: "198.51.100.9", connections: 4 },
            ]),
          },
          checkedAt: "2026-10-16T12:00:01Z",
        },
        {
          id: "r3",
          check: "SnmpServerCpu",
          succeeded: true,
          durationMs: 30,
          error: null,
          values: { cpuPercent: "95", averagePercent: "60", coreCount: "4" },
          checkedAt: "2026-10-16T12:00:02Z",
        },
      ],
    });
    renderPage();

    expect(await screen.findByText(/Rotas & Sessões — rtr-core-01/)).toBeInTheDocument();
    // Rotas.
    expect(await screen.findByText("10.0.0.254")).toBeInTheDocument();
    expect(screen.getByText("12")).toBeInTheDocument(); // routeCount
    // Sessões.
    expect(screen.getByText("210")).toBeInTheDocument(); // established
    expect(screen.getByText("203.0.113.5")).toBeInTheDocument();
    // Saúde: CPU 95% (barra usa o valor formatado).
    expect(screen.getByText("95%")).toBeInTheDocument();
    expect(screen.queryByText(/Nenhum resultado de monitoramento ainda/)).toBeNull();
  });
});
