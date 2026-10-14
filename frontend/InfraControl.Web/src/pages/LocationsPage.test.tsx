import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { LocationsPage } from "./LocationsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { createLocation, listDevices, listLocations, listRacks } from "@/services/api/inventory";

/**
 * F29-T001C-2 — LocationsPage: uma validação de campo e duas recusas de
 * formulário inteiro, no MESMO diálogo.
 *
 * O que este teste tranca: este formulário tem os TRÊS caminhos de erro que a
 * migração precisa separar, e é justamente por isso que ele é um caso mais
 * rico que o piloto (NetworkPage).
 *
 * 1. "O nome é obrigatório" é do campo Nome — o `FormField` daqui já trazia
 *    `error={null}`, ou seja a intenção de mostrar erro por campo existia e
 *    nunca foi ligada.
 * 2. "Cadastre um local antes de criar racks" NÃO é de campo nenhum: o nome
 *    digitado estava preenchido e válido, o que faltava era um local na base.
 *    Colocá-la embaixo de "Nome" apontaria o campo errado — fica no nível do
 *    formulário, de propósito (documentado no código).
 * 3. Recusa do servidor (catch) também é do formulário: o backend sabe menos
 *    sobre qual campo o usuário pretendeu do que a validação local.
 *
 * A tabela tem `<Column header="Nome">`, então tudo é escopado no diálogo —
 * `screen.getByText("Nome")` sem isso acharia o cabeçalho da coluna.
 */
vi.mock("@/services/api/inventory", () => ({
  listLocations: vi.fn(),
  listRacks: vi.fn(),
  listDevices: vi.fn(),
  createLocation: vi.fn(),
  updateLocation: vi.fn(),
  deleteLocation: vi.fn(),
  createRack: vi.fn(),
  updateRack: vi.fn(),
  deleteRack: vi.fn(),
}));

const listLocationsMock = vi.mocked(listLocations);
const listRacksMock = vi.mocked(listRacks);
const listDevicesMock = vi.mocked(listDevices);
const createLocationMock = vi.mocked(createLocation);

function paged<T>(items: T[]) {
  return { items, totalCount: items.length, page: 1, pageSize: 20 };
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Operator"],
    }),
  );
  // As três consultas da página — sem elas o React Query reclama de data undefined.
  listLocationsMock.mockResolvedValue(paged([]));
  listRacksMock.mockResolvedValue(paged([]));
  listDevicesMock.mockResolvedValue(paged([]));
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <LocationsPage />
      </QueryClientProvider>
    </Provider>,
  );
}

/** O `<label>` que envolve um campo — é ali que o `FormField` renderiza o erro. */
function field(dialog: HTMLElement, labelText: string): HTMLElement {
  const wrapper = within(dialog).getByText(labelText).closest("label");
  if (!wrapper) throw new Error(`campo ${labelText} nao encontrado`);
  return wrapper;
}

/**
 * Dispara o `onSubmit` do formulário do diálogo.
 *
 * `user.click` no botão de submit NÃO dispara o submit depois de `user.type`
 * em jsdom (a submissão implícita não é implementada de forma confiável lá);
 * no navegador real o caminho é o mesmo `submitForm`.
 */
function submitForm(dialog: HTMLElement) {
  const button = within(dialog).getByRole("button", { name: "Criar" });
  const form = button.closest("form");
  if (!form) throw new Error("formulario do dialogo nao encontrado");
  fireEvent.submit(form);
}

describe("LocationsPage — erro de validação por campo", () => {
  it("should put the empty-name failure under Nome, and only there", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Novo local" }));
    const dialog = screen.getByRole("dialog", { name: "Novo local" });
    submitForm(dialog);

    const mensagem = "O nome é obrigatório.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(field(dialog, "Nome")).getByText(mensagem)).toBeInTheDocument();
    expect(within(field(dialog, "Descrição (opcional)")).queryByText(mensagem)).toBeNull();
    // Uma vez só: antes saía no pé do diálogo sem dizer qual campo recusou.
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createLocationMock).not.toHaveBeenCalled();
  });

  it("should keep the rack precondition at form level, away from a valid Nome", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Racks" }));
    await user.click(await screen.findByRole("button", { name: "Novo rack" }));
    const dialog = screen.getByRole("dialog", { name: "Novo rack" });
    await user.type(within(dialog).getByPlaceholderText("Rack 01"), "Rack 01");
    submitForm(dialog);

    const mensagem = "Cadastre um local antes de criar racks.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    // O nome estava preenchido e válido — culpar o campo seria mentira.
    expect(within(field(dialog, "Nome")).queryByText(mensagem)).toBeNull();
    expect(within(field(dialog, "Descrição (opcional)")).queryByText(mensagem)).toBeNull();
    expect(createLocationMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from every field", async () => {
    createLocationMock.mockRejectedValue(new Error("Já existe um local com esse nome."));
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Novo local" }));
    const dialog = screen.getByRole("dialog", { name: "Novo local" });
    await user.type(within(dialog).getByPlaceholderText("Sala Rack A"), "Sala Rack B");
    submitForm(dialog);

    const mensagem = "Já existe um local com esse nome.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(within(field(dialog, "Nome")).queryByText(/Já existe/)).toBeNull();
    expect(within(field(dialog, "Descrição (opcional)")).queryByText(/Já existe/)).toBeNull();
    expect(createLocationMock).toHaveBeenCalledTimes(1);
  });
});

/** `paged` com o total FORA do tamanho do `items` (o servidor paginado). */
function pagedTotal<T>(total: number) {
  return { items: [] as T[], totalCount: total, page: 1, pageSize: 20 };
}

/**
 * C-3 — casca do `ListPageShell`.
 *
 * Esta página era a única que mandava DUAS linhas no subtítulo: o composto
 * ("3 locais · 5 racks · 12 dispositivos") e, numa `<span>` estilizada à mão,
 * "3 registro(s) nesta aba" — repetindo o MESMO número da primeira linha.
 *
 * A casca monta `contagem · nota`, então o número da aba vira a contagem e o
 * resto do composto vira a nota. O texto visível não muda: só deixa de estar
 * escrito duas vezes.
 */
describe("LocationsPage — casca da lista (C-3: ListPageShell)", () => {
  it("should keep the composite count on the locations tab", async () => {
    listLocationsMock.mockResolvedValue(pagedTotal(3));
    listRacksMock.mockResolvedValue(pagedTotal(5));
    listDevicesMock.mockResolvedValue(pagedTotal(12));
    renderPage();

    expect(
      await screen.findByText("3 locais · 5 racks · 12 dispositivos"),
    ).toBeInTheDocument();
    // A segunda linha repetia o 3. Sai.
    expect(screen.queryByText(/registro\(s\) nesta aba/)).toBeNull();
  });

  it("should follow the tab when it switches to racks", async () => {
    listLocationsMock.mockResolvedValue(pagedTotal(3));
    listRacksMock.mockResolvedValue(pagedTotal(5));
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Racks" }));

    expect(await screen.findByText("5 racks · 3 locais")).toBeInTheDocument();
    expect(screen.queryByText(/registro\(s\) nesta aba/)).toBeNull();
  });
});
