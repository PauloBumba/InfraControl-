import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Link, MemoryRouter, Route, Routes, createMemoryRouter, RouterProvider } from "react-router-dom";
import { DeviceDetailPage } from "./DeviceDetailPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import {
  getDevice,
  getDeviceStatus,
  getDeviceSystemInfo,
  listLocations,
  type DeviceItem,
} from "@/services/api/inventory";
import { getDeviceResults } from "@/services/api/monitoring";
import { listAlerts } from "@/services/api/alerts";

/**
 * F30-T004 S3 (docs/52 §S3): faixa system-info no detalhe do device —
 * uptime formatado + sysDescr cru vindos de
 * `GET /inventory/devices/{id}/system-info`, no mesmo molde da faixa da
 * SwitchesPage (F30-T001 S5b: label+valor no mesmo text node, "—" onde
 * nunca houve leitura, faixa some em pending/erro — nada inventado).
 *
 * Tranca também o aceite do S2 que o detalhe do device exige: o poll de
 * SnmpRadio com suas chaves por rádio (`channel.5=…`) aparece na tabela
 * "Últimos polls" — a coluna Detalhes renderiza qualquer `values`, então
 * esse teste NASCE VERDE (guarda de regressão, documentada como tal).
 */

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return {
    ...actual,
    getDevice: vi.fn(),
    getDeviceStatus: vi.fn(),
    listLocations: vi.fn(),
    getDeviceSystemInfo: vi.fn(),
  };
});

vi.mock("@/services/api/monitoring", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/monitoring")>();
  return { ...actual, getDeviceResults: vi.fn() };
});

vi.mock("@/services/api/alerts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/alerts")>();
  return { ...actual, listAlerts: vi.fn() };
});

vi.mock("@/services/api/remoteSessions", async (importOriginal) => {
  const actual =
    await importOriginal<typeof import("@/services/api/remoteSessions")>();
  return {
    ...actual,
    listDeviceSessions: vi.fn(),
    openGatewaySession: vi.fn(),
    closeSession: vi.fn(),
    getDeviceCredential: vi.fn(),
    putDeviceCredential: vi.fn(),
    deleteDeviceCredential: vi.fn(),
  };
});

import {
  getDeviceCredential,
  listDeviceSessions,
  openGatewaySession,
  putDeviceCredential,
} from "@/services/api/remoteSessions";

const listSessionsMock = vi.mocked(listDeviceSessions);
const gatewayMock = vi.mocked(openGatewaySession);
const getCredentialMock = vi.mocked(getDeviceCredential);
const putCredentialMock = vi.mocked(putDeviceCredential);

const getDeviceMock = vi.mocked(getDevice);
const statusMock = vi.mocked(getDeviceStatus);
const locationsMock = vi.mocked(listLocations);
const systemInfoMock = vi.mocked(getDeviceSystemInfo);
const resultsMock = vi.mocked(getDeviceResults);
const alertsMock = vi.mocked(listAlerts);

const apDevice: DeviceItem = {
  id: "ap-001",
  locationId: "loc-1",
  rackId: null,
  type: "AccessPoint",
  name: "ap-sala-01",
  ip: "192.0.2.30",
  mac: "aa:bb:cc:dd:ee:01",
  serial: "UAP-01234",
  model: "U6-Lite",
  vendor: "Ubiquiti",
  vlanId: null,
  description: "Sala de reunião",
  monitoringChecks: ["Ping", "SnmpRadio"],
  profileIsCustom: true,
};

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
  getDeviceMock.mockResolvedValue(apDevice);
  statusMock.mockResolvedValue(null);
  locationsMock.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100 });
  systemInfoMock.mockResolvedValue({ uptimeSeconds: null, sysDescr: null, capturedAtUtc: null });
  resultsMock.mockResolvedValue({ deviceId: apDevice.id, deviceName: apDevice.name, results: [] });
  alertsMock.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 5 });
  listSessionsMock.mockResolvedValue({
    deviceId: apDevice.id,
    deviceName: apDevice.name,
    sessions: [],
  });
  getCredentialMock.mockResolvedValue({
    deviceId: apDevice.id,
    username: "",
    hasCredential: false,
    rotatedAt: null,
    updatedAt: "2026-01-01T00:00:00Z",
  });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[`/devices/${apDevice.id}`]}>
          <Routes>
            <Route path="/devices/:id" element={<DeviceDetailPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </Provider>,
  );
}

describe("DeviceDetailPage — faixa system-info (F30-T004 S3, padrão S5b)", () => {
  it("should show uptime and sysDescr from GET /devices/{id}/system-info", async () => {
    systemInfoMock.mockResolvedValue({
      uptimeSeconds: 90061,
      sysDescr: "Linux ap-sala 6.6.20",
      capturedAtUtc: "2026-10-15T12:00:00Z",
    });

    renderPage();

    expect(await screen.findByRole("heading", { name: "ap-sala-01" })).toBeInTheDocument();
    expect(await screen.findByText("Uptime: 1d 1h")).toBeInTheDocument();
    expect(await screen.findByText("Sistema: Linux ap-sala 6.6.20")).toBeInTheDocument();
    expect(systemInfoMock).toHaveBeenCalledWith(apDevice.id);
  });

  it("should show honest dashes when the device never answered system-info", async () => {
    renderPage();

    expect(await screen.findByText("Uptime: —")).toBeInTheDocument();
    expect(await screen.findByText("Sistema: —")).toBeInTheDocument();
  });

  it("should hide the system-info band when the lookup fails", async () => {
    systemInfoMock.mockRejectedValue(new Error("boom"));

    renderPage();

    expect(await screen.findByRole("heading", { name: "ap-sala-01" })).toBeInTheDocument();
    // A falha da faixa não polui a página: ela simplesmente não aparece.
    expect(screen.queryByText(/^Uptime:/)).not.toBeInTheDocument();
  });
});

describe("DeviceDetailPage — poll de SnmpRadio no detalhe (docs/52 §S3, guarda)", () => {
  it("should render SnmpRadio per-radio keys in the Detalhes column", async () => {
    resultsMock.mockResolvedValue({
      deviceId: apDevice.id,
      deviceName: apDevice.name,
      results: [
        {
          id: "res-1",
          check: "SnmpRadio",
          succeeded: true,
          durationMs: 12,
          error: null,
          values: { radioCount: "2", "channel.5": "6", "ssid.5": "Corp" },
          checkedAt: "2026-10-15T12:00:00Z",
        },
      ],
    });

    renderPage();

    // Espera a página carregar antes de abrir a aba (o click síncrono cai
    // no "Carregando…" enquanto o getDevice ainda resolve).
    expect(await screen.findByRole("heading", { name: "ap-sala-01" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Monitoramento" }));

    expect(
      await screen.findByText("radioCount=2 · channel.5=6 · ssid.5=Corp"),
    ).toBeInTheDocument();
  });
});


/**
 * ADR-061 §3 (M1): a troca de :id zera o cache de abas (requested) e os
 * dados carregados — a aba clicada em B recarrega e nunca mostra
 * results/alerts/interfaces/ports do device A sob a URL de B.
 */
describe("DeviceDetailPage — troca de :id zera as abas (M1)", () => {
  it("does not keep A's monitoring results while B loads", async () => {
    getDeviceMock.mockImplementation(async (id: string) =>
      id === "ap-001"
        ? apDevice
        : { ...apDevice, id: "ap-002", name: "ap-sala-02" },
    );
    resultsMock.mockImplementation(async (id: string) => {
      if (id === "ap-001") {
        return {
          deviceId: "ap-001",
          deviceName: "ap-sala-01",
          results: [
            {
              id: "poll-a",
              check: "Ping",
              succeeded: true,
              durationMs: 12,
              error: null,
              values: { probe: "resultado-do-device-A" },
              checkedAt: "2026-10-11T10:00:00Z",
            },
          ],
        };
      }
      return new Promise(() => {}) as never; // B fica pendente
    });

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const router = createMemoryRouter(
      [
        {
          path: "/devices/:id",
          element: (
            <>
              <Link to="/devices/ap-002">ir-para-b</Link>
              <DeviceDetailPage />
            </>
          ),
        },
      ],
      { initialEntries: ["/devices/ap-001"] },
    );

    render(
      <Provider store={store}>
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </Provider>,
    );

    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /monitoramento/i }));
    // A coluna Detalhes renderiza `key=value` ("probe=resultado-do-device-A").
    expect(await screen.findByText(/resultado-do-device-A/)).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "ir-para-b" }));

    await waitFor(() =>
      expect(screen.queryByText(/resultado-do-device-A/)).toBeNull(),
    );
    // Nome do B aparece no breadcrumb e no h1.
    expect(
      (await screen.findAllByText("ap-sala-02")).length,
    ).toBeGreaterThanOrEqual(2);
  });
});

describe("DeviceDetailPage — acesso remoto via gateway + credencial (F34-T007/T008 UI)", () => {
  const linuxDevice: DeviceItem = {
    ...apDevice,
    id: "lin-001",
    type: "Workstation",
    name: "pc-fedora",
    operatingSystem: "Linux",
  };

  it("should show the gateway button only with operatingSystem and open the token URL", async () => {
    getDeviceMock.mockResolvedValue(linuxDevice);
    gatewayMock.mockResolvedValue({
      sessionId: "sess-1",
      protocol: "Ssh",
      token: "abc123",
      path: "/guacamole/",
    });
    const openSpy = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);

    renderPage();
    const user = userEvent.setup();

    const button = await screen.findByRole("button", { name: /acesso remoto/i });
    await user.click(button);

    await waitFor(() => expect(gatewayMock).toHaveBeenCalledWith("ap-001"));
    await waitFor(() =>
      expect(openSpy).toHaveBeenCalledWith(
        "/guacamole/?token=abc123",
        "_blank",
        "noopener",
      ),
    );
    openSpy.mockRestore();
  });

  it("should hide the gateway button when operatingSystem is missing", async () => {
    renderPage();
    await screen.findByText("Identificação");
    expect(
      screen.queryByRole("button", { name: /acesso remoto/i }),
    ).toBeNull();
  });

  it("should save a per-device credential via PUT (Administer)", async () => {
    putCredentialMock.mockResolvedValue({
      deviceId: linuxDevice.id,
      username: "guacpilot",
      hasCredential: true,
      rotatedAt: "2026-10-20T00:00:00Z",
      updatedAt: "2026-10-20T00:00:00Z",
    });

    renderPage();
    const user = userEvent.setup();

    const userInput = await screen.findByPlaceholderText("ex.: guacpilot");
    await user.type(userInput, "guacpilot");
    await user.type(screen.getByPlaceholderText("••••••••"), "S3nh@Dev_2026");
    await user.click(screen.getByRole("button", { name: /salvar/i }));

    await waitFor(() =>
      expect(putCredentialMock).toHaveBeenCalledWith(
        "ap-001",
        "guacpilot",
        "S3nh@Dev_2026",
      ),
    );
  });
});
