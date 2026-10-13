import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { HealthPage } from "./HealthPage";
import { listAlerts, type AlertItem } from "@/services/api/alerts";
import { getZabbixSummary, type ZabbixSummary } from "@/services/api/zabbix";
import { getUniFiDashboard, type UniFiDashboard } from "@/services/api/unifi";

/**
 * Fase 06 (Plano Mestre): HealthPage religada ao backend — os testes
 * travam (1) os cards de integração com dados reais, (2) o resumo de
 * alertas abertos e (3) o estado de erro com retry.
 */
vi.mock("@/services/api/alerts", () => ({ listAlerts: vi.fn() }));
vi.mock("@/services/api/zabbix", () => ({ getZabbixSummary: vi.fn() }));
vi.mock("@/services/api/unifi", () => ({ getUniFiDashboard: vi.fn() }));

const listAlertsMock = vi.mocked(listAlerts);
const getZabbixSummaryMock = vi.mocked(getZabbixSummary);
const getUniFiDashboardMock = vi.mocked(getUniFiDashboard);

const EMPTY_SUMMARY: ZabbixSummary = {
  totalHosts: 0, enabledHosts: 0, missingHosts: 0,
  hostsWithAsset: 0, hostsWithoutAsset: 0, problemsTotal: 0,
  problemsBySeverity: [0, 0, 0, 0, 0, 0], hostsWithProblems: 0,
  problemsAvailable: false, problemsError: null, isConfigured: false,
  detectedVersion: null, lastInventorySyncAtUtc: null,
};

const HEALTHY_SUMMARY: ZabbixSummary = {
  ...EMPTY_SUMMARY,
  totalHosts: 23, enabledHosts: 23, hostsWithAsset: 1,
  problemsAvailable: true, isConfigured: true, detectedVersion: "8.0.0",
  lastInventorySyncAtUtc: "2026-10-12T02:00:00Z",
};

const EMPTY_DASHBOARD: UniFiDashboard = {
  sites: 0, sitesMissing: 0, switches: 0, accessPoints: 0,
  deviceStates: {}, clients: 0, ports: null, unlinked: 0,
  lastSyncAtUtc: null, lastSyncSucceeded: null, recentSyncErrors: 0,
  controllerReachable: true, controllerError: null, stale: false,
};

const HEALTHY_DASHBOARD: UniFiDashboard = {
  ...EMPTY_DASHBOARD,
  sites: 3, switches: 5, accessPoints: 30, clients: 120,
  lastSyncAtUtc: "2026-10-12T01:30:00Z", lastSyncSucceeded: true,
  ports: { total: 48, up: 45 },
};

function makeAlert(overrides: Partial<AlertItem> = {}): AlertItem {
  return {
    id: "11111111-2222-3333-4444-555566667777",
    deviceId: "99999999-8888-7777-6666-555544443333",
    deviceName: "sw-core-01",
    check: "Ping",
    source: "Native",
    severity: "Critical",
    status: "Open",
    message: "Host sem resposta",
    openedAt: "2026-10-12T01:00:00Z",
    resolvedAt: null,
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  getZabbixSummaryMock.mockResolvedValue(HEALTHY_SUMMARY);
  getUniFiDashboardMock.mockResolvedValue(HEALTHY_DASHBOARD);
  listAlertsMock.mockResolvedValue({
    items: [makeAlert()],
    totalCount: 1,
    page: 1,
    pageSize: 10,
  });
});

function renderPage() {
  return render(
    <MemoryRouter>
      <HealthPage />
    </MemoryRouter>,
  );
}

describe("HealthPage — saúde real do sistema", () => {
  it("should show Zabbix and UniFi cards with real data", async () => {
    renderPage();

    expect(await screen.findByText("Zabbix")).toBeInTheDocument();
    expect(screen.getByText("UniFi")).toBeInTheDocument();
    expect(screen.getByText("Versão 8.0.0 · 23 hosts")).toBeInTheDocument();
    expect(screen.getByText("3 sites · 5 switches · 30 APs")).toBeInTheDocument();
    // Duas integrações saudáveis → dois chips "Saudável".
    expect(screen.getAllByText("Saudável")).toHaveLength(2);
  });

  it("should show open alerts summary and the critical chip", async () => {
    renderPage();

    expect(await screen.findByText("Host sem resposta")).toBeInTheDocument();
    expect(screen.getByText("1 alerta(s) · 1 crítico(s)")).toBeInTheDocument();
    expect(screen.getByText("1 crítico(s)")).toBeInTheDocument();
  });

  it("should show warning state when an integration is not configured", async () => {
    getZabbixSummaryMock.mockResolvedValue(EMPTY_SUMMARY);
    getUniFiDashboardMock.mockResolvedValue({
      ...EMPTY_DASHBOARD,
      controllerReachable: false,
      controllerError: "timeout",
    });

    renderPage();

    expect(await screen.findByText("Zabbix")).toBeInTheDocument();
    expect(screen.getByText("Não configurado")).toBeInTheDocument();
    expect(screen.getByText(/Controller inacessível/)).toBeInTheDocument();
    expect(screen.getAllByText("Atenção").length).toBeGreaterThanOrEqual(2);
  });

  it("should show the error banner and retry on failure", async () => {
    getZabbixSummaryMock
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValueOnce(HEALTHY_SUMMARY);

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Não foi possível carregar a saúde das integrações.",
    );
    expect(getZabbixSummaryMock).toHaveBeenCalledTimes(1);
  });

  it("should show zero-alert state when nothing is open", async () => {
    listAlertsMock.mockResolvedValue({
      items: [], totalCount: 0, page: 1, pageSize: 10,
    });

    renderPage();

    expect(await screen.findByText("Nenhum alerta aberto.")).toBeInTheDocument();
    expect(screen.getByText("Normal")).toBeInTheDocument();
  });
});
