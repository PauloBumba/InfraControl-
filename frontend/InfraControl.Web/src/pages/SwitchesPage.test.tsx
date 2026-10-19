import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SwitchesPage } from "./SwitchesPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { getDeviceSystemInfo, listLocations, listSwitchPorts } from "@/services/api/inventory";
import { getDeviceResults } from "@/services/api/monitoring";
import { setUnifiPortConfig } from "@/services/api/unifi";
import {
  getSwitchUnifiPorts,
  getSwitchZabbixInterfaces,
  listSwitches,
} from "@/services/api/network";

/**
 * F29-T001C-3 — SwitchesPage na casca padrão (`ListPageShell`).
 *
 * O que este teste tranca: o subtítulo é sempre "N switches" — sem ramo de
 * loading e sem regra de plural (a produção diz "1 switches", erro de
 * gramática pré-existente que o C-3 preserva de propósito: corrigir muda
 * texto visível e é decisão humana).
 */
vi.mock("@/services/api/network", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/network")>();
  return {
    ...actual,
    listSwitches: vi.fn(),
    getSwitchUnifiPorts: vi.fn(),
    getSwitchZabbixInterfaces: vi.fn(),
  };
});

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return {
    ...actual,
    listLocations: vi.fn(),
    listSwitchPorts: vi.fn(),
    getDeviceSystemInfo: vi.fn(),
  };
});

const listSwitchesMock = vi.mocked(listSwitches);
const listLocationsMock = vi.mocked(listLocations);
const listSwitchPortsMock = vi.mocked(listSwitchPorts);
const unifiPortsMock = vi.mocked(getSwitchUnifiPorts);
const zabbixIfacesMock = vi.mocked(getSwitchZabbixInterfaces);
const systemInfoMock = vi.mocked(getDeviceSystemInfo);

vi.mock("@/services/api/monitoring", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/monitoring")>();
  return { ...actual, getDeviceResults: vi.fn() };
});

vi.mock("@/services/api/unifi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/unifi")>();
  return { ...actual, setUnifiPortConfig: vi.fn() };
});

const setUnifiPortConfigMock = vi.mocked(setUnifiPortConfig);

const deviceResultsMock = vi.mocked(getDeviceResults);

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
  listSwitchesMock.mockResolvedValue({ items: [], totalCount: 3, page: 1, pageSize: 100 });
  // Só rodam com um switch selecionado — esvazia para não haver fetch real.
  unifiPortsMock.mockResolvedValue([]);
  zabbixIfacesMock.mockResolvedValue([]);
  systemInfoMock.mockResolvedValue({ uptimeSeconds: null, sysDescr: null, capturedAtUtc: null });
  deviceResultsMock.mockResolvedValue({ deviceId: switchItem.id, deviceName: "sw-core-01", results: [] });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <SwitchesPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("SwitchesPage — casca da lista (C-3: ListPageShell)", () => {
  it("should count switches without a plural rule", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Switches & Portas" }),
    ).toBeInTheDocument();
    expect(await screen.findByText("3 switches")).toBeInTheDocument();
  });

  it("should keep the pre-existing singular wording byte-identical", async () => {
    listSwitchesMock.mockResolvedValue({ items: [], totalCount: 1, page: 1, pageSize: 100 });
    renderPage();

    // A produção diz "1 switches" (gramática errada, pré-existente). O C-3 só
    // troca a casca — corrigir para "1 switch" muda texto visível e fica para
    // decisão humana.
    expect(await screen.findByText("1 switches")).toBeInTheDocument();
    expect(screen.queryByText("1 switch")).toBeNull();
  });
});

/** Um switch cadastrado — fixture compartilhada dos testes do S5. */
const switchItem = {
  id: "7a000000-0000-0000-0000-000000000001",
  locationId: "7a000000-0000-0000-0000-0000000000ff",
  rackId: null,
  type: "Switch",
  name: "sw-core-01",
  ip: "192.0.2.10",
  mac: null,
  serial: null,
  model: null,
  vendor: null,
  vlanId: null,
  description: null,
  monitoringChecks: ["Ping"],
  profileIsCustom: false,
};

describe("SwitchesPage — portas com velocidade/duplex (F30-T001 S5a)", () => {
  it("should show observed speed/duplex and an em dash when not observed", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    listSwitchPortsMock.mockResolvedValue({
      items: [
        {
          id: "7b000000-0000-0000-0000-000000000001",
          deviceId: switchItem.id,
          name: "Gi1/0/1",
          description: "Uplink",
          linkUp: true,
          speed: "Speed1G",
          duplex: "Full",
        },
        {
          id: "7b000000-0000-0000-0000-000000000002",
          deviceId: switchItem.id,
          name: "Gi1/0/2",
          description: null,
          linkUp: null,
          speed: null,
          duplex: null,
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 100,
    });

    renderPage();

    expect(
      await screen.findByRole("columnheader", { name: "Velocidade" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("columnheader", { name: "Duplex" }),
    ).toBeInTheDocument();
    // O enum serializado ("Speed1G") aparece como token da ficha ("1G").
    expect(await screen.findByText("1G")).toBeInTheDocument();
    expect(screen.getByText("Full")).toBeInTheDocument();
    // Porta nunca observada: "—" da Velocidade + "—" do Duplex + "—" do Link.
    expect(screen.getAllByText("—").length).toBeGreaterThanOrEqual(3);
  });
});

describe("SwitchesPage — system-info do switch (F30-T001 S5b)", () => {
  it("should show uptime and sysDescr from GET /devices/{id}/system-info", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    systemInfoMock.mockResolvedValue({
      uptimeSeconds: 90060, // 25h 1m ⇒ "1d 1h"
      sysDescr: "UniFiSwitch 8 POE, 6.2.26",
      capturedAtUtc: "2026-10-15T11:00:00Z",
    });

    renderPage();

    expect(await screen.findByText("Uptime: 1d 1h")).toBeInTheDocument();
    expect(screen.getByText("Sistema: UniFiSwitch 8 POE, 6.2.26")).toBeInTheDocument();
    expect(systemInfoMock).toHaveBeenCalledWith(switchItem.id);
  });

  it("should show an em dash when the device never answered", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    systemInfoMock.mockResolvedValue({
      uptimeSeconds: null,
      sysDescr: null,
      capturedAtUtc: null,
    });

    renderPage();

    // A faixa aparece, mas não mente: "—" onde nunca houve leitura.
    expect(await screen.findByText("Uptime: —")).toBeInTheDocument();
    expect(screen.getByText("Sistema: —")).toBeInTheDocument();
  });
});

describe("SwitchesPage — saúde SNMP do switch (F30-T001 S5c)", () => {
  const pingResult = {
    id: "7c000000-0000-0000-0000-000000000009",
    check: "Ping",
    succeeded: true,
    durationMs: 3,
    error: null,
    values: { roundtrip_ms: "2" },
    checkedAt: "2026-10-15T11:29:00Z",
  };

  it("should show the latest SnmpHealth metrics (CPU/memory/temperature)", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    deviceResultsMock.mockResolvedValue({
      deviceId: switchItem.id,
      deviceName: "sw-core-01",
      results: [
        {
          id: "7c000000-0000-0000-0000-000000000001",
          check: "SnmpHealth",
          succeeded: true,
          durationMs: 12,
          error: null,
          values: { cpuPercent: "12", memoryPercent: "34", temperatureCelsius: "41" },
          checkedAt: "2026-10-15T11:30:00Z",
        },
        pingResult,
        {
          id: "7c000000-0000-0000-0000-000000000003",
          check: "SnmpHealth",
          succeeded: true,
          durationMs: 10,
          error: null,
          values: { cpuPercent: "99", memoryPercent: "99", temperatureCelsius: "99" },
          checkedAt: "2026-10-15T10:30:00Z",
        },
      ],
    });

    renderPage();

    // O mais recente vale — o de 10:30 (99) não pode aparecer.
    expect(
      await screen.findByText("Saúde: CPU 12% · Memória 34% · Temperatura 41°C"),
    ).toBeInTheDocument();
    expect(screen.queryByText(/99/)).toBeNull();
  });

  it("should show an em dash when SnmpHealth never answered", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    deviceResultsMock.mockResolvedValue({
      deviceId: switchItem.id,
      deviceName: "sw-core-01",
      results: [pingResult],
    });

    renderPage();

    expect(await screen.findByText("Saúde: —")).toBeInTheDocument();
  });

  it("should not invent metrics when the latest SnmpHealth failed", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    deviceResultsMock.mockResolvedValue({
      deviceId: switchItem.id,
      deviceName: "sw-core-01",
      results: [
        {
          id: "7c000000-0000-0000-0000-000000000004",
          check: "SnmpHealth",
          succeeded: false,
          durationMs: 5000,
          error: "timeout",
          values: null,
          checkedAt: "2026-10-15T11:30:00Z",
        },
      ],
    });

    renderPage();

    // Resultado falho: values null ⇒ "—" por métrica, nunca 0.
    expect(
      await screen.findByText("Saúde: CPU — · Memória — · Temperatura —"),
    ).toBeInTheDocument();
  });
});

/**
 * ADR-061 §3 (H6): o botão Habilitar/Desabilitar escreve pelo estado
 * ADMINISTRATIVO (`enabled`), nunca pela telemetria do cabo (`linkUp`).
 * Antes: `disabled: !linkUp` — porta com linkUp null (cabo desplugado) era
 * DESABILITADA ao clicar "Habilitar" (bug de escrita em equipamento real).
 */
describe("SwitchesPage — escrita de porta usa estado administrativo (H6)", () => {
  it("sends disabled=false when the port is enabled but has linkUp=null", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    listSwitchPortsMock.mockResolvedValue({
      items: [], totalCount: 0, page: 1, pageSize: 100,
    });
    unifiPortsMock.mockResolvedValue([
      {
        portIndex: 5,
        name: "Port 5",
        enabled: true,
        linkUp: null,
        speed: null,
        fullDuplex: null,
        portVlan: null,
        poeEnable: null,
        poeStatus: null,
        media: null,
        clientMac: null,
      },
    ]);

    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Desabilitar" }));

    await waitFor(() =>
      expect(setUnifiPortConfigMock).toHaveBeenCalledWith(switchItem.id, 5, {
        disabled: false,
      }),
    );
  });

  it("labels the button by the admin state, not the link", async () => {
    listSwitchesMock.mockResolvedValue({
      items: [switchItem],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    listSwitchPortsMock.mockResolvedValue({
      items: [], totalCount: 0, page: 1, pageSize: 100,
    });
    unifiPortsMock.mockResolvedValue([
      {
        portIndex: 7,
        name: "Port 7",
        enabled: false,
        linkUp: true,
        speed: null,
        fullDuplex: null,
        portVlan: null,
        poeEnable: null,
        poeStatus: null,
        media: null,
        clientMac: null,
      },
    ]);

    renderPage();

    expect(await screen.findByRole("button", { name: "Habilitar" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Desabilitar" })).toBeNull();
  });
});
