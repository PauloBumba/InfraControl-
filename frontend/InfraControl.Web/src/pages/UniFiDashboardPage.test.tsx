import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { UniFiDashboardPage } from "./UniFiDashboardPage";
import { getUniFiDashboard, type UniFiDashboard } from "@/services/api/unifi";

vi.mock("@/services/api/unifi", () => ({ getUniFiDashboard: vi.fn() }));

const getMock = vi.mocked(getUniFiDashboard);

const DASHBOARD: UniFiDashboard = {
  sites: 2,
  sitesMissing: 0,
  switches: 3,
  accessPoints: 12,
  deviceStates: { up: 14, offline: 1 },
  clients: 40,
  ports: { total: 48, up: 45 },
  unlinked: 1,
  lastSyncAtUtc: "2026-11-09T01:00:00Z",
  lastSyncSucceeded: true,
  recentSyncErrors: 0,
  controllerReachable: true,
  controllerError: null,
  stale: false,
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <UniFiDashboardPage />
    </QueryClientProvider>,
  );
}

describe("UniFiDashboardPage — padrão da casa", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getMock.mockResolvedValue(DASHBOARD);
  });

  it("should render PageHeader with the house title", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Dashboard UniFi" }),
    ).toBeInTheDocument();
    expect(getMock).toHaveBeenCalledTimes(1);
  });

  it("should use uni-panel stat cards without raw ProgressSpinner", async () => {
    const { container } = renderPage();

    await screen.findByRole("heading", { name: "Dashboard UniFi" });

    expect(container.querySelector(".p-progress-spinner")).toBeNull();
    expect(container.querySelector(".uni-panel")).not.toBeNull();
    expect(screen.getByText("Sites (persistido)")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("should show a NoticeBanner when data is stale", async () => {
    getMock.mockResolvedValue({ ...DASHBOARD, stale: true });
    renderPage();

    expect(await screen.findByRole("status")).toBeInTheDocument();
    expect(screen.getByText(/desatualizados/i)).toBeInTheDocument();
  });

  it("should show ErrorBanner when the controller is down", async () => {
    getMock.mockResolvedValue({
      ...DASHBOARD,
      controllerReachable: false,
      controllerError: "timeout",
    });
    renderPage();

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getAllByText(/indisponível/i).length).toBeGreaterThan(0);
  });
});
