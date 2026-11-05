import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { Provider } from "react-redux";
import { store } from "@/app/store";
import { clearAuthError } from "@/app/store/slices/authSlice";
import { LoginPage } from "./LoginPage";
import { fetchGoogleLoginFailure } from "@/services/api/auth";

// Só a chamada de rede é stubada — o restante (mensagens, detecção de MFA)
// continua real para os testes exercitarem o código de produção.
vi.mock("@/services/api/auth", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/auth")>();
  return { ...actual, loginWithPassword: vi.fn(), fetchGoogleLoginFailure: vi.fn() };
});

function CurrentPath() {
  const location = useLocation();
  return <output>{`${location.pathname}${location.search}`}</output>;
}

function renderPage(initialEntry = "/login") {
  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <LoginPage />
        <CurrentPath />
      </MemoryRouter>
    </Provider>,
  );
}

/**
 * F43-T003 — login Google via authorization code (ADR-064): o botão não
 * faz mais popup nem envia id_token — ele apenas navega o browser para o
 * start no backend (`GET /auth/google`), que cuida da troca code → token e
 * da sessão. O callback de falha volta como `?error=google[&reason=...]`; o
 * texto exibido vem do backend (`GET /auth/google/failure`).
 */
describe("LoginPage (F43-T003 — Google code flow)", () => {
  beforeEach(() => {
    store.dispatch(clearAuthError());
    vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "client.apps.googleusercontent.com");
    vi.mocked(fetchGoogleLoginFailure).mockResolvedValue(
      "Não foi possível entrar com Google. Tente novamente ou use e-mail e senha.",
    );
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.mocked(fetchGoogleLoginFailure).mockReset();
  });

  it("should send the browser to the backend start endpoint", async () => {
    renderPage();

    const link = await screen.findByRole("link", {
      name: /continuar com google/i,
    });
    expect(link).toHaveAttribute("href", "/api/v1/auth/google");
  });

  it("should hide the Google entry when the client id is not configured", () => {
    vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "");

    renderPage();

    expect(
      screen.queryByRole("link", { name: /continuar com google/i }),
    ).toBeNull();
    // A senha continua disponível — sem Google não há quebra do login.
    expect(
      screen.getByRole("button", { name: /entrar/i }),
    ).toBeInTheDocument();
  });

  it("should show the backend message when redirected back with ?error=google", async () => {
    renderPage("/login?error=google");

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/não foi possível entrar com google/i);
    expect(fetchGoogleLoginFailure).toHaveBeenCalledWith(null);

    // A query some da URL (replace) — um refresh não repete o erro.
    expect(screen.getByRole("status")).toHaveTextContent("/login");
  });

  it("should show the backend 'account unavailable' message for ?reason=account", async () => {
    vi.mocked(fetchGoogleLoginFailure).mockResolvedValue(
      "Conta não encontrada ou inativa. Fale com o administrador.",
    );

    renderPage("/login?error=google&reason=account");

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/conta não encontrada ou inativa/i);
    expect(alert).toHaveTextContent(/fale com o administrador/i);
    expect(fetchGoogleLoginFailure).toHaveBeenCalledWith("account");
  });

  it("should fall back to the local message when the backend lookup fails", async () => {
    // A indisponibilidade do endpoint não pode deixar a tela sem aviso.
    vi.mocked(fetchGoogleLoginFailure).mockRejectedValue(new Error("offline"));

    renderPage("/login?error=google&reason=account");

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/não foi possível entrar com google/i);
  });

  it("should not show a Google error alert without the query param", () => {
    renderPage();

    expect(screen.queryByRole("alert")).toBeNull();
  });
});
