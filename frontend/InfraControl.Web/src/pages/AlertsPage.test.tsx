import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AlertsPage } from "./AlertsPage";
import { listAlerts, type AlertItem } from "@/services/api/alerts";

/**
 * F29-T001B — piloto da `ListPageShell`.
 *
 * Esta página NÃO tinha nenhum teste, e a migração mexe em três coisas que
 * quebrariam em silêncio: o subtítulo de contagem, o bloco de erro com retry e
 * o seletor de status (que manda o filtro para a API). Os três ficam travados
 * aqui.
 */
vi.mock("@/services/api/alerts", () => ({
  listAlerts: vi.fn(),
}));

const listAlertsMock = vi.mocked(listAlerts);

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
    openedAt: "2026-10-11T10:00:00Z",
    resolvedAt: null,
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  listAlertsMock.mockResolvedValue({
    items: [makeAlert()],
    totalCount: 1,
    page: 1,
    pageSize: 20,
  });
});

describe("AlertsPage — casca de índice", () => {
  it("should show the count subtitle in the singular", async () => {
    render(<AlertsPage />);

    expect(await screen.findByText("Host sem resposta")).toBeInTheDocument();
    expect(screen.getByText("1 alerta")).toBeInTheDocument();
  });

  it("should show the error as an alert and retry the same query", async () => {
    listAlertsMock
      .mockRejectedValueOnce(new Error("Não foi possível falar com o servidor. Tente novamente."))
      .mockResolvedValueOnce({
        items: [makeAlert()],
        totalCount: 1,
        page: 1,
        pageSize: 20,
      });

    render(<AlertsPage />);
    const user = userEvent.setup();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Não foi possível falar com o servidor. Tente novamente.",
    );
    expect(listAlertsMock).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: "Tentar de novo" }));

    expect(listAlertsMock).toHaveBeenCalledTimes(2);
    expect(await screen.findByText("1 alerta")).toBeInTheDocument();
  });

  it("should send the chosen status filter to the API", async () => {
    render(<AlertsPage />);
    const user = userEvent.setup();
    await screen.findByText("Host sem resposta");

    await user.click(screen.getByRole("button", { name: "Abertos" }));

    expect(listAlertsMock).toHaveBeenLastCalledWith({
      status: "open",
      page: 1,
      pageSize: 20,
    });
  });
});
