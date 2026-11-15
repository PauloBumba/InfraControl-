import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { NetworkExplorerPage } from "./NetworkExplorerPage";
import { lookupMac, type MacLookupResult } from "@/services/api/network";

/**
 * Network Explorer F1 — página do MAC/IP Explorer. Tranca o contrato da
 * tela: busca chama `lookupMac`, o resumo mostra só o que o backend
 * devolveu (nunca inventa), evidências aparecem por origem e o estado
 * vazio explicita que ausência de descoberta ≠ offline.
 */
vi.mock("@/services/api/network", () => ({
  lookupMac: vi.fn(),
}));

const lookupMacMock = vi.mocked(lookupMac);

function makeResult(overrides: Partial<MacLookupResult> = {}): MacLookupResult {
  return {
    term: "aa:bb:cc:dd:ee:ff",
    matchedMac: "aa:bb:cc:dd:ee:ff",
    ip: "192.0.2.50",
    hostname: "estacao-01",
    vlan: "10",
    wifiClient: false,
    wifiApName: null,
    wifiSsid: null,
    wifiSignal: null,
    isStale: false,
    lastSeenAt: "2026-11-14T10:00:00Z",
    sightings: [
      {
        kind: "MacTableEntry",
        kindLabel: "Tabela MAC (SNMP)",
        deviceId: "dev-1",
        deviceName: "sw-core",
        deviceIp: "192.168.1.2",
        port: "Gi1/0/24",
        portDescription: null,
        mac: "aa:bb:cc:dd:ee:ff",
        ip: null,
        hostname: null,
        vlan: null,
        seenAt: "2026-11-14T10:00:00Z",
        ageDays: 0,
      },
      {
        kind: "DhcpLease",
        kindLabel: "DHCP",
        deviceId: null,
        deviceName: null,
        deviceIp: null,
        port: null,
        portDescription: null,
        mac: "aa:bb:cc:dd:ee:ff",
        ip: "192.0.2.50",
        hostname: "estacao-01",
        vlan: "10",
        seenAt: "2026-11-14T09:00:00Z",
        ageDays: 0,
      },
    ],
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <NetworkExplorerPage />
    </MemoryRouter>,
  );
}

async function searchFor(user: ReturnType<typeof userEvent.setup>, term: string) {
  await user.type(screen.getByPlaceholderText("MAC, IP, hostname ou VLAN"), term);
  await user.click(screen.getByRole("button", { name: "Pesquisar" }));
}

beforeEach(() => {
  vi.clearAllMocks();
  lookupMacMock.mockResolvedValue(makeResult());
});

describe("NetworkExplorerPage", () => {
  it("should show the initial hint before any search", () => {
    renderPage();
    expect(screen.getByText(/Pesquise por MAC, IP, hostname ou VLAN/)).toBeInTheDocument();
    expect(lookupMacMock).not.toHaveBeenCalled();
  });

  it("should show the summary and sightings after a MAC search", async () => {
    const user = userEvent.setup();
    renderPage();
    await searchFor(user, "aa:bb:cc:dd:ee:ff");

    expect(lookupMacMock).toHaveBeenCalledWith("aa:bb:cc:dd:ee:ff");
    expect((await screen.findAllByText("estacao-01")).length).toBeGreaterThan(0);
    expect(screen.getByText("sw-core")).toBeInTheDocument();
    expect(screen.getByText("Gi1/0/24")).toBeInTheDocument();
    expect(screen.getByText("Tabela MAC (SNMP)")).toBeInTheDocument();
    expect(screen.getByText("DHCP")).toBeInTheDocument();
  });

  it("should explain that no evidence does not mean offline", async () => {
    lookupMacMock.mockResolvedValue(makeResult({ sightings: [], matchedMac: null, ip: null, hostname: null, vlan: null, lastSeenAt: null }));
    const user = userEvent.setup();
    renderPage();
    await searchFor(user, "de:ad:be:ef:00:01");

    expect(await screen.findByText("Nenhuma evidência encontrada")).toBeInTheDocument();
    expect(screen.getByText(/não significa/i)).toBeInTheDocument();
  });

  it("should flag stale records", async () => {
    lookupMacMock.mockResolvedValue(makeResult({ isStale: true }));
    const user = userEvent.setup();
    renderPage();
    await searchFor(user, "aa:bb:cc:dd:ee:ff");

    expect(await screen.findByText("Registro antigo")).toBeInTheDocument();
  });

  it("should show the Wi-Fi AP chip when the client is wireless", async () => {
    lookupMacMock.mockResolvedValue(makeResult({ wifiClient: true, wifiApName: "AP-Patio", wifiSsid: "Escritorio-5G" }));
    const user = userEvent.setup();
    renderPage();
    await searchFor(user, "aa:bb:cc:dd:ee:ff");

    expect(await screen.findByText(/Wi-Fi · AP-Patio/)).toBeInTheDocument();
  });
});
