import { useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { PageHeader } from "@/components/ui/PageHeader";
import { Button } from "@/components/ui/Button";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { sessionChecked, signedOut } from "@/app/store/slices/authSlice";
import { AuthApiError, changePassword, updateProfile, uploadAvatar } from "@/services/api/auth";

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
 * Identity (6+, dígito, maiúscula, minúscula, não-alfanumérico —
 * DependencyInjection.cs); o backend continua sendo a autoridade e o
 * motivo pt-BR dele (senha atual errada, política) é o que aparece no
 * envelope de erro.
 */

/** Espelho da política do servidor (RequiredLength=6 + 4 requisitos). */
const PASSWORD_POLICY: { test: (value: string) => boolean; message: string }[] = [
  { test: (value) => value.length >= 6, message: "A nova senha precisa de pelo menos 6 caracteres." },
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
  // F51 — foto de perfil: `avatarPath` guarda o caminho salvo (ou o recém-
  // upado); `avatarDirty` marca que o PUT /auth/profile deve enviar o campo
  // (undefined = não informado → servidor preserva).
  const [avatarPath, setAvatarPath] = useState<string | null>(user?.avatarPath ?? null);
  const [avatarDirty, setAvatarDirty] = useState(false);
  const [avatarUploading, setAvatarUploading] = useState(false);
  const avatarInputRef = useRef<HTMLInputElement>(null);
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
      // avatarPath só viaja quando a foto mudou — ausente = servidor preserva.
      const { displayName, fullName, avatarPath: savedAvatar } = avatarDirty
        ? await updateProfile(trimmed, trimmedFullName, avatarPath ?? "")
        : await updateProfile(trimmed, trimmedFullName);
      // O servidor re-emite o cookie com as claims novas; aqui só espelhamos no
      // estado local o que acabou de mudar, para a sidebar/cabeçalho
      // mostrarem o nome certo sem precisar de F5. Reuso do sessionChecked:
      // é exatamente o mesmo fato ("backend confirmou a sessão").
      dispatch(
        sessionChecked({
          ...user!,
          displayName,
          fullName,
          avatarPath: avatarDirty ? (savedAvatar ?? null) : (user?.avatarPath ?? null),
        }),
      );
      setProfileName(displayName);
      setProfileFullName(fullName ?? "");
      if (avatarDirty) {
        setAvatarPath(savedAvatar ?? null);
        setAvatarDirty(false);
      }
      setProfileSaved(true);
    } catch (submitError) {
      setProfileError(
        toMessage(submitError, "Não foi possível atualizar o perfil agora. Tente novamente em instantes."),
      );
    } finally {
      setProfilePending(false);
    }
  };

  const handleAvatarFile = async (file: File | undefined) => {
    if (!file) return;
    setProfileError(null);
    setProfileSaved(false);
    setAvatarUploading(true);
    try {
      const result = await uploadAvatar(file);
      if (!result?.imagePath) throw new Error("Upload sem caminho.");
      setAvatarPath(result.imagePath);
      setAvatarDirty(true);
    } catch (uploadError) {
      setProfileError(
        toMessage(uploadError, "Não foi possível enviar a foto. Tente novamente."),
      );
    } finally {
      setAvatarUploading(false);
      if (avatarInputRef.current) avatarInputRef.current.value = "";
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

  const displayLabel = user?.fullName?.trim() || user?.displayName || user?.email || "—";
  const initials = displayLabel
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("") || "?";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20, maxWidth: 860 }}>
      <PageHeader
        title="Minha Conta"
        subtitle="Seu perfil de operador, preferências de exibição e segurança de acesso."
      />

      {/* ── Identidade do operador ────────────────────────────────────── */}
      <section
        className="uni-card"
        style={{
          padding: "20px 24px",
          display: "flex",
          alignItems: "center",
          gap: 16,
          flexWrap: "wrap",
        }}
      >
        {avatarPath ? (
          <img
            src={avatarPath}
            alt={`Foto de ${displayLabel}`}
            style={{
              width: 56,
              height: 56,
              borderRadius: "50%",
              objectFit: "cover",
              flexShrink: 0,
              border: "2px solid #dbeafe",
            }}
          />
        ) : (
          <div
            aria-hidden="true"
            style={{
              width: 56,
              height: 56,
              borderRadius: "50%",
              background: "linear-gradient(135deg, #1e3a8a, #3b82f6)",
              color: "#fff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 20,
              fontWeight: 700,
              flexShrink: 0,
            }}
          >
            {initials}
          </div>
        )}
        <div style={{ display: "flex", flexDirection: "column", gap: 4, minWidth: 0, flex: 1 }}>
          <strong style={{ fontSize: 16, color: "var(--uni-text-primary)" }}>{displayLabel}</strong>
          <span style={{ fontSize: 13, color: "var(--uni-text-muted)" }}>{user?.email}</span>
        </div>
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {(user?.roles ?? []).map((role) => (
            <span
              key={role}
              style={{
                fontSize: 11,
                padding: "3px 10px",
                borderRadius: 999,
                background: "#eff6ff",
                color: "#1d4ed8",
                fontWeight: 600,
              }}
            >
              {role}
            </span>
          ))}
        </div>
      </section>

      {/* ── Perfil (F29-T002) ─────────────────────────────────────────── */}
      <div className="uni-card" style={{ padding: 24 }}>
        <div style={{ display: "flex", alignItems: "baseline", gap: 12, marginBottom: 16 }}>
          <h2 style={{ fontSize: 15, margin: 0, color: "var(--uni-text-primary)" }}>Perfil do operador</h2>
          <span style={{ fontSize: 12, color: "var(--uni-text-muted)" }}>
            O nome de exibição aparece em menus, auditoria, relatórios e análises de IA.
          </span>
        </div>

        {/* F51 — foto de perfil: upload imediato (só grava de fato no
            "Salvar alterações" junto com os nomes). */}
        <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 16 }}>
          {avatarPath ? (
            <img
              src={avatarPath}
              alt="Pré-visualização da foto de perfil"
              style={{
                width: 48,
                height: 48,
                borderRadius: "50%",
                objectFit: "cover",
                border: "1px solid #d1d5db",
              }}
            />
          ) : (
            <div
              aria-hidden="true"
              style={{
                width: 48,
                height: 48,
                borderRadius: "50%",
                background: "#e2e8f0",
                color: "#64748b",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <i className="pi pi-user" />
            </div>
          )}
          <input
            ref={avatarInputRef}
            type="file"
            accept="image/*"
            hidden
            onChange={(event) => void handleAvatarFile(event.target.files?.[0])}
          />
          <Button
            type="button"
            variant="ghost"
            style={{ padding: "6px 12px", fontSize: 12 }}
            disabled={avatarUploading || profilePending}
            onClick={() => avatarInputRef.current?.click()}
          >
            <i className={`pi ${avatarUploading ? "pi-spin pi-spinner" : "pi-camera"}`} />
            {avatarPath ? "Trocar foto" : "Enviar foto"}
          </Button>
          {avatarPath && (
            <Button
              type="button"
              variant="ghost"
              style={{ padding: "6px 12px", fontSize: 12, color: "#b91c1c" }}
              disabled={avatarUploading || profilePending}
              onClick={() => {
                setAvatarPath(null);
                setAvatarDirty(true);
                setProfileSaved(false);
              }}
            >
              <i className="pi pi-trash" />
              Remover
            </Button>
          )}
          <span style={{ fontSize: 12, color: "var(--uni-text-muted)" }}>
            PNG, JPG ou WebP até 10 MB.
          </span>
        </div>

        <form
          onSubmit={handleProfileSubmit}
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
            gap: 14,
            marginTop: 8,
          }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <label htmlFor="display-name">Nome de operador</label>
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
              É o nome que aparece em menus, auditoria, relatórios e análises de IA.
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

      {/* ── Senha (F26-T001 · F52-T007) ────────────────────────────────── */}
      <section
        className="uni-card"
        style={{
          padding: 28,
          display: "flex",
          flexDirection: "column",
          gap: 20,
          maxWidth: 640,
        }}
      >
        <header style={{ display: "flex", alignItems: "center", gap: 14 }}>
          <span
            aria-hidden="true"
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              background: "linear-gradient(135deg,#1e3a8a,#3b82f6)",
              color: "#fff",
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 18,
              flexShrink: 0,
            }}
          >
            <i className="pi pi-shield" />
          </span>
          <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
            <h2 style={{ fontSize: 16, fontWeight: 600, margin: 0, color: "var(--uni-text-primary)" }}>
              Alterar senha
            </h2>
            <p style={{ fontSize: 13, color: "var(--uni-text-muted)", margin: 0 }}>
              Troque sua senha de acesso. Ao confirmar, todas as sessões ativas serão encerradas.
            </p>
          </div>
        </header>

        <div className="uni-alert uni-alert-info" role="note">
          <i className="pi pi-info-circle" />
          <span>
            Por segurança, trocar a senha encerra <strong>todas</strong> as suas sessões ativas — inclusive esta. Você fará login novamente em seguida.
          </span>
        </div>

        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 18 }}>
          <PasswordField
            id="current-password"
            label="Senha atual"
            autoComplete="current-password"
            value={current}
            onChange={setCurrent}
            visible={showCurrent}
            onToggle={() => setShowCurrent((v) => !v)}
          />

          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            <PasswordField
              id="new-password"
              label="Nova senha"
              autoComplete="new-password"
              value={next}
              onChange={setNext}
              visible={showNext}
              onToggle={() => setShowNext((v) => !v)}
            />
            {next.length > 0 && <PasswordPolicyChecklist value={next} />}
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <PasswordField
              id="confirm-password"
              label="Confirmar nova senha"
              autoComplete="new-password"
              value={confirm}
              onChange={setConfirm}
              visible={showConfirm}
              onToggle={() => setShowConfirm((v) => !v)}
            />
            {confirm.length > 0 && next.length > 0 && (
              <span
                role="status"
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 6,
                  fontSize: 12,
                  fontWeight: 500,
                  color: confirm === next ? "#15803d" : "#b91c1c",
                }}
              >
                <i className={`pi ${confirm === next ? "pi-check-circle" : "pi-times-circle"}`} />
                {confirm === next ? "As senhas conferem." : "As senhas não conferem."}
              </span>
            )}
          </div>

          {error && (
            <div className="uni-alert uni-alert-error" role="alert">
              <i className="pi pi-exclamation-triangle" />
              <span>{error}</span>
            </div>
          )}

          <div style={{ display: "flex", justifyContent: "flex-end", gap: 10 }}>
            <Button
              type="button"
              variant="ghost"
              style={{ padding: "8px 16px" }}
              disabled={pending}
              onClick={() => {
                setCurrent("");
                setNext("");
                setConfirm("");
                setError(null);
              }}
            >
              Limpar
            </Button>
            <button className="uni-btn uni-btn-primary" type="submit" disabled={pending}>
              <i className={`pi ${pending ? "pi-spin pi-spinner" : "pi-key"}`} />
              <span>{pending ? "Trocando..." : "Trocar senha"}</span>
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

/** Campo de senha com label + toggle de visibilidade (F52-T007). */
function PasswordField({
  id,
  label,
  autoComplete,
  value,
  onChange,
  visible,
  onToggle,
}: {
  id: string;
  label: string;
  autoComplete: string;
  value: string;
  onChange: (value: string) => void;
  visible: boolean;
  onToggle: () => void;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label htmlFor={id} style={{ fontSize: 13, fontWeight: 500, color: "var(--uni-text-primary)" }}>
        {label}
      </label>
      <div className="uni-input-group">
        <input
          id={id}
          className="uni-input uni-input-with-icon"
          type={visible ? "text" : "password"}
          autoComplete={autoComplete}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          required
        />
        <button
          type="button"
          className="uni-input-action"
          onClick={onToggle}
          aria-label={visible ? "Ocultar senha" : "Mostrar senha"}
        >
          <i className={visible ? "pi pi-eye-slash" : "pi pi-eye"} />
        </button>
      </div>
    </div>
  );
}

/** Checklist vivo da política de senha (espelho do Identity — só feedback). */
function PasswordPolicyChecklist({ value }: { value: string }) {
  return (
    <ul
      aria-label="Requisitos da nova senha"
      style={{
        listStyle: "none",
        margin: 0,
        padding: "10px 14px",
        display: "flex",
        flexDirection: "column",
        gap: 6,
        background: "#f8fafc",
        border: "1px solid #e2e8f0",
        borderRadius: 10,
      }}
    >
      {PASSWORD_POLICY.map((rule) => {
        const ok = rule.test(value);
        return (
          <li
            key={rule.message}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              fontSize: 12,
              color: ok ? "#15803d" : "#64748b",
              fontWeight: ok ? 500 : 400,
            }}
          >
            <i className={`pi ${ok ? "pi-check-circle" : "pi-circle"}`} />
            <span>{rule.message.replace(/^A nova senha precisa de /, "")}</span>
          </li>
        );
      })}
    </ul>
  );
}
