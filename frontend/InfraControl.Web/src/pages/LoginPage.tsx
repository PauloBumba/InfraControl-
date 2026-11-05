import { useEffect, useState, type FormEvent } from "react";
import { InputOtp } from "primereact/inputotp";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { loginFailed, loginStarted, loginSucceeded, clearAuthError } from "@/app/store/slices/authSlice";
import {
  AuthApiError,
  fetchGoogleLoginFailure,
  isMfaRequired,
  loginWithPassword,
  MFA_INVALID_MESSAGE,
  MFA_REQUIRED_MESSAGE,
} from "@/services/api/auth";

/**
 * F43-T003 (rev.) — falha do authorization code flow. O backend devolve
 * `/login?error=google` e, quando a conta não está disponível,
 * `&reason=account`; o TEXTO vem de `GET /auth/google/failure` (fonte única
 * no backend). Esta constante é só o fallback para falha de transporte.
 */
const GOOGLE_LOGIN_ERROR_FALLBACK =
  "Não foi possível entrar com Google. Tente novamente ou use e-mail e senha.";

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
  const [searchParams, setSearchParams] = useSearchParams();

  // Client ID público do Google — só decide VISIBILIDADE do link (o fluxo
  // inteiro roda no backend). Lido em runtime (mesmo padrão de
  // VITE_SUPPORT_EMAIL na ContactPage) para ser testável com stubEnv.
  // Vazio = link some e o login por senha continua funcionando.
  // NUNCA aqui um Client Secret: `VITE_*` é embutido no bundle.
  const googleClientId = (import.meta.env.VITE_GOOGLE_CLIENT_ID ?? "").trim();

  // O redirect do callback traz /login?error=google[&reason=...] — a
  // mensagem exibida é a do backend (`GET /auth/google/failure`), que
  // distingue "conta não ativa" sem dizer se a conta existe (anti-
  // enumeração). A query é removida (replace) para um refresh não repetir.
  useEffect(() => {
    if (searchParams.get("error") !== "google") return;
    const reason = searchParams.get("reason");
    setSearchParams({}, { replace: true });

    let active = true;
    void (async () => {
      let message = GOOGLE_LOGIN_ERROR_FALLBACK;
      try {
        message = (await fetchGoogleLoginFailure(reason)) || message;
      } catch {
        // Rede/5xx: mantém o fallback local — o motivo da falha já foi
        // decidido no backend, isto é só indisponibilidade do endpoint.
      }
      if (active) dispatch(loginFailed(message));
    })();

    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- montagem única: o erro chega uma vez no redirect
  }, []);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  // F1-T016d (ADR-019): 2º passo do login. A senha fica no estado local do
  // componente porque o retry envia email+senha+código de uma vez — nada é
  // persistido (some com a navegação, como sempre foi a regra desta tela).
  const [mfaStep, setMfaStep] = useState(false);
  const [mfaCode, setMfaCode] = useState("");
  const [usingRecoveryCode, setUsingRecoveryCode] = useState(false);

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
      navigate("/dashboard", { replace: true });
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
    setUsingRecoveryCode(false);
    dispatch(clearAuthError());
  };

  return (
    <div className="uni-login">
      <div className="uni-login-card">
        <div className="uni-login-brand">
          <div className="uni-login-logo">
            <svg viewBox="0 0 64 64" width="32" height="32">
              <defs>
                <linearGradient id="infraLoginGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style={{stopColor:'#3b82f6',stopOpacity:1}} />
                  <stop offset="100%" style={{stopColor:'#2563eb',stopOpacity:1}} />
                </linearGradient>
              </defs>
              <rect width="64" height="64" rx="12" fill="url(#infraLoginGrad)" />
              <g fill="white">
                <rect x="16" y="12" width="32" height="40" rx="4" fill="none" stroke="white" strokeWidth="2.5"/>
                <rect x="20" y="18" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
                <rect x="20" y="28" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
                <rect x="20" y="38" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
                <circle cx="38" cy="21" r="1.5" fill="#22c55e"/>
                <circle cx="38" cy="31" r="1.5" fill="#22c55e"/>
                <circle cx="38" cy="41" r="1.5" fill="#eab308"/>
              </g>
            </svg>
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
                  type={showPassword ? "text" : "password"}
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
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}
                >
                  <i className="pi pi-eye" />
                </button>
              </div>
            </>
          )}

          {mfaStep && (
            <>
              <label className="uni-login-label" htmlFor="login-mfa">Código de verificação</label>
              {usingRecoveryCode ? <input id="login-mfa" className="uni-login-input" type="text" name="one-time-code" autoComplete="one-time-code" placeholder="XXXX-XXXX" value={mfaCode} onChange={(event) => setMfaCode(event.target.value)} disabled={pending} autoFocus required /> : <InputOtp id="login-mfa" value={mfaCode} onChange={(event) => setMfaCode(String(event.value ?? ""))} length={6} integerOnly disabled={pending} autoFocus className="uni-input-otp" aria-label="Código MFA de seis dígitos" />}
              <p style={{ fontSize: 12, color: "#64748b", margin: "-4px 0 8px" }}>
                Digite o código de 6 dígitos do aplicativo autenticador ou um
                código de recuperação (XXXX-XXXX).
              </p>
              <button type="button" className="uni-login-link" onClick={() => { setUsingRecoveryCode((value) => !value); setMfaCode(""); }}>
                {usingRecoveryCode ? "Usar código do autenticador" : "Usar código de recuperação"}
              </button>
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
              {/* F43-T003/ADR-064: sem popup nem id_token no browser — o
                  link navega para o start no backend (GET /auth/google),
                  que emite o state, troca o code e cria a sessão. */}
              <a
                className="uni-login-social-btn"
                href="/api/v1/auth/google"
              >
                <i className="pi pi-google" aria-hidden="true" />
                <span>Continuar com Google</span>
              </a>
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
