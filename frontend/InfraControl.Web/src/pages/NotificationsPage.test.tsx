import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { NotificationsPage } from "./NotificationsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import {
  listFeed,
  listPreferences,
  setPreference,
  type NotificationFeedItem,
} from "@/services/api/notifications";

/**
 * Adoção do padrão de visões (backlog Operations UX, pedido 2026-10-11).
 *
 * A NotificationsPage não é uma página de tabela: o feed é um `Timeline`
 * do PrimeReact e a aba Preferências é uma tabela FIXA de 6 canais de
 * configuração. Por isso o seletor entra SÓ na aba Feed, com o 1º modo
 * rotulado "Linha do tempo" — o `Timeline` vive no slot `table` do
 * `EntityView`, que aceita qualquer ReactNode ali.
 *
 * O que se tranca aqui: o Timeline continua sendo o modo padrão (nada
 * muda para quem já usava), o quadro agrupa por `eventType` (string
 * livre, então as colunas saem dos dados carregados) e a aba
 * Preferências NÃO ganha seletor — é config, não índice.
 */
vi.mock("@/services/api/notifications", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/services/api/notifications")>()),
  listFeed: vi.fn(),
  listPreferences: vi.fn(),
  setPreference: vi.fn(),
}));

vi.mock("@/services/signalr/notifications", () => ({
  onNotificationPush: vi.fn(() => () => {}),
}));

const listFeedMock = vi.mocked(listFeed);
const listPreferencesMock = vi.mocked(listPreferences);
const setPreferenceMock = vi.mocked(setPreference);

const feed: NotificationFeedItem[] = [
  {
    id: "notif-1",
    eventType: "IncidentCreated",
    title: "Incidente INC-42 aberto",
    message: "Perda de sinal no switch-rd-01.",
    createdAt: "2026-10-11T10:00:00Z",
  },
  {
    id: "notif-2",
    eventType: "SlaBreached",
    title: "SLA de INC-40 estourou",
    message: "Tempo de recuperação excedido.",
    createdAt: "2026-10-11T09:00:00Z",
  },
];

function renderPage() {
  return render(
    <Provider store={store}>
      <NotificationsPage />
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
  listFeedMock.mockResolvedValue({ items: feed, totalCount: 2, page: 1, pageSize: 20 });
  listPreferencesMock.mockResolvedValue([]);
});

describe("NotificationsPage — visões linha do tempo / grid / quadro (aba Feed)", () => {
  it("should keep the timeline as the default view", async () => {
    const { container } = renderPage();

    expect(await screen.findByText("Incidente INC-42 aberto")).toBeInTheDocument();
    expect(container.querySelector(".p-timeline")).not.toBeNull();
    expect(screen.getByRole("tab", { name: "Linha do tempo" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
  });

  it("should switch the feed to a grid of cards", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();
    await screen.findByText("Incidente INC-42 aberto");

    await user.click(screen.getByRole("tab", { name: "Grid" }));

    expect(container.querySelector(".p-timeline")).toBeNull();
    expect(screen.getByText("SLA de INC-40 estourou")).toBeInTheDocument();
  });

  it("should switch the feed to a kanban grouped by eventType", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();
    await screen.findByText("Incidente INC-42 aberto");

    await user.click(screen.getByRole("tab", { name: "Kanban" }));

    expect(container.querySelector(".p-timeline")).toBeNull();
    // as colunas saem dos `eventType` carregados; o mesmo rótulo aparece no
    // card, por isso All.
    expect(screen.getAllByText("IncidentCreated").length).toBeGreaterThan(0);
    expect(screen.getAllByText("SlaBreached").length).toBeGreaterThan(0);
  });

  it("should keep the preferences tab as the fixed 6-channel table, without a switcher", async () => {
    const user = userEvent.setup();
    const { container } = renderPage();
    await screen.findByText("Incidente INC-42 aberto");

    await user.click(screen.getByText("Preferências"));

    expect(await screen.findByText("Preferências de entrega")).toBeInTheDocument();
    // é uma tabela de configuração, não um índice: segue tabela e sem seletor.
    expect(container.querySelector("table")).not.toBeNull();
    expect(screen.queryByRole("tab", { name: "Linha do tempo" })).toBeNull();
    // queryAllByRole (e não getAllByRole): getAllByRole LANÇA quando não há
    // nenhum match, em vez de devolver [].
    expect(screen.queryAllByRole("tab")).toHaveLength(0);
  });
});

/**
 * ADR-061 §3 (M4): com o GET de preferências falho, a UI mostra "Ativo"
 * (fallback) e o toggle deve inverter o valor EFETIVO — antes
 * `!undefined = true` reativava o canal quando o usuário clicava para
 * desligar.
 */
describe("NotificationsPage — toggle com preferências falhas (M4)", () => {
  it("turns the channel off when the preferences map is empty", async () => {
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "ana@exemplo.com",
        displayName: "Ana",
        roles: ["Operator"],
      }),
    );
    listPreferencesMock.mockRejectedValue(new Error("offline"));
    setPreferenceMock.mockResolvedValue(null);

    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Preferências" }));

    // Fallback exibido = Ativo; o clique precisa DESLIGAR (false).
    await user.click(await screen.findByRole("switch", { name: "Email: ativo" }));

    await waitFor(() =>
      expect(setPreferenceMock).toHaveBeenCalledWith(
        "3f1a1111-2222-3333-4444-555566667777",
        2,
        false,
      ),
    );
  });
});
