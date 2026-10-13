import { useMemo, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { SearchInput } from "@/components/ui/SearchInput";
import { Button } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/SelectField";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { InlineMessages, useInlineMessages } from "@/components/ui/InlineMessages";
import { Can } from "@/components/auth/Can";
import { useAppSelector } from "@/app/store/hooks";
import { formatDateTime } from "@/utils/format";
import { USER_ROLES } from "@/types/roles";
import {
  listInvitations,
  getMyInvitations,
  acceptInvitation,
  rejectInvitation,
  updateInvitationRoles,
  cancelInvitation,
  type InvitationItem,
} from "@/services/api/invitations";
import {
  useListQuery,
  useUpdateMutation,
  errorMessage,
  mutationErrorMessage,
} from "@/hooks/queries";
import { useFormErrors } from "@/hooks/useFormErrors";

/**
 * ADR-030 Fase 5 — InvitationsPage com React Query e visões
 * tabela/grid/kanban (preferência persistida). Kanban agrupa por status
 * (6 estados). Admin ganha CRUD de convite: edição de roles
 * (`PUT /admin/invitations/{id}` — só pendente no backend) e cancelamento
 * (`DELETE`, só pendente); accept/reject continuam em
 * `POST /invitations/{id}/accept|reject`. Escrita atrás de `Can administer`
 * para admin (exibição; policy + antiforgery continuam sendo a autoridade).
 */

const statusColors: Record<string, { bg: string; color: string; label: string }> = {
  Pending: { bg: "#fef3c7", color: "#d97706", label: "Pendente" },
  Accepted: { bg: "#dcfce7", color: "#16a34a", label: "Aceito" },
  Rejected: { bg: "#fee2e2", color: "#dc2626", label: "Recusado" },
  Expired: { bg: "#f1f5f9", color: "#64748b", label: "Expirado" },
  Cancelled: { bg: "#f1f5f9", color: "#64748b", label: "Cancelado" },
  Activated: { bg: "#dcfce7", color: "#16a34a", label: "Ativado" },
};

const statusColumns = Object.entries(statusColors).map(([key, meta]) => ({
  key,
  label: meta.label,
}));

const statusFilterOptions = Object.entries(statusColors).map(([value, meta]) => ({
  value,
  label: meta.label,
}));

export function InvitationsPage() {
  const [view, setView] = useEntityViewMode("invitations", "table");
  const { messagesRef, showMessage } = useInlineMessages();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("");

  const roles = useAppSelector((state) => state.auth.user?.roles || []);
  const isAdmin = roles.includes("Admin");

  const invitations = useListQuery<InvitationItem>(
    ["invitations", isAdmin, search, statusFilter],
    async () => {
      if (isAdmin) {
        const data = await listInvitations({
          status: statusFilter || undefined,
          email: search || undefined,
          skip: 0,
          take: 50,
        });
        return data.items;
      }
      // Usuário comum vê apenas seus convites.
      return getMyInvitations();
    },
  );

  const [editTarget, setEditTarget] = useState<InvitationItem | null>(null);
  const [editRoles, setEditRoles] = useState<string[]>([]);
  // F29-T001C-2 — o erro continua adjacente ao grupo de checkboxes, que é onde
  // já estava e onde faz sentido: as "roles" são um grupo, não um campo com
  // `FormField`, então não há um input para pendurar a mensagem.
  const { error: formError, reset: resetFormError, fail: failForm } = useFormErrors();
  const [cancelTarget, setCancelTarget] = useState<InvitationItem | null>(null);

  const acceptMutation = useUpdateMutation({
    invalidate: ["invitations"],
    mutationFn: (id: string) => acceptInvitation(id),
    onSuccess: () => showMessage({ severity: "success", summary: "Convite aceito com sucesso." }),
    onError: (error) => showMessage({ severity: "error", summary: mutationErrorMessage(error) ?? "" }),
  });
  const rejectMutation = useUpdateMutation({
    invalidate: ["invitations"],
    mutationFn: (id: string) => rejectInvitation(id),
    onSuccess: () => showMessage({ severity: "success", summary: "Convite recusado com sucesso." }),
    onError: (error) => showMessage({ severity: "error", summary: mutationErrorMessage(error) ?? "" }),
  });
  const updateRolesMutation = useUpdateMutation({
    invalidate: ["invitations"],
    mutationFn: (input: { id: string; roles: string[] }) =>
      updateInvitationRoles(input.id, input.roles),
    onSuccess: () => showMessage({ severity: "success", summary: "Roles do convite atualizadas." }),
    // Recusa do servidor — nível do formulário, não de um campo.
    onError: (error) => failForm(errorMessage(error)),
  });
  const cancelMutation = useUpdateMutation({
    invalidate: ["invitations"],
    mutationFn: (id: string) => cancelInvitation(id),
    onSuccess: () => showMessage({ severity: "success", summary: "Convite cancelado." }),
    onError: (error) => showMessage({ severity: "error", summary: mutationErrorMessage(error) ?? "" }),
  });

  const busyId =
    acceptMutation.isPending ? acceptMutation.variables
    : rejectMutation.isPending ? rejectMutation.variables
    : cancelMutation.isPending ? cancelMutation.variables
    : null;

  const list = invitations.items;
  const filtered = useMemo(() => {
    return list.filter((inv) => {
      if (statusFilter && inv.status !== statusFilter) return false;
      if (search && !inv.email.toLowerCase().includes(search.toLowerCase())) return false;
      return true;
    });
  }, [list, statusFilter, search]);

  function openRolesEdit(invitation: InvitationItem) {
    setEditTarget(invitation);
    setEditRoles([...invitation.roles]);
    resetFormError();
  }

  async function submitRoles() {
    if (!editTarget) return;
    if (editRoles.length === 0) {
      failForm("Selecione ao menos uma role.");
      return;
    }
    resetFormError();
    try {
      await updateRolesMutation.mutateAsync({ id: editTarget.id, roles: editRoles });
      setEditTarget(null);
    } catch {
      // erro já exibido no onError (formError)
    }
  }

  async function confirmCancel() {
    if (!cancelTarget) return;
    try {
      await cancelMutation.mutateAsync(cancelTarget.id);
      setCancelTarget(null);
    } catch {
      // erro já exibido no onError (banner inline)
    }
  }

  const listError = mutationErrorMessage(invitations.error as Error | null);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Convites"
        subtitle={`${filtered.length} ${filtered.length === 1 ? "convite" : "convites"}${filtered.length !== list.length ? ` de ${list.length}` : ""}`}
        actions={
          <div className="flex align-items-center gap-2 flex-wrap">
            <ViewSwitcher mode={view} onChange={setView} />
          </div>
        }
      />

      {listError && (
        <ErrorBanner
          message={listError}
          onRetry={() => void invitations.refetch()}
        />
      )}

      <InlineMessages ref={messagesRef} />

      {isAdmin && (
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <div style={{ flex: 1 }}>
            <SearchInput
              value={search}
              onChange={setSearch}
              placeholder="Buscar por e-mail..."
            />
          </div>
          <SelectField
            value={statusFilter}
            onChange={setStatusFilter}
            options={[{ value: "", label: "Todos os status" }, ...statusFilterOptions]}
            style={{ minWidth: 170 }}
          />
        </div>
      )}

      <EntityView<InvitationItem>
        mode={view}
        items={filtered}
        getKey={(invitation) => invitation.id}
        emptyMessage={listError ? "—" : "Nenhum convite encontrado"}
        kanban={{ groupOf: (invitation) => invitation.status, columns: statusColumns }}
        table={
          <DataTablePanel value={filtered} dataKey="id" loading={invitations.isPending} stripedRows showGridlines emptyMessage={listError ? "—" : "Nenhum convite encontrado"} style={{ fontSize: 13 }}>
            <Column
              field="email"
              header="E-mail"
              sortable
              style={{ minWidth: 200 }}
            />
            <Column
              field="status"
              header="Status"
              sortable
              body={(row: InvitationItem) => {
                const meta = statusColors[row.status] || {
                  bg: "#f1f5f9",
                  color: "#64748b",
                  label: row.status,
                };
                return (
                  <StatusChip
                    label={meta.label}
                    background={meta.bg}
                    color={meta.color}
                  />
                );
              }}
              style={{ minWidth: 120 }}
            />
            <Column
              field="roles"
              header="Roles"
              body={(row: InvitationItem) =>
                row.projectId
                  ? `${row.projectCode ? `${row.projectCode} · ` : ""}${row.projectTitle ?? "Projeto"}${row.projectRole ? ` (${row.projectRole})` : ""}`
                  : row.roles.join(", ")
              }
              style={{ minWidth: 220 }}
            />
            <Column
              field="createdAt"
              header="Criado em"
              sortable
              body={(row: InvitationItem) => formatDateTime(new Date(row.createdAt))}
              style={{ minWidth: 150 }}
            />
            <Column
              field="expiresAt"
              header="Expira em"
              sortable
              body={(row: InvitationItem) => formatDateTime(new Date(row.expiresAt))}
              style={{ minWidth: 150 }}
            />
            <Column
              header="Ações"
              body={(row: InvitationItem) => (
                <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                  {row.status === "Pending" && (
                    <>
                      <Button
                        variant="primary"
                        onClick={() => acceptMutation.mutate(row.id)}
                        disabled={busyId === row.id}
                      >
                        {busyId === row.id ? "..." : "Aceitar"}
                      </Button>
                      <Button
                        variant="ghost"
                        onClick={() => rejectMutation.mutate(row.id)}
                        disabled={busyId === row.id}
                      >
                        {busyId === row.id ? "..." : "Recusar"}
                      </Button>
                    </>
                  )}
                  <Can level="administer">
                    {row.status === "Pending" && (
                      <>
                        {!row.projectId && (
                          <Button variant="ghost" onClick={() => openRolesEdit(row)}>
                            Roles
                          </Button>
                        )}
                        <Button variant="danger" onClick={() => setCancelTarget(row)}>
                          Cancelar
                        </Button>
                      </>
                    )}
                  </Can>
                </div>
              )}
              style={{ minWidth: 240 }}
            />
          </DataTablePanel>
        }
        renderCard={(invitation) => {
          const meta = statusColors[invitation.status] ?? {
            bg: "#f1f5f9", color: "#64748b", label: invitation.status,
          };
          return (
            <div className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 6 }}>
                <span style={{ fontWeight: 600, fontSize: 13, wordBreak: "break-all" }}>{invitation.email}</span>
                <StatusChip label={meta.label} background={meta.bg} color={meta.color} fontSize={10} fontWeight={600} />
              </div>
              <div style={{ display: "flex", gap: 4, flexWrap: "wrap", marginBottom: 8 }}>
                {invitation.projectId ? (
                  <span style={{ fontSize: 11, color: "#475569", marginBottom: 8 }}>
                    Projeto: {invitation.projectCode ? `${invitation.projectCode} · ` : ""}
                    {invitation.projectTitle ?? invitation.projectId}
                    {invitation.projectRole ? ` · Papel: ${invitation.projectRole}` : ""}
                  </span>
                ) : invitation.roles.map((role) => (
                  <span
                    key={role}
                    style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 999, background: "#eef2ff", color: "#6366f1" }}
                  >
                    {role}
                  </span>
                ))}
              </div>
              <div style={{ fontSize: 11, color: "#94a3b8" }}>
                Criado {formatDateTime(new Date(invitation.createdAt))} · Expira {formatDateTime(new Date(invitation.expiresAt))}
              </div>
              <div style={{ display: "flex", gap: 8, marginTop: 10, flexWrap: "wrap" }}>
                {invitation.status === "Pending" && (
                  <>
                    <Button
                      variant="primary"
                      onClick={() => acceptMutation.mutate(invitation.id)}
                      disabled={busyId === invitation.id}
                    >
                      {busyId === invitation.id ? "..." : "Aceitar"}
                    </Button>
                    <Button
                      variant="ghost"
                      onClick={() => rejectMutation.mutate(invitation.id)}
                      disabled={busyId === invitation.id}
                    >
                      {busyId === invitation.id ? "..." : "Recusar"}
                    </Button>
                  </>
                )}
                <Can level="administer">
                  {invitation.status === "Pending" && (
                    <>
                      <Button variant="ghost" onClick={() => openRolesEdit(invitation)}>
                        Roles
                      </Button>
                      <Button variant="danger" onClick={() => setCancelTarget(invitation)}>
                        Cancelar
                      </Button>
                    </>
                  )}
                </Can>
              </div>
            </div>
          );
        }}
      />

      <Dialog
        visible={editTarget !== null}
        onHide={() => setEditTarget(null)}
        header={editTarget ? `Roles do convite — ${editTarget.email}` : ""}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {USER_ROLES.map((role) => (
            <label
              key={role}
              style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13, cursor: "pointer" }}
            >
              <input
                type="checkbox"
                checked={editRoles.includes(role)}
                onChange={(event) =>
                  setEditRoles((current) =>
                    event.target.checked
                      ? [...current, role]
                      : current.filter((existing) => existing !== role),
                  )
                }
              />
              {role}
            </label>
          ))}
          {formError && (
            <p style={{ color: "#dc2626", fontSize: 12, margin: 0 }}>{formError}</p>
          )}
          <div className="flex justify-content-end gap-2" style={{ marginTop: 4 }}>
            <Button variant="ghost" onClick={() => setEditTarget(null)}>
              Cancelar
            </Button>
            <Button
              variant="primary"
              onClick={() => void submitRoles()}
              disabled={updateRolesMutation.isPending}
            >
              {updateRolesMutation.isPending ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        </div>
      </Dialog>

      <ConfirmDialog
        visible={cancelTarget !== null}
        onHide={() => setCancelTarget(null)}
        onConfirm={() => void confirmCancel()}
        header="Cancelar convite"
        message={
          cancelTarget
            ? `Cancelar o convite para "${cancelTarget.email}"? Só convites pendentes podem ser cancelados.`
            : ""
        }
        confirmLabel="Cancelar convite"
      />
    </div>
  );
}
