import { useEffect, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { SelectField } from "@/components/ui/SelectField";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { StatusChip } from "@/components/ui/StatusChip";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import {
  listLocations,
  listSwitchPorts,
  type DeviceItem,
  type LocationItem,
  type SwitchPortItem,
} from "@/services/api/inventory";
import { listSwitches } from "@/services/api/network";
import {
  useListQuery,
  usePagedQuery,
  mutationErrorMessage,
} from "@/hooks/queries";

/**
 * F17-T020 + ADR-030 Fase 5 — Switches & Portas com React Query
 * (`usePagedQuery(["switches"])` + `usePagedQuery(["switchPorts", id],
 * ..., { enabled })` no drill-down) e visões tabela/grid/kanban
 * (preferência persistida; kanban agrupa por localização). Sem mutations:
 * switch é Device (type=Switch) e o CRUD vive na DevicesPage; portas são
 * somente leitura (`SwitchPortDto` sem endpoints de escrita).
 *
 * Fontes reais: `GET /network/switches` (F3-T001) — `PagedResult<DeviceDto>`
 * + localização resolvida via `GET /inventory/locations`;
 * `GET /network/switches/{id}/ports` (F3-T002) — `{id, deviceId, name,
 * description, linkUp}` tri-estado (Up/Down/—). Divergências do protótipo
 * documentadas em docs/17 (chip Online/Offline sem endpoint de estado,
 * PoE/firmware sem contrato, VLAN por porta só via 1 req/PORTA — futuro).
 */

export function SwitchesPage() {
  const [view, setView] = useEntityViewMode("switches", "grid");

  const switches = usePagedQuery<DeviceItem>(["switches"], () => listSwitches());
  const locationsQuery = useListQuery<LocationItem>(["locations"], () =>
    listLocations().then((page) => page.items),
  );

  const [selectedId, setSelectedId] = useState<string | null>(null);

  // Auto-seleciona o primeiro switch depois que a lista chega.
  useEffect(() => {
    if (!selectedId && switches.items.length > 0) {
      setSelectedId(switches.items[0]?.id ?? null);
    }
  }, [switches.items, selectedId]);

  const ports = usePagedQuery<SwitchPortItem>(
    ["switchPorts", selectedId ?? ""],
    () => listSwitchPorts(selectedId as string),
    { enabled: selectedId !== null },
  );

  const locations = locationsQuery.items;
  const locationName = (id: string): string =>
    locations.find((entry) => entry.id === id)?.name ?? id;

  const switchesTotal = switches.totalCount;
  const switchesError = mutationErrorMessage(switches.error as Error | null);
  const portsError = mutationErrorMessage(ports.error as Error | null);

  const selectedSwitch = switches.items.find((entry) => entry.id === selectedId) ?? null;
  const upCount = ports.items.filter((port) => port.linkUp === true).length;
  const downCount = ports.items.filter((port) => port.linkUp === false).length;

  const kanbanColumns = [
    ...locations.map((location) => ({ key: location.id, label: location.name })),
    { key: "unknown", label: "Sem localização" },
  ];

  if (switches.isPending || locationsQuery.isPending) {
    return <LoadingBlock />;
  }

  if (switches.items.length === 0 && switchesError) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{switchesError}</h2>
        <Button variant="primary" onClick={() => void switches.refetch()} style={{ marginTop: 8, padding: "6px 14px" }}>
          Tentar de novo
        </Button>
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Switches & Portas"
        subtitle={`${switchesTotal} switches`}
        actions={
          <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
            <ViewSwitcher mode={view} onChange={setView} />
            <SelectField
              value={selectedId ?? ""}
              onChange={setSelectedId}
              options={switches.items.map((sw) => ({ value: sw.id, label: sw.name }))}
              style={{ minWidth: 180 }}
            />
          </div>
        }
      />

      {switchesError && (
        <ErrorBanner message={switchesError} onRetry={() => void switches.refetch()} />
      )}

      <EntityView<DeviceItem>
        mode={view}
        items={switches.items}
        getKey={(sw) => sw.id}
        emptyMessage={switchesError ? "—" : "Nenhum switch cadastrado"}
        kanban={{
          groupOf: (sw) => sw.locationId || "unknown",
          columns: kanbanColumns,
          overflowColumnKey: "unknown",
        }}
        table={
          <DataTablePanel
            value={switches.items}
            dataKey="id"
            emptyMessage={switchesError ? "—" : "Nenhum switch cadastrado"}
          >
            <Column header="Switch" body={(sw: DeviceItem) => (
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <i className="pi pi-server" style={{ color: "#8b5cf6" }} />
                <span style={{ fontWeight: 600 }}>{sw.name}</span>
              </div>
            )} style={{ minWidth: 200 }} />
            <Column header="Modelo" body={(sw: DeviceItem) => sw.model ?? "—"} style={{ width: 160 }} />
            <Column header="IP" body={(sw: DeviceItem) => sw.ip ?? "—"} style={{ width: 140 }} />
            <Column header="Localização" body={(sw: DeviceItem) => locationName(sw.locationId)} style={{ minWidth: 160 }} />
            <Column
              header=""
              style={{ width: 120 }}
              body={(sw: DeviceItem) => (
                <Button
                  variant={selectedId === sw.id ? "primary" : "ghost"}
                  onClick={() => setSelectedId(sw.id)}
                >
                  Ver portas
                </Button>
              )}
            />
          </DataTablePanel>
        }
        renderCard={(sw) => (
          <div
            className="uni-panel"
            style={{
              padding: 16,
              cursor: "pointer",
              border: selectedId === sw.id ? "2px solid #3b82f6" : "1px solid #e5e7eb",
            }}
            onClick={() => setSelectedId(sw.id)}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
              <i className="pi pi-server" style={{ color: "#8b5cf6" }} />
              <span style={{ fontWeight: 600, fontSize: 14 }}>{sw.name}</span>
            </div>
            <div style={{ fontSize: 12, color: "#64748b" }}>
              {sw.model ?? sw.type} · {sw.ip ?? "sem IP"} · {locationName(sw.locationId)}
            </div>
          </div>
        )}
      />

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Portas — {selectedSwitch?.name ?? "—"}</h2>
          {!ports.isPending && !portsError && (
            <span style={{ fontSize: 12, color: "#64748b" }}>
              {ports.totalCount} portas · {upCount} up · {downCount} down
            </span>
          )}
        </div>

        {portsError && (
          <ErrorBanner
            message={portsError}
            onRetry={() => void ports.refetch()}
            style={{ padding: "10px 16px", borderRadius: 0 }}
          />
        )}

        {ports.isFetching && ports.items.length === 0 && <LoadingBlock />}

        {!ports.isFetching && !portsError && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(90px, 1fr))", gap: 8, padding: 16 }}>
            {ports.items.map((port) => {
              const up = port.linkUp === true;
              const down = port.linkUp === false;
              return (
                <div
                  key={port.id}
                  style={{
                    padding: "10px 6px",
                    borderRadius: 6,
                    border: `2px solid ${up ? "#22c55e" : down ? "#ef4444" : "#e5e7eb"}`,
                    background: up ? "#f0fdf4" : down ? "#fef2f2" : "#f9fafb",
                    textAlign: "center",
                    fontSize: 10,
                  }}
                  title={port.description ?? port.name}
                >
                  <div style={{ fontSize: 15, fontWeight: 700 }}>{port.name}</div>
                  <div style={{ fontSize: 8, fontWeight: 700, color: up ? "#16a34a" : down ? "#dc2626" : "#94a3b8" }}>
                    {up ? "Up" : down ? "Down" : "—"}
                  </div>
                </div>
              );
            })}
            {ports.items.length === 0 && (
              <div style={{ gridColumn: "1 / -1", padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
                Nenhuma porta cadastrada neste switch
              </div>
            )}
          </div>
        )}
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Detalhe de portas</h2></div>
        <DataTablePanel panel={false} value={portsError ? [] : ports.items} dataKey="id" loading={ports.isPending} emptyMessage={portsError ? "—" : "Nenhuma porta cadastrada"}>
          <Column header="Porta" field="name" style={{ fontFamily: "monospace", fontWeight: 600, fontSize: 12, width: 120 }} />
          <Column header="Descrição" body={(port: SwitchPortItem) => port.description ?? "—"} style={{ fontSize: 13, color: "#64748b" }} />
          <Column header="Link" body={(port: SwitchPortItem) => (
            <StatusChip
              label={port.linkUp === true ? "Up" : port.linkUp === false ? "Down" : "—"}
              background={port.linkUp === true ? "#dcfce7" : port.linkUp === false ? "#fee2e2" : "#f1f5f9"}
              color={port.linkUp === true ? "#16a34a" : port.linkUp === false ? "#dc2626" : "#64748b"}
            />
          )} style={{ width: 90 }} />
        </DataTablePanel>
      </div>
    </div>
  );
}
