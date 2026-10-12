import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { Provider } from "react-redux";
import { ProjectDetailPage } from "./ProjectDetailPage";
import { store } from "@/app/store";
import {
  getProject,
  listProjectPhases,
  submitProjectForApproval,
  type ProjectItem,
} from "@/services/api/projects";

vi.mock("@/services/api/projects", () => ({
  approveProject: vi.fn(),
  createProjectInvitation: vi.fn(),
  getProject: vi.fn(),
  listProjectPhases: vi.fn(),
  submitProjectForApproval: vi.fn(),
}));

const getProjectMock = vi.mocked(getProject);
const listProjectPhasesMock = vi.mocked(listProjectPhases);
const submitMock = vi.mocked(submitProjectForApproval);

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

function renderPage() {
  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={["/projects/project-1"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectDetailPage />} />
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
