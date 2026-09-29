import { useState, type FormEvent } from "react";
import { GoogleLogin, GoogleOAuthProvider } from "@react-oauth/google";
import { useNavigate } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { loginFailed, loginStarted, loginSucceeded, clearAuthError } from "@/app/store/slices/authSlice";
import {
  AuthApiError,
  isMfaRequired,
  loginWithGoogle,
  loginWithPassword,
  MFA_INVALID_MESSAGE,
  MFA_REQUIRED_MESSAGE,
} from "@/services/api/auth";

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
  // F1-T016d (ADR-019): 2º passo do login. A senha fica no estado local do
  // componente porque o retry envia email+senha+código de uma vez — nada é
  // persistido (some com a navegação, como sempre foi a regra desta tela).
  const [mfaStep, setMfaStep] = useState(false);
  const [mfaCode, setMfaCode] = useState("");

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    if (mfaStep && !mfaCode.trim()) {
      dispatch(loginFailed(MFA_REQUIRED_MESSAGE));
      return;
    }

    dispatch(loginStarted());

    try {
      const user = await loginWithPassword(
        email.trim(),
        password,
        mfaStep ? mfaCode.trim() : undefined,
      );
      dispatch(loginSucceeded(user));
      // A senha vive só no estado local do componente e some com ele.
      setPassword("");
      setMfaCode("");
      navigate("/", { replace: true });
    } catch (error) {
      if (!mfaStep && isMfaRequired(error)) {
        // Senha validada: a mensagem do backend leva a UI ao campo do 2º fator
        // e a senha permanece para o retry.
        setMfaStep(true);
        dispatch(clearAuthError());
        return;
      }

      dispatch(loginFailed(messageFor(error, mfaStep ? MFA_INVALID_MESSAGE : "E-mail ou senha inválidos.")));
      // Falhou o passo 1: a senha errada some. No passo 2 a senha fica (é
      // necessária para o retry) e o código também — para correção in loco.
      if (!mfaStep) setPassword("");
    }
  };

  const handleBackToPassword = () => {
    setMfaStep(false);
    setMfaCode("");
    dispatch(clearAuthError());
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

        {mfaStep && (
          <div style={{ marginBottom: 4 }}>
            <p style={{ fontSize: 14, fontWeight: 600 }}>Verificação em duas etapas</p>
            <p style={{ fontSize: 12, color: "#64748b", marginTop: 2 }}>
              Entrando como <strong>{email}</strong>
            </p>
          </div>
        )}

        <form className="uni-login-form" onSubmit={handleSubmit}>
          {!mfaStep && (
            <>
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
            </>
          )}

          {mfaStep && (
            <>
              <label className="uni-login-label" htmlFor="login-mfa">Código de verificação</label>
              <input
                id="login-mfa"
                className="uni-login-input"
                type="text"
                name="one-time-code"
                autoComplete="one-time-code"
                placeholder="000000"
                value={mfaCode}
                onChange={(event) => setMfaCode(event.target.value)}
                disabled={pending}
                autoFocus
                required
              />
              <p style={{ fontSize: 12, color: "#64748b", margin: "-4px 0 8px" }}>
                Digite o código de 6 dígitos do aplicativo autenticador ou um
                código de recuperação (XXXX-XXXX).
              </p>
            </>
          )}

          <button className="uni-login-submit" type="submit" disabled={pending}>
            {pending ? (
              <i className="pi pi-spin pi-spinner" />
            ) : (
              <i className={mfaStep ? "pi pi-shield" : "pi pi-sign-in"} />
            )}
            <span>
              {pending ? (mfaStep ? "Verificando..." : "Entrando...") : mfaStep ? "Verificar" : "Entrar"}
            </span>
          </button>

          {mfaStep && (
            <button
              type="button"
              className="uni-login-link"
              onClick={handleBackToPassword}
              style={{ marginTop: 8 }}
            >
              <i className="pi pi-arrow-left" /> Voltar
            </button>
          )}
        </form>

        {!mfaStep && googleClientId && (
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

        {!mfaStep && (
          <div className="uni-login-footer">
            <button type="button" className="uni-login-link" onClick={() => navigate("/forgot-password")}>
              Esqueceu sua senha?
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
