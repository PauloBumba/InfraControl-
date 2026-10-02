import { useState, type FormEvent } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  AuthApiError,
  acceptInvitation,
  rejectInvitation,
  setInvitationPassword,
  type InvitationAcceptance,
} from "@/services/api/auth";

type Phase = "intro" | "password" | "activated" | "rejected";

/** Erro da API é pt-BR no envelope (backend é a autoridade); só o fallback é local. */
function toMessage(error: unknown, fallback: string): string {
  return error instanceof AuthApiError ? error.message : fallback;
}

/**
 * Página pública de aceite de convite — fecha a pendência da F1-T017 (o link
 * e-maiado apontava para a API: GET em rota só-POST respondia 405 e não
 * existia página). O fluxo espelha o backend: `accept` provisiona a conta
 * INATIVA e devolve e-mail/papéis do convidado; `set-password` é o único
 * passo que ativa a conta; `reject` recusa. Nada aqui autoriza — token,
 * estado do convite e política de senha continuam valendo no servidor
 * (F1-T015/ADR-017: escrita anônima não exige antiforgery).
 */
export function InvitationAcceptancePage() {
  const navigate = useNavigate();
  const { id: invitationId } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token")?.trim() ?? "";

  const [phase, setPhase] = useState<Phase>("intro");
  const [accepted, setAccepted] = useState<InvitationAcceptance | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");

  // Link montado sem token (ou sem id) não tem como ser validado — não
  // tentamos a API com credencial incompleta; o pedido de novo convite é a
  // única saída honesta.
  if (!invitationId || !token) {
    return (
      <div className="uni-login">
        <div className="uni-login-card uni-login-card-narrow">
          <div className="uni-login-brand">
            <div className="uni-login-logo">
              <i className="pi pi-exclamation-triangle" />
            </div>
            <h1>Link de convite inválido</h1>
            <p>
              O token está ausente. Peça um novo convite ao administrador ou
              copie o link completo do e-mail.
            </p>
          </div>
          <button className="uni-login-link uni-login-link-center" type="button" onClick={() => navigate("/login")}>
            Voltar para o login
          </button>
        </div>
      </div>
    );
  }

  const handleAccept = async (event: FormEvent) => {
    event.preventDefault();
    setPending(true);
    setError(null);

    try {
      const result = await acceptInvitation(invitationId, token);
      setAccepted(result);
      setPhase("password");
    } catch (submitError) {
      setError(toMessage(submitError, "Não foi possível aceitar o convite agora. Tente novamente em instantes."));
    } finally {
      setPending(false);
    }
  };

  const handleReject = async () => {
    setPending(true);
    setError(null);

    try {
      await rejectInvitation(invitationId, token);
      setPhase("rejected");
    } catch (submitError) {
      setError(toMessage(submitError, "Não foi possível recusar o convite agora. Tente novamente em instantes."));
    } finally {
      setPending(false);
    }
  };

  const handleActivate = async (event: FormEvent) => {
    event.preventDefault();

    if (password !== confirm) {
      setError("As senhas não coincidem.");
      return;
    }

    setPending(true);
    setError(null);

    try {
      await setInvitationPassword(invitationId, token, password);
      setPassword("");
      setConfirm("");
      setPhase("activated");
    } catch (submitError) {
      setError(toMessage(submitError, "Não foi possível ativar a conta agora. Tente novamente em instantes."));
    } finally {
      setPending(false);
    }
  };

  if (phase === "activated") {
    return (
      <div className="uni-login">
        <div className="uni-login-card uni-login-card-narrow">
          <div className="uni-login-brand">
            <div className="uni-login-logo">
              <i className="pi pi-check-circle" />
            </div>
            <h1>Conta ativada</h1>
            <p>Sua senha foi definida. Entre com seu e-mail e a senha para acessar o painel.</p>
          </div>
          <button className="uni-login-submit" type="button" onClick={() => navigate("/login")}>
            <i className="pi pi-arrow-right" />
            <span>Ir para o login</span>
          </button>
        </div>
      </div>
    );
  }

  if (phase === "rejected") {
    return (
      <div className="uni-login">
        <div className="uni-login-card uni-login-card-narrow">
          <div className="uni-login-brand">
            <div className="uni-login-logo">
              <i className="pi pi-times-circle" />
            </div>
            <h1>Convite recusado</h1>
            <p>Nenhuma conta foi criada. Se mudar de ideia, peça um novo convite ao administrador.</p>
          </div>
          <button className="uni-login-link uni-login-link-center" type="button" onClick={() => navigate("/login")}>
            Voltar para o login
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
            <i className={phase === "intro" ? "pi pi-user-plus" : "pi pi-user-edit"} />
          </div>
          {phase === "intro" ? (
            <>
              <h1>Convite ao InfraControl</h1>
              <p>Aceite o convite e defina sua senha para criar sua conta.</p>
            </>
          ) : (
            <>
              <h1>Defina sua senha</h1>
              <p>
                Convite para <strong>{accepted?.email}</strong>
                {accepted?.roles?.length ? ` — papéis: ${accepted.roles.join(", ")}` : ""}
              </p>
            </>
          )}
        </div>

        {error && (
          <div className="uni-login-alert" role="alert">
            <i className="pi pi-exclamation-circle" />
            <span>{error}</span>
          </div>
        )}

        {phase === "intro" ? (
          <form className="uni-login-form" onSubmit={handleAccept}>
            <button className="uni-login-submit" type="submit" disabled={pending}>
              {pending ? <i className="pi pi-spin pi-spinner" /> : <i className="pi pi-check-circle" />}
              <span>{pending ? "Aceitando..." : "Aceitar convite"}</span>
            </button>

            <button
              className="uni-login-link uni-login-link-center"
              type="button"
              onClick={handleReject}
              disabled={pending}
            >
              Recusar convite
            </button>
          </form>
        ) : (
          <form className="uni-login-form" onSubmit={handleActivate}>
            <label className="uni-login-label" htmlFor="invite-password">Senha</label>
            <input
              id="invite-password"
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

            <label className="uni-login-label" htmlFor="invite-confirm">Confirmar senha</label>
            <input
              id="invite-confirm"
              className="uni-login-input"
              type="password"
              name="confirm"
              autoComplete="new-password"
              placeholder="Repita a senha"
              value={confirm}
              onChange={(event) => setConfirm(event.target.value)}
              disabled={pending}
              required
            />

            <button className="uni-login-submit" type="submit" disabled={pending}>
              {pending ? <i className="pi pi-spin pi-spinner" /> : <i className="pi pi-key" />}
              <span>{pending ? "Ativando..." : "Definir senha e ativar"}</span>
            </button>
            {/* Sem "voltar": o aceite é single-use — depois de aceito o único
                passo válido do convite é definir a senha e ativar. */}
          </form>
        )}
      </div>
    </div>
  );
}
