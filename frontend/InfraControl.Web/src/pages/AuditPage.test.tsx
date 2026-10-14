import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
  mockFiltrando([UMA_ENTRADA]);
});

function renderPage() {
  return render(<AuditPage />);
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
