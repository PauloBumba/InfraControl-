import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { Provider } from "react-redux";
import { KnowledgePage } from "./KnowledgePage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";

/**
 * F24 reconstrução — KnowledgePage com edição, exclusão, troca de status
 * (matriz ADR-034), gerenciamento de categorias e as 3 visões.
 *
 * O merge 0d3d417/91de6be deixou uma versão INCOMPLETA destas features na
 * página (chamava funções que ninguém exportava) e quebrava o build; este
 * arquivo tranca o comportamento reconstruído:
 *  - edição exige o motivo (K3: o PUT {id} versiona);
 *  - o seletor de status só oferece transições válidas (ADR-034);
 *  - exclusão passa por confirmação;
 *  - categoria é criada pelo formulário de CRUD.
 *
 * Backend continua autoridade — aqui só o encaminhamento certo da UI.
 */
vi.mock("@/services/api/knowledge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/knowledge")>();
  return {
    ...actual,
    listArticles: vi.fn(),
    listCategories: vi.fn(),
    createArticle: vi.fn(),
    updateArticle: vi.fn(),
    deleteArticle: vi.fn(),
    changeArticleStatus: vi.fn(),
    createCategory: vi.fn(),
    updateCategory: vi.fn(),
    deleteCategory: vi.fn(),
  };
});

vi.mock("@/services/api/adminUsers", () => ({
  listUsers: vi.fn().mockResolvedValue([]),
}));

/**
 * O `SelectField` é um wrapper do `Dropdown` do PrimeReact, cujo painel de
 * opções só existe em portal/DOM real (não abre no jsdom). Trocamos por um
 * `<select>` nativo para testar o que INTERESSA aqui: quais opções a
 * PÁGINA passa ao campo (a matriz ADR-034) e o valor que ela envia.
 */
vi.mock("@/components/ui/SelectField", () => ({
  SelectField: (props: {
    value: string;
    onChange: (value: string) => void;
    options: { label: string; value: string }[];
    "aria-label"?: string;
  }) => (
    <select
      aria-label={props["aria-label"]}
      value={props.value}
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

import {
  ARTICLE_STATUS_DRAFT,
  changeArticleStatus,
  createArticle,
  createCategory,
  deleteArticle,
  listArticles,
  listCategories,
  updateArticle,
  type KnowledgeArticle,
  type KnowledgeCategory,
  type Paged,
} from "@/services/api/knowledge";

const listArticlesMock = vi.mocked(listArticles);
const listCategoriesMock = vi.mocked(listCategories);
const createArticleMock = vi.mocked(createArticle);
const updateArticleMock = vi.mocked(updateArticle);
const deleteArticleMock = vi.mocked(deleteArticle);
const changeArticleStatusMock = vi.mocked(changeArticleStatus);
const createCategoryMock = vi.mocked(createCategory);

function makeArticle(overrides: Partial<KnowledgeArticle> = {}): KnowledgeArticle {
  return {
    id: "art-1",
    title: "Procedimento de troca de switch",
    content: "Passo 1: desligar a energia do rack.",
    categoryId: null,
    status: ARTICLE_STATUS_DRAFT,
    type: "Procedure",
    description: "Como trocar o switch principal",
    tags: ["switch"],
    authorId: "u-1",
    authorName: "Ana Souza",
    responsibleId: null,
    responsibleName: null,
    version: "1.0",
    origins: [],
    nextReviewAt: null,
    reviewIntervalDays: null,
    reviewResponsibleId: null,
    reviewResponsibleName: null,
    pendingReview: false,
    deviceIds: [],
    incidentIds: [],
    serviceIds: [],
    ...overrides,
  };
}

function pagedArticles(items: KnowledgeArticle[]): Paged<KnowledgeArticle> {
  return { items, totalCount: items.length, page: 1, pageSize: 20 };
}

function pagedCategories(items: KnowledgeCategory[]): Paged<KnowledgeCategory> {
  return { items, totalCount: items.length, page: 1, pageSize: 20 };
}

function renderPage() {
  return render(
    <Provider store={store}>
      <MemoryRouter>
        <KnowledgePage />
      </MemoryRouter>
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
      roles: ["Operator"],
    }),
  );
  listArticlesMock.mockResolvedValue(pagedArticles([makeArticle()]));
  listCategoriesMock.mockResolvedValue(
    pagedCategories([{ id: "cat-1", name: "Redes", description: null }]),
  );
  updateArticleMock.mockResolvedValue(null);
  deleteArticleMock.mockResolvedValue(null);
  changeArticleStatusMock.mockResolvedValue(makeArticle());
  createCategoryMock.mockResolvedValue({ id: "cat-2", name: "Novo", description: null });
});

describe("KnowledgePage", () => {
  it("should render the articles returned by the API", async () => {
    renderPage();

    expect(await screen.findByText("Procedimento de troca de switch")).toBeInTheDocument();
    expect(screen.getByText("Como trocar o switch principal")).toBeInTheDocument();
  });

  it("should switch between the grid, table and kanban views", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();

    const switcher = await screen.findByLabelText("Modo de visualização");

    // Grid (default da página): cards, sem <table>.
    expect(within(switcher).getByRole("tab", { name: "Grid" })).toBeInTheDocument();
    expect(container.querySelector("table")).toBeNull();

    // Tabela: colunas do DataTable (a coluna "Categoria" só existe aqui).
    await user.click(within(switcher).getByRole("tab", { name: "Tabela" }));
    expect(await screen.findByText("Categoria")).toBeInTheDocument();
    expect(container.querySelector("table")).not.toBeNull();

    // Kanban: colunas por status — as 8 aparecem, inclusive as que nenhum
    // artigo ocupa ("Obsoleta" não está em nenhum card/grid).
    await user.click(within(switcher).getByRole("tab", { name: "Kanban" }));
    expect(await screen.findByText("Obsoleta")).toBeInTheDocument();
    // "Rascunho" existe na coluna do quadro E no chip do card.
    expect(screen.getAllByText("Rascunho").length).toBeGreaterThan(0);
  });

  it("should block the edit until the change reason is filled (K3 versiona)", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Editar" }));
    const dialog = await screen.findByRole("dialog", { name: "Editar artigo" });
    await user.type(within(dialog).getByLabelText(/^título/i), " — revisado");
    await user.click(within(dialog).getByRole("button", { name: "Salvar" }));

    expect(updateArticleMock).not.toHaveBeenCalled();
    expect(await within(dialog).findByText(/informe o motivo/i)).toBeInTheDocument();
  });

  it("should save the edit sending the reason to PUT /knowledge/articles/{id}", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Editar" }));
    const dialog = await screen.findByRole("dialog", { name: "Editar artigo" });
    await user.type(within(dialog).getByLabelText(/^título/i), " — revisado");
    await user.type(within(dialog).getByLabelText(/motivo/i), "corrige passo do procedimento");
    await user.click(within(dialog).getByRole("button", { name: "Salvar" }));

    expect(updateArticleMock).toHaveBeenCalledWith("art-1", expect.objectContaining({
      title: "Procedimento de troca de switch — revisado",
      reason: "corrige passo do procedimento",
    }));
  });

  it("should offer only valid ADR-034 transitions for a Draft article", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Status" }));
    const dialog = await screen.findByRole("dialog", { name: "Alterar status" });

    // Rascunho → apenas "Em revisão" (matriz de 16 pares; nenhum destino
    // tipo "Publicada" é oferecido para um rascunho).
    const labels = within(dialog)
      .getAllByRole("option")
      .map((option) => option.textContent?.trim());
    expect(labels).toEqual(["Novo status", "Em revisão"]);
    expect(labels).not.toContain("Publicada");
  });

  it("should send the chosen status as a number when applying the transition", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Status" }));
    const dialog = await screen.findByRole("dialog", { name: "Alterar status" });
    await user.selectOptions(within(dialog).getByLabelText("Novo status"), "3");
    await user.click(within(dialog).getByRole("button", { name: "Aplicar" }));

    // enum sem JsonStringEnumConverter — viaja como NÚMERO (3 = InReview).
    expect(changeArticleStatusMock).toHaveBeenCalledWith("art-1", 3);
  });

  it("should delete an article only after the confirmation dialog", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Excluir" }));
    expect(deleteArticleMock).not.toHaveBeenCalled();

    const confirm = await screen.findByRole("dialog", { name: "Excluir artigo" });
    await user.click(within(confirm).getByRole("button", { name: "Excluir" }));

    expect(deleteArticleMock).toHaveBeenCalledWith("art-1");
  });

  it("should create a category through the management form", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /nova categoria/i }));
    const dialog = await screen.findByRole("dialog", { name: "Nova categoria" });
    await user.type(within(dialog).getByLabelText(/^nome/i), "Servidores");
    await user.click(within(dialog).getByRole("button", { name: "Salvar" }));

    expect(createCategoryMock).toHaveBeenCalledWith({ name: "Servidores", description: undefined });
    expect(listCategoriesMock).toHaveBeenCalled();
  });

  it("should keep the header count exactly as the shell builds it (C-3)", async () => {
    listArticlesMock.mockResolvedValue(pagedArticles([makeArticle()]));
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Base de Conhecimento" }),
    ).toBeInTheDocument();
    // Singular: `countLabel(1, "artigo", "artigos")` → "1 artigo".
    expect(await screen.findByText("1 artigo")).toBeInTheDocument();
    expect(screen.queryByText("1 artigos")).toBeNull();
  });

  it("should pluralize the header count (C-3)", async () => {
    listArticlesMock.mockResolvedValue(
      pagedArticles([makeArticle(), makeArticle({ id: "art-2" })]),
    );
    renderPage();

    expect(await screen.findByText("2 artigos")).toBeInTheDocument();
    expect(screen.queryByText("2 artigo")).toBeNull();
  });
});

// ── F29-T001C-2 — C-2: cada validação no campo dela ───────────────────
//
// A página tem 3 estados de erro com validações de campo, um por form, e o
// quarto (`statusError`) fica de fora de propósito: ele só recebe recusa do
// servidor, que já vive no nível do formulário — migrá-lo seria trocar um
// `useState` por um hook que devolve sempre `field = null`.
//
// O que estes testes tranca, decisão por decisão:
//
// 1. criação: "Informe o título do artigo." → campo Título, uma única vez
//    na tela (o `FormError` não repete), sem mutation;
// 2. criação: "Informe o conteúdo do artigo." → campo Conteúdo, e o Título
//    já válido não é culpado;
// 3. criação: recusa do servidor → no formulário, longe dos dois campos;
// 4. edição: "Informe o motivo da alteração (vira version — K3)." → campo
//    Motivo, sem culpar Título/Conteúdo pré-preenchidos (K3 versiona);
// 5. categoria: "Informe o nome da categoria." → campo Nome.
//
// Antes da migração os 5 iam todos para o mesmo `<FormError>` no pé do form
// (e a criação nem tinha estrutura de campo — os 7 controles eram inputs com
// `placeholder` dentro de um `grid`).

/** O `<form>` de criação — o único sem dialog; acha pelo cabeçalho dele. */
function createForm(): HTMLElement {
  const header = screen.getByText("Novo artigo");
  const wrapper = header.closest("form");
  if (!wrapper) throw new Error("formulario de criacao nao encontrado");
  return wrapper;
}

/** O `<form>` dentro de um dialog, pelo botão primário "Salvar…". */
function dialogForm(header: "Editar artigo" | "Nova categoria"): HTMLElement {
  const dialog = screen.getByRole("dialog", { name: header });
  const wrapper = within(dialog)
    .getByRole("button", { name: /Salvar/ })
    .closest("form");
  if (!wrapper) throw new Error(`formulario do dialog ${header} nao encontrado`);
  return wrapper;
}

/**
 * O wrapper de campo — é ali que a validação tem que acender.
 *
 * Casa pelo **texto inicial** do label e não por `getByLabelText`: quando o
 * erro acende, a mensagem entra DENTRO do mesmo `<label>` e o `textContent`
 * deixa de ser "Conteúdo" (vira "ConteúdoInforme o conteúdo do artigo."), então
 * o match exato do RTL não acha mais nada. Mesmo procedimento da `PdiPage`.
 */
function fieldIn(container: HTMLElement, labelText: string): HTMLElement {
  const labels = Array.from(container.querySelectorAll("label"));
  const found = labels.find((label) => (label.textContent ?? "").trim().startsWith(labelText));
  if (!found) throw new Error(`campo ${labelText} nao encontrado no container`);
  return found;
}

describe("KnowledgePage — C-2: cada validação no campo dela", () => {
  it("should put the empty-title failure under Título, and only there", async () => {
    renderPage();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /Novo Artigo/ }));

    fireEvent.submit(createForm());

    const mensagem = "Informe o título do artigo.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(createForm(), "Título")).getByText(mensagem)).toBeInTheDocument();
    // Uma única vez na tela inteira: o <FormError> não repete o do campo.
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createArticleMock).not.toHaveBeenCalled();
  });

  it("should put the empty-content failure under Conteúdo, without blaming Título", async () => {
    renderPage();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /Novo Artigo/ }));
    await user.type(
      within(createForm()).getByPlaceholderText("Título * (máx. 200)"),
      "Troca de switch",
    );

    fireEvent.submit(createForm());

    const mensagem = "Informe o conteúdo do artigo.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(createForm(), "Conteúdo")).getByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(createForm(), "Título")).queryByText(/informe o conteúdo/)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createArticleMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from Título and Conteúdo", async () => {
    createArticleMock.mockRejectedValue(new Error("Já existe um artigo com esse título."));
    renderPage();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /Novo Artigo/ }));
    await user.type(
      within(createForm()).getByPlaceholderText("Título * (máx. 200)"),
      "Troca de switch",
    );
    await user.type(
      within(createForm()).getByPlaceholderText("Conteúdo * (máx. 20000)"),
      "Passo 1: desligar a energia.",
    );

    fireEvent.submit(createForm());

    const mensagem = "Já existe um artigo com esse título.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(createForm()).getAllByText(mensagem)).toHaveLength(1);
    expect(within(fieldIn(createForm(), "Título")).queryByText(/já existe/)).toBeNull();
    expect(within(fieldIn(createForm(), "Conteúdo")).queryByText(/já existe/)).toBeNull();
    expect(createArticleMock).toHaveBeenCalledTimes(1);
  });

  it("should put the missing-reason failure under Motivo, without blaming the filled fields", async () => {
    renderPage();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Editar" }));
    const dialog = await screen.findByRole("dialog", { name: "Editar artigo" });

    // Título e Conteúdo vêm pré-preenchidos do artigo — só falta o motivo.
    fireEvent.submit(dialogForm("Editar artigo"));

    const mensagem = "Informe o motivo da alteração (vira version — K3).";
    expect(await within(dialog).findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(dialog, "Motivo")).getByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(dialog, "Título")).queryByText(/informe o motivo/)).toBeNull();
    expect(within(fieldIn(dialog, "Conteúdo")).queryByText(/informe o motivo/)).toBeNull();
    expect(within(dialog).getAllByText(mensagem)).toHaveLength(1);
    expect(updateArticleMock).not.toHaveBeenCalled();
  });

  it("should put the empty-name failure under Nome in the category dialog", async () => {
    renderPage();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /nova categoria/i }));
    const dialog = await screen.findByRole("dialog", { name: "Nova categoria" });

    fireEvent.submit(dialogForm("Nova categoria"));

    const mensagem = "Informe o nome da categoria.";
    expect(await within(dialog).findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(dialog, "Nome")).getByText(mensagem)).toBeInTheDocument();
    expect(within(dialog).getAllByText(mensagem)).toHaveLength(1);
    expect(createCategoryMock).not.toHaveBeenCalled();
  });
});
