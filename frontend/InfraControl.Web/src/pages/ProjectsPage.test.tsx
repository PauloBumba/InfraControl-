import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { ProjectsPage } from "./ProjectsPage";
import { listProjects, type ProjectItem } from "@/services/api/projects";

vi.mock("@/services/api/projects", () => ({
  listProjects: vi.fn(),
}));

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

describe("ProjectsPage", () => {
  beforeEach(() => {
    vi.mocked(listProjects).mockReset();
  });

  it("shows the overview and navigates to a project from the paged responsive table", async () => {
    vi.mocked(listProjects).mockResolvedValue({
      items: [project],
      totalCount: 25,
      page: 1,
      pageSize: 20,
    });

    render(
      <MemoryRouter>
        <ProjectsPage />
        <CurrentPath />
      </MemoryRouter>,
    );

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
});
