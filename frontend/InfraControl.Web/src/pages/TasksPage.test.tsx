import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { MemoryRouter } from "react-router-dom";
import { TasksPage } from "./TasksPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { createTask, listTasks, setTaskIdentification } from "@/services/api/tasks";
import { listUsers } from "@/services/api/adminUsers";

/**
 * F29-T001C-2 — TasksPage: a página tinha o bug que o C-2 existe para corrigir.
 *
 * O que estava errado: o `FormField` do Título já recebia erro, mas com a
 * mensagem **hardcodada em cima do estado errado**:
 *
 *     <FormField label="Título" error={formError ? "Informe o título da tarefa." : null}>
 *
 * Qualquer `formError` — inclusive uma recusa do servidor — fazia o campo
 * Título acender com "Informe o título da tarefa." (mentindo: o título estava
 * preenchido) E a mensagem real aparecia de novo no `<FormError>` do pé do
 * form. Erro culando campo + mensagem duplicada, na mesma tela.
 *
 * O que este teste tranca:
 *
 * 1. Título vazio → "Informe o título da tarefa." **só** no campo Título,
 *    uma única vez na tela inteira (o `FormError` não repete), sem mutation.
 * 2. Recusa do servidor na criação → a mensagem sai do campo e vai para o
 *    nível do formulário, longe do Título. É a regressão direta do bug acima.
 * 3. Recusa do servidor na edição → idem, e no modo edição o campo Título nem
 *    é renderizado (`{!editing && …}`), então não há para onde culpá-lo.
 *
 * Observação de escopo: `createMutation`/`identificationMutation` também têm
 * `onError → setActionError`, então a mesma recusa sobe ao `ErrorBanner` de
 * página. Isso é pré-existente, é o `actionError` (mesma decisão da
 * LocationsPage: banner de página não migra no C-2), e por isso as asserções
 * da recusa são escopadas no `<form>`.
 */
vi.mock("@/services/api/tasks", () => ({
  listTasks: vi.fn(),
  createTask: vi.fn(),
  updateTask: vi.fn(),
  deleteTask: vi.fn(),
  changeTaskState: vi.fn(),
  setTaskIdentification: vi.fn(),
}));

vi.mock("@/services/api/adminUsers", () => ({
  listUsers: vi.fn(),
}));

/**
 * `SelectField` é wrapper do `Dropdown` do PrimeReact — painel em portal, não
 * abre no jsdom. Mesmo precedente de `KnowledgePage.test.tsx`/`DevicesPage`.
 * Aqui nenhum teste seleciona opção; o mock serve só para o form renderizar.
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

const listTasksMock = vi.mocked(listTasks);
const createTaskMock = vi.mocked(createTask);
const setTaskIdentificationMock = vi.mocked(setTaskIdentification);
const listUsersMock = vi.mocked(listUsers);

const umaTarefa = {
  id: "1f1a1111-2222-3333-4444-555566667777",
  title: "Trocar switch do sala B",
  description: null,
  kind: "Manual",
  state: "Open",
  openedAt: "2026-10-01T10:00:00Z",
  assigneeCount: 0,
  assigneeIds: [],
  deviceCount: 0,
  deviceIds: [],
  incidentCount: 0,
  incidentIds: [],
  articleCount: 0,
  articleIds: [],
  pdiCount: 0,
  pdiUserIds: [],
  priority: "Medium",
  dueDate: null,
  responsibleId: null,
  responsibleName: null,
  plannedStart: null,
  plannedEnd: null,
  result: null,
  parentTaskId: null,
  parentTitle: null,
  subtaskCount: 0,
  progress: null,
  createdById: "9e9a1111-2222-3333-4444-555566667777",
  createdByName: "Ana",
};

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
  listTasksMock.mockResolvedValue({
    items: [umaTarefa],
    totalCount: 1,
    page: 1,
    pageSize: 100,
  });
  listUsersMock.mockResolvedValue([]);
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <TasksPage />
        </MemoryRouter>
      </QueryClientProvider>
    </Provider>,
  );
}

/** O `<form>` de "Nova tarefa" / "Editar identificação — …". */
function form(): HTMLElement {
  const header = screen.getByText(/^(Nova tarefa|Editar identificação — .+)$/);
  const wrapper = header.closest("form");
  if (!wrapper) throw new Error("formulario nao encontrado");
  return wrapper;
}

/** O `<label>` do `FormField` — é ali que a validação tem que acender. */
function field(labelText: string): HTMLElement {
  const wrapper = within(form()).getByText(labelText).closest("label");
  if (!wrapper) {
    throw new Error(`campo ${labelText} sem <label> — FormField não aplicado`);
  }
  return wrapper;
}

/** Dispara o `onSubmit` real do form (padrão jsdom dos testes da suíte). */
function submit() {
  const wrapper = primaryButton().closest("form");
  if (!wrapper) throw new Error("formulario nao encontrado");
  fireEvent.submit(wrapper);
}

/** Botão primário do form (Criar tarefa / Salvar identificação). */
function primaryButton(): HTMLElement {
  return within(form()).getByRole("button", {
    name: /Criar tarefa|Salvar identificação/,
  });
}

async function openCreate(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: /Nova Tarefa/ }));
}

describe("TasksPage — erro de validação por campo", () => {
  it("should put the empty-title failure under Título, once, without repeating it", async () => {
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);

    submit();

    const mensagem = "Informe o título da tarefa.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(field("Título")).getByText(mensagem)).toBeInTheDocument();
    // Uma única vez na tela inteira: o <FormError> não repete o do campo.
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createTaskMock).not.toHaveBeenCalled();
  });

  it("should take a server refusal out of Título and put it at form level", async () => {
    createTaskMock.mockRejectedValue(new Error("Já existe uma tarefa com esse título."));
    renderPage();
    const user = userEvent.setup();
    await openCreate(user);
    await user.type(within(form()).getByPlaceholderText("Título *"), "Trocar switch");

    submit();

    const mensagem = "Já existe uma tarefa com esse título.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    // A recusa não aponta campo: fica no nível do formulário…
    expect(within(form()).getAllByText(mensagem)).toHaveLength(1);
    // …e o Título não é culpado por um erro que não é dele (bug prévio).
    expect(within(field("Título")).queryByText(/Já existe/)).toBeNull();
    expect(within(field("Tipo")).queryByText(/Já existe/)).toBeNull();
    expect(within(field("Prioridade")).queryByText(/Já existe/)).toBeNull();
    expect(createTaskMock).toHaveBeenCalledTimes(1);
  });

  it("should show a identification-refusal at form level in edit mode, where Título is not rendered", async () => {
    setTaskIdentificationMock.mockRejectedValue(
      new Error("Prazo anterior à data de abertura."),
    );
    renderPage();
    const user = userEvent.setup();

    // Modo edição abre pelo card do kanban (view default). `findBy*`: a query
    // de `listTasks` ainda não resolveu quando o componente monta.
    await user.click(
      await screen.findByTitle("Editar prioridade, prazo e responsável"),
    );
    expect(form()).toBeInTheDocument();

    fireEvent.submit(form());

    const mensagem = "Prazo anterior à data de abertura.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(form()).getAllByText(mensagem)).toHaveLength(1);
    // No modo edição o bloco Título/Tipo/Descrição não existe na árvore.
    expect(within(form()).queryByText("Título")).toBeNull();
    expect(setTaskIdentificationMock).toHaveBeenCalledTimes(1);
  });
});

/**
 * Casca do C-3: o subtítulo montava a contagem com DOIS separadores
 * diferentes — `—` em "exibindo as N primeiras" e `·` em "filtros ativos".
 * A casca padroniza tudo em `·`.
 *
 * O total continua sendo o do SERVIDOR (`totalCount`), não o da página
 * filtrada: é o que a tela já mostrava, e mudar isso é requisito novo.
 */
describe("TasksPage — casca da lista (C-3: ListPageShell)", () => {
  it("should count without a note when nothing is truncated or filtered", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { name: "Tarefas" })).toBeInTheDocument();
    expect(await screen.findByText("1 tarefa")).toBeInTheDocument();
    // Sem nota, não sobra nenhum separador — nem o travessão velho.
    expect(screen.queryByText(/—/)).toBeNull();
  });

  it("should carry the truncation note with the standard separator", async () => {
    listTasksMock.mockResolvedValue({ items: [umaTarefa], totalCount: 3, page: 1, pageSize: 1 });
    renderPage();

    expect(
      await screen.findByText("3 tarefas · exibindo as 1 primeiras"),
    ).toBeInTheDocument();
  });

  it("should mention active filters after the separator", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.type(screen.getByPlaceholderText("Buscar por título…"), "switch");
    await user.click(screen.getByRole("button", { name: "Filtrar" }));

    expect(await screen.findByText("1 tarefa · filtros ativos")).toBeInTheDocument();
  });
});

/**
 * F32-T002 — "Mostrar concluídas" (padrão Kanera/Todoist): o quadro
 * oculta Resolved/Cancelled/Closed por default; o toggle traz de volta.
 */
describe("TasksPage — concluídas ocultas por default (F32-T002)", () => {
  const tarefaConcluida = {
    ...umaTarefa,
    id: "2f1a1111-2222-3333-4444-555566667777",
    title: "Trocar toner da impressora",
    state: "Resolved",
  };

  beforeEach(() => {
    listTasksMock.mockResolvedValue({
      items: [umaTarefa, tarefaConcluida],
      totalCount: 2,
      page: 1,
      pageSize: 100,
    });
  });

  it("should hide terminal-state tasks by default and show a hidden-count note", async () => {
    renderPage();

    expect(await screen.findByText("Trocar switch do sala B")).toBeInTheDocument();
    // A concluída some do quadro…
    expect(screen.queryByText("Trocar toner da impressora")).toBeNull();
    // …e o nota avisa quantas estão ocultas.
    expect(await screen.findByText(/1 concluída\(s\) oculta\(s\)/)).toBeInTheDocument();
  });

  it("should bring completed tasks back when the Concluídas toggle is on", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: /Concluídas/ }));

    expect(await screen.findByText("Trocar toner da impressora")).toBeInTheDocument();
  });

  it("should persist the toggle in localStorage across remounts", async () => {
    const first = renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: /Concluídas/ }));
    expect(localStorage.getItem("tasks:showCompleted")).toBe("1");
    first.unmount();

    renderPage();
    expect(await screen.findByText("Trocar toner da impressora")).toBeInTheDocument();
  });

  it("should show creator name on the card when present", async () => {
    renderPage();

    expect(await screen.findByTitle("Criado por: Ana")).toBeInTheDocument();
  });
});
