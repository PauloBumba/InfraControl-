import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { MemoryRouter } from "react-router-dom";
import { DevicesPage } from "./DevicesPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { createDevice, listDevices, listLocations } from "@/services/api/inventory";

/**
 * F29-T001C-2 — DevicesPage: as TRÊS validações do formulário são de TRÊS
 * campos diferentes, e nenhuma delas tinha para onde ir.
 *
 * O que estava errado e o que este teste tranca: os 7 controles do form são
 * inputs com `placeholder` dentro de um grid — a página não tinha um sequer
 * `FormField`, então os três recados saíam todos no MESMO `<div>` no pé do
 * formulário, longe do campo que falhou. Aqui cada validação declara o próprio
 * campo e o recado aparece embaixo dele.
 *
 * 1. "Informe o nome do dispositivo." → Nome.
 * 2. "Selecione o tipo do dispositivo." → Tipo. Note que o Nome já estava
 *    preenchido e válido: culpar o campo errado seria mentira.
 * 3. "Selecione a localização do dispositivo." → Localização (só na criação;
 *    no edit a localização é travada pelo PUT).
 * 4. Recusa do servidor (catch) → nível do formulário, longe de TODOS os
 *    campos: o backend não sabe qual campo o usuário pretendeu.
 *
 * A tabela tem `<Column header="Nome">`/`"Tipo"`, então tudo é escopado no
 * `<form>` — `screen.getByText("Nome")` sem isso acharia o cabeçalho.
 */
vi.mock("@/services/api/inventory", () => ({
  INVENTORY_PAGE_SIZE: 100,
  listDevices: vi.fn(),
  listLocations: vi.fn(),
  listSwitchPorts: vi.fn(),
  createDevice: vi.fn(),
  updateDevice: vi.fn(),
  deleteDevice: vi.fn(),
}));

/**
 * O `SelectField` é um wrapper do `Dropdown` do PrimeReact, cujo painel de
 * opções só existe em portal/DOM real (não abre no jsdom). Trocamos por um
 * `<select>` nativo para testar o que INTERESSA aqui: qual campo recebe o
 * erro. Mesmo precedente de `KnowledgePage.test.tsx`.
 */
vi.mock("@/components/ui/SelectField", () => ({
  SelectField: (props: {
    value: string;
    onChange: (value: string) => void;
    options: { label: string; value: string }[];
    disabled?: boolean;
  }) => (
    <select
      value={props.value}
      disabled={props.disabled}
      onChange={(event) => props.onChange(event.target.value)}
    >
      {props.options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  ),
}));

const listDevicesMock = vi.mocked(listDevices);
const listLocationsMock = vi.mocked(listLocations);
const createDeviceMock = vi.mocked(createDevice);

const umaLocalizacao = { id: "loc-1", name: "Sala Rack A", description: null };

function paged<T>(items: T[]) {
  return { items, totalCount: items.length, page: 1, pageSize: 100 };
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
  listDevicesMock.mockResolvedValue(paged([]));
  listLocationsMock.mockResolvedValue(paged([umaLocalizacao]));
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <DevicesPage />
        </MemoryRouter>
      </QueryClientProvider>
    </Provider>,
  );
}

/** O `<form>` do "Novo dispositivo" — a página o renderiza inline, sem Dialog. */
function form(): HTMLElement {
  const wrapper = screen.getByText("Novo dispositivo").closest("form");
  if (!wrapper) throw new Error("formulario nao encontrado");
  return wrapper;
}

/**
 * Controles do form, na ordem do DOM: nome, ip, modelo, fabricante, descrição.
 *
 * Role, não label: `getByLabelText` exige o `<label>` que a migração ainda vai
 * criar — o teste precisa rodar ANTES (vermelho) e DEPOIS (verde) com os
 * mesmos seletores. `noUncheckedIndexedAccess` torna o índice `T | undefined`,
 * então os acessores abaixo explodem com nome em vez de propagar `undefined`.
 */
function textboxes(): HTMLElement[] {
  return within(form()).getAllByRole("textbox");
}

/** `select`s na ordem do DOM: tipo, localização. */
function selects(): HTMLSelectElement[] {
  return within(form()).getAllByRole("combobox") as HTMLSelectElement[];
}

function at<T>(items: T[], index: number, what: string): T {
  const item = items[index];
  if (item === undefined) throw new Error(`campo ${what} nao encontrado`);
  return item;
}

function nome(): HTMLElement {
  return at(textboxes(), 0, "Nome");
}

function tipo(): HTMLSelectElement {
  return at(selects(), 0, "Tipo");
}

function localizacao(): HTMLSelectElement {
  return at(selects(), 1, "Localização");
}

function fabricante(): HTMLElement {
  return at(textboxes(), 3, "Fabricante");
}

/** O `<label>` que envolve um campo — é ali que o `FormField` renderiza o erro. */
function fieldOf(control: HTMLElement): HTMLElement {
  const wrapper = control.closest("label");
  if (!wrapper) {
    throw new Error(`controle ${control.tagName} sem <label> — FormField não aplicado`);
  }
  return wrapper;
}

/** Dispara o `onSubmit` real do form. */
function submit() {
  const button = within(form()).getByRole("button", { name: "Criar dispositivo" });
  const wrapper = button.closest("form");
  if (!wrapper) throw new Error("formulario nao encontrado");
  fireEvent.submit(wrapper);
}

/** Abre o form de criação (botão "Adicionar" do cabeçalho). */
async function openCreate(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: "Adicionar" }));
}

describe("DevicesPage — erro de validação por campo", () => {
  it("should put the empty-name failure under Nome, and only there", async () => {
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);

    submit();

    const mensagem = "Informe o nome do dispositivo.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldOf(nome())).getByText(mensagem)).toBeInTheDocument();
    // Campo sem validação nenhuma (fabricante) — o recado não pode aparecer lá.
    expect(within(fieldOf(fabricante())).queryByText(mensagem)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createDeviceMock).not.toHaveBeenCalled();
  });

  it("should put the missing-type failure under Tipo, not under a valid Nome", async () => {
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);
    await user.type(nome(), "srv-web-01");

    submit();

    const mensagem = "Selecione o tipo do dispositivo.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldOf(at(selects(), 0, "Tipo"))).getByText(mensagem)).toBeInTheDocument();
    // O Nome veio preenchido e válido — pendurar aqui apontaria o campo errado.
    expect(within(fieldOf(nome())).queryByText(/Selecione/)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createDeviceMock).not.toHaveBeenCalled();
  });

  it("should put the missing-location failure under Localização, and only there", async () => {
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);
    await user.type(nome(), "srv-web-01");
    fireEvent.change(tipo(), { target: { value: "Server" } });

    submit();

    const mensagem = "Selecione a localização do dispositivo.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldOf(localizacao())).getByText(mensagem)).toBeInTheDocument();
    // Nem o Tipo nem o Nome falharam — lá não pode haver recado nenhum.
    expect(within(fieldOf(tipo())).queryByText(/Selecione/)).toBeNull();
    expect(within(fieldOf(nome())).queryByText(/Selecione/)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createDeviceMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from every field", async () => {
    createDeviceMock.mockRejectedValue(
      new Error("Já existe um dispositivo com esse nome."),
    );
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);
    await user.type(nome(), "srv-web-01");
    fireEvent.change(tipo(), { target: { value: "Server" } });
    fireEvent.change(localizacao(), { target: { value: umaLocalizacao.id } });

    submit();

    const mensagem = "Já existe um dispositivo com esse nome.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    // A recusa do servidor não aponta campo: fica no nível do formulário.
    expect(within(fieldOf(nome())).queryByText(/Já existe/)).toBeNull();
    expect(within(fieldOf(tipo())).queryByText(/Já existe/)).toBeNull();
    expect(within(fieldOf(localizacao())).queryByText(/Já existe/)).toBeNull();
    expect(createDeviceMock).toHaveBeenCalledTimes(1);
  });
});
