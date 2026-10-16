import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ServersPage } from "./ServersPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listLocations } from "@/services/api/inventory";
import { listServers } from "@/services/api/servers";
import { getDeviceResults } from "@/services/api/monitoring";

/**
 * F30-T003d — ServersPage: lista DeviceType.Server + painel de saúde
 * (CPU/memória/discos) a partir do histórico de monitoramento.
 * Tranca: contagem da casca (singular/plural), o estado vazio com dica de
 * "Executar checks" e as 3 barras vindas do histórico (com JSON de discos).
 */
vi.mock("@/services/api/servers", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/servers")>();
  return { ...actual, listServers: vi.fn() };
});

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return { ...actual, listLocations: vi.fn() };
});

vi.mock("@/services/api/monitoring", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/monitoring")>();
  return { ...actual, getDeviceResults: vi.fn() };
});

const server = {
  id: "sv-1",
  locationId: "loc-1",
  rackId: null,
  type: "Server",
  name: "srv-app-01",
  ip: "192.0.2.30",
  mac: null,
  serial: null,
  model: "PowerEdge R650",
  vendor: "Dell",
  vlanId: null,
  description: null,
  monitoringChecks: ["Ping", "SnmpSystem"],
  profileIsCustom: false,
};

const listServersMock = vi.mocked(listServers);
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
  listServersMock.mockResolvedValue({
    items: [server],
    totalCount: 2,
    page: 1,
    pageSize: 100,
  });
  getDeviceResultsMock.mockResolvedValue({
    deviceId: "sv-1",
    deviceName: "srv-app-01",
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
        <ServersPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("ServersPage — casca da lista (F30-T003d)", () => {
  it("should show heading and server count", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Servidores" }),
    ).toBeInTheDocument();
    expect(await screen.findByText("2 servidores")).toBeInTheDocument();
  });

  it("should use singular for one server", async () => {
    listServersMock.mockResolvedValue({ items: [server], totalCount: 1, page: 1, pageSize: 100 });
    renderPage();

    expect(await screen.findByText("1 servidor")).toBeInTheDocument();
  });

  it("should hint running checks when there is no SNMP result yet", async () => {
    renderPage();

    // Espera a auto-seleção do primeiro servidor — antes disso o hint
    // renderizaria de forma transitória (query ainda desabilitada).
    expect(
      await screen.findByText(/Saúde do Servidor — srv-app-01/),
    ).toBeInTheDocument();
    expect(
      await screen.findByText(/Nenhum resultado de monitoramento ainda/),
    ).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Executar checks" })).toBeInTheDocument();
  });

  it("should render CPU, memory and disk bars from history", async () => {
    getDeviceResultsMock.mockResolvedValue({
      deviceId: "sv-1",
      deviceName: "srv-app-01",
      results: [
        {
          id: "r1",
          check: "SnmpServerCpu",
          succeeded: true,
          durationMs: 14,
          error: null,
          values: {
            cpuPercent: "95",
            averagePercent: "61",
            coreCount: "8",
            corePercents: "12,95,44,50,60,70,80,90",
            processCount: "240",
          },
          checkedAt: "2026-10-15T12:00:00Z",
        },
        {
          id: "r2",
          check: "SnmpServerMemory",
          succeeded: true,
          durationMs: 11,
          error: null,
          values: {
            usedPercent: "64",
            totalMB: "16384",
            usedMB: "10486",
            availableMB: "5898",
          },
          checkedAt: "2026-10-15T12:00:01Z",
        },
        {
          id: "r3",
          check: "SnmpServerDisk",
          succeeded: true,
          durationMs: 9,
          error: null,
          values: {
            diskCount: "2",
            worstDisk: "/",
            worstPercent: "87",
            disks: JSON.stringify([
              { name: "/var", usedPercent: 51, totalMB: 51200, usedMB: 26112 },
              { name: "/", usedPercent: 87, totalMB: 102400, usedMB: 89088 },
            ]),
          },
          checkedAt: "2026-10-15T12:00:02Z",
        },
      ],
    });
    renderPage();

    expect(await screen.findByText(/Saúde do Servidor — srv-app-01/)).toBeInTheDocument();
    expect(await screen.findByText("95%")).toBeInTheDocument(); // pior core
    expect(screen.getByText("64%")).toBeInTheDocument(); // RAM
    expect(screen.getByText("87%")).toBeInTheDocument(); // pior partição
    expect(screen.getByText("/ (pior)")).toBeInTheDocument();
    expect(screen.getByText("/var")).toBeInTheDocument();
    expect(screen.queryByText(/Nenhum resultado de monitoramento ainda/)).toBeNull();
  });
});
