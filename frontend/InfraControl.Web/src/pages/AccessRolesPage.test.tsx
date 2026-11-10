import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AccessRolesPage } from "./AccessRolesPage";
import {
  createAccessRole,
  deleteAccessRole,
  getUserAccessRoles,
  listAccessRoles,
  setUserAccessRoles,
  type AccessRoleItem,
} from "@/services/api/accessPolicies";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";

vi.mock("@/services/api/accessPolicies", () => ({
  listAccessRoles: vi.fn(),
  createAccessRole: vi.fn(),
  updateAccessRole: vi.fn(),
  deleteAccessRole: vi.fn(),
  getUserAccessRoles: vi.fn(),
  setUserAccessRoles: vi.fn(),
}));

vi.mock("@/services/api/adminUsers", () => ({
  listUsers: vi.fn(),
}));

const listRolesMock = vi.mocked(listAccessRoles);
const createMock = vi.mocked(createAccessRole);
const deleteMock = vi.mocked(deleteAccessRole);
const getAssignMock = vi.mocked(getUserAccessRoles);
const setAssignMock = vi.mocked(setUserAccessRoles);
const listUsersMock = vi.mocked(listUsers);

const ROLE: AccessRoleItem = {
  id: "11111111-1111-1111-1111-111111111111",
  key: "auditor",
  label: "Auditor",
  description: "Somente leitura de auditoria",
  userCount: 2,
};

const USER: AdminUser = {
  userId: "22222222-2222-2222-2222-222222222222",
  email: "ana@exemplo.test",
  displayName: "Ana",
  isActive: true,
  roles: [],
  updatedAt: "2026-11-09T10:00:00Z",
};

function renderPage() {
  return render(<AccessRolesPage />);
}

describe("AccessRolesPage — padrão da casa", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listRolesMock.mockResolvedValue([ROLE]);
    listUsersMock.mockResolvedValue([USER]);
    createMock.mockResolvedValue(ROLE);
    deleteMock.mockResolvedValue(undefined);
    getAssignMock.mockResolvedValue([]);
    setAssignMock.mockResolvedValue([]);
  });

  it("should use ListPageShell title and count the roles", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Perfis de acesso" }),
    ).toBeInTheDocument();
    expect(await screen.findByText(/1 perfil/)).toBeInTheDocument();
    expect(screen.getAllByText("Auditor").length).toBeGreaterThan(0);
  });

  it("should not show raw ProgressSpinner while loading", () => {
    listRolesMock.mockReturnValue(new Promise(() => {}));
    listUsersMock.mockReturnValue(new Promise(() => {}));
    const { container } = renderPage();

    expect(container.querySelector(".p-progress-spinner")).toBeNull();
    expect(screen.getByText("Carregando…")).toBeInTheDocument();
  });

  it("should open the house Dialog to create a profile", async () => {
    renderPage();
    const user = userEvent.setup();

    await screen.findByRole("heading", { name: "Perfis de acesso" });
    await user.click(screen.getByRole("button", { name: /Novo perfil/ }));

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(screen.getByLabelText(/Nome de exibição/)).toBeInTheDocument();
  });
});
