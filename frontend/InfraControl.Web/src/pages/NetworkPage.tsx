import { useState, type FormEvent } from "react";
import { Can } from "@/components/auth/Can";
import {
  NETWORK_PAGE_SIZE,
  createVlan,
  deleteVlan,
  listVlans,
  updateVlan,
  type VlanItem,
} from "@/services/api/network";
import { Pagination } from "@/components/ui/Pagination";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField, FormError } from "@/components/ui/FormField";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { NetworkServicesSection } from "@/components/network/NetworkServicesSection";
import { Column } from "primereact/column";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import {
  useCreateMutation,
  useDeleteMutation,
  usePagedQuery,
  useUpdateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";

/**
 * F17-T008 + ADR-030 Fase 5 — NetworkPage (VLANs) com CRUD completo
 * (Operate) e visões tabela/grid (preferência persistida). Kanban não se
 * aplica: VLAN não tem campo de estado (EntityView sem config → grid).
 *
 * Contrato real: `GET/POST/PUT/DELETE /api/v1/network/vlans`. As abas
 * DHCP/Rotas/Firewall do protótipo voltaram no F18-T005 (ADR-042) via
 * `NetworkServicesSection` — leitura do equipamento configurado.
 */

export function NetworkPage() {
  const [page, setPage] = useState(1);
  const [view, setView] = useEntityViewMode("vlans", "grid");

  const vlans = usePagedQuery<VlanItem>(["vlans", page], () => listVlans(page));
  const totalCount = vlans.totalCount;

  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<VlanItem | null>(null);
  const [deleting, setDeleting] = useState<VlanItem | null>(null);
  const [name, setName] = useState("");
  const [vlanNumber, setVlanNumber] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const createMutation = useCreateMutation({
    invalidate: ["vlans"],
    mutationFn: (input: { name: string; vlanNumber: number; description?: string }) =>
      createVlan(input),
  });
  const updateMutation = useUpdateMutation({
    invalidate: ["vlans"],
    mutationFn: (input: {
      id: string;
      name: string;
      vlanNumber: number;
      description?: string;
    }) =>
      updateVlan(input.id, {
        name: input.name,
        vlanNumber: input.vlanNumber,
        description: input.description,
      }),
  });
  const deleteMutation = useDeleteMutation({
    invalidate: ["vlans"],
    mutationFn: (id: string) => deleteVlan(id),
  });

  function openCreate() {
    setEditing(null);
    setCreating(true);
    setName("");
    setVlanNumber("");
    setDescription("");
    setFormError(null);
  }

  function openEdit(vlan: VlanItem) {
    setCreating(false);
    setEditing(vlan);
    setName(vlan.name);
    setVlanNumber(String(vlan.vlanNumber));
    setDescription(vlan.description ?? "");
    setFormError(null);
  }

  function closeForm() {
    setCreating(false);
    setEditing(null);
    setFormError(null);
  }

  async function submitForm(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    const number = Number(vlanNumber);
    if (!trimmedName) {
      setFormError("Informe o nome da VLAN.");
      return;
    }
    if (!Number.isInteger(number) || number < 1 || number > 4094) {
      setFormError("O número da VLAN deve ser inteiro entre 1 e 4094.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          name: trimmedName,
          vlanNumber: number,
          description: description.trim() || undefined,
        });
        setNotice(`VLAN "${trimmedName}" (${number}) atualizada.`);
      } else {
        await createMutation.mutateAsync({
          name: trimmedName,
          vlanNumber: number,
          description: description.trim() || undefined,
        });
        setNotice(`VLAN "${trimmedName}" (${number}) criada.`);
      }
      closeForm();
    } catch (caught) {
      setFormError(mutationErrorMessage(caught as Error));
    } finally {
      setSaving(false);
    }
  }

  async function confirmDelete() {
    if (!deleting) return;
    try {
      await deleteMutation.mutateAsync(deleting.id);
      setNotice(`VLAN "${deleting.name}" excluída.`);
      setActionError(null);
    } catch (caught) {
      setActionError(mutationErrorMessage(caught as Error));
    }
  }

  const loading = vlans.isLoading;
  const formVisible = creating || editing !== null;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Rede"
        subtitle={
          loading
            ? "Carregando…"
            : `${totalCount} ${totalCount === 1 ? "VLAN" : "VLANs"} cadastradas`
        }
        actions={
          <div className="flex align-items-center gap-2 flex-wrap">
            <ViewSwitcher mode={view} onChange={setView} />
            <Can level="operate">
              <Button variant="primary" onClick={openCreate} style={{ padding: "6px 14px" }}>
                <i className="pi pi-plus" style={{ fontSize: 11 }} /> Adicionar
              </Button>
            </Can>
          </div>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}
      {(actionError || vlans.error) && (
        <ErrorBanner
          message={
            actionError ??
            mutationErrorMessage(vlans.error as Error) ??
            "Não foi possível falar com o servidor. Tente novamente."
          }
          onRetry={() => {
            setActionError(null);
            void vlans.refetch();
          }}
        />
      )}

      {loading && vlans.items.length === 0 ? (
        <LoadingBlock style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
      ) : (
        <EntityView<VlanItem>
          mode={view}
          items={vlans.items}
          getKey={(vlan) => vlan.id}
          emptyMessage={vlans.error ? "—" : "Nenhuma VLAN cadastrada"}
          table={
            <DataTablePanel value={vlans.items} dataKey="id" emptyMessage="Nenhuma VLAN cadastrada">
              <Column
                header="Número"
                style={{ width: 100 }}
                body={(vlan: VlanItem) => (
                  <span style={{ fontFamily: "monospace", fontWeight: 600, fontSize: 12 }}>
                    {vlan.vlanNumber}
                  </span>
                )}
              />
              <Column header="Nome" body={(vlan: VlanItem) => vlan.name} />
              <Column header="Descrição" body={(vlan: VlanItem) => vlan.description ?? "—"} />
              <Column
                header=""
                style={{ width: 150 }}
                body={(vlan: VlanItem) => (
                  <Can level="operate">
                    <div className="flex gap-1">
                      <Button variant="ghost" onClick={() => openEdit(vlan)}>
                        Editar
                      </Button>
                      <Button variant="ghost" onClick={() => setDeleting(vlan)}>
                        Excluir
                      </Button>
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(vlan) => (
            <div
              style={{
                background: "white",
                borderRadius: 8,
                border: "1px solid #e5e7eb",
                padding: 16,
              }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  marginBottom: 8,
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <div
                    style={{ width: 10, height: 10, borderRadius: "50%", background: "#3b82f6" }}
                  />
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>{vlan.name}</div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>
                      VLAN {vlan.vlanNumber}
                    </div>
                  </div>
                </div>
              </div>
              {vlan.description && (
                <div style={{ fontSize: 11, color: "#6b7280", lineHeight: 1.4 }}>
                  {vlan.description}
                </div>
              )}
              <Can level="operate">
                <div className="flex gap-1" style={{ marginTop: 12 }}>
                  <Button variant="ghost" onClick={() => openEdit(vlan)}>
                    Editar
                  </Button>
                  <Button variant="ghost" onClick={() => setDeleting(vlan)}>
                    Excluir
                  </Button>
                </div>
              </Can>
            </div>
          )}
        />
      )}

      {totalCount > NETWORK_PAGE_SIZE && (
        <Pagination
          page={page}
          totalPages={Math.max(1, Math.ceil(totalCount / NETWORK_PAGE_SIZE))}
          disabled={loading}
          onPage={setPage}
        />
      )}

      <NetworkServicesSection />

      <Dialog
        visible={formVisible}
        onHide={closeForm}
        header={editing ? "Editar VLAN" : "Nova VLAN"}
      >
        <form onSubmit={submitForm} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <FormField label="Nome *" error={null}>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Ex.: Voz"
              autoFocus
              maxLength={120}
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
              }}
            />
          </FormField>
          <FormField label="Número (1–4094) *" error={null}>
            <input
              type="number"
              min={1}
              max={4094}
              value={vlanNumber}
              onChange={(event) => setVlanNumber(event.target.value)}
              placeholder="100"
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
              }}
            />
          </FormField>
          <FormField label="Descrição" error={null}>
            <input
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Opcional"
              maxLength={500}
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
              }}
            />
          </FormField>
          <FormError message={formError ?? ""} />
          <div className="flex justify-content-end gap-2" style={{ marginTop: 4 }}>
            <Button type="button" variant="ghost" onClick={closeForm}>
              Cancelar
            </Button>
            <Button type="submit" variant="primary" disabled={saving}>
              {saving ? "Salvando…" : editing ? "Salvar" : "Criar VLAN"}
            </Button>
          </div>
        </form>
      </Dialog>

      <ConfirmDialog
        visible={deleting !== null}
        onHide={() => setDeleting(null)}
        onConfirm={() => void confirmDelete()}
        header="Excluir VLAN"
        message={
          deleting
            ? `Excluir "${deleting.name}" (VLAN ${deleting.vlanNumber})? Vínculos de porta bloqueiam a exclusão com 409.`
            : ""
        }
        confirmLabel="Excluir"
      />
    </div>
  );
}
