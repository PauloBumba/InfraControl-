import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ZabbixSettingsPage } from "./ZabbixSettingsPage";
import {
  getZabbixConfig,
  getZabbixSummary,
  saveZabbixConfig,
  syncZabbixInventory,
  testZabbixConnection,
  type ZabbixSettings,
  type ZabbixSummary,
} from "@/services/api/zabbix";

vi.mock("@/services/api/zabbix", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/zabbix")>();
  return {
    ...actual,
    getZabbixConfig: vi.fn(),
    getZabbixSummary: vi.fn(),
    saveZabbixConfig: vi.fn(),
    syncZabbixInventory: vi.fn(),
    testZabbixConnection: vi.fn(),
  };
});

const configMock = vi.mocked(getZabbixConfig);
const summaryMock = vi.mocked(getZabbixSummary);
const syncMock = vi.mocked(syncZabbixInventory);

const SETTINGS: ZabbixSettings = {
  apiUrl: "http://zabbix:8086/api_jsonrpc.php",
  username: "Admin",
  passwordSet: true,
  source: "database",
  detectedVersion: "8.0.0",
  lastCommunicationAtUtc: "2026-11-10T10:00:00Z",
  lastInventorySyncAtUtc: "2026-11-10T09:00:00Z",
  lastTestedAtUtc: "2026-11-10T10:00:00Z",
  lastTestOk: true,
  lastTestError: null,
  updatedAtUtc: "2026-11-10T08:00:00Z",
};

const SUMMARY: ZabbixSummary = {
  totalHosts: 215,
  enabledHosts: 210,
  missingHosts: 5,
  hostsWithAsset: 100,
  hostsWithoutAsset: 115,
  problemsTotal: 7,
  problemsBySeverity: [0, 1, 2, 3, 1, 0],
  hostsWithProblems: 6,
  problemsAvailable: true,
  problemsError: null,
  isConfigured: true,
  detectedVersion: "8.0.0",
  lastInventorySyncAtUtc: "2026-11-10T09:00:00Z",
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ZabbixSettingsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("ZabbixSettingsPage — integração completa", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    configMock.mockResolvedValue(SETTINGS);
    summaryMock.mockResolvedValue(SUMMARY);
    vi.mocked(saveZabbixConfig).mockResolvedValue(SETTINGS);
    vi.mocked(testZabbixConnection).mockResolvedValue({
      ok: true,
      version: "8.0.0",
      error: null,
    });
    syncMock.mockResolvedValue({
      created: 4,
      updated: 211,
      missing: 0,
      errors: 0,
      version: "8.0.0",
    });
  });

  it("should render config, state and inventory summary sections", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Zabbix" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Conexão com a API")).toBeInTheDocument();
    expect(screen.getByText("Estado")).toBeInTheDocument();
    expect(screen.getByText("Inventário sincronizado")).toBeInTheDocument();
    expect(await screen.findByText("215")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
  });

  it("should run inventory sync and show the result banner", async () => {
    const user = userEvent.setup();
    renderPage();

    const button = await screen.findByRole("button", {
      name: "Sincronizar inventário",
    });
    await user.click(button);

    await waitFor(() => expect(syncMock).toHaveBeenCalledTimes(1));
    expect(
      await screen.findByText(/Sincronização concluída · criados 4/),
    ).toBeInTheDocument();
  });

  it("should show sync failure without hiding the config form", async () => {
    syncMock.mockRejectedValue(new Error("Zabbix indisponível"));
    const user = userEvent.setup();
    renderPage();

    const button = await screen.findByRole("button", {
      name: "Sincronizar inventário",
    });
    await user.click(button);

    expect(
      await screen.findByText(/Falha na sincronização: Zabbix indisponível/),
    ).toBeInTheDocument();
    expect(screen.getByText("Conexão com a API")).toBeInTheDocument();
  });
});
