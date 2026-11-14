import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import {
  EventsCenter,
  FleetStatusStrip,
  PerformanceSection,
} from "./DashboardInsights";
import { getFleetStatusOverview } from "@/services/api/inventory";
import { getMonitoringAvailability } from "@/services/api/monitoring";
import {
  getZabbixHosts,
  getZabbixProblems,
} from "@/services/api/zabbix";
import { listAlerts } from "@/services/api/alerts";

vi.mock("@/services/api/inventory", () => ({
  getFleetStatusOverview: vi.fn(),
  listDevices: vi.fn(),
}));
vi.mock("@/services/api/monitoring", () => ({
  getMonitoringAvailability: vi.fn(),
}));
vi.mock("@/services/api/unifi", () => ({
  getUnifiTopology: vi.fn(),
  getUniFiDashboard: vi.fn(),
}));
vi.mock("@/services/api/zabbix", () => ({
  getZabbixHostItems: vi.fn(),
  getZabbixHostSeries: vi.fn(),
  getZabbixHosts: vi.fn(),
  getZabbixProblems: vi.fn(),
}));
vi.mock("@/services/api/alerts", () => ({ listAlerts: vi.fn() }));
vi.mock("@/components/ui/MetricChart", () => ({
  MetricChart: ({ ariaLabel }: { ariaLabel?: string }) => (
    <div data-testid="metric-chart" aria-label={ariaLabel} />
  ),
}));
vi.mock("@/components/ui/UniFiTopologyMap", () => ({
  UniFiTopologyMap: () => <div data-testid="topology-map" />,
}));

function renderWithRouter(ui: React.ReactElement) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

describe("FleetStatusStrip (F54-T002)", () => {
  beforeEach(() => {
    vi.mocked(getFleetStatusOverview).mockReset();
  });

  it("renders real fleet totals and status breakdown", async () => {
    vi.mocked(getFleetStatusOverview).mockResolvedValue({
      totalDevices: 12,
      byType: [{ type: "Switch", count: 8 }, { type: "Server", count: 4 }],
      byStatus: [
        { status: "Available", count: 9 },
        { status: "Degraded", count: 2 },
        { status: "Unavailable", count: 1 },
        { status: "Unknown", count: 0 },
      ],
      openAlerts: 5,
      openCriticalAlerts: 1,
      openWarningAlerts: 4,
    });

    renderWithRouter(<FleetStatusStrip />);

    expect(await screen.findByText("12")).toBeInTheDocument();
    expect(screen.getByText("Disponíveis")).toBeInTheDocument();
    expect(screen.getByText("Indisponíveis")).toBeInTheDocument();
    expect(screen.getByText(/5/)).toBeInTheDocument();
    expect(screen.getByText(/8 Switch · 4 Server/)).toBeInTheDocument();
  });

  it("renders nothing when the endpoint fails (fail-soft)", async () => {
    vi.mocked(getFleetStatusOverview).mockRejectedValue(new Error("down"));
    const { container } = renderWithRouter(<FleetStatusStrip />);
    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });
});

describe("PerformanceSection (F54-T003)", () => {
  beforeEach(() => {
    vi.mocked(getMonitoringAvailability).mockReset().mockResolvedValue({
      fromUtc: "2026-11-14T00:00:00Z",
      toUtc: "2026-11-15T00:00:00Z",
      bucketMinutes: 20,
      points: [
        { bucketStartUtc: "2026-11-14T10:00:00Z", total: 4, succeeded: 3, failed: 1 },
        { bucketStartUtc: "2026-11-14T10:20:00Z", total: 0, succeeded: 0, failed: 0 },
      ],
    });
    vi.mocked(getZabbixHosts).mockReset().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 50 });
  });

  it("offers the 1h/24h/7d/30d selector and refetches on change", async () => {
    const user = userEvent.setup();
    renderWithRouter(<PerformanceSection />);

    expect(await screen.findByText("1h")).toBeInTheDocument();
    expect(screen.getByText("30d")).toBeInTheDocument();
    const callsBefore = vi.mocked(getMonitoringAvailability).mock.calls.length;

    await user.click(screen.getByText("7d"));
    await waitFor(() =>
      expect(vi.mocked(getMonitoringAvailability).mock.calls.length).toBeGreaterThan(callsBefore),
    );
  });
});

describe("EventsCenter (F54-T006)", () => {
  beforeEach(() => {
    vi.mocked(listAlerts).mockReset();
    vi.mocked(getZabbixProblems).mockReset();
  });

  it("merges native alerts and Zabbix problems into one feed", async () => {
    vi.mocked(listAlerts).mockResolvedValue({
      items: [
        {
          id: "a1", deviceId: "d1", deviceName: "sw-core", check: "Ping",
          source: "Native", severity: "Critical", status: "Open",
          message: "host sem resposta", openedAt: "2026-11-14T10:00:00Z",
          resolvedAt: null, operatorNote: null, resolutionCode: null, resolutionNote: null,
        } as never,
      ],
      totalCount: 1, page: 1, pageSize: 10,
    });
    vi.mocked(getZabbixProblems).mockResolvedValue({
      items: [
        {
          eventId: "e1", name: "CPU alta", severity: 3, severityLabel: "Average",
          clockUtc: "2026-11-14T11:00:00Z", recovered: false, hostNames: ["srv-01"],
        },
      ],
      total: 1,
    });

    renderWithRouter(<EventsCenter />);

    expect(await screen.findByText("host sem resposta")).toBeInTheDocument();
    expect(screen.getByText("CPU alta")).toBeInTheDocument();
    expect(screen.getByText(/srv-01/)).toBeInTheDocument();
  });

  it("still renders when Zabbix is unavailable", async () => {
    vi.mocked(listAlerts).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    vi.mocked(getZabbixProblems).mockRejectedValue(new Error("not configured"));

    renderWithRouter(<EventsCenter />);
    expect(await screen.findByText("Nenhum evento recente.")).toBeInTheDocument();
  });
});
