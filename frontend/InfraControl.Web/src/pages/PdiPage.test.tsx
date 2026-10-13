import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { PdiPage } from "./PdiPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import {
  getPdiAdminDashboard,
  getPdiDashboard,
  getPdiManagerDashboard,
  listPdis,
  upsertPdi,
  type Pdi,
} from "@/services/api/pdi";
import { listUsers } from "@/services/api/adminUsers";

/**
 * F29-T001C-2 — PdiPage: o caso em que **cada validação cai num lugar
 * diferente**, e uma delas não pode ir para campo nenhum.
 *
 * O que este teste tranca, decisão por decisão:
 *
 * 1. "Selecione o usuário do PDI." → campo **Usuário** (só existe na
 *    criação; na edição o dono vem do plano).
 * 2. "Informe o prazo do PDI." → campo **Prazo**, e o Usuário já válido não
 *    é culpado.
 * 3. "O fim do período não pode ser anterior ao início." → **fica no
 *    formulário**. A frase descreve DOIS campos (Início e Fim); pendurar em
 *    um só seria apontar metade do problema — mesma regra do "De posterior a
 *    Até" da AuditPage e do "Resposta e resolução" da SlaPage.
 * 4. "O motivo da alteração é obrigatório." → campo **Motivo da alteração**.
 *    Esse caso exige edição: `needsReason` depende de `original`, então na
 *    criação o campo nem é renderizado.
 * 5. Recusa do servidor → no formulário, longe de todos.
 */
vi.mock("@/services/api/pdi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/pdi")>();
  return {
    ...actual,
    listPdis: vi.fn(),
    upsertPdi: vi.fn(),
    getPdiDashboard: vi.fn(),
    getPdiManagerDashboard: vi.fn(),
    getPdiAdminDashboard: vi.fn(),
    getPdiHistory: vi.fn(),
    generatePdiKnowledge: vi.fn(),
  };
});

vi.mock("@/services/api/adminUsers", () => ({
  listUsers: vi.fn(),
}));

/**
 * `SelectField` é wrapper do `Dropdown` do PrimeReact — não abre no jsdom.
 * Mesmo precedente de `KnowledgePage.test.tsx`/`DevicesPage`/`TasksPage`.
 */
vi.mock("@/components/ui/SelectField", () => ({
  SelectField: (props: {
    value: string;
    onChange: (value: string) => void;
    options: { label: string; value: string }[];
    placeholder?: string;
    disabled?: boolean;
    "aria-label"?: string;
  }) => (
    <select
      value={props.value}
      disabled={props.disabled}
      aria-label={props["aria-label"]}
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

const listPdisMock = vi.mocked(listPdis);
const upsertPdiMock = vi.mocked(upsertPdi);
const listUsersMock = vi.mocked(listUsers);

const usuario = {
  userId: "7f1a1111-2222-3333-4444-555566667777",
  email: "bruno@exemplo.com",
  displayName: "Bruno",
  isActive: true,
  roles: [2],
  updatedAt: "2026-10-01T10:00:00Z",
};

const umPlano: Pdi = {
  userId: "9f1a1111-2222-3333-4444-555566667777",
  ownerName: "Carla",
  ownerEmail: "carla@exemplo.com",
  strategy: "Assumir o monitoramento de rede",
  targetDate: "2026-12-31",
  status: "Active",
  managerId: null,
  managerName: null,
  periodStart: null,
  periodEnd: null,
  updateDateTime: "2026-10-01T10:00:00Z",
};

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Admin"],
    }),
  );
  listPdisMock.mockResolvedValue([]);
  listUsersMock.mockResolvedValue([usuario]);
  vi.mocked(getPdiDashboard).mockRejectedValue(new Error("sem dashboard"));
  vi.mocked(getPdiManagerDashboard).mockRejectedValue(new Error("sem dashboard"));
  vi.mocked(getPdiAdminDashboard).mockRejectedValue(new Error("sem dashboard"));
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <PdiPage />
      </QueryClientProvider>
    </Provider>,
  );
}

/** O `<form>` dentro do dialog — `role="dialog"` + `aria-label={header}`. */
function form(header: "Novo PDI" | "Editar PDI"): HTMLElement {
  const dialog = screen.getByRole("dialog", { name: header });
  const wrapper = within(dialog).getByRole("button", { name: /Salvar/ }).closest("form");
  if (!wrapper) throw new Error("formulario nao encontrado");
  return wrapper;
}

/**
 * O `<label>` do `FormField` — é ali que a validação tem que acender.
 *
 * Casa pelo **texto inicial** do label e não por `getByLabelText`: quando o
 * erro acende, a mensagem entra DENTRO do mesmo `<label>` e o `textContent`
 * deixa de ser igual a "Prazo" (vira "PrazoInforme o prazo do PDI."), então o
 * match exato do RTL não acha mais nada. `startsWith` + escopo no `<form>`
 * mantém o mesmo seletor antes e depois da migração.
 */
function field(labelText: string, header: "Novo PDI" | "Editar PDI" = "Novo PDI"): HTMLElement {
  const labels = Array.from(form(header).querySelectorAll("label"));
  const found = labels.find((label) => (label.textContent ?? "").trim().startsWith(labelText));
  if (!found) throw new Error(`campo ${labelText} nao encontrado no form`);
  return found;
}

function submit(header: "Novo PDI" | "Editar PDI") {
  const wrapper = form(header);
  fireEvent.submit(wrapper);
}

function setDate(labelText: string, value: string) {
  const input = field(labelText).querySelector("input");
  if (!input) throw new Error(`input de data do campo ${labelText} nao encontrado`);
  fireEvent.change(input, { target: { value } });
}

async function openCreate(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: /Novo PDI/ }));
}

/** Seleciona o primeiro usuário no `SelectField` mockado como `<select>`. */
function selectUsuario(value: string) {
  fireEvent.change(within(form("Novo PDI")).getByLabelText("Usuário do PDI"), {
    target: { value },
  });
}

describe("PdiPage — erro de validação por campo", () => {
  it("should put the missing-user failure under Usuário, and only there", async () => {
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);

    submit("Novo PDI");

    const mensagem = "Selecione o usuário do PDI.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(field("Usuário")).getByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(upsertPdiMock).not.toHaveBeenCalled();
  });

  it("should put the missing-target failure under Prazo, not under a valid Usuário", async () => {
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);
    selectUsuario(usuario.userId);

    submit("Novo PDI");

    const mensagem = "Informe o prazo do PDI.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(field("Prazo")).getByText(mensagem)).toBeInTheDocument();
    // Usuário veio escolhido — culpar aqui apontaria o campo errado.
    expect(within(field("Usuário")).queryByText(/Informe o prazo/)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(upsertPdiMock).not.toHaveBeenCalled();
  });

  it("should keep the reversed-period failure at form level, away from both period fields", async () => {
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);
    selectUsuario(usuario.userId);
    setDate("Prazo", "2026-12-31");
    setDate("Início do período", "2026-10-10");
    setDate("Fim do período", "2026-10-01");

    submit("Novo PDI");

    const mensagem = "O fim do período não pode ser anterior ao início.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(form("Novo PDI")).getAllByText(mensagem)).toHaveLength(1);
    // A frase cobre DOIS campos: em nenhum dos dois ela seria completa.
    expect(within(field("Início do período")).queryByText(/período não pode/)).toBeNull();
    expect(within(field("Fim do período")).queryByText(/período não pode/)).toBeNull();
    expect(upsertPdiMock).not.toHaveBeenCalled();
  });

  it("should put the missing-reason failure under Motivo da alteração in edit mode", async () => {
    listPdisMock.mockResolvedValue([umPlano]);
    renderPage();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Editar" }));
    expect(form("Editar PDI")).toBeInTheDocument();

    // Alterar um campo é o que torna o motivo obrigatório (§14).
    await user.type(within(form("Editar PDI")).getByLabelText("Estratégia"), " e também redes");

    fireEvent.submit(form("Editar PDI"));

    const mensagem = "O motivo da alteração é obrigatório.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(
      within(field("Motivo da alteração", "Editar PDI")).getByText(mensagem),
    ).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(upsertPdiMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from every field", async () => {
    upsertPdiMock.mockRejectedValue(new Error("PDI já existe para este usuário."));
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);
    selectUsuario(usuario.userId);
    setDate("Prazo", "2026-12-31");

    submit("Novo PDI");

    const mensagem = "PDI já existe para este usuário.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(form("Novo PDI")).getAllByText(mensagem)).toHaveLength(1);
    expect(within(field("Usuário")).queryByText(/já existe/)).toBeNull();
    expect(within(field("Prazo")).queryByText(/já existe/)).toBeNull();
    expect(upsertPdiMock).toHaveBeenCalledTimes(1);
  });
});
