import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Can } from "@/components/auth/Can";
import { formatDateTime } from "@/utils/format";
import {
  ACCESS_LEVEL_LABEL,
  ROLE_ACCESS,
  ROLE_LABEL,
  USER_ROLES,
  type UserRole,
} from "@/types/roles";
import {
  ROLE_BY_VALUE,
  ROLE_VALUE,
  assignRole,
  createInvitation,
  listUsers,
  removeRole,
  setUserStatus,
  type AdminUser,
} from "@/services/api/adminUsers";

/**
 * F17-T006 — UsersPage conectada ao backend (substitui `users`/`roles` de
 * `data/dashboardData`). Contrato real: `GET /admin/users` devolve
 * `AdminUserSummary[]` (sem paginação, policy Operate) com `roles` NUMÉRICAS
 * (enum sem converter no MVC) e `isActive` booleano — não existem colunas de
 * sessões/último login nem status "Pendente" (convite não é usuário). A
 * aba Roles não tem endpoint de catálogo (não existe): os 3 papéis são o
 * enum canônico ADR-016 — estáticos aqui, com a contagem REAL de usuários
 * derivada da lista carregada. Escritas (status/papéis/convite) atrás de
 * `Can administer` (exibição; policy Administer + antiforgery são a
 * autoridade) — lista é Operate, então Viewer nem chega.
 *
 * Decisão de rota: só `api/v1/admin/users` (o `RolesController` em
 * `api/identity/users`, fora do padrão v1, fica de fora — ver TASKS).
 */

const tabs = [
  { id: "users", label: "Usuários", icon: "pi pi-users" },
  { id: "roles", label: "Roles", icon: "pi pi-id-card" },
];

const roleColors: Record<UserRole, { bg: string; color: string }> = {
  Admin: { bg: "#fee2e2", color: "#dc2626" },
  Operator: { bg: "#dbeafe", color: "#2563eb" },
  Viewer: { bg: "#f1f5f9", color: "#64748b" },
};

/** Copy fixa do catálogo (UI) — a semântica vem do ADR-016. */
const roleDescriptions: Record<UserRole, string> = {
  Admin: "Acesso total à plataforma",
  Operator: "Operação e tarefas do dia a dia",
  Viewer: "Somente leitura de dashboards e relatórios",
};

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function UsersPage() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState("users");
  const [busyUserId, setBusyUserId] = useState<string | null>(null);

  const [inviting, setInviting] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState<UserRole>("Viewer");
  const [invitingSaving, setInvitingSaving] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setUsers(await listUsers());
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function mutate(userId: string, action: () => Promise<unknown>, success: string) {
    setBusyUserId(userId);
    setError(null);
    try {
      await action();
      setNotice(success);
      await load();
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setBusyUserId(null);
    }
  }

  async function submitInvite(event: FormEvent) {
    event.preventDefault();
    const email = inviteEmail.trim();
    if (!email) {
      setInviteError("Informe o e-mail do convidado.");
      return;
    }
    setInvitingSaving(true);
    setInviteError(null);
    try {
      await createInvitation({ email, role: inviteRole });
      setInviting(false);
      setInviteEmail("");
      setInviteRole("Viewer");
      setNotice("Convite criado — o link sai por e-mail (hoje registrado no log do backend).");
    } catch (caught) {
      setInviteError(messageFor(caught));
    } finally {
      setInvitingSaving(false);
    }
  }

  const filtered = users.filter((user) => {
    const needle = search.trim().toLowerCase();
    if (!needle) return true;
    const roleNames = user.roles.map((value) => ROLE_BY_VALUE[value] ?? "");
    return (
      user.displayName.toLowerCase().includes(needle) ||
      user.email.toLowerCase().includes(needle) ||
      roleNames.some((name) => name.toLowerCase().includes(needle))
    );
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Usuários & Roles</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {loading ? "Carregando…" : `${users.length} ${users.length === 1 ? "usuário" : "usuários"} · ${USER_ROLES.length} roles`}
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#f1f5f9", borderRadius: 6, padding: "6px 12px" }}>
            <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
            <input
              type="text"
              placeholder="Pesquisar usuários..."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              style={{ border: "none", background: "none", outline: "none", fontSize: 13, width: 180 }}
            />
          </div>
          <Can level="administer">
            <button
              onClick={() => { setInviting((value) => !value); setInviteError(null); }}
              style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 12px", background: "#3b82f6", color: "white", fontSize: 13, fontWeight: 500, border: "none", borderRadius: 6 }}
            >
              <i className="pi pi-user-plus" style={{ fontSize: 12 }} /> Convidar
            </button>
          </Can>
        </div>
      </div>

      {notice && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#f0fdf4", color: "#15803d" }}>
          <i className="pi pi-check-circle" />
          <span>{notice}</span>
          <button onClick={() => setNotice(null)} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer" }}>
            <i className="pi pi-times" />
          </button>
        </div>
      )}

      {error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
          <button onClick={() => void load()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
            Tentar de novo
          </button>
        </div>
      )}

      {inviting && (
        <form onSubmit={submitInvite} style={{ display: "flex", alignItems: "flex-end", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8, flexWrap: "wrap" }}>
          <label style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 12, color: "#64748b" }}>
            E-mail *
            <input
              type="email"
              value={inviteEmail}
              onChange={(event) => setInviteEmail(event.target.value)}
              placeholder="nome@empresa.com"
              autoFocus
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: 240 }}
            />
          </label>
          <label style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 12, color: "#64748b" }}>
            Role
            <select
              value={inviteRole}
              onChange={(event) => setInviteRole(event.target.value as UserRole)}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, background: "white" }}
            >
              {USER_ROLES.map((role) => (
                <option key={role} value={role}>{ROLE_LABEL[role]}</option>
              ))}
            </select>
          </label>
          {inviteError && <div style={{ fontSize: 12, color: "#b91c1c", alignSelf: "center" }}>{inviteError}</div>}
          <div style={{ display: "flex", gap: 8, marginLeft: "auto" }}>
            <button
              type="button"
              onClick={() => { setInviting(false); setInviteError(null); }}
              style={{ padding: "6px 14px", borderRadius: 6, background: "white", border: "1px solid #e5e7eb", fontSize: 12, color: "#374151" }}
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={invitingSaving}
              style={{ padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500, opacity: invitingSaving ? 0.6 : 1 }}
            >
              {invitingSaving ? "Enviando…" : "Criar convite"}
            </button>
          </div>
        </form>
      )}

      <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              display: "flex", alignItems: "center", gap: 6, padding: "8px 14px",
              fontSize: 13, fontWeight: 500,
              color: activeTab === tab.id ? "#3b82f6" : "#64748b",
              background: "none", border: "none",
              borderBottom: activeTab === tab.id ? "2px solid #3b82f6" : "2px solid transparent",
              marginBottom: -1, cursor: "pointer",
            }}
          >
            <i className={tab.icon} style={{ fontSize: 12 }} />
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === "users" && (
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Usuário</th>
                <th>Roles</th>
                <th style={{ width: 90 }}>Status</th>
                <th style={{ width: 150 }}>Atualizado em</th>
                <th style={{ width: 110 }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((user) => (
                <UserRow
                  key={user.userId}
                  user={user}
                  busy={busyUserId === user.userId}
                  onMutate={(action, success) => mutate(user.userId, action, success)}
                />
              ))}
              {!loading && filtered.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                    {error ? "—" : "Nenhum usuário encontrado"}
                  </td>
                </tr>
              )}
              {loading && filtered.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                    Carregando…
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {activeTab === "roles" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 16 }}>
          {USER_ROLES.map((role) => {
            const color = roleColors[role];
            const count = users.filter((user) => user.roles.includes(ROLE_VALUE[role])).length;
            const level = ROLE_ACCESS[role];
            return (
              <div key={role} className="uni-panel" style={{ padding: 16 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span style={{ fontSize: 12, fontWeight: 600, padding: "3px 10px", borderRadius: 999, background: color.bg, color: color.color }}>
                      {ROLE_LABEL[role]}
                    </span>
                    <span style={{ fontSize: 10, color: "#94a3b8" }}>Sistema</span>
                  </div>
                  <span style={{ fontSize: 12, color: "#64748b" }}>{count} {count === 1 ? "usuário" : "usuários"}</span>
                </div>
                <p style={{ fontSize: 13, color: "#64748b", marginBottom: 12 }}>{roleDescriptions[role]}</p>
                <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f1f5f9", color: "#475569" }}>
                  Nível: {ACCESS_LEVEL_LABEL[level]}
                </span>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function UserRow({
  user,
  busy,
  onMutate,
}: {
  user: AdminUser;
  busy: boolean;
  onMutate: (action: () => Promise<unknown>, success: string) => Promise<void>;
}) {
  const [addRole, setAddRole] = useState("");

  const assigned = user.roles
    .map((value) => ROLE_BY_VALUE[value])
    .filter((role): role is UserRole => role !== undefined);
  const available = USER_ROLES.filter((role) => !assigned.includes(role));

  const initials = (user.displayName || user.email)
    .split(" ")
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();

  return (
    <tr>
      <td>
        <div className="uni-client-table-name">
          <div className="uni-client-table-icon" style={{ background: "#eff6ff", color: "#3b82f6", fontWeight: 600, fontSize: 12 }}>
            {initials}
          </div>
          <div>
            <div style={{ fontWeight: 500 }}>{user.displayName}</div>
            <div style={{ fontSize: 11, color: "#94a3b8" }}>{user.email}</div>
          </div>
        </div>
      </td>
      <td>
        <div style={{ display: "flex", gap: 4, flexWrap: "wrap", alignItems: "center" }}>
          {assigned.map((role) => {
            const color = roleColors[role];
            return (
              <span
                key={role}
                style={{
                  display: "inline-flex", alignItems: "center", gap: 4,
                  fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4,
                  background: color.bg, color: color.color,
                }}
              >
                {ROLE_LABEL[role]}
                <Can level="administer">
                  <button
                    title={`Remover ${ROLE_LABEL[role]}`}
                    disabled={busy}
                    onClick={() => onMutate(
                      () => removeRole(user.userId, role),
                      `${ROLE_LABEL[role]} removido de ${user.displayName}.`,
                    )}
                    style={{ background: "none", border: "none", color: "inherit", cursor: busy ? "default" : "pointer", padding: 0, fontSize: 10, opacity: busy ? 0.5 : 1 }}
                  >
                    <i className="pi pi-times" />
                  </button>
                </Can>
              </span>
            );
          })}
          <Can level="administer">
            {available.length > 0 && (
              <select
                value={addRole}
                disabled={busy}
                title="Atribuir role"
                onChange={(event) => {
                  const role = event.target.value as UserRole;
                  setAddRole("");
                  if (role) void onMutate(() => assignRole(user.userId, role), `${ROLE_LABEL[role]} atribuído a ${user.displayName}.`);
                }}
                style={{ fontSize: 11, padding: "2px 4px", borderRadius: 4, border: "1px dashed #cbd5e1", background: "white", color: "#64748b" }}
              >
                <option value="">+ role</option>
                {available.map((role) => (
                  <option key={role} value={role}>{ROLE_LABEL[role]}</option>
                ))}
              </select>
            )}
          </Can>
        </div>
      </td>
      <td>
        <span
          style={{
            fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4,
            background: user.isActive ? "#dcfce7" : "#f1f5f9",
            color: user.isActive ? "#16a34a" : "#64748b",
          }}
        >
          {user.isActive ? "Ativo" : "Inativo"}
        </span>
      </td>
      <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(user.updatedAt))}</td>
      <td>
        <Can level="administer">
          <button
            title={user.isActive ? "Desativar" : "Ativar"}
            disabled={busy}
            onClick={() => onMutate(
              () => setUserStatus(user.userId, !user.isActive),
              user.isActive ? `${user.displayName} desativado.` : `${user.displayName} ativado.`,
            )}
            style={{
              width: 28, height: 28, borderRadius: 6, border: "1px solid #e2e8f0",
              background: "white", color: user.isActive ? "#dc2626" : "#16a34a",
              cursor: busy ? "default" : "pointer", fontSize: 12, opacity: busy ? 0.5 : 1,
            }}
          >
            <i className="pi pi-power-off" />
          </button>
        </Can>
      </td>
    </tr>
  );
}
