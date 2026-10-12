import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { TopologyPage } from "./TopologyPage";
import { listDevices, listLocations } from "@/services/api/inventory";
import { getTopology, listVlans } from "@/services/api/network";

vi.mock("@/services/api/inventory", () => ({
  listDevices: vi.fn(),
  listLocations: vi.fn(),
}));

vi.mock("@/services/api/network", () => ({
  getTopology: vi.fn(),
  listVlans: vi.fn(),
}));

const topologyMock = vi.mocked(getTopology);

/**
 * F29-T004 — o erro INICIAL virava estado vazio. `if (error && !graph)`
 * devolvia um `uni-page-empty` com a mensagem dentro de um `<h2>` e um botão:
 * a tela dizia "não há dados aqui" quando na verdade a REQUISIÇÃO falhou. Os
 * dois estados pedem ações diferentes — um é "ajuste o filtro", o outro é
 * "tente de novo".
 *
 * O que se tranca: a falha aparece com semântica de alerta (`role="alert"`)
 * E a página mantém o cabeçalho (não vira estado vazio).
 */
describe("TopologyPage — tom do erro (F29-T004)", () => {
  beforeEach(() => {
    topologyMock.mockReset();
    vi.mocked(listDevices).mockReset();
    vi.mocked(listLocations).mockReset();
    vi.mocked(listVlans).mockReset();
    localStorage.clear();
  });

  it("should announce an initial load failure as an alert, not as an empty state", async () => {
    topologyMock.mockRejectedValue(new Error("Não foi possível falar com o servidor."));
    vi.mocked(listDevices).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(listLocations).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(listVlans).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });

    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Não foi possível falar com o servidor.");

    // Continua sendo a página de Topologia, com cabeçalho — não um vazio.
    expect(screen.getByText("Topologia de Rede")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /tentar de novo/i })).toBeInTheDocument();
  });
});
