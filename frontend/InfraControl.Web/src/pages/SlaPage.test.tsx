import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { SlaPage } from "./SlaPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import {
  createService,
  createSlaPolicy,
  listServiceBudgets,
  listSlaPolicies,
  listServices,
  listSlos,
} from "@/services/api/sla";

/**
 * F29-T001C-2 — SlaPage: o formulário com mais validações do recorte, e onde a
 * decisão "campo ou formulário?" muda de resposta de uma validação para outra.
 *
 * Três decisões que este teste tranca:
 *
 * 1. "O alvo deve estar entre 0 e 100 (%)" é do campo Alvo — e não do Nome,
 *    que é onde TODA validação desta página aparecia antes (os quatro
 *    `FormField` traziam `error={null}`, ligação que nunca tinha sido feita).
 * 2. "Resposta e resolução devem ser números positivos" NÃO vai para campo:
 *    a frase cobre DOIS campos e não diz qual deles falhou — atribuir a um
 *    só apontaria errado quando o erro fosse do outro. Fica no nível do
 *    formulário, mesmo raciocínio do "De posterior a Até" da AuditPage.
 * 3. Recusa do servidor (catch) é do formulário — o backend não sabe qual
 *    campo o usuário pretendeu.
 */
vi.mock("@/services/api/sla", () => ({
  listServices: vi.fn(),
  listSlos: vi.fn(),
  listSlaPolicies: vi.fn(),
  listServiceBudgets: vi.fn(),
  createService: vi.fn(),
  updateService: vi.fn(),
  deleteService: vi.fn(),
  createSlaPolicy: vi.fn(),
  updateSlaPolicy: vi.fn(),
  deleteSlaPolicy: vi.fn(),
  createSlo: vi.fn(),
  updateSlo: vi.fn(),
  deleteSlo: vi.fn(),
}));

const listServicesMock = vi.mocked(listServices);
const listSlosMock = vi.mocked(listSlos);
const listSlaPoliciesMock = vi.mocked(listSlaPolicies);
const listServiceBudgetsMock = vi.mocked(listServiceBudgets);
const createServiceMock = vi.mocked(createService);
const createSlaPolicyMock = vi.mocked(createSlaPolicy);

const umServico = {
  id: "svc-1",
  name: "Core",
  description: null,
};

function page<T>(items: T[]) {
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
  // As 4 consultas da página. `listServices` leva 1 item de propósito: o
  // `openCreate` de SLO pré-preenche `serviceId` com o primeiro serviço, e
  // sem ele a validação de serviço dispararia antes da de alvo.
  listServicesMock.mockResolvedValue(page([umServico]));
  listSlosMock.mockResolvedValue(page([]));
  listSlaPoliciesMock.mockResolvedValue(page([]));
  listServiceBudgetsMock.mockResolvedValue(page([]));
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <SlaPage />
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
 * em jsdom; no navegador real o caminho é o mesmo `submitForm`.
 */
function submitForm(dialog: HTMLElement) {
  const button = within(dialog).getByRole("button", { name: "Criar" });
  const form = button.closest("form");
  if (!form) throw new Error("formulario do dialogo nao encontrado");
  fireEvent.submit(form);
}

/** Troca de aba e abre o diálogo de criação daquela aba. */
async function openCreate(tabLabel: string, createButton: string, dialogName: string) {
  const user = userEvent.setup();
  await user.click(await screen.findByRole("button", { name: tabLabel }));
  await user.click(await screen.findByRole("button", { name: createButton }));
  return { user, dialog: screen.getByRole("dialog", { name: dialogName }) };
}

describe("SlaPage — erro de validação por campo", () => {
  it("should put the empty-name failure under Nome, and only there", async () => {
    renderPage();
    const { user, dialog } = await openCreate("Serviços", "Novo Serviço", "Novo serviço");

    await user.clear(within(dialog).getByPlaceholderText("Nome *"));
    submitForm(dialog);

    const mensagem = "Informe o nome.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(field(dialog, "Nome")).getByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createServiceMock).not.toHaveBeenCalled();
  });

  it("should put the out-of-range failure under Alvo, not under Nome", async () => {
    renderPage();
    const { user, dialog } = await openCreate("SLOs", "Novo SLO", "Novo SLO");

    // O Nome vem primeiro no `submitForm` e retorna cedo — sem ele preenchido
    // a validação de alvo nunca é alcançada. `type="number"` não recebe texto
    // via user.type em jsdom — change direto.
    await user.type(within(dialog).getByPlaceholderText("Nome *"), "Disponibilidade");
    fireEvent.change(within(dialog).getByLabelText("Alvo (%)"), {
      target: { value: "150" },
    });
    submitForm(dialog);

    const mensagem = "O alvo deve estar entre 0 e 100 (%).";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(field(dialog, "Alvo (%)")).getByText(mensagem)).toBeInTheDocument();
    // Antes esta frase saía no pé do diálogo, longe do campo que a originou.
    expect(within(field(dialog, "Nome")).queryByText(/alvo/)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
  });

  it("should keep the response/resolution failure at form level, since it names two fields", async () => {
    renderPage();
    const { user, dialog } = await openCreate(
      "Políticas",
      "Nova Política",
      "Nova política de SLA",
    );

    // O Nome vem primeiro no `submitForm` e retorna cedo — sem ele preenchido
    // a validação de resposta/resolução nunca é alcançada.
    await user.type(within(dialog).getByPlaceholderText("Nome *"), "Resposta padrão");
    // `type="number"` não recebe texto via user.type em jsdom — change direto.
    fireEvent.change(within(dialog).getByLabelText("Resposta (min)"), {
      target: { value: "0" },
    });
    submitForm(dialog);

    const mensagem = "Resposta e resolução devem ser números positivos (minutos).";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    // A frase não diz qual dos dois falhou — pendurar em um só seria mentira.
    expect(within(field(dialog, "Resposta (min)")).queryByText(/positivos/)).toBeNull();
    expect(within(field(dialog, "Resolução (min)")).queryByText(/positivos/)).toBeNull();
    expect(within(field(dialog, "Nome")).queryByText(/positivos/)).toBeNull();
    expect(createSlaPolicyMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from every field", async () => {
    createServiceMock.mockRejectedValue(new Error("Já existe um serviço com esse nome."));
    renderPage();
    const { user, dialog } = await openCreate("Serviços", "Novo Serviço", "Novo serviço");

    await user.type(within(dialog).getByPlaceholderText("Nome *"), "Backup");
    submitForm(dialog);

    const mensagem = "Já existe um serviço com esse nome.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(within(field(dialog, "Nome")).queryByText(/Já existe/)).toBeNull();
    expect(createServiceMock).toHaveBeenCalledTimes(1);
  });
});
