import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { UsersPage } from "./UsersPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";

/**
 * F29-T001C-3 — UsersPage na casca padrão (`ListPageShell`).
 *
 * O que este teste tranca: o subtítulo sai de
 * `countLabel(total, "usuário", "usuários") · ${USER_ROLES.length} roles`,
 * que é exatamente o texto que a página construía à mão — inclusive o singular
 * de 1 e a nota de roles.
 */
vi.mock("@/services/api/adminUsers", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/adminUsers")>();
  return { ...actual, listUsers: vi.fn() };
});

const listUsersMock = vi.mocked(listUsers);

const umUser: AdminUser = {
  userId: "u1",
  email: "ana@exemplo.com",
  displayName: "Ana",
  isActive: true,
  roles: [2],
  updatedAt: "2026-01-01T00:00:00Z",
};

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "admin@exemplo.com",
      displayName: "Admin",
      roles: ["Admin"],
    }),
  );
  listUsersMock.mockResolvedValue([umUser]);
});

function renderPage() {
  return render(
    <Provider store={store}>
      <UsersPage />
    </Provider>,
  );
}

describe("UsersPage — casca da lista (C-3: ListPageShell)", () => {
  it("should count singularly and keep the roles note", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Usuários & Roles" }),
    ).toBeInTheDocument();
    // `USER_ROLES` tem 3 papéis — a nota é literal, não passa pelo countLabel.
    expect(await screen.findByText("1 usuário · 3 roles")).toBeInTheDocument();
    expect(screen.queryByText("1 usuários · 3 roles")).toBeNull();
  });

  it("should pluralize the head count without touching the note", async () => {
    listUsersMock.mockResolvedValue([
      umUser,
      { ...umUser, userId: "u2", email: "bia@exemplo.com", displayName: "Bia" },
    ]);
    renderPage();

    expect(await screen.findByText("2 usuários · 3 roles")).toBeInTheDocument();
    expect(screen.queryByText("2 usuário · 3 roles")).toBeNull();
  });
});
