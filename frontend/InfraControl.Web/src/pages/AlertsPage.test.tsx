import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { AlertsPage } from "./AlertsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listAlerts, resolveAlert, setAlertNote, type AlertItem } from "@/services/api/alerts";

/**
 * F29-T001B — piloto da `ListPageShell`.
 *
 * Esta página NÃO tinha nenhum teste, e a migração mexe em três coisas que
 * quebrariam em silêncio: o subtítulo de contagem, o bloco de erro com retry e
 * o seletor de status (que manda o filtro para a API). Os três ficam travados
 * aqui.
 *
 * F50-T003: ganhou anotação do operador (`setAlertNote` atrás de
 * `Can operate`) — os testes de nota cobrem o dialog e a chamada.
 */
vi.mock("@/services/api/alerts", () => ({
  listAlerts: vi.fn(),
  setAlertNote: vi.fn(),
  resolveAlert: vi.fn(),
}));

const listAlertsMock = vi.mocked(listAlerts);
const setAlertNoteMock = vi.mocked(setAlertNote);
const resolveAlertMock = vi.mocked(resolveAlert);

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
    operatorNote: null,
    resolutionCode: null,
    resolutionNote: null,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <Provider store={store}>
      <AlertsPage />
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "op@exemplo.com",
      displayName: "Operador",
      roles: ["Operator"],
    }),
  );
  listAlertsMock.mockResolvedValue({
    items: [makeAlert()],
    totalCount: 1,
    page: 1,
    pageSize: 20,
  });
});

describe("AlertsPage — casca de índice", () => {
  it("should show the count subtitle in the singular", async () => {
    renderPage();

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

    renderPage();
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
    renderPage();
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

/**
 * F50-T003 — anotação do operador: o alerta nasce do sistema, mas o
 * operador pode registrar contexto nele. O que se tranca: a nota existente
 * aparece na linha, o botão "Anotar" abre o dialog já preenchido e o
 * salvar chama `setAlertNote` (PUT de substituição) antes de recarregar.
 */
describe("AlertsPage — anotação do operador (F50-T003)", () => {
  it("should render the existing note on the alert row", async () => {
    listAlertsMock.mockResolvedValue({
      items: [makeAlert({ operatorNote: "troca do switch em andamento" })],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });
    renderPage();

    expect(await screen.findByText("troca do switch em andamento")).toBeInTheDocument();
  });

  it("should open the note dialog prefilled and save through setAlertNote", async () => {
    setAlertNoteMock.mockResolvedValue(null);
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Anotar" }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toBeInTheDocument();
    const textarea = screen.getByRole("textbox");
    expect(textarea).toHaveValue(""); // sem nota anterior

    await user.type(textarea, "verificar enlace com o ISP");
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    await waitFor(() =>
      expect(setAlertNoteMock).toHaveBeenCalledWith(
        "11111111-2222-3333-4444-555566667777",
        "verificar enlace com o ISP",
      ),
    );
    // Recarrega a lista para refletir a nota salva.
    await waitFor(() => expect(listAlertsMock).toHaveBeenCalledTimes(2));
  });
});

/**
 * F52-T003 — baixa justificada: o botão "Resolver" (só em alerta
 * não-Resolved) abre o dialog com código + justificativa e chama
 * `resolveAlert`; a linha já resolvida não mostra o botão e exibe a
 * baixa gravada.
 */
describe("AlertsPage — baixa justificada (F52-T003)", () => {
  it("should open the resolve dialog and call resolveAlert with code and note", async () => {
    resolveAlertMock.mockResolvedValue(null);
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Resolver" }));

    const dialog = await screen.findByRole("dialog", { name: "Baixar alerta" });
    const textarea = within(dialog).getByPlaceholderText(/O que foi feito para baixar/);
    await user.type(textarea, "condição normalizada após troca do cabo");
    await user.click(within(dialog).getByRole("button", { name: "Baixar" }));

    await waitFor(() =>
      expect(resolveAlertMock).toHaveBeenCalledWith(
        "11111111-2222-3333-4444-555566667777",
        "Fixed",
        "condição normalizada após troca do cabo",
      ),
    );
    // Recarrega a lista para refletir a baixa.
    await waitFor(() => expect(listAlertsMock).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole("dialog", { name: "Baixar alerta" })).toBeNull();
  });

  it("should not offer Resolver on an already-resolved alert and show its code", async () => {
    listAlertsMock.mockResolvedValue({
      items: [
        makeAlert({
          status: "Resolved",
          resolvedAt: "2026-10-11T11:00:00Z",
          resolutionCode: "Fixed",
          resolutionNote: "cabo trocado",
        }),
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });
    renderPage();

    expect(await screen.findByText(/Baixa: Corrigido/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Resolver" })).toBeNull();
    expect(screen.getByRole("button", { name: "Anotar" })).toBeInTheDocument();
  });

  it("should require the justification before calling the API", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Resolver" }));
    await screen.findByRole("dialog", { name: "Baixar alerta" });
    await user.click(screen.getByRole("button", { name: "Baixar" }));

    expect(await screen.findByText("A justificativa da baixa é obrigatória.")).toBeInTheDocument();
    expect(resolveAlertMock).not.toHaveBeenCalled();
  });
});
