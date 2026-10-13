import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { MemoryRouter, useLocation } from "react-router-dom";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { ProjectsPage } from "./ProjectsPage";
import {
  createProject,
  deleteProject,
  listProjects,
  updateProject,
  type ProjectItem,
} from "@/services/api/projects";

vi.mock("@/services/api/projects", () => ({
  listProjects: vi.fn(),
  createProject: vi.fn(),
  updateProject: vi.fn(),
  deleteProject: vi.fn(),
}));

const CURRENT_USER_ID = "3f1a1111-2222-3333-4444-555566667777";

vi.mock("@/components/ui/MetricChart", () => ({
  MetricChart: ({ ariaLabel }: { ariaLabel: string }) => <div role="img" aria-label={ariaLabel} />,
}));

const project: ProjectItem = {
  id: "project-1",
  code: "PRJ-001",
  title: "Migração Core",
  description: "Atualização da infraestrutura principal.",
  justification: null,
  priority: "High",
  status: "InProgress",
  leaderId: "leader-1",
  managerId: null,
  adminApproverId: null,
  approvalNotes: null,
  approvedAt: null,
  plannedStartDate: "2026-10-01",
  plannedEndDate: "2026-10-31",
  actualStartDate: null,
  actualEndDate: null,
  members: [{ userId: "member-1", role: "Member" }],
  createdById: "creator-1",
};

function CurrentPath() {
  const location = useLocation();
  return <output>{location.pathname}</output>;
}

function renderPage() {
  return render(
    <Provider store={store}>
      <MemoryRouter>
        <ProjectsPage />
        <CurrentPath />
      </MemoryRouter>
    </Provider>,
  );
}

describe("ProjectsPage", () => {
  beforeEach(() => {
    vi.mocked(listProjects).mockReset();
    vi.mocked(createProject).mockReset();
    vi.mocked(updateProject).mockReset();
    vi.mocked(deleteProject).mockReset();
    // a preferência de visão vive no localStorage — zera p/ default determinístico
    localStorage.clear();
    store.dispatch(
      sessionChecked({
        id: CURRENT_USER_ID,
        email: "ana@exemplo.com",
        displayName: "Ana",
        roles: ["Operator"],
      }),
    );
  });

  it("shows the overview and navigates to a project from the paged responsive table", async () => {
    vi.mocked(listProjects).mockResolvedValue({
      items: [project],
      totalCount: 25,
      page: 1,
      pageSize: 20,
    });

    renderPage();

    expect(await screen.findByText("Migração Core")).toBeInTheDocument();
    expect(screen.getByText("Total encontrado").nextElementSibling).toHaveTextContent("25");
    expect(screen.getByRole("img", { name: "Quantidade de projetos por status na página atual" }))
      .toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Ver projeto Migração Core" }));
    expect(screen.getByText("/projects/project-1")).toBeInTheDocument();
    expect(listProjects).toHaveBeenCalledWith({
      term: undefined,
      status: undefined,
      priority: undefined,
      page: 1,
      pageSize: 20,
    });
  });

  it("should switch between table, grid and kanban (quadro por status)", async () => {
    vi.mocked(listProjects).mockResolvedValue({
      items: [project],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });

    const { container } = renderPage();
    const user = userEvent.setup();
    await screen.findByText("Migração Core");

    // Grid: cards, sem <table>.
    await user.click(screen.getByRole("tab", { name: "Grid" }));
    expect(container.querySelector("table")).toBeNull();
    expect(screen.getAllByText("PRJ-001").length).toBeGreaterThan(0);

    // Kanban: as 7 colunas de status — inclusive as sem nenhum projeto.
    // (o mesmo rótulo existe no <option> do filtro de status, por isso All)
    await user.click(screen.getByRole("tab", { name: "Kanban" }));
    expect(container.querySelector("table")).toBeNull();
    expect(screen.getAllByText("Aguardando homologação").length).toBeGreaterThan(1);
    expect(screen.getAllByText("Rascunho").length).toBeGreaterThan(1);
  });

  /**
   * F29-T004 — divergência de tom. O resto do app usa ErrorBanner (role=alert)
   * em 27 páginas; aqui o erro era um <div> vermelho artesanal: mesma cor,
   * porém sem semântica — leitor de tela não anuncia e ninguém percebe que é
   * falha de carregamento.
   */
  it("should render a load failure as an alert, not a plain red div", async () => {
    vi.mocked(listProjects).mockRejectedValue(
      new Error("Não foi possível falar com o servidor."),
    );

    renderPage();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Não foi possível falar com o servidor.");
  });

  it("creates a project through the new-project dialog (leader = current user)", async () => {
    vi.mocked(listProjects).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(createProject).mockResolvedValue(null);

    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Novo projeto" }));
    await user.type(screen.getByLabelText("Código *"), "PRJ-009");
    await user.type(screen.getByLabelText("Título *"), "Novo Núcleo");
    await user.type(screen.getByLabelText("Descrição *"), "Descrição do novo projeto.");
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(createProject).toHaveBeenCalledWith({
      code: "PRJ-009",
      title: "Novo Núcleo",
      description: "Descrição do novo projeto.",
      leaderId: CURRENT_USER_ID,
      priority: "Medium",
      justification: null,
      plannedStartDate: null,
      plannedEndDate: null,
    });
  });

  it("edits a project through the edit dialog, keeping code and leader", async () => {
    vi.mocked(listProjects).mockResolvedValue({ items: [project], totalCount: 1, page: 1, pageSize: 20 });
    vi.mocked(updateProject).mockResolvedValue(null);

    renderPage();
    const user = userEvent.setup();

    await screen.findByText("Migração Core");
    await user.click(screen.getByRole("button", { name: "Editar projeto Migração Core" }));

    const title = screen.getByLabelText("Título *");
    await user.clear(title);
    await user.type(title, "Migração Core v2");
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(updateProject).toHaveBeenCalledWith(project.id, expect.objectContaining({
      title: "Migração Core v2",
      description: project.description,
      priority: project.priority,
    }));
  });

  it("deletes a project after confirmation", async () => {
    vi.mocked(listProjects).mockResolvedValue({ items: [project], totalCount: 1, page: 1, pageSize: 20 });
    vi.mocked(deleteProject).mockResolvedValue(undefined);

    renderPage();
    const user = userEvent.setup();

    await screen.findByText("Migração Core");
    await user.click(screen.getByRole("button", { name: "Remover projeto Migração Core" }));
    await user.click(screen.getByRole("button", { name: "Remover" }));

    expect(deleteProject).toHaveBeenCalledWith(project.id);
  });
});
