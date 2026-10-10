import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { Provider } from "react-redux";
import { AccountPage } from "./AccountPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { AuthApiError } from "@/services/api/auth";

/**
 * F26-T001 — tela "Minha Conta" (troca de senha autenticada). O backend
 * `POST /auth/change-password` já existia (revoga TODAS as sessões +
 * audit); o que faltava era consumidor. Regras testadas aqui:
 *  - validação local espelha a política do servidor (12+, dígito,
 *    maiúscula, minúscula, não-alfanumérico) antes de chamar a API;
 *  - erro pt-BR do envelope (senha atual errada) aparece na tela;
 *  - sucesso = redireciona para /login (a sessão atual morreu no
 *    servidor junto com as demais).
 */
vi.mock("@/services/api/auth", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/auth")>();
  return {
    ...actual,
    changePassword: vi.fn(),
  };
});

import { changePassword } from "@/services/api/auth";

const changePasswordMock = vi.mocked(changePassword);

function renderPage() {
  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={["/account"]}>
        <AccountPage />
      </MemoryRouter>
    </Provider>,
  );
}

beforeEach(() => {
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Viewer"],
    }),
  );
  changePasswordMock.mockReset();
});

describe("AccountPage", () => {
  it("should block submission when the new password breaks the policy", async () => {
    changePasswordMock.mockResolvedValue(undefined);
    renderPage();

    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Senha atual"), "Atual!123456");
    await user.type(screen.getByLabelText("Nova senha", { selector: "input" }), "curta");
    await user.type(screen.getByLabelText("Confirmar nova senha"), "curta");
    await user.click(screen.getByRole("button", { name: /trocar senha/i }));

    expect(changePasswordMock).not.toHaveBeenCalled();
    expect(await screen.findAllByText(/pelo menos 12 caracteres/i)).not.toHaveLength(0);
  });

  it("should block submission when confirmation does not match", async () => {
    changePasswordMock.mockResolvedValue(undefined);
    renderPage();

    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Senha atual"), "Atual!123456");
    await user.type(screen.getByLabelText("Nova senha", { selector: "input" }), "Nova!Senha123");
    await user.type(screen.getByLabelText("Confirmar nova senha"), "Nova!Senha124");
    await user.click(screen.getByRole("button", { name: /trocar senha/i }));

    expect(changePasswordMock).not.toHaveBeenCalled();
    expect(await screen.findAllByText(/não confere/i)).not.toHaveLength(0);
  });

  it("should show the pt-BR reason from the envelope when the current password is wrong", async () => {
    changePasswordMock.mockRejectedValue(new AuthApiError("Senha atual incorreta.", 400));
    renderPage();

    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Senha atual"), "Errada!12345");
    await user.type(screen.getByLabelText("Nova senha", { selector: "input" }), "Nova!Senha123");
    await user.type(screen.getByLabelText("Confirmar nova senha"), "Nova!Senha123");
    await user.click(screen.getByRole("button", { name: /trocar senha/i }));

    expect(await screen.findByText("Senha atual incorreta.")).toBeInTheDocument();
  });

  it("should redirect to /login after a successful change (sessions revoked server-side)", async () => {
    changePasswordMock.mockResolvedValue(undefined);
    render(
      <Provider store={store}>
        <MemoryRouter initialEntries={["/account"]}>
          <Routes>
            <Route path="/account" element={<AccountPage />} />
            <Route path="/login" element={<div>tela de login</div>} />
          </Routes>
        </MemoryRouter>
      </Provider>,
    );

    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Senha atual"), "Atual!123456");
    await user.type(screen.getByLabelText("Nova senha", { selector: "input" }), "Nova!Senha123");
    await user.type(screen.getByLabelText("Confirmar nova senha"), "Nova!Senha123");
    await user.click(screen.getByRole("button", { name: /trocar senha/i }));

    expect(await screen.findByText("tela de login")).toBeInTheDocument();
    expect(changePasswordMock).toHaveBeenCalledWith("Atual!123456", "Nova!Senha123");
  });
});
