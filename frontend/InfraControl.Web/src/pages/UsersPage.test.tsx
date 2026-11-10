import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { Provider } from "react-redux";
import { UsersPage } from "./UsersPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listUsers, setUserStatus, type AdminUser } from "@/services/api/adminUsers";

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
  return { ...actual, listUsers: vi.fn(), setUserStatus: vi.fn() };
});

const listUsersMock = vi.mocked(listUsers);
const setUserStatusMock = vi.mocked(setUserStatus);

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

/**
 * F50-T001 — habilitar/desabilitar DIRETO na listagem.
 *
 * O que estes testes tranca: (1) a coluna Status ganha um `SwitchField`
 * (role="switch") para quem tem `administer`, exibindo o estado atual;
 * (2) o clique NÃO dispara a mutação de imediato — abre o mesmo
 * `ConfirmDialog` da ação antiga (desativar revoga sessões ativas);
 * (3) a confirmação chama `setUserStatus(id, !isActive)` e recarrega a
 * lista; (4) sem `administer` (Viewer) não há switch — só o chip.
 */
describe("UsersPage — toggle de status na listagem (F50-T001)", () => {
  it("should render a switch in the Status column for admins", async () => {
    renderPage();

    const toggle = await screen.findByRole("switch", { name: "Ana: ativo" });
    expect(toggle).toBeInTheDocument();
    expect(toggle).toBeChecked();
  });

  it("should confirm before calling setUserStatus(false) and reload", async () => {
    renderPage();

    fireEvent.click(await screen.findByRole("switch", { name: "Ana: ativo" }));

    const dialog = await screen.findByRole("dialog", { name: "Desativar usuário" });
    expect(dialog).toHaveTextContent(
      'Tem certeza que deseja desativar o usuário "Ana"? Ele não poderá mais acessar o sistema.',
    );
    // A mutação só acontece APÓS a confirmação.
    expect(setUserStatusMock).not.toHaveBeenCalled();

    fireEvent.click(within(dialog).getByRole("button", { name: "Desativar" }));

    await waitFor(() => expect(setUserStatusMock).toHaveBeenCalledWith("u1", false));
    await waitFor(() => expect(listUsersMock).toHaveBeenCalledTimes(2));
  });

  it("should activate an inactive user through the switch", async () => {
    listUsersMock.mockResolvedValue([{ ...umUser, isActive: false }]);
    renderPage();

    const toggle = await screen.findByRole("switch", { name: "Ana: inativo" });
    expect(toggle).not.toBeChecked();
    fireEvent.click(toggle);

    const dialog = await screen.findByRole("dialog", { name: "Ativar usuário" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Ativar" }));

    await waitFor(() => expect(setUserStatusMock).toHaveBeenCalledWith("u1", true));
  });

  it("should not render the switch for viewers (no administer)", async () => {
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "viewer@exemplo.com",
        displayName: "Viewer",
        roles: ["Viewer"],
      }),
    );
    renderPage();

    expect(await screen.findByText("1 usuário · 3 roles")).toBeInTheDocument();
    expect(screen.queryByRole("switch")).toBeNull();
    // O chip de status continua visível para leitura.
    expect(screen.getByText("Ativo")).toBeInTheDocument();
  });
});
