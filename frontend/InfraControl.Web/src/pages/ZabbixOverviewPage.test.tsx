import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ZabbixOverviewPage } from "./ZabbixOverviewPage";
import { getZabbixProblems, getZabbixSummary, type ZabbixSummary } from "@/services/api/zabbix";

vi.mock("@/services/api/zabbix", () => ({
  getZabbixSummary: vi.fn(),
  getZabbixProblems: vi.fn(),
}));

const summaryMock = vi.mocked(getZabbixSummary);
const problemsMock = vi.mocked(getZabbixProblems);

const baseSummary: ZabbixSummary = {
  totalHosts: 12,
  enabledHosts: 12,
  missingHosts: 2,
  hostsWithAsset: 8,
  hostsWithoutAsset: 4,
  problemsTotal: 0,
  problemsBySeverity: [0, 0, 0, 0, 0, 0],
  hostsWithProblems: 0,
  // O caso da divergência: a consulta de problemas FALHOU, mas a integração
  // está configurada. "Falha" e "sem problemas" são estados diferentes.
  problemsAvailable: false,
  problemsError: "A API do Zabbix não respondeu.",
  isConfigured: true,
  detectedVersion: "7.0.0",
  lastInventorySyncAtUtc: null,
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ZabbixOverviewPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/**
 * F29-T004 — tom errado. A página já separava com precisão o estado
 * `problemsAvailable === false` (falha da consulta) de "sem problemas"
 * (contagem 0) — mas EXIBIA a falha com `NoticeBanner`, o banner verde de
 * sucesso, com ícone de ✅. Quem lê a tela vê "sucesso" no lugar de erro, e
 * `role="status"` (polite) em vez de `role="alert"` (assertive).
 *
 * O segundo teste tranca que a separação de estados NÃO foi perdida na
 * correção: a falha não pode voltar a virar contagem 0 nem sumir.
 */
describe("ZabbixOverviewPage — tom do erro (F29-T004)", () => {
  beforeEach(() => {
    summaryMock.mockReset();
    problemsMock.mockReset();
    problemsMock.mockResolvedValue({ items: [], total: 0 });
  });

  it("should show a failed problems query as an alert, not as a green success notice", async () => {
    summaryMock.mockResolvedValue(baseSummary);

    renderPage();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("A API do Zabbix não respondeu.");

    // A MESMA mensagem não pode estar num banner verde de sucesso.
    const notices = screen.queryAllByRole("status");
    expect(
      notices.some((node) => node.textContent?.includes("A API do Zabbix não respondeu.")),
    ).toBe(false);
  });

  it("should keep failure and 'no problems' as distinct states", async () => {
    summaryMock.mockResolvedValue({
      ...baseSummary,
      problemsAvailable: true,
      problemsError: null,
      problemsTotal: 0,
    });

    renderPage();

    // Sucesso com zero problemas NÃO vira erro: espera a página carregar e
    // confirma que nenhum alert foi emitido.
    await screen.findByText("Visão geral");
    expect(screen.queryByRole("alert")).toBeNull();
  });
});
