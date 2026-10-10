import { useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { AuthApiError, resetPassword } from "@/services/api/auth";

type Phase = "form" | "done";

/** Erro da API é pt-BR no envelope (backend é a autoridade); só o fallback é local. */
function toMessage(error: unknown, fallback: string): string {
  return error instanceof AuthApiError ? error.message : fallback;
}

/**
 * Página pública de redefinição de senha — fecha a pendência da auditoria
 * front×back de 01/10/2026: o link e-maiado apontava para a API (GET em rota
 * só-POST respondia 405 e não existia tela) e o fluxo morria no e-mail. O
 * link agora nasce em `/reset-password?email=...&token=...` (mesma origem do
 * convite: `Frontend:BaseUrl` com fallback à request) e a página consome o
 * `POST /auth/reset-password` que já existia sem consumidor. Nada aqui
 * autoriza — token de uso único (Redis, 30 min), conta ativa e política de
 * senha continuam valendo no servidor (F1-T015/ADR-017: escrita anônima não
 * exige antiforgery).
 */
export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email")?.trim() ?? "";
  const token = searchParams.get("token")?.trim() ?? "";

  const [phase, setPhase] = useState<Phase>("form");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  // Link montado sem token (ou sem e-mail) não tem como ser validado — não
  // tentamos a API com credencial incompleta; pedir um link novo é a única
  // saída honesta (mesma decisão da página de aceite de convite).
  if (!email || !token) {
    return (
      <div className="uni-login">
        <div className="uni-login-card uni-login-card-narrow">
          <div className="uni-login-brand">
            <div className="uni-login-logo">
              <i className="pi pi-exclamation-triangle" />
            </div>
            <h1>Link de redefinição inválido</h1>
            <p>
              O e-mail ou o token estão ausentes. Solicite um novo link de
              recuperação ou copie o link completo do e-mail.
            </p>
          </div>
          <button
            className="uni-login-submit"
            type="button"
            onClick={() => navigate("/forgot-password")}
          >
            <i className="pi pi-envelope" />
            <span>Solicitar novo link</span>
          </button>
          <button
            className="uni-login-link uni-login-link-center"
            type="button"
            onClick={() => navigate("/login")}
          >
            Voltar para o login
          </button>
        </div>
      </div>
    );
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    if (password !== confirm) {
      setError("As senhas não coincidem.");
      return;
    }

    setPending(true);
    setError(null);

    try {
      await resetPassword(email, token, password);
      setPassword("");
      setConfirm("");
      setPhase("done");
    } catch (submitError) {
      // 400 do envelope traz o motivo real (token expirado, política de
      // senha) — a autoridade é o backend, aqui só cai o fallback de rede.
      setError(
        toMessage(submitError, "Não foi possível redefinir a senha agora. Tente novamente em instantes."),
      );
    } finally {
      setPending(false);
    }
  };

  if (phase === "done") {
    return (
      <div className="uni-login">
        <div className="uni-login-card uni-login-card-narrow">
          <div className="uni-login-brand">
            <div className="uni-login-logo">
              <i className="pi pi-check-circle" />
            </div>
            <h1>Senha redefinida</h1>
            <p>
              Sua senha foi alterada e as sessões anteriores foram encerradas.
              Entre com seu e-mail e a nova senha.
            </p>
          </div>
          <button className="uni-login-submit" type="button" onClick={() => navigate("/login")}>
            <i className="pi pi-arrow-right" />
            <span>Ir para o login</span>
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="uni-login">
      <div className="uni-login-card">
        <div className="uni-login-brand">
          <div className="uni-login-logo">
            <i className="pi pi-key" />
          </div>
          <h1>Redefinir senha</h1>
          <p>
            Defina uma nova senha para <strong>{email}</strong>.
          </p>
        </div>

        {error && (
          <div className="uni-login-alert" role="alert">
            <i className="pi pi-exclamation-circle" />
            <span>{error}</span>
          </div>
        )}

        <form className="uni-login-form" onSubmit={handleSubmit}>
          <label className="uni-login-label" htmlFor="reset-password">Nova senha</label>
          <input
            id="reset-password"
            className="uni-login-input"
            type="password"
            name="password"
            autoComplete="new-password"
            placeholder="Sua nova senha"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            disabled={pending}
            required
          />

          <label className="uni-login-label" htmlFor="reset-confirm">Confirmar senha</label>
          <input
            id="reset-confirm"
            className="uni-login-input"
            type="password"
            name="confirm"
            autoComplete="new-password"
            placeholder="Repita a nova senha"
            value={confirm}
            onChange={(event) => setConfirm(event.target.value)}
            disabled={pending}
            required
          />

          <button className="uni-login-submit" type="submit" disabled={pending}>
            {pending ? <i className="pi pi-spin pi-spinner" /> : <i className="pi pi-key" />}
            <span>{pending ? "Redefinindo..." : "Redefinir senha"}</span>
          </button>

          <button
            className="uni-login-link uni-login-link-center"
            type="button"
            onClick={() => navigate("/login")}
          >
            Voltar para o login
          </button>
        </form>
      </div>
    </div>
  );
}
