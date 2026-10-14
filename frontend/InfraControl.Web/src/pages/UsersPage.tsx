import { useCallback, useEffect, useState, type FormEvent } from "react";
import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { StatusChip } from "@/components/ui/StatusChip";
import { SearchInput } from "@/components/ui/SearchInput";
import { FormField, FormError } from "@/components/ui/FormField";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { Button } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/SelectField";
import { InlineMessages, useInlineMessages } from "@/components/ui/InlineMessages";
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
  deleteUser,
  listUsers,
  removeRole,
  setUserStatus,
  updateUser,
  type AdminUser,
} from "@/services/api/adminUsers";
import { errorMessage } from "@/hooks/queries";

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
 * `api/identity/users` foi removido do backend no F18-T004).
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

export function UsersPage() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { messagesRef, showMessage } = useInlineMessages();
  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState("users");
  const [busyUserId, setBusyUserId] = useState<string | null>(null);

  const [inviting, setInviting] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState<UserRole>("Viewer");
  const [invitingSaving, setInvitingSaving] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);

  const [editingUser, setEditingUser] = useState<AdminUser | null>(null);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [editSaving, setEditSaving] = useState(false);
  const [editError, setEditError] = useState<string | null>(null);

  const [deletingUser, setDeletingUser] = useState<AdminUser | null>(null);

  const [statusConfirmUser, setStatusConfirmUser] = useState<AdminUser | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setUsers(await listUsers());
    } catch (caught) {
      setError(errorMessage(caught));
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
      showMessage({ severity: "success", summary: success });
      await load();
    } catch (caught) {
      showMessage({ severity: "error", summary: "Não foi possível atualizar o usuário.", detail: errorMessage(caught) });
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
      showMessage({ severity: "success", summary: "Convite criado", detail: "O link sai por e-mail (hoje registrado no log do backend)." });
    } catch (caught) {
      setInviteError(errorMessage(caught));
    } finally {
      setInvitingSaving(false);
    }
  }

  function openEditDialog(user: AdminUser) {
    setEditingUser(user);
    setEditDisplayName(user.displayName);
    setEditEmail(user.email);
    setEditError(null);
  }

  async function submitEdit() {
    if (!editingUser) return;
    const displayName = editDisplayName.trim();
    const email = editEmail.trim();
    if (!displayName || !email) {
      setEditError("Nome e e-mail são obrigatórios.");
      return;
    }
    setEditSaving(true);
    setEditError(null);
    try {
      await updateUser(editingUser.userId, displayName, email);
      setEditingUser(null);
      showMessage({ severity: "success", summary: `${displayName} atualizado.` });
      await load();
    } catch (caught) {
      setEditError(errorMessage(caught));
    } finally {
      setEditSaving(false);
    }
  }

  function openDeleteDialog(user: AdminUser) {
    setDeletingUser(user);
  }

  async function confirmDelete() {
    if (!deletingUser) return;
    try {
      await deleteUser(deletingUser.userId);
      setDeletingUser(null);
      showMessage({ severity: "success", summary: `${deletingUser.displayName} excluído.` });
      await load();
    } catch (caught) {
      showMessage({ severity: "error", summary: "Não foi possível excluir o usuário.", detail: errorMessage(caught) });
      setDeletingUser(null);
    }
  }

  function openStatusConfirm(user: AdminUser) {
    setStatusConfirmUser(user);
  }

  async function confirmStatusChange() {
    if (!statusConfirmUser) return;
    const newStatus = !statusConfirmUser.isActive;
    await mutate(statusConfirmUser.userId, () => setUserStatus(statusConfirmUser.userId, newStatus), newStatus ? `${statusConfirmUser.displayName} ativado.` : `${statusConfirmUser.displayName} desativado.`);
    setStatusConfirmUser(null);
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
    <ListPageShell
      title="Usuários & Roles"
      singular="usuário"
      plural="usuários"
      total={users.length}
      loading={loading}
      note={`${USER_ROLES.length} roles`}
      notice={<InlineMessages ref={messagesRef} />}
      error={error}
      onRetry={() => void load()}
      actions={
        <div style={{ display: "flex", gap: 8 }}>
          <SearchInput
            value={search}
            onChange={setSearch}
            placeholder="Pesquisar usuários..."
            variant="muted"
          />
          <Can level="administer">
            <Button
              icon="pi pi-user-plus"
              onClick={() => { setInviting((value) => !value); setInviteError(null); }}
            >
              Convidar
            </Button>
          </Can>
        </div>
      }
    >

      {inviting && (
        <Dialog
          visible={inviting}
          onHide={() => { setInviting(false); setInviteError(null); }}
          header="Convidar usuário"
          footer={
            <div className="uni-modal-actions">
              <Button
                variant="ghost"
                onClick={() => { setInviting(false); setInviteError(null); }}
              >
                Cancelar
              </Button>
              <Button onClick={submitInvite} disabled={invitingSaving}>
                {invitingSaving ? "Enviando…" : "Criar convite"}
              </Button>
            </div>
          }
        >
          <form onSubmit={submitInvite} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <FormField label="E-mail *">
              <input
                type="email"
                value={inviteEmail}
                onChange={(event) => setInviteEmail(event.target.value)}
                placeholder="nome@empresa.com"
                autoFocus
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            <FormField label="Role">
              <SelectField
                value={inviteRole}
                onChange={(value) => setInviteRole(value as UserRole)}
                options={USER_ROLES.map((role) => ({ value: role, label: ROLE_LABEL[role] }))}
                style={{ width: "100%" }}
              />
            </FormField>
            <FormError message={inviteError ?? ""} />
          </form>
        </Dialog>
      )}

      {editingUser && (
        <Dialog
          visible={!!editingUser}
          onHide={() => setEditingUser(null)}
          header="Editar usuário"
          footer={
            <div className="uni-modal-actions">
              <Button variant="ghost" onClick={() => setEditingUser(null)}>
                Cancelar
              </Button>
              <Button onClick={submitEdit} disabled={editSaving}>
                {editSaving ? "Salvando…" : "Salvar"}
              </Button>
            </div>
          }
        >
          <form onSubmit={(e) => { e.preventDefault(); void submitEdit(); }} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <FormField label="Nome de exibição *">
              <input
                type="text"
                value={editDisplayName}
                onChange={(event) => setEditDisplayName(event.target.value)}
                placeholder="Nome do usuário"
                autoFocus
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            <FormField label="E-mail *">
              <input
                type="email"
                value={editEmail}
                onChange={(event) => setEditEmail(event.target.value)}
                placeholder="email@exemplo.com"
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            <FormError message={editError ?? ""} />
          </form>
        </Dialog>
      )}

      {deletingUser && (
        <ConfirmDialog
          visible={!!deletingUser}
          onHide={() => setDeletingUser(null)}
          onConfirm={confirmDelete}
          header="Excluir usuário"
          message={`Tem certeza que deseja excluir o usuário "${deletingUser.displayName}" (${deletingUser.email})? Esta ação não pode ser desfeita.`}
          confirmLabel="Excluir"
          severity="danger"
        />
      )}

      {statusConfirmUser && (
        <ConfirmDialog
          visible={!!statusConfirmUser}
          onHide={() => setStatusConfirmUser(null)}
          onConfirm={confirmStatusChange}
          header={statusConfirmUser.isActive ? "Desativar usuário" : "Ativar usuário"}
          message={statusConfirmUser.isActive
            ? `Tem certeza que deseja desativar o usuário "${statusConfirmUser.displayName}"? Ele não poderá mais acessar o sistema.`
            : `Tem certeza que deseja ativar o usuário "${statusConfirmUser.displayName}"?`}
          confirmLabel={statusConfirmUser.isActive ? "Desativar" : "Ativar"}
          severity={statusConfirmUser.isActive ? "warning" : "info"}
        />
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
          <DataTable
            value={filtered}
            loading={loading}
            dataKey="userId"
            size="small"
            emptyMessage={error ? "—" : "Nenhum usuário encontrado"}
            tableStyle={{ width: "100%" }}
          >
            <Column
              header="Usuário"
              body={(user: AdminUser) => <UserIdentityCell user={user} />}
            />
            <Column
              header="Roles"
              body={(user: AdminUser) => (
                <UserRolesCell
                  user={user}
                  busy={busyUserId === user.userId}
                  onMutate={(action, success) => mutate(user.userId, action, success)}
                />
              )}
            />
            <Column
              header="Status"
              body={(user: AdminUser) => (
                <StatusChip
                  label={user.isActive ? "Ativo" : "Inativo"}
                  background={user.isActive ? "#dcfce7" : "#f1f5f9"}
                  color={user.isActive ? "#16a34a" : "#64748b"}
                />
              )}
              style={{ width: 90 }}
            />
            <Column
              header="Atualizado em"
              body={(user: AdminUser) => formatDateTime(new Date(user.updatedAt))}
              style={{ width: 150, fontSize: 12, color: "#64748b" }}
            />
            <Column
              header="Ações"
              body={(user: AdminUser) => (
                <UserActionsCell
                  user={user}
                  busy={busyUserId === user.userId}
                  onEdit={() => openEditDialog(user)}
                  onDelete={() => openDeleteDialog(user)}
                  onStatusConfirm={() => openStatusConfirm(user)}
                />
              )}
              style={{ width: 110 }}
            />
          </DataTable>
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
                    <StatusChip label={ROLE_LABEL[role]} background={color.bg} color={color.color} fontWeight={600} />
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
    </ListPageShell>
  );
}

function UserIdentityCell({ user }: { user: AdminUser }) {
  const initials = (user.displayName || user.email)
    .split(" ")
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();

  return (
    <div className="uni-client-table-name">
      <div className="uni-client-table-icon" style={{ background: "#eff6ff", color: "#3b82f6", fontWeight: 600, fontSize: 12 }}>
        {initials}
      </div>
      <div>
        <div style={{ fontWeight: 500 }}>{user.displayName}</div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>{user.email}</div>
      </div>
    </div>
  );
}

function UserRolesCell({
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

  return (
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
                type="button"
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
          <SelectField
            value={addRole}
            disabled={busy}
            title="Atribuir role"
            placeholder="+ role"
            options={[
              { value: "", label: "+ role" },
              ...available.map((role) => ({ value: role, label: ROLE_LABEL[role] })),
            ]}
            onChange={(value) => {
              const role = value as UserRole;
              setAddRole("");
              if (role) void onMutate(() => assignRole(user.userId, role), `${ROLE_LABEL[role]} atribuído a ${user.displayName}.`);
            }}
            style={{ fontSize: 11, minWidth: 88 }}
          />
        )}
      </Can>
    </div>
  );
}

function UserActionsCell({
  user,
  busy,
  onEdit,
  onDelete,
  onStatusConfirm,
}: {
  user: AdminUser;
  busy: boolean;
  onEdit: () => void;
  onDelete: () => void;
  onStatusConfirm: () => void;
}) {
  return (
    <Can level="administer">
      <div style={{ display: "flex", gap: 4 }}>
        <button
          type="button"
          title="Editar"
          disabled={busy}
          onClick={onEdit}
          style={{
            width: 28, height: 28, borderRadius: 6, border: "1px solid #e2e8f0",
            background: "white", color: "#3b82f6",
            cursor: busy ? "default" : "pointer", fontSize: 12, opacity: busy ? 0.5 : 1,
          }}
        >
          <i className="pi pi-pencil" />
        </button>
        <button
          type="button"
          title={user.isActive ? "Desativar" : "Ativar"}
          disabled={busy}
          onClick={onStatusConfirm}
          style={{
            width: 28, height: 28, borderRadius: 6, border: "1px solid #e2e8f0",
            background: "white", color: user.isActive ? "#dc2626" : "#16a34a",
            cursor: busy ? "default" : "pointer", fontSize: 12, opacity: busy ? 0.5 : 1,
          }}
        >
          <i className="pi pi-power-off" />
        </button>
        <button
          type="button"
          title="Excluir"
          disabled={busy}
          onClick={onDelete}
          style={{
            width: 28, height: 28, borderRadius: 6, border: "1px solid #e2e8f0",
            background: "white", color: "#dc2626",
            cursor: busy ? "default" : "pointer", fontSize: 12, opacity: busy ? 0.5 : 1,
          }}
        >
          <i className="pi pi-trash" />
        </button>
      </div>
    </Can>
  );
}
