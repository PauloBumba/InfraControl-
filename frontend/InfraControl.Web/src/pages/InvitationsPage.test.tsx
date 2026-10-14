import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { InvitationsPage } from "./InvitationsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listInvitations } from "@/services/api/invitations";
import { updateInvitationRoles } from "@/services/api/invitations";

/**
 * F29-T001C-2 — InvitationsPage no `useFormErrors`.
 *
 * O que este teste tranca: a validação "Selecione ao menos uma role." é de um
 * GRUPO (várias checkboxes), não de um input com `FormField`. Ela já renderizava
 * logo abaixo do grupo — que é o lugar certo — e a migração preservou isso.
 * A única mudança real aqui é o estado passar pelo hook compartilhado.
 */
vi.mock("@/services/api/invitations", () => ({
  listInvitations: vi.fn(),
  updateInvitationRoles: vi.fn(),
}));

const listInvitationsMock = vi.mocked(listInvitations);
const updateRolesMock = vi.mocked(updateInvitationRoles);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "9a111111-2222-3333-4444-555566667777",
      email: "admin@exemplo.com",
      displayName: "Admin",
      roles: ["Admin"],
    }),
  );
  listInvitationsMock.mockResolvedValue({
    items: [
      {
        id: "c1",
        email: "convidado@exemplo.com",
        status: "Pending",
        expiresAt: "2026-12-31T00:00:00Z",
        createdAt: "2026-01-01T00:00:00Z",
        createdBy: "admin",
        roles: ["Viewer"],
        decidedAt: null,
      },
    ],
    totalCount: 1,
    skip: 0,
    take: 50,
  });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <InvitationsPage />
      </QueryClientProvider>
    </Provider>,
  );
}

/**
 * O diálogo de roles não tem `<form>` — o `submitRoles` é chamado por `onClick`
 * no botão "Salvar". Aqui não há a armadilha do submit implícito do jsdom.
 */
async function clickSave(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: "Salvar" }));
}

describe("InvitationsPage — validação do grupo de roles", () => {
  it("should show the message under the checkbox group when no role is selected", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /Roles/i }));

    // Desmarco a única role que veio marcada ("Viewer").
    const dialog = screen.getByRole("dialog");
    const viewer = within(dialog).getByRole("checkbox", { name: "Viewer" }) as HTMLInputElement;
    if (viewer.checked) await user.click(viewer);

    await clickSave(user);

    const mensagem = "Selecione ao menos uma role.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    // Uma vez só, e junto do grupo de checkboxes — não num rodapé solto.
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(updateRolesMock).not.toHaveBeenCalled();
  });

  it("should clear the message when a role is selected again", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /Roles/i }));
    const dialog = screen.getByRole("dialog");
    const viewer = within(dialog).getByRole("checkbox", { name: "Viewer" }) as HTMLInputElement;
    if (viewer.checked) await user.click(viewer);
    await clickSave(user);

    expect(await screen.findByText("Selecione ao menos uma role.")).toBeInTheDocument();

    await user.click(viewer);
    await clickSave(user);

    expect(screen.queryByText("Selecione ao menos uma role.")).toBeNull();
    expect(updateRolesMock).toHaveBeenCalledWith("c1", ["Viewer"]);
  });
});

/**
 * Casca do C-3: o `ListPageShell` sempre monta a contagem — a nota é o que
 * sobra depois do `·`. Hoje o "de 10" vinha colado no rótulo sem separador
 * ("3 convites de 10"); com a casca ele vira "1 convite · de 2".
 */
describe("InvitationsPage — casca da lista (C-3: ListPageShell)", () => {
  it("should count without a note when the filter hides nothing", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { name: "Convites" })).toBeInTheDocument();
    expect(await screen.findByText("1 convite")).toBeInTheDocument();
    expect(screen.queryByText(/·/)).toBeNull();
  });

  it("should carry the server total as the note after the separator", async () => {
    listInvitationsMock.mockResolvedValue({
      items: [
        {
          id: "c1",
          email: "ana@exemplo.com",
          status: "Pending",
          expiresAt: "2026-12-31T00:00:00Z",
          createdAt: "2026-01-01T00:00:00Z",
          createdBy: "admin",
          roles: ["Viewer"],
          decidedAt: null,
        },
        {
          id: "c2",
          email: "bruno@exemplo.com",
          status: "Pending",
          expiresAt: "2026-12-31T00:00:00Z",
          createdAt: "2026-01-01T00:00:00Z",
          createdBy: "admin",
          roles: ["Viewer"],
          decidedAt: null,
        },
      ],
      totalCount: 2,
      skip: 0,
      take: 50,
    });
    renderPage();
    const user = userEvent.setup();

    await user.type(await screen.findByPlaceholderText("Buscar por e-mail..."), "ana@");

    expect(await screen.findByText("1 convite · de 2")).toBeInTheDocument();
  });
});
