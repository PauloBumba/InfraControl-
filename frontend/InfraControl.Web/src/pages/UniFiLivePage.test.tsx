import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { UniFiLivePage } from "./UniFiLivePage";
import {
  getUnifiLiveClients,
  getUnifiLivePorts,
  getUnifiNetworks,
  getUnifiTopology,
} from "@/services/api/unifi";
import { listDevices } from "@/services/api/inventory";

vi.mock("@/services/api/unifi", () => ({
  getUnifiLiveClients: vi.fn(),
  getUnifiLivePorts: vi.fn(),
  getUnifiNetworks: vi.fn(),
  getUnifiTopology: vi.fn(),
}));

vi.mock("@/services/api/inventory", () => ({ listDevices: vi.fn() }));

const clientsMock = vi.mocked(getUnifiLiveClients);
const networksMock = vi.mocked(getUnifiNetworks);
const topologyMock = vi.mocked(getUnifiTopology);
const portsMock = vi.mocked(getUnifiLivePorts);
const devicesMock = vi.mocked(listDevices);

beforeEach(() => {
  vi.clearAllMocks();
  clientsMock.mockResolvedValue([
    {
      mac: "aa:bb:cc:dd:ee:01",
      ip: "10.0.0.10",
      hostname: "notebook-dev",
      essid: "CorpWiFi",
      networkName: null,
      signal: -55,
      isWired: false,
      apMac: "11:22:33:44:55:66",
      switchMac: null,
      lastSeenUnix: 1760000000,
      radio: "na",
    },
  ]);
  networksMock.mockResolvedValue([
    { id: "n1", siteId: "default", name: "Corp", purpose: "corporate", vlanId: 10, enabled: true },
  ]);
  topologyMock.mockResolvedValue({ nodes: [], links: [] });
  portsMock.mockResolvedValue([]);
  devicesMock.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100 });
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <UniFiLivePage />
    </QueryClientProvider>,
  );
}

describe("UniFiLivePage — padrão da casa", () => {
  it("should render PageHeader with the house title", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { name: "UniFi ao vivo" })).toBeInTheDocument();
    expect(clientsMock).toHaveBeenCalledTimes(1);
  });

  it("should list clients and networks without raw ProgressSpinner", async () => {
    const { container } = renderPage();

    await screen.findByRole("heading", { name: "UniFi ao vivo" });

    expect(container.querySelector(".p-progress-spinner")).toBeNull();
    expect(await screen.findByText("aa:bb:cc:dd:ee:01")).toBeInTheDocument();
    expect(screen.getAllByText("Corp").length).toBeGreaterThan(0);
    expect(screen.getByText(/AP 11:22:33:44:55:66/)).toBeInTheDocument();
    expect(screen.getByText("habilitada")).toBeInTheDocument();
  });

  it("should expose the house SearchInput in the header", async () => {
    renderPage();

    await screen.findByRole("heading", { name: "UniFi ao vivo" });
    expect(screen.getByPlaceholderText("mac, ip, hostname, ssid")).toBeInTheDocument();
  });
});
