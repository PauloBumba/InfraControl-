import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { NetworkPage } from "./NetworkPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { createVlan, listVlans } from "@/services/api/network";

/**
 * F29-T001C-2 — piloto do `useFormErrors` ligando erro a campo.
 *
 * O que estava errado e o que este teste tranca: as duas validações eram de
 * campos DIFERENTES ("Informe o nome da VLAN" e "O número deve ser inteiro
 * entre 1 e 4094") e as duas apareciam no MESMO `FormError` no pé do diálogo,
 * sem indicar qual campo recusou. Os dois `FormField` já traziam
 * `error={null}` — a intenção de mostrar erro por campo já estava no código,
 * só nunca tinha sido ligada.
 */
vi.mock("@/services/api/network", () => ({
  NETWORK_PAGE_SIZE: 20,
  listVlans: vi.fn(),
  createVlan: vi.fn(),
  updateVlan: vi.fn(),
  deleteVlan: vi.fn(),
}));

// Fora do alcance deste teste — ele faz as próprias consultas.
vi.mock("@/components/network/NetworkServicesSection", () => ({
  NetworkServicesSection: () => null,
}));

const listVlansMock = vi.mocked(listVlans);
const createVlanMock = vi.mocked(createVlan);

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
  listVlansMock.mockResolvedValue({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <NetworkPage />
      </QueryClientProvider>
    </Provider>,
  );
}

/** O `<label>` que envolve um campo — é ali que o `FormField` renderiza o erro. */
function field(labelText: string): HTMLElement {
  const wrapper = screen.getByText(labelText).closest("label");
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
function submitForm() {
  const button = screen.getByRole("button", { name: "Criar VLAN" });
  const form = button.closest("form");
  if (!form) throw new Error("formulario do dialogo nao encontrado");
  fireEvent.submit(form);
}

describe("NetworkPage — erro de validação por campo", () => {
  it("should put the name failure under Nome, and only there", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Adicionar" }));
    submitForm();

    expect(await screen.findByText("Informe o nome da VLAN.")).toBeInTheDocument();
    expect(within(field("Nome *")).getByText("Informe o nome da VLAN.")).toBeInTheDocument();
    expect(within(field("Número (1–4094) *")).queryByText("Informe o nome da VLAN.")).toBeNull();
    // Uma vez só: antes a mesma frase saía no pé do diálogo sem dizer o campo.
    expect(screen.getAllByText("Informe o nome da VLAN.")).toHaveLength(1);
    expect(createVlanMock).not.toHaveBeenCalled();
  });

  it("should put the range failure under Número, not under Nome", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Adicionar" }));
    await user.type(screen.getByPlaceholderText("Ex.: Voz"), "Voz");
    // `type="number"` não recebe texto via user.type em jsdom — change direto.
    fireEvent.change(screen.getByPlaceholderText("100"), { target: { value: "5000" } });
    submitForm();

    const mensagem = "O número da VLAN deve ser inteiro entre 1 e 4094.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(field("Número (1–4094) *")).getByText(mensagem)).toBeInTheDocument();
    expect(within(field("Nome *")).queryByText(/deve ser inteiro/)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createVlanMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from every field", async () => {
    createVlanMock.mockRejectedValue(new Error("Já existe uma VLAN com esse número."));
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Adicionar" }));
    await user.type(screen.getByPlaceholderText("Ex.: Voz"), "Voz");
    fireEvent.change(screen.getByPlaceholderText("100"), { target: { value: "100" } });
    submitForm();

    const mensagem = "Já existe uma VLAN com esse número.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    // Nenhuma vez no campo: o número digitado estava válido, o backend recusou.
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(within(field("Nome *")).queryByText(/Já existe/)).toBeNull();
    expect(within(field("Número (1–4094) *")).queryByText(/Já existe/)).toBeNull();
  });
});
