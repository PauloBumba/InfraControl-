import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
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
});
