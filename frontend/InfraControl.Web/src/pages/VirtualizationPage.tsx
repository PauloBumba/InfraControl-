import { Column } from "primereact/column";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import {
  EntityView,
  ViewSwitcher,
  useEntityViewMode,
} from "@/components/ui/EntityView";
import {
  getVirtualizationOverview,
  type VirtualizationOverview,
  type VirtualMachineOverviewItem,
} from "@/services/api/virtualization";
import { useObjectQuery, mutationErrorMessage } from "@/hooks/queries";

/**
 * F31-T004 (docs/53 §4.4) — página Virtualização (VMs + vCenter).
 *
 * Índice no padrão EntityView (tabela/grid/kanban — decisão F26 "sempre os
 * 3 modos"; kanban agrupa por hypervisor, com coluna-overflow "Sem
 * hypervisor") sobre o read-model de `GET /inventory/virtualization`
 * (F31-T003): contagens no topo (VMs / vCenters / Órfãs — label+valor no
 * mesmo node, molde F30-T001 S5b) e a lista de VMs com hypervisor/vCenter
 * resolvidos por NOME. VM sem vínculo nenhum aparece com selo "Órfã".
 *
 * Somente leitura: vínculos e CRUD de device vivem na DevicesPage (D1);
 * atributos ao vivo (power/vCPU/RAM) chegam na F31-T005 com a ADR.
 */

/** Tile de contagem — label e valor no MESMO text node (contrato de teste S5b). */
function StatTile({ label, value }: { label: string; value: number }) {
  return (
    <div
      className="uni-panel"
      style={{ padding: "10px 16px", fontSize: 15, fontWeight: 600 }}
    >
      {label}: {value}
    </div>
  );
}

/** Selo da VM sem hypervisor e sem vCenter. */
function OrphanBadge() {
  return (
    <span
      style={{
        fontSize: 11,
        fontWeight: 700,
        padding: "2px 8px",
        borderRadius: 999,
        background: "#fef3c7",
        color: "#b45309",
      }}
    >
      Órfã
    </span>
  );
}

export function VirtualizationPage() {
  const [view, setView] = useEntityViewMode("virtualization", "table");

  const overview = useObjectQuery<VirtualizationOverview>(
    ["virtualizationOverview"],
    () => getVirtualizationOverview(),
  );

  if (overview.isPending) {
    return <LoadingBlock />;
  }

  const data = overview.data ?? null;
  const error = mutationErrorMessage(overview.error as Error | null);

  return (
    <ListPageShell
      title="Virtualização"
      singular="VM"
      plural="VMs"
      total={data?.totalVirtualMachines ?? 0}
      error={error}
      onRetry={() => void overview.refetch()}
      actions={<ViewSwitcher mode={view} onChange={setView} />}
    >
      {data && (
        <>
          <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
            <StatTile label="VMs" value={data.totalVirtualMachines} />
            <StatTile label="vCenters" value={data.totalVCenters} />
            <StatTile label="Órfãs" value={data.orphanVirtualMachines} />
          </div>

          <EntityView<VirtualMachineOverviewItem>
            mode={view}
            items={data.virtualMachines}
            getKey={(vm) => vm.id}
            emptyMessage="Nenhuma VM cadastrada"
            kanban={{
              groupOf: (vm) => vm.hypervisorId ?? "none",
              columns: [
                ...data.byHypervisor.map((group) => ({
                  key: group.deviceId,
                  label: group.name,
                })),
                { key: "none", label: "Sem hypervisor" },
              ],
              overflowColumnKey: "none",
            }}
            table={
              <DataTablePanel
                value={data.virtualMachines}
                dataKey="id"
                emptyMessage="Nenhuma VM cadastrada"
              >
                <Column
                  header="VM"
                  body={(vm: VirtualMachineOverviewItem) => (
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <i className="pi pi-desktop" style={{ color: "#8b5cf6" }} />
                      <span style={{ fontWeight: 600 }}>{vm.name}</span>
                    </div>
                  )}
                  style={{ minWidth: 180 }}
                />
                <Column
                  header="IP"
                  body={(vm: VirtualMachineOverviewItem) => vm.ip ?? "—"}
                  style={{ width: 140 }}
                />
                <Column
                  header="Hypervisor"
                  body={(vm: VirtualMachineOverviewItem) =>
                    vm.hypervisorName ?? "—"
                  }
                  style={{ minWidth: 160 }}
                />
                <Column
                  header="vCenter"
                  body={(vm: VirtualMachineOverviewItem) =>
                    vm.vCenterName ?? "—"
                  }
                  style={{ minWidth: 160 }}
                />
                <Column
                  header="Status"
                  body={(vm: VirtualMachineOverviewItem) =>
                    vm.isOrphan ? <OrphanBadge /> : "—"
                  }
                  style={{ width: 110 }}
                />
              </DataTablePanel>
            }
            renderCard={(vm) => (
              <div className="uni-panel" style={{ padding: 16 }}>
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    marginBottom: 8,
                  }}
                >
                  <i className="pi pi-desktop" style={{ color: "#8b5cf6" }} />
                  <span style={{ fontWeight: 600, fontSize: 14 }}>
                    {vm.name}
                  </span>
                  {vm.isOrphan && <OrphanBadge />}
                </div>
                <div style={{ fontSize: 12, color: "#64748b" }}>
                  {vm.ip ?? "sem IP"} · {vm.hypervisorName ?? "sem hypervisor"}
                  {" · "}
                  {vm.vCenterName ?? "sem vCenter"}
                </div>
              </div>
            )}
          />
        </>
      )}
    </ListPageShell>
  );
}
