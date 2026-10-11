import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ClientsPage } from "./ClientsPage";
import { listClients, type ClientItem } from "@/services/api/clients";

/**
 * Adoção do padrão de visões (backlog Operations UX, pedido 2026-10-11:
 * "índice tem que permitir o usuário escolher tabela/grid/quadro").
 *
 * Antes a ClientsPage era SÓ tabela. O `EntityView`/`ViewSwitcher` já eram
 * componentes reutilizáveis (10 páginas usavam) — aqui ela passa a
 * expor os 3 modos, agrupando o quadro por `kind` (origem do achado:
 * ARP/DHCP/MAC/Vizinho/Host).
 *
 * O que se tranca aqui: a tabela continua sendo o modo padrão (nada muda
 * para quem já usava a página) e o quadro renderiza TODAS as colunas de
 * tipo, inclusive as sem nenhum item.
 */
vi.mock("@/services/api/clients", () => ({
  listClients: vi.fn(),
}));

const listClientsMock = vi.mocked(listClients);

function makeClient(overrides: Partial<ClientItem> = {}): ClientItem {
  return {
    ip: "192.168.1.10",
    mac: "aa:bb:cc:dd:ee:01",
    hostname: "notebook-ana",
    vlan: "10",
    localIf: "eth0",
    kind: "ArpEntry",
    lastSeenAt: "2026-10-11T10:00:00Z",
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  // a preferência de visão vive no localStorage — zera p/ default determinístico
  localStorage.clear();
  listClientsMock.mockResolvedValue({
    items: [
      makeClient(),
      makeClient({
        ip: "192.168.1.20",
        mac: "aa:bb:cc:dd:ee:02",
        hostname: "servidor-web",
        kind: "DhcpLease",
      }),
    ],
    totalCount: 2,
    page: 1,
    pageSize: 20,
  });
});

describe("ClientsPage — visões tabela/grid/quadro", () => {
  it("should keep the table as the default view", async () => {
    const { container } = render(<ClientsPage />);

    expect(await screen.findByText("notebook-ana")).toBeInTheDocument();
    expect(container.querySelector("table")).not.toBeNull();
  });

  it("should switch to a grid of cards", async () => {
    const { container } = render(<ClientsPage />);
    const user = userEvent.setup();
    await screen.findByText("notebook-ana");

    await user.click(screen.getByRole("tab", { name: "Grid" }));

    expect(container.querySelector("table")).toBeNull();
    expect(screen.getAllByText("192.168.1.10").length).toBeGreaterThan(0);
    expect(screen.getByText("servidor-web")).toBeInTheDocument();
  });

  it("should switch to a kanban grouped by kind, with every kind column", async () => {
    const { container } = render(<ClientsPage />);
    const user = userEvent.setup();
    await screen.findByText("notebook-ana");

    await user.click(screen.getByRole("tab", { name: "Kanban" }));

    expect(container.querySelector("table")).toBeNull();
    // "Vizinho" (Neighbor) não tem nenhum item no fixture — só o quadro
    // renderiza as 5 colunas de tipo, vazias ou não.
    expect(screen.getByText("Vizinho")).toBeInTheDocument();
    // "ARP" existe na coluna do quadro E no chip do card.
    expect(screen.getAllByText("ARP").length).toBeGreaterThan(0);
  });

  it("should persist the chosen view across remounts", async () => {
    const first = render(<ClientsPage />);
    const user = userEvent.setup();
    await screen.findByText("notebook-ana");
    await user.click(screen.getByRole("tab", { name: "Kanban" }));
    first.unmount();

    const second = render(<ClientsPage />);
    await screen.findByText("Vizinho");
    expect(second.container.querySelector("table")).toBeNull();
  });
});
