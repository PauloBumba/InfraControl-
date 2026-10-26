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
  // no card do outro. F38: `fullName` (nome completo, opcional) viaja junto
  // com o nome exibido no MESMO PUT /auth/profile.
  const [profileName, setProfileName] = useState(user?.displayName ?? "");
  const [profileFullName, setProfileFullName] = useState(user?.fullName ?? "");
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSaved, setProfileSaved] = useState(false);
  const [profilePending, setProfilePending] = useState(false);

  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNext, setShowNext] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const handleProfileSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setProfileError(null);
    setProfileSaved(false);

    const trimmed = profileName.trim();
    // "" (em branco) = limpar: o backend grava null. O valor SEMPRE viaja,
    // nunca `undefined` — ausência no corpo significa "não informado".
    const trimmedFullName = profileFullName.trim();

    setProfilePending(true);
    try {
      const { displayName, fullName } = await updateProfile(trimmed, trimmedFullName);
      // O servidor re-emite o cookie com as claims novas; aqui só espelhamos no
      // estado local o que acabou de mudar, para a sidebar/cabeçalho
      // mostrarem o nome certo sem precisar de F5. Reuso do sessionChecked:
      // é exatamente o mesmo fato ("backend confirmou a sessão").
      dispatch(sessionChecked({ ...user!, displayName, fullName }));
      setProfileName(displayName);
      setProfileFullName(fullName ?? "");
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
    <div style={{ display: "flex", flexDirection: "column", gap: 24, maxWidth: 720 }}>
      <PageHeader
        title="Minha Conta"
        subtitle="Edite seu perfil e troque sua senha de acesso ao InfraControl."
      />

      {/* ── Perfil (F29-T002) ─────────────────────────────────────────── */}
      <div className="uni-card" style={{ maxWidth: 560, padding: 24 }}>
        <h2 style={{ fontSize: 15, margin: "0 0 4px" }}>Perfil</h2>
        <p style={{ fontSize: 13, color: "#64748b", margin: 0 }}>
          O nome de exibição aparece para toda a equipe. O e-mail é o seu
          login e não é editável por aqui.
        </p>

        <form
          onSubmit={handleProfileSubmit}
          style={{ display: "flex", flexDirection: "column", gap: 12, marginTop: 16 }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="display-name">Nome de exibição</label>
            <input
              id="display-name"
              type="text"
              autoComplete="nickname"
              value={profileName}
              onChange={(event) => {
                setProfileName(event.target.value);
                setProfileSaved(false);
              }}
              required
            />
            <span style={{ fontSize: 12, color: "#64748b" }}>
              É o nome que aparece em menus, auditoria e notificações.
            </span>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="full-name">Nome completo</label>
            <input
              id="full-name"
              type="text"
              autoComplete="name"
              value={profileFullName}
              onChange={(event) => {
                setProfileFullName(event.target.value);
                setProfileSaved(false);
              }}
            />
            <span style={{ fontSize: 12, color: "#64748b" }}>
              Opcional — deixe em branco para não registrar um nome completo.
            </span>
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
            <div className="uni-alert uni-alert-error" role="alert" style={{ gridColumn: "1 / -1" }}>
              <i className="pi pi-exclamation-triangle" />
              <span>{profileError}</span>
            </div>
          )}

          {profileSaved && (
            <div className="uni-alert uni-alert-success" role="status" style={{ gridColumn: "1 / -1" }}>
              <i className="pi pi-check-circle" />
              <span>Perfil atualizado com sucesso.</span>
            </div>
          )}

          <div style={{ gridColumn: "1 / -1", display: "flex", justifyContent: "flex-end" }}>
            <button className="uni-btn uni-btn-primary" type="submit" disabled={profilePending}>
              <i className={`pi ${profilePending ? "pi-spin pi-spinner" : "pi-save"}`} />
              <span>Salvar alterações</span>
            </button>
          </div>
        </form>
      </div>

      {/* ── Senha (F26-T001) ──────────────────────────────────────────── */}
      <section className="uni-card" style={{ padding: 28, display: "flex", flexDirection: "column", gap: 20, maxWidth: 640 }}>
        <header style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          <h2 style={{ fontSize: 16, fontWeight: 600, margin: 0, color: "var(--uni-text-primary)" }}>Segurança</h2>
          <p style={{ fontSize: 13, color: "var(--uni-text-muted)", margin: 0 }}>
            Altere sua senha. Ao trocar, todas as sessões ativas serão encerradas.
          </p>
        </header>

        <div className="uni-alert uni-alert-info" role="note">
          <i className="pi pi-info-circle" />
          <span>
            Por segurança, trocar a senha encerra <strong>todas</strong> as suas sessões ativas — inclusive esta. Você fará login novamente em seguida.
          </span>
        </div>

        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <label htmlFor="current-password" style={{ fontSize: 13, fontWeight: 500, color: "var(--uni-text-primary)" }}>Senha atual</label>
            <div className="uni-input-group">
              <input
                id="current-password"
                className="uni-input uni-input-with-icon"
                type={showCurrent ? "text" : "password"}
                autoComplete="current-password"
                value={current}
                onChange={(event) => setCurrent(event.target.value)}
                required
              />
              <button type="button" className="uni-input-action" onClick={() => setShowCurrent((v) => !v)} aria-label={showCurrent ? "Ocultar senha" : "Mostrar senha"}>
                <i className={showCurrent ? "pi pi-eye-slash" : "pi pi-eye"} />
              </button>
            </div>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <label htmlFor="new-password" style={{ fontSize: 13, fontWeight: 500, color: "var(--uni-text-primary)" }}>Nova senha</label>
            <div className="uni-input-group">
              <input
                id="new-password"
                className="uni-input uni-input-with-icon"
                type={showNext ? "text" : "password"}
                autoComplete="new-password"
                value={next}
                onChange={(event) => setNext(event.target.value)}
                required
              />
              <button type="button" className="uni-input-action" onClick={() => setShowNext((v) => !v)} aria-label={showNext ? "Ocultar senha" : "Mostrar senha"}>
                <i className={showNext ? "pi pi-eye-slash" : "pi pi-eye"} />
              </button>
            </div>
            <span style={{ fontSize: 12, color: "var(--uni-text-muted)" }}>
              Mínimo de 12 caracteres, com dígito, maiúscula, minúscula e caractere especial.
            </span>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <label htmlFor="confirm-password" style={{ fontSize: 13, fontWeight: 500, color: "var(--uni-text-primary)" }}>Confirmar nova senha</label>
            <div className="uni-input-group">
              <input
                id="confirm-password"
                className="uni-input uni-input-with-icon"
                type={showConfirm ? "text" : "password"}
                autoComplete="new-password"
                value={confirm}
                onChange={(event) => setConfirm(event.target.value)}
                required
              />
              <button type="button" className="uni-input-action" onClick={() => setShowConfirm((v) => !v)} aria-label={showConfirm ? "Ocultar senha" : "Mostrar senha"}>
                <i className={showConfirm ? "pi pi-eye-slash" : "pi pi-eye"} />
              </button>
            </div>
          </div>

          {error && (
            <div className="uni-alert uni-alert-error" role="alert">
              <i className="pi pi-exclamation-triangle" />
              <span>{error}</span>
            </div>
          )}

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <button className="uni-btn uni-btn-primary" type="submit" disabled={pending}>
              <i className={`pi ${pending ? "pi-spin pi-spinner" : "pi-key"}`} />
              <span>Trocar senha</span>
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}
