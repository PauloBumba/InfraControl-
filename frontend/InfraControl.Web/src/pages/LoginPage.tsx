import { useState, type FormEvent } from "react";
import { GoogleLogin, GoogleOAuthProvider } from "@react-oauth/google";
import { useNavigate } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { loginFailed, loginStarted, loginSucceeded, clearAuthError } from "@/app/store/slices/authSlice";
import { AuthApiError, loginWithGoogle, loginWithPassword } from "@/services/api/auth";

/**
 * Client ID público do Google (Google Identity Services). Vazio = botão do
 * Google não é renderizado e o login por senha continua funcionando.
 * NUNCA coloque aqui um Client Secret: `VITE_*` é embutido no bundle.
 */
const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID ?? "";

const GENERIC_ERROR = "Não foi possível entrar. Verifique suas credenciais e tente novamente.";

function messageFor(error: unknown, fallback: string): string {
  if (error instanceof AuthApiError) {
    if (error.status === 403) return error.message; // ex.: conta inativa (backend não revela qual caso)
    if (error.status === 401) return fallback;
    return error.message;
  }
  return fallback;
}

export function LoginPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const pending = useAppSelector((state) => state.auth.pending);
  const authError = useAppSelector((state) => state.auth.error);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    dispatch(loginStarted());

    try {
      const user = await loginWithPassword(email.trim(), password);
      dispatch(loginSucceeded(user));
      navigate("/", { replace: true });
    } catch (error) {
      dispatch(loginFailed(messageFor(error, "E-mail ou senha inválidos.")));
    } finally {
      // A senha vive só no estado local do componente e some com ele.
      setPassword("");
    }
  };

  const handleGoogle = async (credential: string) => {
    dispatch(loginStarted());

    try {
      const user = await loginWithGoogle(credential);
      dispatch(loginSucceeded(user));
      navigate("/", { replace: true });
    } catch (error) {
      dispatch(loginFailed(messageFor(error, GENERIC_ERROR)));
    }
  };

  return (
    <div className="uni-login">
      <div className="uni-login-card">
        <div className="uni-login-brand">
          <div className="uni-login-logo">
            <i className="pi pi-server" />
          </div>
          <h1>InfraControl</h1>
          <p>Operações de TI · central de infraestrutura</p>
        </div>

        {authError && (
          <div className="uni-login-alert" role="alert">
            <i className="pi pi-exclamation-circle" />
            <span>{authError}</span>
            <button type="button" onClick={() => dispatch(clearAuthError())} aria-label="Fechar aviso">
              <i className="pi pi-times" />
            </button>
          </div>
        )}

        <form className="uni-login-form" onSubmit={handleSubmit}>
          <label className="uni-login-label" htmlFor="login-email">E-mail</label>
          <input
            id="login-email"
            className="uni-login-input"
            type="email"
            name="email"
            autoComplete="username"
            placeholder="voce@empresa.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            disabled={pending}
            required
          />

          <label className="uni-login-label" htmlFor="login-password">Senha</label>
          <div className="uni-login-password">
            <input
              id="login-password"
              className="uni-login-input"
              type="password"
              name="password"
              autoComplete="current-password"
              placeholder="••••••••"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              disabled={pending}
              required
            />
            <button
              type="button"
              onClick={() => {
                const field = document.getElementById("login-password") as HTMLInputElement | null;
                if (field) field.type = field.type === "password" ? "text" : "password";
              }}
              aria-label="Mostrar ou ocultar a senha"
              tabIndex={-1}
            >
              <i className="pi pi-eye" />
            </button>
          </div>

          <button className="uni-login-submit" type="submit" disabled={pending}>
            {pending ? <i className="pi pi-spin pi-spinner" /> : <i className="pi pi-sign-in" />}
            <span>{pending ? "Entrando..." : "Entrar"}</span>
          </button>
        </form>

        {googleClientId && (
          <>
            <div className="uni-login-divider"><span>ou continue com</span></div>
            <div className="uni-login-google">
              <GoogleOAuthProvider clientId={googleClientId}>
                <GoogleLogin
                  onSuccess={(response) => {
                    if (response.credential) void handleGoogle(response.credential);
                  }}
                  onError={() => dispatch(loginFailed("Login com Google cancelado ou indisponível."))}
                  useOneTap={false}
                  theme="outline"
                  size="large"
                  shape="rectangular"
                  width="100%"
                  text="continue_with"
                />
              </GoogleOAuthProvider>
            </div>
          </>
        )}

        <div className="uni-login-footer">
          <button type="button" className="uni-login-link" onClick={() => navigate("/forgot-password")}>
            Esqueceu sua senha?
          </button>
        </div>
      </div>
    </div>
  );
}
