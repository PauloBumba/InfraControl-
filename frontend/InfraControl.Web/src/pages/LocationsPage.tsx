import { useMemo, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { FormField, FormError } from "@/components/ui/FormField";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { Column } from "primereact/column";
import { Can } from "@/components/auth/Can";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import {
  errorMessage,
  useCreateMutation,
  useDeleteMutation,
  usePagedQuery,
  useUpdateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";
import { useFormErrors } from "@/hooks/useFormErrors";
import {
  createLocation,
  createRack,
  deleteLocation,
  deleteRack,
  listDevices,
  listLocations,
  listRacks,
  updateLocation,
  updateRack,
  type LocationItem,
  type RackItem,
} from "@/services/api/inventory";

/**
 * F17-T018 + ADR-030 Fase 5 — Locations & Racks com CRUD completo
 * (Operate) e visões tabela/grid/kanban (preferência persistida por
 * entidade). Leitura/mutations via React Query (`usePagedQuery` +
 * `use*Mutation` com invalidação por prefixo da chave).
 *
 * Kanban: racks agrupados por local (colunas = locais); locations não têm
 * campo de estado, então o kanban cai no grid (EntityView sem config).
 * Exclusão bloqueada pelo backend (409: racks/devices vinculados).
 */

interface FormState {
  mode: "create" | "edit";
  /** `null` = criação. */
  target: LocationItem | RackItem | null;
}

const emptyForm = { name: "", description: "" };

export function LocationsPage() {
  const [tab, setTab] = useState<"locations" | "racks">("locations");
  const [locationView, setLocationView] = useEntityViewMode("locations", "grid");
  const [rackView, setRackView] = useEntityViewMode("racks", "grid");

  const locations = usePagedQuery(["locations"], () => listLocations());
  const racks = usePagedQuery(["racks"], () => listRacks());
  const devices = usePagedQuery(["devices"], () => listDevices());

  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState<FormState>({ mode: "create", target: null });
  const [values, setValues] = useState(emptyForm);
  // F29-T001C-2 — cada falha declara o campo que a originou. Só "O nome é
  // obrigatório" é de campo; "Cadastre um local antes de criar racks" e a
  // recusa do servidor são do formulário inteiro (nenhum campo isolado está
  // errado) e ficam em `FormError`, de propósito.
  const {
    error: formError,
    field,
    reset: resetFormError,
    fail: failForm,
    errorFor,
  } = useFormErrors();
  const [deleteTarget, setDeleteTarget] = useState<
    { kind: "location" | "rack"; target: LocationItem | RackItem } | null
  >(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const createLocationMutation = useCreateMutation({
    invalidate: ["locations"],
    mutationFn: (input: { name: string; description?: string }) => createLocation(input),
  });
  const updateLocationMutation = useUpdateMutation({
    invalidate: ["locations"],
    mutationFn: (input: { id: string; name: string; description?: string }) =>
      updateLocation(input.id, { name: input.name, description: input.description }),
  });
  const deleteLocationMutation = useDeleteMutation({
    invalidate: ["locations"],
    mutationFn: (id: string) => deleteLocation(id),
  });
  const createRackMutation = useCreateMutation({
    invalidate: ["racks"],
    mutationFn: (input: { locationId: string; name: string; description?: string }) =>
      createRack(input),
  });
  const updateRackMutation = useUpdateMutation({
    invalidate: ["racks"],
    mutationFn: (input: { id: string; name: string; description?: string }) =>
      updateRack(input.id, { name: input.name, description: input.description }),
  });
  const deleteRackMutation = useDeleteMutation({
    invalidate: ["racks"],
    mutationFn: (id: string) => deleteRack(id),
  });

  const racksTruncated = racks.totalCount > racks.items.length;
  const devicesTruncated = devices.totalCount > devices.items.length;
  const countsTruncated = racksTruncated || devicesTruncated;

  const countFor = (list: Array<{ locationId: string }>, locationId: string): number =>
    list.filter((entry) => entry.locationId === locationId).length;

  const locationName = (id: string): string =>
    locations.items.find((loc) => loc.id === id)?.name ?? id;

  const kanbanColumns = useMemo(
    () => [
      ...locations.items.map((loc) => ({ key: loc.id, label: loc.name })),
      { key: "none", label: "Sem local" },
    ],
    [locations.items],
  );

  if (locations.isLoading || racks.isLoading) {
    return <LoadingBlock />;
  }

  const isLocationTab = tab === "locations";
  const total = isLocationTab ? locations.totalCount : racks.totalCount;
  const subtitle = isLocationTab
    ? `${locations.totalCount} locais · ${racks.totalCount} racks · ${devices.totalCount} dispositivos`
    : `${racks.totalCount} racks · ${locations.totalCount} locais`;

  function openCreate() {
    setForm({ mode: "create", target: null });
    setValues(emptyForm);
    resetFormError();
    setFormOpen(true);
  }

  function openEdit(target: LocationItem | RackItem) {
    setForm({ mode: "edit", target });
    setValues({ name: target.name, description: target.description ?? "" });
    resetFormError();
    setFormOpen(true);
  }

  async function submitForm(event: FormEvent) {
    event.preventDefault();
    resetFormError();
    const name = values.name.trim();
    if (!name) {
      // Única validação deste formulário que é de UM campo.
      failForm("O nome é obrigatório.", "name");
      return;
    }

    try {
      if (isLocationTab) {
        if (form.mode === "create") {
          await createLocationMutation.mutateAsync({ name, description: values.description });
        } else if (form.target) {
          await updateLocationMutation.mutateAsync({
            id: form.target.id,
            name,
            description: values.description,
          });
        }
      } else if (form.mode === "create") {
        const primary = locations.items[0];
        if (!primary) {
          // Pré-condição de dado, não de campo: o nome digitado está válido,
          // o que falta é um local na base. Culpar "Nome" apontaria errado.
          failForm("Cadastre um local antes de criar racks.");
          return;
        }
        await createRackMutation.mutateAsync({
          locationId: primary.id,
          name,
          description: values.description,
        });
      } else if (form.target) {
        await updateRackMutation.mutateAsync({
          id: form.target.id,
          name,
          description: values.description,
        });
      }
      setFormOpen(false);
    } catch (caught) {
      // Recusa do servidor não aponta campo: o backend sabe menos sobre o
      // que o usuário pretendeu do que a validação local. `errorMessage`
      // aceita `unknown` (o `catch` devolve isso); `mutationErrorMessage`
      // exigiria `Error | null` e devolveria `string | null`, que o `fail`
      // não aceita.
      failForm(errorMessage(caught));
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    try {
      if (deleteTarget.kind === "location") {
        await deleteLocationMutation.mutateAsync(deleteTarget.target.id);
      } else {
        await deleteRackMutation.mutateAsync(deleteTarget.target.id);
      }
      setActionError(null);
    } catch (caught) {
      setActionError(mutationErrorMessage(caught as Error));
    }
  }

  const formTitle =
    isLocationTab
      ? form.mode === "create" ? "Novo local" : "Editar local"
      : form.mode === "create" ? "Novo rack" : "Editar rack";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Locations & Racks"
        subtitle={
          <>
            {subtitle}
            <span style={{ display: "block", fontSize: 12, marginTop: 2 }}>
              {total} registro(s) nesta aba
            </span>
          </>
        }
        actions={
          <div className="flex align-items-center gap-2 flex-wrap">
            <ViewSwitcher
              mode={isLocationTab ? locationView : rackView}
              onChange={isLocationTab ? setLocationView : setRackView}
            />
            <Can level="operate">
              <Button variant="primary" onClick={openCreate}>
                <i className="pi pi-plus" /> {isLocationTab ? "Novo local" : "Novo rack"}
              </Button>
            </Can>
          </div>
        }
      />

      <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
        {([
          { id: "locations", label: "Locations" },
          { id: "racks", label: "Racks" },
        ] as const).map((t) => (
          <button
            key={t.id}
            type="button"
            onClick={() => setTab(t.id)}
            style={{
              padding: "8px 14px",
              fontSize: 13,
              fontWeight: 500,
              color: tab === t.id ? "#3b82f6" : "#64748b",
              background: "none",
              border: "none",
              borderBottom: tab === t.id ? "2px solid #3b82f6" : "2px solid transparent",
              marginBottom: -1,
              cursor: "pointer",
            }}
          >
            {t.label}
          </button>
        ))}
      </div>

      {(actionError || locations.error || racks.error) && (
        <ErrorBanner
          message={
            actionError ??
            mutationErrorMessage((locations.error ?? racks.error) as Error) ??
            "Não foi possível falar com o servidor. Tente novamente."
          }
          onRetry={() => {
            setActionError(null);
            void locations.refetch();
            void racks.refetch();
          }}
        />
      )}

      {countsTruncated && (
        <p style={{ fontSize: 11, color: "#94a3b8" }}>
          Contagens por local calculadas sobre os primeiros {racks.items.length} racks e{" "}
          {devices.items.length} dispositivos (marcados com ~)
        </p>
      )}

      {isLocationTab ? (
        <EntityView<LocationItem>
          mode={locationView}
          items={locations.items}
          getKey={(loc) => loc.id}
          emptyMessage="Nenhum local cadastrado"
          table={
            <DataTablePanel value={locations.items} dataKey="id" emptyMessage="Nenhum local cadastrado">
              <Column header="Nome" body={(loc: LocationItem) => loc.name} />
              <Column header="Descrição" body={(loc: LocationItem) => loc.description ?? "—"} />
              <Column
                header="Racks"
                style={{ width: 90 }}
                body={(loc: LocationItem) =>
                  `${racksTruncated ? "~" : ""}${countFor(racks.items, loc.id)}`
                }
              />
              <Column
                header="Dispositivos"
                style={{ width: 120 }}
                body={(loc: LocationItem) =>
                  `${devicesTruncated ? "~" : ""}${countFor(devices.items, loc.id)}`
                }
              />
              <Column
                header=""
                style={{ width: 150 }}
                body={(loc: LocationItem) => (
                  <Can level="operate">
                    <div className="flex gap-1">
                      <Button variant="ghost" onClick={() => openEdit(loc)}>
                        Editar
                      </Button>
                      <Button variant="ghost" onClick={() => setDeleteTarget({ kind: "location", target: loc })}>
                        Excluir
                      </Button>
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(loc) => (
            <div className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
                <div
                  style={{
                    width: 36,
                    height: 36,
                    borderRadius: 8,
                    background: "#eff6ff",
                    color: "#3b82f6",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  <i className="pi pi-map-marker" />
                </div>
                <div style={{ fontWeight: 600, fontSize: 14 }}>{loc.name}</div>
              </div>
              <p style={{ fontSize: 12, color: "#64748b", marginBottom: 12 }}>
                {loc.description ?? "—"}
              </p>
              <div style={{ display: "flex", gap: 16 }}>
                <div>
                  <div style={{ fontSize: 10, color: "#94a3b8" }}>Racks</div>
                  <div style={{ fontSize: 16, fontWeight: 700 }}>
                    {racksTruncated ? "~" : ""}
                    {countFor(racks.items, loc.id)}
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: 10, color: "#94a3b8" }}>Dispositivos</div>
                  <div style={{ fontSize: 16, fontWeight: 700 }}>
                    {devicesTruncated ? "~" : ""}
                    {countFor(devices.items, loc.id)}
                  </div>
                </div>
              </div>
              <Can level="operate">
                <div className="flex gap-1" style={{ marginTop: 12 }}>
                  <Button variant="ghost" onClick={() => openEdit(loc)}>
                    Editar
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => setDeleteTarget({ kind: "location", target: loc })}
                  >
                    Excluir
                  </Button>
                </div>
              </Can>
            </div>
          )}
        />
      ) : (
        <EntityView<RackItem>
          mode={rackView}
          items={racks.items}
          getKey={(rack) => rack.id}
          emptyMessage="Nenhum rack cadastrado"
          kanban={{
            groupOf: (rack) => rack.locationId || "none",
            columns: kanbanColumns,
            overflowColumnKey: "none",
          }}
          table={
            <DataTablePanel value={racks.items} dataKey="id" emptyMessage="Nenhum rack cadastrado">
              <Column header="Nome" body={(rack: RackItem) => rack.name} />
              <Column header="Local" body={(rack: RackItem) => locationName(rack.locationId)} />
              <Column header="Descrição" body={(rack: RackItem) => rack.description ?? "—"} />
              <Column
                header=""
                style={{ width: 150 }}
                body={(rack: RackItem) => (
                  <Can level="operate">
                    <div className="flex gap-1">
                      <Button variant="ghost" onClick={() => openEdit(rack)}>
                        Editar
                      </Button>
                      <Button
                        variant="ghost"
                        onClick={() => setDeleteTarget({ kind: "rack", target: rack })}
                      >
                        Excluir
                      </Button>
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(rack) => (
            <div className="uni-panel" style={{ padding: 16 }}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  marginBottom: 8,
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <i className="pi pi-server" style={{ color: "#8b5cf6" }} />
                  <span style={{ fontWeight: 600, fontSize: 14 }}>{rack.name}</span>
                </div>
                <span style={{ fontSize: 11, color: "#94a3b8" }}>
                  {locationName(rack.locationId)}
                </span>
              </div>
              <p style={{ fontSize: 12, color: "#64748b" }}>{rack.description ?? "—"}</p>
              <Can level="operate">
                <div className="flex gap-1" style={{ marginTop: 12 }}>
                  <Button variant="ghost" onClick={() => openEdit(rack)}>
                    Editar
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => setDeleteTarget({ kind: "rack", target: rack })}
                  >
                    Excluir
                  </Button>
                </div>
              </Can>
            </div>
          )}
        />
      )}

      <Dialog visible={formOpen} onHide={() => setFormOpen(false)} header={formTitle}>
        <form onSubmit={submitForm} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <FormField label="Nome" error={errorFor("name")}>
            <input
              autoFocus
              value={values.name}
              onChange={(event) => setValues((v) => ({ ...v, name: event.target.value }))}
              placeholder={isLocationTab ? "Sala Rack A" : "Rack 01"}
              style={{
                padding: "8px 10px",
                fontSize: 13,
                border: "1px solid #e2e8f0",
                borderRadius: 6,
              }}
            />
          </FormField>
          <FormField label="Descrição (opcional)" error={null}>
            <input
              value={values.description}
              onChange={(event) => setValues((v) => ({ ...v, description: event.target.value }))}
              placeholder="Andar 1"
              style={{
                padding: "8px 10px",
                fontSize: 13,
                border: "1px solid #e2e8f0",
                borderRadius: 6,
              }}
            />
          </FormField>
          <FormError message={field ? "" : (formError ?? "")} />
          <div className="flex justify-content-end gap-2" style={{ marginTop: 4 }}>
            <Button variant="ghost" onClick={() => setFormOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" type="submit">
              {form.mode === "create" ? "Criar" : "Salvar"}
            </Button>
          </div>
        </form>
      </Dialog>

      <ConfirmDialog
        visible={deleteTarget !== null}
        onHide={() => setDeleteTarget(null)}
        onConfirm={() => void confirmDelete()}
        header={deleteTarget?.kind === "location" ? "Excluir local" : "Excluir rack"}
        message={
          deleteTarget
            ? `Excluir "${deleteTarget.target.name}"? Vínculos (racks/devices) bloqueiam a exclusão com 409.`
            : ""
        }
        confirmLabel="Excluir"
      />
    </div>
  );
}
