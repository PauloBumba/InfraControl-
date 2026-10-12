import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { PageHeader } from "@/components/ui/PageHeader";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { sessionChecked, signedOut } from "@/app/store/slices/authSlice";
import { AuthApiError, changePassword, updateProfile } from "@/services/api/auth";

/**
 * F26-T001 — "Minha Conta": troca de senha autenticada. O backend
 * (`POST /auth/change-password`) já existia desde a Fase Identity sem
 * consumidor de UI; esta tela fecha o gap. Detalhe de contrato que
 * manda no design: o servidor REVOGA TODAS as sessões do usuário no
 * sucesso (inclusive a do cookie atual) — por isso o sucesso não mostra
 * "tudo certo, siga navegando": ele derruba o estado local e leva ao
 * /login, coerente com a sessão que morreu no servidor.
 *
 * A validação abaixo é só feedback imediato espelhando a política do
 * Identity (12+, dígito, maiúscula, minúscula, não-alfanumérico —
 * DependencyInjection.cs); o backend continua sendo a autoridade e o
 * motivo pt-BR dele (senha atual errada, política) é o que aparece no
 * envelope de erro.
 */

/** Espelho da política do servidor (RequiredLength=12 + 4 requisitos). */
const PASSWORD_POLICY: { test: (value: string) => boolean; message: string }[] = [
  { test: (value) => value.length >= 12, message: "A nova senha precisa de pelo menos 12 caracteres." },
  { test: (value) => /\d/.test(value), message: "A nova senha precisa de pelo menos um dígito." },
  { test: (value) => /[A-Z]/.test(value), message: "A nova senha precisa de pelo menos uma maiúscula." },
  { test: (value) => /[a-z]/.test(value), message: "A nova senha precisa de pelo menos uma minúscula." },
  {
    test: (value) => /[^A-Za-z0-9]/.test(value),
    message: "A nova senha precisa de pelo menos um caractere especial.",
  },
];

function toMessage(error: unknown, fallback: string): string {
  return error instanceof AuthApiError ? error.message : fallback;
}

export function AccountPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const user = useAppSelector((state) => state.auth.user);

  // ── Perfil (F29-T002) — estado próprio, separado do formulário de senha:
  // os dois enviam para endpoints diferentes e o erro de um não pode aparecer
  // no card do outro.
  const [profileName, setProfileName] = useState(user?.displayName ?? "");
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSaved, setProfileSaved] = useState(false);
  const [profilePending, setProfilePending] = useState(false);

  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const handleProfileSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setProfileError(null);
    setProfileSaved(false);

    const trimmed = profileName.trim();

    setProfilePending(true);
    try {
      const { displayName } = await updateProfile(trimmed);
      // O servidor re-emite o cookie com a claim nova; aqui só espelhamos no
      // estado local o que acabou de mudar, para a sidebar/cabeçalho
      // mostrarem o nome certo sem precisar de F5. Reuso do sessionChecked:
      // é exatamente o mesmo fato ("backend confirmou a sessão").
      dispatch(sessionChecked({ ...user!, displayName }));
      setProfileName(displayName);
      setProfileSaved(true);
    } catch (submitError) {
      setProfileError(
        toMessage(submitError, "Não foi possível atualizar o perfil agora. Tente novamente em instantes."),
      );
    } finally {
      setProfilePending(false);
    }
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);

    const policyMessage = PASSWORD_POLICY.find((rule) => !rule.test(next));
    if (policyMessage) {
      setError(policyMessage.message);
      return;
    }

    if (next !== confirm) {
      setError("A confirmação não confere com a nova senha.");
      return;
    }

    setPending(true);
    try {
      await changePassword(current, next);
      // A sessão atual foi revogada no servidor junto com as demais —
      // limpa o estado local e devolve ao login (mesmo caminho do logout).
      dispatch(signedOut());
      navigate("/login");
    } catch (submitError) {
      setError(
        toMessage(submitError, "Não foi possível trocar a senha agora. Tente novamente em instantes."),
      );
    } finally {
      setPending(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Minha Conta"
        subtitle="Edite seu perfil e troque sua senha de acesso ao InfraControl."
      />

      {/* ── Perfil (F29-T002) ─────────────────────────────────────────── */}
      <div className="uni-card" style={{ maxWidth: 560, padding: 24 }}>
        <h2 style={{ fontSize: 15, margin: "0 0 4px" }}>Perfil</h2>
        <p style={{ fontSize: 13, color: "#64748b", margin: 0 }}>
          O nome aparece para toda a equipe. O e-mail é o seu login e não é
          editável por aqui.
        </p>

        <form
          onSubmit={handleProfileSubmit}
          style={{ display: "flex", flexDirection: "column", gap: 12, marginTop: 16 }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="display-name">Nome completo</label>
            <input
              id="display-name"
              type="text"
              autoComplete="name"
              value={profileName}
              onChange={(event) => {
                setProfileName(event.target.value);
                setProfileSaved(false);
              }}
              required
            />
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="profile-email">E-mail</label>
            <input
              id="profile-email"
              type="email"
              value={user?.email ?? ""}
              readOnly
              disabled
              aria-describedby="profile-email-hint"
            />
            <span id="profile-email-hint" style={{ fontSize: 12, color: "#64748b" }}>
              Alterar o e-mail é uma operação de administrador.
            </span>
          </div>

          {profileError && (
            <div className="uni-login-alert" role="alert">
              <i className="pi pi-exclamation-triangle" />
              <span>{profileError}</span>
            </div>
          )}

          {profileSaved && (
            <div className="uni-login-alert" role="status" style={{ color: "#15803d" }}>
              <i className="pi pi-check-circle" />
              <span>Nome atualizado. Já vale para toda a equipe.</span>
            </div>
          )}

          <button className="uni-login-submit" type="submit" disabled={profilePending}>
            <i className={`pi ${profilePending ? "pi-spin pi-spinner" : "pi-save"}`} />
            <span>Salvar</span>
          </button>
        </form>
      </div>

      {/* ── Senha (F26-T001) ──────────────────────────────────────────── */}
      <div className="uni-card" style={{ maxWidth: 560, padding: 24 }}>
        <h2 style={{ fontSize: 15, margin: "0 0 4px" }}>Segurança</h2>

        <div
          className="uni-login-alert"
          role="note"
          style={{ marginBottom: 16, alignItems: "flex-start" }}
        >
          <i className="pi pi-info-circle" />
          <span>
            Por segurança, trocar a senha encerra <strong>todas</strong> as suas
            sessões ativas — inclusive esta. Você fará login novamente em seguida.
          </span>
        </div>

        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="current-password">Senha atual</label>
            <input
              id="current-password"
              type="password"
              autoComplete="current-password"
              value={current}
              onChange={(event) => setCurrent(event.target.value)}
              required
            />
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="new-password">Nova senha</label>
            <input
              id="new-password"
              type="password"
              autoComplete="new-password"
              value={next}
              onChange={(event) => setNext(event.target.value)}
              required
            />
            <span style={{ fontSize: 12, color: "#64748b" }}>
              Mínimo de 12 caracteres, com dígito, maiúscula, minúscula e especial.
            </span>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="confirm-password">Confirmar nova senha</label>
            <input
              id="confirm-password"
              type="password"
              autoComplete="new-password"
              value={confirm}
              onChange={(event) => setConfirm(event.target.value)}
              required
            />
          </div>

          {error && (
            <div className="uni-login-alert" role="alert">
              <i className="pi pi-exclamation-triangle" />
              <span>{error}</span>
            </div>
          )}

          <button className="uni-login-submit" type="submit" disabled={pending}>
            <i className={`pi ${pending ? "pi-spin pi-spinner" : "pi-key"}`} />
            <span>Trocar senha</span>
          </button>
        </form>
      </div>
    </div>
  );
}
