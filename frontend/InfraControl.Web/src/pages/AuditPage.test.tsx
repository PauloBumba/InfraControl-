import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { AuditPage } from "./AuditPage";
import { listAuditEntries, type AuditEntry, type PagedAudit } from "@/services/api/audit";

/**
 * C-3 — casca do `ListPageShell` + unificação do erro.
 *
 * O que este teste tranca:
 * 1. A contagem "N eventos" com o ramo de loading, já pela casca.
 * 2. A nota de busca (`busca "…"`) vindo depois do `·`, no lugar que a casca
 *    reserva para complemento.
 * 3. A página tinha DOIS `<ErrorBanner>` adjacentes (um do formulário, um da
 *    lista). Com os dois erros simultâneos ela mostrava a mesma frase
 *    "Não foi possível…" DUAS vezes. A casca tem UM slot de erro — os dois
 *    passam a se fundir, com o do formulário na frente.
 *
 * F50-T004 — o filtro por ator lê o e-mail logado do Redux (default do
 * campo), por isso os testes agora montam o `Provider` + `sessionChecked`.
 */
vi.mock("@/services/api/audit", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/services/api/audit")>()),
  listAuditEntries: vi.fn(),
}));

const listAuditEntriesMock = vi.mocked(listAuditEntries);

const UMA_ENTRADA: AuditEntry = {
  id: "3f1a1111-2222-3333-4444-555566667777",
  action: "LoginWithMfa",
  resource: "Auth",
  outcome: "Succeeded",
  actorId: "9a111111-2222-3333-4444-555566667777",
  actorName: "Ana",
  correlationId: null,
  error: null,
  occurredAt: "2026-01-02T10:00:00Z",
  description: "Ana executou Login With Mfa em Auth — sucesso",
};

/** Mock que aplica o filtro de ação igualdade, como o servidor. */
function mockFiltrando(items: AuditEntry[]) {
  listAuditEntriesMock.mockImplementation(async (filters = {}) =>
    filters.action
      ? { items: items.filter((e) => e.action === filters.action), totalCount: 0, page: 1, pageSize: 100 }
      : { items, totalCount: items.length, page: 1, pageSize: 100 },
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Admin"],
    }),
  );
  mockFiltrando([UMA_ENTRADA]);
});

function renderPage() {
  return render(
    <Provider store={store}>
      <AuditPage />
    </Provider>,
  );
}

function paged(items: AuditEntry[], totalCount = items.length): PagedAudit {
  return { items, totalCount, page: 1, pageSize: 100 };
}

describe("AuditPage — casca da lista (C-3: ListPageShell)", () => {
  it("should count the entries", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { name: "Auditoria" })).toBeInTheDocument();
    expect(await screen.findByText("1 evento")).toBeInTheDocument();
  });

  it("should keep the plain count when a filter is applied", async () => {
    // `hasFilters` só acende o botão "Limpar" — a página NÃO tem nota de
    // busca no subtítulo. A casca não pode inventar uma.
    listAuditEntriesMock.mockImplementation(async (filters = {}) => {
      if (filters.action) return paged([], 0);
      return paged([UMA_ENTRADA]);
    });
    renderPage();
    const user = userEvent.setup();

    const busca = screen.getByPlaceholderText("Ação exata — ex.: LoginWithMfa");
    await user.type(busca, "zzz");
    await user.type(busca, "{Enter}");

    expect(await screen.findByText("0 eventos")).toBeInTheDocument();
  });

  it("should show ONE banner when the form and the list both fail", async () => {
    listAuditEntriesMock.mockRejectedValue(new Error("servidor fora"));
    renderPage();

    // O load falhou → banner da lista.
    await screen.findByRole("button", { name: "Filtrar" });

    // Agora o formulário também falha: "De" posterior ao "Até".
    fireEvent.change(screen.getByTitle("De — início do período (relógio local)"), {
      target: { value: "2026-01-02T10:00" },
    });
    fireEvent.change(screen.getByTitle("Até — fim do período, inclusivo até o fim do minuto"), {
      target: { value: "2026-01-01T10:00" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Filtrar" }));

    expect(
      await screen.findByText('O período "De" é posterior ao "Até".'),
    ).toBeInTheDocument();
    // Uma frase, um alerta — não dois.
    expect(screen.getAllByRole("alert")).toHaveLength(1);
  });
});

/**
 * F50-T004 — filtro por usuário (ator) + descrição detalhada por evento.
 *
 * O campo "Usuário" nasce com o username (e-mail) de quem está logado — cada
 * usuário pode trocar — e casa no backend também com sobrenome e com o nome
 * do operador exibido na auditoria. A descrição vem pronta do backend.
 */
describe("AuditPage — filtro por usuário (F50-T004)", () => {
  it("should default the actor filter to the logged username and send it to the API", async () => {
    renderPage();

    await screen.findByRole("heading", { name: "Auditoria" });

    expect(listAuditEntriesMock).toHaveBeenCalledWith(
      expect.objectContaining({ actor: "ana@exemplo.com" }),
    );
    // O campo nasce preenchido com o username logado.
    expect(screen.getByPlaceholderText(/Usuário/)).toHaveValue("ana@exemplo.com");
  });

  it("should allow changing the actor filter", async () => {
    renderPage();
    const user = userEvent.setup();
    listAuditEntriesMock.mockClear();

    const campo = screen.getByPlaceholderText(/Usuário/);
    await user.clear(campo);
    await user.type(campo, "silva");
    await user.type(campo, "{Enter}");

    expect(await screen.findByText("1 evento")).toBeInTheDocument();
    expect(listAuditEntriesMock).toHaveBeenCalledWith(
      expect.objectContaining({ actor: "silva" }),
    );
  });

  it("should render the detailed description of the event", async () => {
    renderPage();

    expect(
      await screen.findByText("Ana executou Login With Mfa em Auth — sucesso"),
    ).toBeInTheDocument();
  });
});
