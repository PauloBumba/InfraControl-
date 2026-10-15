import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PrintersPage } from "./PrintersPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listLocations } from "@/services/api/inventory";
import { listPrinters } from "@/services/api/printers";
import { getDeviceResults } from "@/services/api/monitoring";

/**
 * F30-T002d — PrintersPage: lista DeviceType.Printer + painel de
 * suprimentos/estado/contadores a partir do histórico de monitoramento.
 * Tranca: contagem da casca (singular/plural) e o estado vazio com dica
 * de "Executar checks" quando ainda não há resultado SNMP.
 */
vi.mock("@/services/api/printers", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/printers")>();
  return { ...actual, listPrinters: vi.fn() };
});

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return { ...actual, listLocations: vi.fn() };
});

vi.mock("@/services/api/monitoring", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/monitoring")>();
  return { ...actual, getDeviceResults: vi.fn() };
});

const printer = {
  id: "pr-1",
  locationId: "loc-1",
  rackId: null,
  type: "Printer",
  name: "HP-01",
  ip: "192.0.2.70",
  mac: null,
  serial: null,
  model: "LaserJet M404dn",
  vendor: "HP",
  vlanId: null,
  description: null,
  monitoringChecks: ["Ping", "SnmpToner", "SnmpDrum"],
  profileIsCustom: false,
};

const listPrintersMock = vi.mocked(listPrinters);
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
  listPrintersMock.mockResolvedValue({
    items: [printer],
    totalCount: 2,
    page: 1,
    pageSize: 100,
  });
  getDeviceResultsMock.mockResolvedValue({
    deviceId: "pr-1",
    deviceName: "HP-01",
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
        <PrintersPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("PrintersPage — casca da lista (F30-T002d)", () => {
  it("should show heading and printer count", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Impressoras" }),
    ).toBeInTheDocument();
    expect(await screen.findByText("2 impressoras")).toBeInTheDocument();
  });

  it("should use singular for one printer", async () => {
    listPrintersMock.mockResolvedValue({ items: [printer], totalCount: 1, page: 1, pageSize: 100 });
    renderPage();

    expect(await screen.findByText("1 impressora")).toBeInTheDocument();
  });

  it("should hint running checks when there is no SNMP result yet", async () => {
    renderPage();

    // Espera a auto-seleção da primeira impressora — antes disso o hint
    // renderizaria de forma transitória (query ainda desabilitada).
    expect(await screen.findByText(/Suprimentos & Estado — HP-01/)).toBeInTheDocument();
    expect(
      await screen.findByText(/Nenhum resultado de monitoramento ainda/),
    ).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Executar checks" })).toBeInTheDocument();
  });

  it("should render supply bars and counters from history", async () => {
    getDeviceResultsMock.mockResolvedValue({
      deviceId: "pr-1",
      deviceName: "HP-01",
      results: [
        {
          id: "r1",
          check: "SnmpToner",
          succeeded: true,
          durationMs: 12,
          error: null,
          values: { levelPercent: "45", description: "Black Toner" },
          checkedAt: "2026-10-13T12:00:00Z",
        },
        {
          id: "r2",
          check: "SnmpPrinterCounters",
          succeeded: true,
          durationMs: 9,
          error: null,
          values: { totalPages: "125000" },
          checkedAt: "2026-10-13T12:00:01Z",
        },
      ],
    });
    renderPage();

    expect(await screen.findByText("Toner preto")).toBeInTheDocument();
    expect(await screen.findByText("45%")).toBeInTheDocument();
    expect(await screen.findByText("125.000")).toBeInTheDocument();
    expect(screen.queryByText(/Nenhum resultado de monitoramento ainda/)).toBeNull();
  });
});
