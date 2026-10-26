import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
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
    updateProfile: vi.fn(),
  };
});

import { changePassword, updateProfile } from "@/services/api/auth";

const changePasswordMock = vi.mocked(changePassword);
const updateProfileMock = vi.mocked(updateProfile);

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
  updateProfileMock.mockReset();
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

/**
 * F29-T002 — perfil de autoedição do nome completo.
 *
 * O `User.DisplayName` já existia no backend, mas só o admin o editava
 * (PUT /admin/users/{id}). O que se tranca aqui:
 *  - o formulário nasce preenchido com o nome da sessão;
 *  - só o NOME vai para a API (o e-mail é somente leitura — trocar e-mail é
 *    operação de admin com verificação de unicidade);
 *  - erro do envelope aparece na tela;
 *  - sucesso NÃO desloga — diferente da troca de senha, que revoga todas as
 *    sessões no servidor e por isso manda para /login.
 */
describe("AccountPage — perfil (F29-T002)", () => {
  it("should prefill the profile form with the signed-in display name", async () => {
    updateProfileMock.mockResolvedValue({ displayName: "Ana", fullName: null });
    renderPage();

    expect(await screen.findByLabelText("Nome de exibição")).toHaveValue("Ana");
    // Nome completo é o campo SEPARADO (F38) e nasce em branco aqui.
    expect(screen.getByLabelText("Nome completo")).toHaveValue("");
    // O e-mail está na tela como campo travado, não como texto solto —
    // é ele quem declara a imutabilidade, não uma frase avulsa.
    const email = screen.getByLabelText("E-mail");
    expect(email).toHaveValue("ana@exemplo.com");
    expect(email).toBeDisabled();
  });

  it("should send only the new display name", async () => {
    updateProfileMock.mockResolvedValue({ displayName: "Ana Maria", fullName: null });
    renderPage();

    const user = userEvent.setup();
    const field = await screen.findByLabelText("Nome de exibição");
    await user.clear(field);
    await user.type(field, "Ana Maria");
    await user.click(screen.getByRole("button", { name: /salvar/i }));

    // Sob contenção o clique devolve antes da cadeia do handler invocar a
    // mutation — espera o chamado em vez de assumir a microtask.
    await waitFor(() => {
      expect(updateProfileMock).toHaveBeenCalledTimes(1);
      // o nome completo viaja junto (F38) — em branco = limpar no servidor.
      expect(updateProfileMock).toHaveBeenCalledWith("Ana Maria", "");
    });
    // o e-mail não viaja: é somente leitura nesta tela.
    expect(updateProfileMock).not.toHaveBeenCalledWith("ana@exemplo.com", expect.anything());
  });

  it("should send the full name alongside the display name (F38)", async () => {
    updateProfileMock.mockResolvedValue({
      displayName: "Ana",
      fullName: "Ana Maria da Silva",
    });
    renderPage();

    const user = userEvent.setup();
    const field = await screen.findByLabelText("Nome completo");
    await user.type(field, "Ana Maria da Silva");
    await user.click(screen.getByRole("button", { name: /salvar/i }));

    await waitFor(() => {
      expect(updateProfileMock).toHaveBeenCalledWith("Ana", "Ana Maria da Silva");
    });

    // o nome completo volta para a sessão (o /auth/me lê a claim re-emitida)
    expect(await screen.findByRole("status")).toHaveTextContent(/perfil atualizado/i);
    expect(store.getState().auth.user?.fullName).toBe("Ana Maria da Silva");
    expect(store.getState().auth.status).toBe("authenticated");
  });

  it("should show the pt-BR envelope message when the name is rejected", async () => {
    updateProfileMock.mockRejectedValue(
      new AuthApiError("O nome de exibição é obrigatório.", 400),
    );
    renderPage();

    const user = userEvent.setup();
    const field = await screen.findByLabelText("Nome de exibição");
    // Só espaços: passa pelo `required` nativo (o valor NÃO é vazio) e só o
    // servidor recusa — é ele quem manda na regra, não uma guarda local.
    await user.clear(field);
    await user.type(field, "   ");
    await user.click(screen.getByRole("button", { name: /salvar/i }));

    expect(
      await screen.findByText("O nome de exibição é obrigatório."),
    ).toBeInTheDocument();
    expect(updateProfileMock).toHaveBeenCalledTimes(1);
    // nada mudou no estado da sessão.
    expect(store.getState().auth.user?.displayName).toBe("Ana");
    expect(store.getState().auth.status).toBe("authenticated");
  });

  it("should keep the user signed in after saving the profile", async () => {
    updateProfileMock.mockResolvedValue({ displayName: "Ana Maria", fullName: null });
    changePasswordMock.mockReset();
    renderPage();

    const user = userEvent.setup();
    const field = await screen.findByLabelText("Nome de exibição");
    await user.clear(field);
    await user.type(field, "Ana Maria");
    await user.click(screen.getByRole("button", { name: /salvar/i }));

    expect(await screen.findByRole("status")).toHaveTextContent(/perfil atualizado/i);
    expect(changePasswordMock).not.toHaveBeenCalled();

    // O nome novo tem que valer já na MESMA sessão (sidebar/cabeçalho): o
    // cookie foi re-emitido no servidor, mas o estado local também precisa
    // acompanhar, senão o usuário salvaria e continuaria vendo o nome antigo
    // até dar F5.
    expect(store.getState().auth.user?.displayName).toBe("Ana Maria");
    expect(store.getState().auth.status).toBe("authenticated");
  });
});
