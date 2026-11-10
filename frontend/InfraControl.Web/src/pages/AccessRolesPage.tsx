import { useCallback, useEffect, useState } from "react";
import { Button } from "primereact/button";
import { Card } from "primereact/card";
import { Dialog } from "primereact/dialog";
import { Dropdown } from "primereact/dropdown";
import { InputText } from "primereact/inputtext";
import { Message } from "primereact/message";
import { MultiSelect } from "primereact/multiselect";
import { ProgressSpinner } from "primereact/progressspinner";
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

/**
 * Perfis de acesso customizados: o admin cria papéis nomeados, define o
 * que cada um pode ver/fazer na matriz `/admin/access-policies` e atribui
 * aos usuários aqui. Perfis embutidos (Leitor/Operador/Admin) não são
 * editáveis — vêm do ADR-016.
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
  const [assignUser, setAssignUser] = useState<AdminUser | null>(null);
  const [assignKeys, setAssignKeys] = useState<string[]>([]);
  const [assignLoading, setAssignLoading] = useState(false);
  const [assignSaving, setAssignSaving] = useState(false);

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
    if (!window.confirm(`Remover o perfil "${role.label}"? Atribuições e permissões dele serão apagadas.`)) return;
    try {
      await deleteAccessRole(role.id);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao remover perfil.");
    }
  };

  const openAssign = async (user: AdminUser) => {
    setAssignUser(user);
    setAssignKeys([]);
    setAssignLoading(true);
    try {
      const assigned = await getUserAccessRoles(user.userId);
      setAssignKeys(assigned.map((a) => a.key));
    } catch {
      setAssignKeys([]);
    } finally {
      setAssignLoading(false);
    }
  };

  const saveAssign = async () => {
    if (!assignUser) return;
    setAssignSaving(true);
    setError(null);
    try {
      await setUserAccessRoles(assignUser.userId, assignKeys);
      setAssignUser(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao atribuir perfis.");
    } finally {
      setAssignSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center py-16">
        <ProgressSpinner />
      </div>
    );
  }

  const roleOptions = roles.map((r) => ({ label: r.label, value: r.key }));

  return (
    <div className="p-4 md:p-6 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Perfis de acesso</h1>
          <p className="text-sm text-gray-500">
            Crie perfis personalizados e defina o que cada um pode ver ou fazer
            na tela "Políticas de acesso". Depois atribua aos usuários.
          </p>
        </div>
        <Button label="Novo perfil" icon="pi pi-plus" onClick={openCreate} />
      </div>

      {error && <Message severity="error" text={error} />}

      <Card>
        {roles.length === 0 ? (
          <p className="text-sm text-gray-500 py-4">
            Nenhum perfil customizado ainda. Os perfis embutidos (Leitor,
            Operador, Administrador) já existem e são gerenciados na matriz de
            políticas.
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm border-collapse">
              <thead>
                <tr className="border-b text-left">
                  <th className="py-2 pr-4 font-medium">Perfil</th>
                  <th className="py-2 pr-4 font-medium">Chave</th>
                  <th className="py-2 pr-4 font-medium">Descrição</th>
                  <th className="py-2 pr-4 font-medium text-center">Usuários</th>
                  <th className="py-2 font-medium text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {roles.map((role) => (
                  <tr key={role.id} className="border-b last:border-0">
                    <td className="py-2 pr-4 font-medium">{role.label}</td>
                    <td className="py-2 pr-4 font-mono text-xs">{role.key}</td>
                    <td className="py-2 pr-4 text-gray-500">{role.description ?? "—"}</td>
                    <td className="py-2 pr-4 text-center">{role.userCount}</td>
                    <td className="py-2 text-right whitespace-nowrap">
                      <Button
                        icon="pi pi-pencil"
                        className="p-button-text p-button-sm"
                        onClick={() => openEdit(role)}
                      />
                      <Button
                        icon="pi pi-trash"
                        className="p-button-text p-button-danger p-button-sm"
                        onClick={() => void removeRole(role)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <Card title="Atribuir perfis a um usuário">
        <div className="flex flex-col md:flex-row gap-3 items-start md:items-end">
          <div className="flex flex-col gap-1 flex-1 min-w-56">
            <label className="text-sm font-medium">Usuário</label>
            <Dropdown
              value={assignUser}
              options={users}
              optionLabel="displayName"
              onChange={(e) => {
                const u = e.value as AdminUser;
                setAssignUser(u);
                void openAssign(u);
              }}
              placeholder="Selecione um usuário"
              className="w-full"
              filter
              showClear
            />
          </div>
          <div className="flex flex-col gap-1 flex-1 min-w-56">
            <label className="text-sm font-medium">Perfis customizados</label>
            <MultiSelect
              value={assignKeys}
              options={roleOptions}
              onChange={(e) => setAssignKeys(e.value as string[])}
              placeholder={assignLoading ? "Carregando…" : "Selecione os perfis"}
              className="w-full"
              disabled={!assignUser || assignLoading}
            />
          </div>
          <Button
            label="Salvar atribuição"
            icon="pi pi-save"
            loading={assignSaving}
            disabled={!assignUser || assignLoading}
            onClick={() => void saveAssign()}
          />
        </div>
        <p className="text-xs text-gray-400 mt-3">
          Perfis embutidos (Leitor/Operador/Admin) continuam sendo gerenciados
          em Usuários. O novo perfil passa a valer no próximo login do usuário.
        </p>
      </Card>

      <Dialog
        header={editing ? "Editar perfil" : "Novo perfil"}
        visible={dialogOpen}
        onHide={() => setDialogOpen(false)}
        className="w-full max-w-lg"
      >
        <div className="flex flex-col gap-4">
          {!editing && (
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Chave (imutável)</label>
              <InputText
                value={form.key}
                onChange={(e) => setForm({ ...form, key: e.target.value })}
                placeholder="ex.: auditor, gestorpdi"
              />
              <small className="text-gray-400">
                Slug minúsculo com hífens; usado internamente e não muda depois.
              </small>
            </div>
          )}
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Nome de exibição</label>
            <InputText
              value={form.label}
              onChange={(e) => setForm({ ...form, label: e.target.value })}
              placeholder="ex.: Auditor, Gestor de PDI"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Descrição</label>
            <InputText
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
            />
          </div>
          <div className="flex justify-end gap-2">
            <Button label="Cancelar" className="p-button-text" onClick={() => setDialogOpen(false)} />
            <Button
              label="Salvar"
              icon="pi pi-save"
              loading={saving}
              disabled={!form.label.trim() || (!editing && !form.key.trim())}
              onClick={() => void saveRole()}
            />
          </div>
        </div>
      </Dialog>
    </div>
  );
}
