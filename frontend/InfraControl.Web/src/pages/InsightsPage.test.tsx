import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { InsightsPage } from "./InsightsPage";
import { getUnifiInsights, type UnifiInsights } from "@/services/api/integrations";

/**
 * Adoção do padrão de visões (backlog Operations UX, pedido 2026-10-11).
 *
 * A InsightsPage não é um índice: são 2 painéis de métrica distintos.
 * Cada painel ganha seu PRÓPRIO seletor no cabeçalho, com preferência
 * própria no localStorage — um seletor global não teria sobre o que agir.
 *
 * Agrupamentos: DPI → `category` (campo real, colunas saem dos dados);
 * Tráfego por Cliente → tercil de `totalBytes` calculado sobre o conjunto
 * carregado (sem constante mágica inventada).
 *
 * O painel "Histórico de Speed Test" continua com estado vazio honesto e
 * sem seletor (não há índice ali).
 */
vi.mock("@/services/api/integrations", () => ({
  getUnifiInsights: vi.fn(),
  getUnifiWifi: vi.fn(),
}));

const getUnifiInsightsMock = vi.mocked(getUnifiInsights);

const insights: UnifiInsights = {
  capturedAtUtc: "2026-10-11T10:00:00Z",
  dpiPerApplication: [
    { app: "YouTube", category: "Video", totalBytes: 900_000_000 },
    { app: "WhatsApp", category: "Mensagens", totalBytes: 100_000_000 },
    { app: "Teams", category: "Video", totalBytes: 500_000_000 },
  ],
  trafficPerClient: [
    {
      mac: "aa:bb:cc:00:00:01",
      hostname: "notebook-ana",
      ip: "192.168.10.5",
      rxBytes: 30,
      txBytes: 70,
      lastSeen: "2026-10-11T09:55:00Z",
      totalBytes: 100,
    },
    {
      mac: "aa:bb:cc:00:00:02",
      hostname: "servidor-web",
      ip: "192.168.10.8",
      rxBytes: 500,
      txBytes: 500,
      lastSeen: null,
      totalBytes: 1_000,
    },
    {
      mac: "aa:bb:cc:00:00:03",
      hostname: "ipad-sala",
      ip: "192.168.10.9",
      rxBytes: 5,
      txBytes: 5,
      lastSeen: null,
      totalBytes: 10,
    },
  ],
};

/** Cabeçalho do painel — é onde o seletor daquela seção mora. */
function panelHeader(title: string): HTMLElement {
  const header = screen.getByText(title).closest(".uni-panel-header");
  if (!header) throw new Error(`cabeçalho do painel "${title}" não encontrado`);
  return header as HTMLElement;
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <InsightsPage />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  // a preferência de visão vive no localStorage — zera p/ default determinístico
  localStorage.clear();
  getUnifiInsightsMock.mockResolvedValue(insights);
});

describe("InsightsPage — seletor por painel (tabela/grid/quadro)", () => {
  it("should keep both panels as tables by default", async () => {
    const { container } = renderPage();

    expect(await screen.findByText("YouTube")).toBeInTheDocument();
    expect(container.querySelectorAll("table")).toHaveLength(2);
  });

  it("should group the DPI panel by category in kanban", async () => {
    const user = userEvent.setup();
    const { container } = renderPage();
    await screen.findByText("YouTube");

    await user.click(within(panelHeader("Aplicações por Tráfego (DPI)")).getByRole("tab", { name: "Kanban" }));

    // "Mensagens" tem 1 app; "Video" tem 2 — e o rótulo também está no chip
    // do card, por isso All.
    expect(screen.getAllByText("Mensagens").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Video").length).toBeGreaterThan(0);
    expect(container.querySelectorAll("table")).toHaveLength(1);
  });

  it("should switch only the traffic panel to a grid, leaving DPI as a table", async () => {
    const user = userEvent.setup();
    const { container } = renderPage();
    await screen.findByText("YouTube");

    await user.click(within(panelHeader("Tráfego por Cliente")).getByRole("tab", { name: "Grid" }));

    expect(container.querySelectorAll("table")).toHaveLength(1);
    expect(screen.getByText("notebook-ana")).toBeInTheDocument();
    expect(screen.getByText("servidor-web")).toBeInTheDocument();
  });

  it("should group the traffic panel by traffic tercil in kanban", async () => {
    const user = userEvent.setup();
    const { container } = renderPage();
    await screen.findByText("YouTube");

    await user.click(within(panelHeader("Tráfego por Cliente")).getByRole("tab", { name: "Kanban" }));

    // as 3 faixas do tercil, inclusive a vazia de cabeçalho…
    expect(screen.getAllByText("Maior tráfego").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Tráfego intermediário").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Menor tráfego").length).toBeGreaterThan(0);
    // …e os 3 clientes distribuídos, um por faixa.
    expect(screen.getByText("ipad-sala")).toBeInTheDocument();
    expect(screen.getByText("notebook-ana")).toBeInTheDocument();
    expect(screen.getByText("servidor-web")).toBeInTheDocument();
    // só o painel de tráfego mudou: o DPI continua em tabela.
    expect(container.querySelectorAll("table")).toHaveLength(1);
  });
});
