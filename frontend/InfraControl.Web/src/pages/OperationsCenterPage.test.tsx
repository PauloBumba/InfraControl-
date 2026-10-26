import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { OperationsCenterPage } from "./OperationsCenterPage";

vi.mock("../services/api/incidents", () => ({
  listIncidents: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100 }),
}));

vi.mock("../services/api/inventory", () => ({
  listDevices: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 1 }),
}));

vi.mock("../services/api/zabbix", () => ({
  getZabbixSummary: vi.fn().mockResolvedValue(null),
}));

vi.mock("../hooks", () => ({
  useAudio: () => ({
    isInitialized: false,
    isInitializing: false,
    config: { enabled: false },
    initialize: vi.fn(),
    announce: vi.fn(),
    updateConfig: vi.fn(),
  }),
}));

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <OperationsCenterPage />
    </QueryClientProvider>
  );
}

describe("OperationsCenterPage (NOC TV - 5 páginas)", () => {
  it("should present the NOC header and first page", () => {
    renderPage();

    expect(screen.getByText("INFRA CONTROLE")).toBeInTheDocument();
    expect(screen.getByText("CENTRAL DE OPERAÇÕES")).toBeInTheDocument();
    expect(screen.getByText(/1 \/ 5/)).toBeInTheDocument();
  });

  it("should expose audio and fullscreen controls", () => {
    renderPage();

    expect(screen.getByRole("button", { name: /ATIVAR ÁUDIO/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /TELA CHEIA/i })).toBeInTheDocument();
  });

  it("should render through NOC styles", () => {
    const { container } = renderPage();

    expect(container.querySelector(".noc-page")).not.toBeNull();
    expect(container.querySelector(".noc-header")).not.toBeNull();
    expect(container.querySelector(".noc-main")).not.toBeNull();
  });
});
