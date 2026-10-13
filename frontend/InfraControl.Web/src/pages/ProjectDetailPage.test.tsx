import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { Provider } from "react-redux";
import { ProjectDetailPage } from "./ProjectDetailPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import {
  createProjectImpediment,
  deleteProject,
  deleteProjectImpediment,
  getProject,
  listProjectEvidences,
  listProjectImpediments,
  listProjectPhases,
  reopenProjectImpediment,
  resolveProjectImpediment,
  reviewProjectEvidence,
  submitProjectForApproval,
  updateProject,
  updateProjectImpediment,
  uploadProjectEvidence,
  type ProjectImpedimentItem,
  type ProjectItem,
} from "@/services/api/projects";

vi.mock("@/services/api/projects", () => ({
  approveProject: vi.fn(),
  createProjectImpediment: vi.fn(),
  createProjectInvitation: vi.fn(),
  deleteProject: vi.fn(),
  deleteProjectEvidence: vi.fn(),
  deleteProjectImpediment: vi.fn(),
  downloadProjectEvidence: vi.fn(),
  getProject: vi.fn(),
  listProjectEvidences: vi.fn(),
  listProjectImpediments: vi.fn(),
  listProjectPhases: vi.fn(),
  reopenProjectImpediment: vi.fn(),
  resolveProjectImpediment: vi.fn(),
  reviewProjectEvidence: vi.fn(),
  submitProjectForApproval: vi.fn(),
  updateProject: vi.fn(),
  updateProjectImpediment: vi.fn(),
  uploadProjectEvidence: vi.fn(),
}));

const getProjectMock = vi.mocked(getProject);
const listProjectPhasesMock = vi.mocked(listProjectPhases);
const listProjectImpedimentsMock = vi.mocked(listProjectImpediments);
const listProjectEvidencesMock = vi.mocked(listProjectEvidences);
const submitMock = vi.mocked(submitProjectForApproval);
const updateProjectMock = vi.mocked(updateProject);
const deleteProjectMock = vi.mocked(deleteProject);
const createImpedimentMock = vi.mocked(createProjectImpediment);
const resolveImpedimentMock = vi.mocked(resolveProjectImpediment);
const reopenImpedimentMock = vi.mocked(reopenProjectImpediment);
const updateImpedimentMock = vi.mocked(updateProjectImpediment);
const deleteImpedimentMock = vi.mocked(deleteProjectImpediment);
const uploadEvidenceMock = vi.mocked(uploadProjectEvidence);
const reviewEvidenceMock = vi.mocked(reviewProjectEvidence);

const project: ProjectItem = {
  id: "project-1",
  code: "PRJ-001",
  title: "Migração Core",
  description: "Atualização da infraestrutura principal.",
  justification: null,
  priority: "High",
  status: "Draft",
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

const impediment: ProjectImpedimentItem = {
  id: "imp-1",
  projectId: "project-1",
  title: "Fornecedor atrasou",
  description: "Equipamento em trânsito.",
  category: "Vendor",
  severity: "High",
  mitigationPlan: null,
  status: "Open",
  isResolved: false,
  resolvedAt: null,
};

function renderPage() {
  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={["/projects/project-1"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectDetailPage />} />
          <Route path="/projects" element={<div>Lista de projetos</div>} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

/**
 * F29-T004 — duas divergências na mesma página.
 *
 * 1. O bloco de erro era um `<div>` vermelho artesanal (mesma cor do
 *    ErrorBanner, sem semântica de alerta).
 * 2. Achado durante a tarefa e registrado aqui: quando o CARREGAMENTO falha,
 *    `project` fica null e a página caía no retorno `if (!project)` — exibindo
 *    **"Projeto não encontrado"** para um erro de rede. Não existe é diferente
 *    de não ter conseguido perguntar; quem lê "não encontrado" acha que o id
 *    está errado.
 */
describe("ProjectDetailPage — tom do erro (F29-T004)", () => {
  beforeEach(() => {
    getProjectMock.mockReset();
    listProjectPhasesMock.mockReset();
    submitMock.mockReset();
    listProjectPhasesMock.mockResolvedValue([]);
    listProjectImpedimentsMock.mockReset();
    listProjectImpedimentsMock.mockResolvedValue([]);
    listProjectEvidencesMock.mockReset();
    listProjectEvidencesMock.mockResolvedValue([]);
    updateProjectMock.mockReset();
    deleteProjectMock.mockReset();
    createImpedimentMock.mockReset();
    resolveImpedimentMock.mockReset();
    reopenImpedimentMock.mockReset();
    updateImpedimentMock.mockReset();
    deleteImpedimentMock.mockReset();
    uploadEvidenceMock.mockReset();
    reviewEvidenceMock.mockReset();
    localStorage.clear();
  });

  it("should report a failed load as an alert, not as 'project not found'", async () => {
    getProjectMock.mockRejectedValue(new Error("Não foi possível falar com o servidor."));

    renderPage();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Não foi possível falar com o servidor.");
    expect(screen.queryByText("Projeto não encontrado")).toBeNull();
  });

  it("should render a failed action as an alert too", async () => {
    getProjectMock.mockResolvedValue(project);
    submitMock.mockRejectedValue(new Error("Não foi possível enviar o projeto para homologação."));

    renderPage();

    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /enviar para homologação/i }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Não foi possível enviar o projeto para homologação.");
    expect(getProjectMock).toHaveBeenCalledTimes(1);
  });
});

/**
 * CRUD-Projetos (2026-10-12) — a tela de detalhe passou a expor U/D do
 * projeto e CRUD das sub-entidades (impedimentos + evidências) cujo backend
 * ficou pronto nesta sessão.
 */
describe("ProjectDetailPage — CRUD de projeto e sub-entidades", () => {
  beforeEach(() => {
    // Os botões de U/D e das sub-entidades só aparecem para Operator/Admin.
    store.dispatch(
      sessionChecked({
        id: "user-1",
        email: "operador@exemplo.com",
        displayName: "Operador",
        roles: ["Operator"],
      }),
    );
    getProjectMock.mockResolvedValue(project);
    listProjectImpedimentsMock.mockResolvedValue([]);
    listProjectEvidencesMock.mockResolvedValue([]);
    updateProjectMock.mockResolvedValue(project);
    deleteProjectMock.mockResolvedValue(undefined);
    createImpedimentMock.mockResolvedValue(impediment);
    resolveImpedimentMock.mockResolvedValue({ ...impediment, isResolved: true, status: "Resolved" });
    uploadEvidenceMock.mockResolvedValue(null);
    reviewEvidenceMock.mockResolvedValue(null);
  });

  it("should edit the project and refresh", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /^editar$/i }));
    const titleInput = screen.getByDisplayValue("Migração Core");
    await user.clear(titleInput);
    await user.type(titleInput, "Novo título");
    await user.click(screen.getByRole("button", { name: /^salvar$/i }));

    await screen.findByText("Projeto atualizado.");
    expect(updateProjectMock).toHaveBeenCalledWith(
      "project-1",
      expect.objectContaining({ title: "Novo título" }),
    );
  });

  it("should delete the project after confirmation and navigate away", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /^remover$/i }));
    const confirmButtons = await screen.findAllByRole("button", { name: /^remover$/i });
    const confirmButton = confirmButtons[confirmButtons.length - 1];
    if (confirmButton) await user.click(confirmButton);

    await screen.findByText("Lista de projetos");
    expect(deleteProjectMock).toHaveBeenCalledWith("project-1");
  });

  it("should create an impediment from the dialog", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /registrar impedimento/i }));
    await user.type(screen.getByLabelText("Título *"), "Novo impedimento");
    await user.type(screen.getByLabelText("Descrição *"), "Detalhe do impedimento");
    await user.click(screen.getByRole("button", { name: /^salvar$/i }));

    await screen.findByText("Impedimento registrado.");
    expect(createImpedimentMock).toHaveBeenCalledWith(
      "project-1",
      expect.objectContaining({ title: "Novo impedimento" }),
    );
  });

  it("should resolve an open impediment", async () => {
    listProjectImpedimentsMock.mockResolvedValue([impediment]);
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /marcar resolvido/i }));

    await screen.findByText("Impedimento marcado como resolvido.");
    expect(resolveImpedimentMock).toHaveBeenCalledWith("project-1", "imp-1");
  });

  it("should upload an evidence file", async () => {
    const { container } = renderPage();
    const user = userEvent.setup();

    // Espera o projeto carregar (o formulário de evidência só renderiza depois).
    await screen.findByRole("button", { name: /anexar/i });

    const file = new File(["conteudo"], "relatorio.pdf", { type: "application/pdf" });
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, file);
    await user.click(screen.getByRole("button", { name: /anexar/i }));

    await screen.findByText("Evidência anexada.");
    expect(uploadEvidenceMock).toHaveBeenCalledWith(
      expect.objectContaining({ projectId: "project-1", file }),
    );
  });

  it("should reopen a resolved impediment", async () => {
    listProjectImpedimentsMock.mockResolvedValue([
      { ...impediment, isResolved: true, status: "Resolved" },
    ]);
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /reabrir/i }));

    await screen.findByText("Impedimento reaberto.");
    expect(reopenImpedimentMock).toHaveBeenCalledWith("project-1", "imp-1");
  });
});
