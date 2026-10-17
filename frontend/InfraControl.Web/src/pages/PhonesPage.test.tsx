import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PhonesPage } from "./PhonesPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listLocations } from "@/services/api/inventory";
import { listPhones } from "@/services/api/phones";
import { getDeviceResults } from "@/services/api/monitoring";

/**
 * F30-T006d — PhonesPage: lista DeviceType.Phone + painel SIP & Qualidade
 * (Yealink ao vivo) a partir do histórico de monitoramento.
 * Tranca: contagem da casca (singular/plural), o estado vazio com dica de
 * "Executar checks" e os blocos Registrado + qualidade (MOS/perda com
 * limiares) + PoE.
 */
vi.mock("@/services/api/phones", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/phones")>();
  return { ...actual, listPhones: vi.fn() };
});

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return { ...actual, listLocations: vi.fn() };
});

vi.mock("@/services/api/monitoring", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/monitoring")>();
  return { ...actual, getDeviceResults: vi.fn() };
});

const phone = {
  id: "phone-1",
  locationId: "loc-1",
  rackId: null,
  type: "Phone",
  name: "phone-reception",
  ip: "192.0.2.80",
  mac: null,
  serial: null,
  model: "SIP-T33G",
  vendor: "Yealink",
  vlanId: null,
  description: null,
  extension: "1001",
  sipServer: "pbx.local",
  monitoringChecks: ["Ping", "YealinkSip", "YealinkCallQuality"],
  profileIsCustom: false,
};

const listPhonesMock = vi.mocked(listPhones);
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
  listPhonesMock.mockResolvedValue({
    items: [phone],
    totalCount: 1,
    page: 1,
    pageSize: 100,
  });
  getDeviceResultsMock.mockResolvedValue({
    deviceId: "phone-1",
    deviceName: "phone-reception",
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
        <PhonesPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("PhonesPage — casca da lista (F30-T006d)", () => {
  it("should show heading and phone count", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { name: "Telefones" })).toBeInTheDocument();
    expect(await screen.findByText("1 telefone")).toBeInTheDocument();
  });

  it("should hint running checks when there is no Yealink result yet", async () => {
    renderPage();

    // Espera a auto-seleção do primeiro telefone — antes disso o hint
    // renderizaria de forma transitória (query ainda desabilitada).
    expect(await screen.findByText(/SIP & Qualidade — phone-reception/)).toBeInTheDocument();
    expect(
      await screen.findByText(/Nenhum resultado de monitoramento ainda/),
    ).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Executar checks" })).toBeInTheDocument();
  });

  it("should render SIP registered chip, quality thresholds and PoE from history", async () => {
    getDeviceResultsMock.mockResolvedValue({
      deviceId: "phone-1",
      deviceName: "phone-reception",
      results: [
        {
          id: "r1",
          check: "YealinkSip",
          succeeded: true,
          durationMs: 20,
          error: null,
          values: {
            sipRegistered: "true",
            extension: "1001",
            sipServer: "pbx.local",
            codec: "G.711u",
          },
          checkedAt: "2026-10-16T12:00:00Z",
        },
        {
          id: "r2",
          check: "YealinkCallQuality",
          succeeded: true,
          durationMs: 25,
          error: null,
          values: {
            mos: "2.8",
            jitterMs: "80",
            latencyMs: "40",
            packetLossPercent: "12.5",
            poePowered: "false",
            poePowerW: "0",
          },
          checkedAt: "2026-10-16T12:00:01Z",
        },
      ],
    });
    renderPage();

    expect(await screen.findByText(/SIP & Qualidade — phone-reception/)).toBeInTheDocument();
    expect(await screen.findByText("Registrado")).toBeInTheDocument();
    expect(await screen.findByText("G.711u")).toBeInTheDocument();
    // Qualidade: MOS 2.8 (crit ≤3.0) e perda 12.5% (crit ≥10%).
    expect(await screen.findByText(/crítico \(3\)/)).toBeInTheDocument();
    // Jitter 80ms (atenção ≥50).
    expect(screen.getByText(/atenção \(50\)/)).toBeInTheDocument();
    // PoE: sem energia.
    expect(screen.getByText("Sem energia")).toBeInTheDocument();
    expect(screen.queryByText(/Nenhum resultado de monitoramento ainda/)).toBeNull();
  });

  it("should render non-registered SIP as red chip", async () => {
    getDeviceResultsMock.mockResolvedValue({
      deviceId: "phone-1",
      deviceName: "phone-reception",
      results: [
        {
          id: "r1",
          check: "YealinkSip",
          succeeded: true,
          durationMs: 18,
          error: null,
          values: {
            sipRegistered: "false",
            extension: "1001",
            sipServer: "pbx.local",
            codec: "G.729",
          },
          checkedAt: "2026-10-16T12:00:00Z",
        },
      ],
    });
    renderPage();

    expect(await screen.findByText("Não registrado")).toBeInTheDocument();
  });
});
