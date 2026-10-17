import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { CamerasPage } from "./CamerasPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listLocations } from "@/services/api/inventory";
import { listCameras } from "@/services/api/cameras";
import { getDeviceResults } from "@/services/api/monitoring";

/**
 * F30-T005d — CamerasPage: lista DeviceType.Camera + painel Gravação &
 * Armazenamento (ONVIF) a partir do histórico de monitoramento.
 * Tranca: contagem da casca (singular/plural), o estado vazio com dica de
 * "Executar checks" e os blocos Gravando/Parado + barras + retenção.
 */
vi.mock("@/services/api/cameras", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/cameras")>();
  return { ...actual, listCameras: vi.fn() };
});

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return { ...actual, listLocations: vi.fn() };
});

vi.mock("@/services/api/monitoring", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/monitoring")>();
  return { ...actual, getDeviceResults: vi.fn() };
});

const camera = {
  id: "cam-1",
  locationId: "loc-1",
  rackId: null,
  type: "Camera",
  name: "cam-front-01",
  ip: "192.0.2.50",
  mac: null,
  serial: null,
  model: "IP Camera X",
  vendor: "Hikvision",
  vlanId: null,
  description: null,
  monitoringChecks: ["Ping", "OnvifStorage", "OnvifRecording"],
  profileIsCustom: false,
};

const listCamerasMock = vi.mocked(listCameras);
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
  listCamerasMock.mockResolvedValue({
    items: [camera],
    totalCount: 2,
    page: 1,
    pageSize: 100,
  });
  getDeviceResultsMock.mockResolvedValue({
    deviceId: "cam-1",
    deviceName: "cam-front-01",
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
        <CamerasPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("CamerasPage — casca da lista (F30-T005d)", () => {
  it("should show heading and camera count", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { name: "Câmeras" })).toBeInTheDocument();
    expect(await screen.findByText("2 câmeras")).toBeInTheDocument();
  });

  it("should use singular for one camera", async () => {
    listCamerasMock.mockResolvedValue({ items: [camera], totalCount: 1, page: 1, pageSize: 100 });
    renderPage();

    expect(await screen.findByText("1 câmera")).toBeInTheDocument();
  });

  it("should hint running checks when there is no ONVIF result yet", async () => {
    renderPage();

    // Espera a auto-seleção da primeira câmera — antes disso o hint
    // renderizaria de forma transitória (query ainda desabilitada).
    expect(
      await screen.findByText(/Gravação & Armazenamento — cam-front-01/),
    ).toBeInTheDocument();
    expect(
      await screen.findByText(/Nenhum resultado de monitoramento ainda/),
    ).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Executar checks" })).toBeInTheDocument();
  });

  it("should render recording chip, storage bar and retention from history", async () => {
    getDeviceResultsMock.mockResolvedValue({
      deviceId: "cam-1",
      deviceName: "cam-front-01",
      results: [
        {
          id: "r1",
          check: "OnvifRecording",
          succeeded: true,
          durationMs: 12,
          error: null,
          values: {
            recordingActive: "false",
            videoLoss: "true",
            tamper: "false",
          },
          checkedAt: "2026-10-16T12:00:00Z",
        },
        {
          id: "r2",
          check: "OnvifStorage",
          succeeded: true,
          durationMs: 15,
          error: null,
          values: {
            totalMB: "1024000",
            usedMB: "952320",
            availableMB: "71680",
            usedPercent: "93",
            retentionDays: "30",
            manufacturer: "Hikvision",
            model: "IP Camera X",
          },
          checkedAt: "2026-10-16T12:00:01Z",
        },
      ],
    });
    renderPage();

    expect(await screen.findByText(/Gravação & Armazenamento — cam-front-01/)).toBeInTheDocument();
    expect(await screen.findByText("Parado")).toBeInTheDocument();
    expect(await screen.findByText("Perda de vídeo")).toBeInTheDocument();
    expect(await screen.findByText("93%")).toBeInTheDocument();
    expect(screen.getByText("Crítico (limiar 90%)")).toBeInTheDocument();
    expect(screen.getByText("30 dia(s)")).toBeInTheDocument();
    expect(screen.queryByText(/Nenhum resultado de monitoramento ainda/)).toBeNull();
  });
});
