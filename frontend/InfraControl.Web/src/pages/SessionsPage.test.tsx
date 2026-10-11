import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { Provider } from "react-redux";
import { SessionsPage } from "./SessionsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listSessions, type SessionItem } from "@/services/api/sessions";

/**
 * Adoção do padrão de visões (backlog Operations UX, pedido 2026-10-11:
 * "índice = tabela → permitir o usuário escolher tabela/grid/quadro").
 *
 * A SessionsPage é um índice de uma tabela só; aqui ela ganha os 3 modos,
 * com o quadro agrupado por `isCurrent` — o mesmo recorte da coluna
 * "Sessão" ("Este dispositivo" × "Outro dispositivo"). Tabela = padrão.
 */
vi.mock("@/services/api/sessions", () => ({
  listSessions: vi.fn(),
  revokeSession: vi.fn(),
  revokeOtherSessions: vi.fn(),
}));

const listSessionsMock = vi.mocked(listSessions);

function makeSession(overrides: Partial<SessionItem> = {}): SessionItem {
  return {
    id: "sess-1111-2222-3333",
    createdAt: "2026-10-10T08:00:00Z",
    lastSeenAt: "2026-10-11T09:30:00Z",
    expiresAt: "2026-10-11T18:00:00Z",
    absoluteExpiresAt: "2026-10-17T08:00:00Z",
    isCurrent: true,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <Provider store={store}>
      <MemoryRouter>
        <SessionsPage />
      </MemoryRouter>
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  // a preferência de visão vive no localStorage — zera p/ default determinístico
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Viewer"],
    }),
  );
  listSessionsMock.mockResolvedValue([
    makeSession(),
    makeSession({
      id: "sess-9999-8888-7777",
      isCurrent: false,
      createdAt: "2026-10-09T14:00:00Z",
      lastSeenAt: "2026-10-10T11:00:00Z",
    }),
  ]);
});

describe("SessionsPage — visões tabela/grid/quadro", () => {
  it("should keep the table as the default view", async () => {
    const { container } = renderPage();

    expect(await screen.findByText("Este dispositivo")).toBeInTheDocument();
    expect(container.querySelector("table")).not.toBeNull();
  });

  it("should switch to a grid of cards", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();
    await screen.findByText("Este dispositivo");

    await user.click(screen.getByRole("tab", { name: "Grid" }));

    expect(container.querySelector("table")).toBeNull();
    // as duas sessões viram cards com o prefixo do id
    expect(screen.getAllByText(/sess-/).length).toBeGreaterThanOrEqual(2);
  });

  it("should switch to a kanban grouped by current vs other device", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();
    await screen.findByText("Este dispositivo");

    await user.click(screen.getByRole("tab", { name: "Kanban" }));

    expect(container.querySelector("table")).toBeNull();
    // as 2 colunas do quadro (o rótulo também está no chip do card → All)
    expect(screen.getAllByText("Este dispositivo").length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText("Outro dispositivo").length).toBeGreaterThanOrEqual(2);
  });
});
