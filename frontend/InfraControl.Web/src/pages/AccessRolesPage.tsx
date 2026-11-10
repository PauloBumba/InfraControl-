import { useCallback, useEffect, useState } from "react";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { Button } from "@/components/ui/Button";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { FormField } from "@/components/ui/FormField";
import { SelectField } from "@/components/ui/SelectField";
import {
  createAccessRole,
  deleteAccessRole,
  getUserAccessRoles,
  listAccessRoles,
  setUserAccessRoles,
  updateAccessRole,
  type AccessRoleItem,
} from "@/services/api/accessPolicies";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: "100%",
  boxSizing: "border-box" as const,
};

/**
 * Perfis de acesso customizados: o admin cria papéis nomeados, define o
 * que cada um pode ver/fazer na matriz `/admin/access-policies` e atribui
 * aos usuários aqui. Perfis embutidos (Leitor/Operador/Admin) não são
 * editáveis — vêm do ADR-016.
 *
 * Layout no padrão da casa: ListPageShell + Dialog/FormField/SelectField.
 */
export function AccessRolesPage() {
  const [roles, setRoles] = useState<AccessRoleItem[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Dialog de perfil
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<AccessRoleItem | null>(null);
  const [form, setForm] = useState({ key: "", label: "", description: "" });
  const [saving, setSaving] = useState(false);

  // Atribuição a usuário
  const [assignUserId, setAssignUserId] = useState("");
  const [assignKeys, setAssignKeys] = useState<string[]>([]);
  const [assignLoading, setAssignLoading] = useState(false);
  const [assignSaving, setAssignSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<AccessRoleItem | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [r, u] = await Promise.all([listAccessRoles(), listUsers()]);
      setRoles(r);
      setUsers(u.filter((x) => x.isActive));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar perfis.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ key: "", label: "", description: "" });
    setDialogOpen(true);
  };

  const openEdit = (role: AccessRoleItem) => {
    setEditing(role);
    setForm({ key: role.key, label: role.label, description: role.description ?? "" });
    setDialogOpen(true);
  };

  const saveRole = async () => {
    setSaving(true);
    setError(null);
    try {
      if (editing) {
        await updateAccessRole(editing.id, {
          key: editing.key,
          label: form.label,
          description: form.description || null,
        });
      } else {
        await createAccessRole({
          key: form.key,
          label: form.label,
          description: form.description || null,
        });
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao salvar perfil.");
    } finally {
      setSaving(false);
    }
  };

  const removeRole = async (role: AccessRoleItem) => {
    try {
      await deleteAccessRole(role.id);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao remover perfil.");
    }
  };

  const openAssign = async (userId: string) => {
    setAssignUserId(userId);
    setAssignKeys([]);
    setAssignLoading(true);
    try {
      const assigned = await getUserAccessRoles(userId);
      setAssignKeys(assigned.map((a) => a.key));
    } catch {
      setAssignKeys([]);
    } finally {
      setAssignLoading(false);
    }
  };

  const saveAssign = async () => {
    if (!assignUserId) return;
    setAssignSaving(true);
    setError(null);
    try {
      await setUserAccessRoles(assignUserId, assignKeys);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao atribuir perfis.");
    } finally {
      setAssignSaving(false);
    }
  };

  const roleOptions = roles.map((r) => ({ label: r.label, value: r.key }));
  const assignUser = users.find((u) => u.userId === assignUserId) ?? null;

  return (
    <ListPageShell
      title="Perfis de acesso"
      singular="perfil"
      plural="perfis"
      total={roles.length}
      loading={loading}
      error={error}
      onRetry={() => void load()}
      actions={
        <Button icon="pi pi-plus" onClick={openCreate}>
          Novo perfil
        </Button>
      }
    >
      {!loading && (
        <>
          <div className="uni-panel" style={{ padding: 16 }}>
            {roles.length === 0 ? (
              <p style={{ fontSize: 13, color: "#6b7280", margin: 0 }}>
                Nenhum perfil customizado ainda. Os perfis embutidos (Leitor,
                Operador, Administrador) já existem e são gerenciados na matriz de
                políticas.
              </p>
            ) : (
              <div style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", fontSize: 13, borderCollapse: "collapse" }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid #e5e7eb", textAlign: "left" }}>
                      <th style={{ padding: "8px 16px 8px 0", fontWeight: 600 }}>Perfil</th>
                      <th style={{ padding: "8px 16px 8px 0", fontWeight: 600 }}>Chave</th>
                      <th style={{ padding: "8px 16px 8px 0", fontWeight: 600 }}>Descrição</th>
                      <th style={{ padding: "8px 8px", fontWeight: 600, textAlign: "center" }}>
                        Usuários
                      </th>
                      <th style={{ padding: "8px 0 8px 8px", fontWeight: 600, textAlign: "right" }}>
                        Ações
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {roles.map((role) => (
                      <tr key={role.id} style={{ borderBottom: "1px solid #f3f4f6" }}>
                        <td style={{ padding: "8px 16px 8px 0", fontWeight: 600 }}>
                          {role.label}
                        </td>
                        <td
                          style={{
                            padding: "8px 16px 8px 0",
                            fontFamily: "ui-monospace, monospace",
                            fontSize: 12,
                          }}
                        >
                          {role.key}
                        </td>
                        <td style={{ padding: "8px 16px 8px 0", color: "#6b7280" }}>
                          {role.description ?? "—"}
                        </td>
                        <td style={{ padding: "8px 8px", textAlign: "center" }}>
                          {role.userCount}
                        </td>
                        <td
                          style={{
                            padding: "8px 0 8px 8px",
                            textAlign: "right",
                            whiteSpace: "nowrap",
                          }}
                        >
                          <Button
                            variant="ghost"
                            icon="pi pi-pencil"
                            title="Editar perfil"
                            aria-label={`Editar ${role.label}`}
                            onClick={() => openEdit(role)}
                          />
                          <Button
                            variant="danger"
                            icon="pi pi-trash"
                            title="Remover perfil"
                            aria-label={`Remover ${role.label}`}
                            onClick={() => setConfirmDelete(role)}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="uni-panel" style={{ padding: 16 }}>
            <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 12 }}>
              Atribuir perfis a um usuário
            </div>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
                gap: 12,
                alignItems: "end",
              }}
            >
              <FormField label="Usuário">
                <SelectField
                  value={assignUserId}
                  options={users.map((u) => ({
                    label: u.displayName || u.email,
                    value: u.userId,
                  }))}
                  onChange={(v) => void openAssign(v)}
                  placeholder="Selecione um usuário"
                  aria-label="Usuário"
                />
              </FormField>
              <FormField label="Perfis customizados">
                <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
                  {roleOptions.length === 0 ? (
                    <span style={{ fontSize: 12, color: "#9ca3af" }}>
                      {assignLoading ? "Carregando…" : "Sem perfis customizados"}
                    </span>
                  ) : (
                    roleOptions.map((opt) => (
                      <label
                        key={opt.value}
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: 6,
                          fontSize: 13,
                          cursor: assignUser ? "pointer" : "not-allowed",
                          opacity: assignUser ? 1 : 0.5,
                        }}
                      >
                        <input
                          type="checkbox"
                          disabled={!assignUser || assignLoading}
                          checked={assignKeys.includes(opt.value)}
                          onChange={(e) =>
                            setAssignKeys((prev) =>
                              e.target.checked
                                ? [...prev, opt.value]
                                : prev.filter((k) => k !== opt.value),
                            )
                          }
                        />
                        {opt.label}
                      </label>
                    ))
                  )}
                </div>
              </FormField>
              <div>
                <Button
                  icon="pi pi-save"
                  disabled={!assignUser || assignLoading || assignSaving}
                  onClick={() => void saveAssign()}
                >
                  {assignSaving ? "Salvando…" : "Salvar atribuição"}
                </Button>
              </div>
            </div>
            <p style={{ fontSize: 12, color: "#9ca3af", marginTop: 12, marginBottom: 0 }}>
              Perfis embutidos (Leitor/Operador/Admin) continuam sendo gerenciados
              em Usuários. O novo perfil passa a valer no próximo login do usuário.
            </p>
          </div>

          <Dialog
            visible={dialogOpen}
            onHide={() => setDialogOpen(false)}
            header={editing ? "Editar perfil" : "Novo perfil"}
            width={480}
            footer={
              <div className="uni-modal-actions">
                <Button variant="ghost" onClick={() => setDialogOpen(false)}>
                  Cancelar
                </Button>
                <Button
                  disabled={
                    !form.label.trim() || (!editing && !form.key.trim()) || saving
                  }
                  onClick={() => void saveRole()}
                >
                  {saving ? "Salvando…" : "Salvar"}
                </Button>
              </div>
            }
          >
            <div style={{ display: "grid", gap: 12 }}>
              {!editing && (
                <FormField
                  label="Chave (imutável)"
                  error={
                    form.key !== "" && !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(form.key)
                      ? "Slug minúsculo com hífens."
                      : null
                  }
                >
                  <input
                    value={form.key}
                    onChange={(e) => setForm({ ...form, key: e.target.value })}
                    placeholder="ex.: auditor, gestorpdi"
                    style={inputStyle}
                    autoComplete="off"
                  />
                </FormField>
              )}
              <FormField label="Nome de exibição">
                <input
                  value={form.label}
                  onChange={(e) => setForm({ ...form, label: e.target.value })}
                  placeholder="ex.: Auditor, Gestor de PDI"
                  style={inputStyle}
                  autoComplete="off"
                />
              </FormField>
              <FormField label="Descrição">
                <input
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                  style={inputStyle}
                  autoComplete="off"
                />
              </FormField>
            </div>
          </Dialog>

          <ConfirmDialog
            visible={confirmDelete != null}
            onHide={() => setConfirmDelete(null)}
            onConfirm={() => {
              if (confirmDelete) void removeRole(confirmDelete);
            }}
            header="Remover perfil"
            message={
              confirmDelete
                ? `Remover o perfil "${confirmDelete.label}"? Atribuições e permissões dele serão apagadas.`
                : ""
            }
            confirmLabel="Remover"
          />
        </>
      )}
    </ListPageShell>
  );
}
