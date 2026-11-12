import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { ClusterPage } from "./ClusterPage";
import {
  checkAllInstances,
  getClusterInsights,
  getClusterOverview,
  getComposeServices,
  getDockerContainers,
  getDockerLogsInsight,
  getDockerTopology,
  getTrafficMetrics,
  type ClusterOverview,
  type ComposeServiceItem,
  type DockerContainersResult,
  type DockerTopologyResult,
  type TrafficMetrics,
} from "@/services/api/accessPolicies";

vi.mock("@/services/api/accessPolicies", () => ({
  checkAllInstances: vi.fn(),
  checkServiceInstance: vi.fn(),
  deleteServiceInstance: vi.fn(),
  dockerContainerAction: vi.fn(),
  getClusterInsights: vi.fn(),
  getClusterOverview: vi.fn(),
  getComposeServices: vi.fn(),
  getDockerContainerLogs: vi.fn(),
  getDockerContainers: vi.fn(),
  getDockerLogsInsight: vi.fn(),
  getDockerTopology: vi.fn(),
  getTrafficMetrics: vi.fn(),
  removeDockerContainer: vi.fn(),
  scaleComposeService: vi.fn(),
  updateDockerResources: vi.fn(),
  upsertServiceInstance: vi.fn(),
}));

const overviewMock = vi.mocked(getClusterOverview);
const containersMock = vi.mocked(getDockerContainers);
const topologyMock = vi.mocked(getDockerTopology);
const trafficMock = vi.mocked(getTrafficMetrics);
const composeMock = vi.mocked(getComposeServices);
const insightsMock = vi.mocked(getClusterInsights);
const logsInsightMock = vi.mocked(getDockerLogsInsight);
const checkAllMock = vi.mocked(checkAllInstances);

const OVERVIEW: ClusterOverview = {
  instances: [
    {
      id: "11111111-2222-3333-4444-555566667777",
      kind: "api",
      name: "API réplica 2",
      host: "api-2",
      port: 8080,
      healthUrl: "http://api-2:8080/health/live",
      weight: 1,
      isActive: true,
      notes: null,
      lastStatus: "up",
      lastCheckedAt: "2026-11-09T01:00:00Z",
    },
  ],
  distribution: [],
  upCount: 1,
  downCount: 0,
  unknownCount: 0,
};

const CONTAINERS: DockerContainersResult = {
  available: true,
  containers: [
    {
      id: "abc123def456",
      name: "infracontrol-api-1",
      image: "infracontrol/api:dev",
      state: "running",
      status: "Up 2 hours",
      healthStatus: "healthy",
      cpuPercent: 1.5,
      memoryUsageBytes: 1024 * 1024 * 120,
      startedAt: new Date(Date.now() - 7200000).toISOString(),
      restartCount: 0,
    },
  ],
};

const TOPOLOGY: DockerTopologyResult = {
  available: true,
  topology: {
    containers: [],
    networks: [{ name: "infracontrol_default", containers: ["infracontrol-api-1"] }],
  },
};

const TRAFFIC: TrafficMetrics = {
  windowMinutes: 60,
  totalRequests: 120,
  totalErrors: 2,
  errorRatePercent: 1.7,
  avgLatencyMs: 42,
  p50Ms: 30,
  p95Ms: 90,
  p99Ms: 150,
  requestsPerMinute: 2,
  routes: [
    {
      route: "GET /api/v1/health",
      requests: 100,
      errors: 0,
      errorRatePercent: 0,
      avgLatencyMs: 12,
      p50Ms: 10,
      p95Ms: 20,
      p99Ms: 30,
    },
  ],
  timeline: [],
  instances: [],
};

const COMPOSE: ComposeServiceItem[] = [
  {
    service: "api",
    total: 2,
    running: 2,
    containers: [
      { name: "infracontrol-api-1", state: "running", status: "Up" },
      { name: "infracontrol-api-2", state: "running", status: "Up" },
    ],
  },
];

beforeEach(() => {
  vi.clearAllMocks();
  overviewMock.mockResolvedValue(OVERVIEW);
  containersMock.mockResolvedValue(CONTAINERS);
  topologyMock.mockResolvedValue(TOPOLOGY);
  trafficMock.mockResolvedValue(TRAFFIC);
  composeMock.mockResolvedValue(COMPOSE);
  insightsMock.mockResolvedValue({
    analysis: "Tudo operacional.",
    provider: "ollama",
    model: "llama3",
    usedFallback: false,
    durationMs: 850,
  });
  logsInsightMock.mockResolvedValue({
    analysis: "Sem erros nos logs.",
    provider: "ollama",
    model: "llama3",
    usedFallback: false,
    durationMs: 850,
  });
  checkAllMock.mockResolvedValue(OVERVIEW);
});

function renderPage() {
  return render(<ClusterPage />);
}

describe("ClusterPage — padrão da casa", () => {
  it("should render PageHeader with the house title", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Administração do cluster" }),
    ).toBeInTheDocument();
    expect(overviewMock).toHaveBeenCalledTimes(1);
    expect(containersMock).toHaveBeenCalledTimes(1);
  });

  it("should not use raw ProgressSpinner, Card or Tailwind page root", async () => {
    const { container } = renderPage();

    await screen.findByRole("heading", { name: "Administração do cluster" });

    expect(container.querySelector(".p-progress-spinner")).toBeNull();
    expect(container.querySelector(".p-card")).toBeNull();
    expect(container.querySelector(".p-button")).toBeNull();
    expect(
      container.querySelector('[class*="space-y-4"]'),
    ).toBeNull();
    expect(container.querySelector(".uni-panel")).not.toBeNull();
    expect(container.querySelector('[class*="text-2xl"]')).toBeNull();
  });

  it("should show containers and instances in uni-panels without Message/Tag", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Administração do cluster" }),
    ).toBeInTheDocument();
    expect(await screen.findByText("infracontrol-api-1")).toBeInTheDocument();
    expect(await screen.findByText("API réplica 2")).toBeInTheDocument();
    expect(await screen.findByText("Em execução")).toBeInTheDocument();
    expect(screen.getAllByText("Online").length).toBeGreaterThan(0);
    expect(containerNoSpinner()).toBeNull();
  });

  it("should open the house Dialog for a new replica", async () => {
    renderPage();
    const user = (await import("@testing-library/user-event")).default.setup();

    await screen.findByRole("heading", { name: "Administração do cluster" });
    await user.click(screen.getByRole("button", { name: /Nova réplica/ }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toBeInTheDocument();
    expect(screen.getByLabelText(/Papel/)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Nome/)).toBeInTheDocument();
  });
});

function containerNoSpinner(): HTMLElement | null {
  return document.querySelector(".p-progress-spinner");
}

// silence unused in case of tree-shake of waitFor
void waitFor;
