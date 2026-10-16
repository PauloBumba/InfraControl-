import { useEffect, useMemo, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { listServers, runAllServerChecks } from "@/services/api/servers";
import { getDeviceResults, type DeviceResults, type MonitoringResultItem } from "@/services/api/monitoring";
import { listLocations, type DeviceItem, type LocationItem } from "@/services/api/inventory";
import {
  useListQuery,
  usePagedQuery,
  useObjectQuery,
  useCreateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";

/**
 * F30-T003d — Servidores & Saúde (docs/44). Padrão PrintersPage: lista
 * (DeviceType.Server, inventário é System of Record) + painel com os 3
 * blocos novos do HOST-RESOURCES-MIB lidos do histórico de monitoramento
 * (getDeviceResults, mais recente por check):
 *
 *  - CPU: SnmpServerCpu (pior core/média/cores + processos);
 *  - Memória: SnmpServerMemory (RAM usada + total/livre [+ swap]);
 *  - Discos: SnmpServerDisk (barra por partição, pior destacado).
 *
 * Botão "Executar checks" roda a bateria SNMP ao vivo (MON-006, Operate) e
 * refaz o histórico. Checks novos são opt-in no perfil — sem resultado
 * ainda = estado vazio com dica, não erro.
 */

interface LatestByCheck {
  cpu?: MonitoringResultItem;
  memory?: MonitoringResultItem;
  disk?: MonitoringResultItem;
}

function latestByCheck(results: MonitoringResultItem[]): LatestByCheck {
  const latest: LatestByCheck = {};
  for (const result of results) {
    switch (result.check) {
      case "SnmpServerCpu":
        latest.cpu ??= result;
        break;
      case "SnmpServerMemory":
        latest.memory ??= result;
        break;
      case "SnmpServerDisk":
        latest.disk ??= result;
        break;
    }
  }
  return latest;
}

function valueOf(result: MonitoringResultItem | undefined, key: string): number | null {
  const raw = result?.values?.[key];
  return raw != null && Number.isFinite(Number(raw)) ? Number(raw) : null;
}

interface DiskRow {
  name: string;
  usedPercent: number;
  totalMB: number;
  usedMB: number;
}

function parseDisks(result: MonitoringResultItem | undefined): DiskRow[] {
  const raw = result?.values?.disks;
  if (!raw) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as DiskRow[]) : [];
  } catch {
    return [];
  }
}

/** Barra de uso (CPU/RAM/disco) — limiares 80/90 iguais ao AlertEvaluator. */
function UsageBar({ label, percent, color }: { label: string; percent: number | null; color: string }) {
  if (percent == null) {
    return (
      <div style={{ fontSize: 12, color: "#94a3b8" }}>
        {label}: — <span style={{ fontSize: 10 }}>(sem dado)</span>
      </div>
    );
  }
  const tone = percent >= 90 ? "#dc2626" : percent >= 80 ? "#f59e0b" : "#16a34a";
  return (
    <div style={{ fontSize: 12 }}>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 2 }}>
        <span style={{ fontWeight: 600 }}>{label}</span>
        <span style={{ color: tone, fontWeight: 700 }}>{percent}%</span>
      </div>
      <div style={{ height: 8, borderRadius: 4, background: "#f1f5f9", overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${Math.min(percent, 100)}%`, background: color, borderRadius: 4 }} />
      </div>
      {percent >= 90 && (
        <div style={{ fontSize: 10, color: "#dc2626", fontWeight: 700, marginTop: 2 }}>
          Crítico (limiar 90%)
        </div>
      )}
      {percent >= 80 && percent < 90 && (
        <div style={{ fontSize: 10, color: "#b45309", fontWeight: 700, marginTop: 2 }}>
          Atenção (limiar 80%)
        </div>
      )}
    </div>
  );
}

function checkedAtLabel(result: MonitoringResultItem | undefined): string {
  if (!result) return "sem execução";
  const at = new Date(result.checkedAt);
  return `última execução ${at.toLocaleString("pt-BR")}`;
}

function mb(value: number | null): string {
  return value != null ? `${value.toLocaleString("pt-BR")} MB` : "—";
}

export function ServersPage() {
  const [view, setView] = useEntityViewMode("servers", "grid");

  const servers = usePagedQuery<DeviceItem>(["servers"], () => listServers());
  const locationsQuery = useListQuery<LocationItem>(["locations"], () =>
    listLocations().then((page) => page.items),
  );

  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedId && servers.items.length > 0) {
      setSelectedId(servers.items[0]?.id ?? null);
    }
  }, [servers.items, selectedId]);

  const results = useObjectQuery<DeviceResults | null>(["serverResults", selectedId ?? ""], () =>
    getDeviceResults(selectedId as string, 100),
    { enabled: selectedId !== null },
  );

  // Execução ao vivo da bateria SNMP (MON-006, Operate).
  const runMutation = useCreateMutation({
    invalidate: ["serverResults"],
    mutationFn: (deviceId: string) => runAllServerChecks(deviceId),
    onSuccess: (data) => {
      const list = data ?? [];
      const failed = list.filter((entry) => !entry.succeeded).length;
      setRunMessage(
        failed > 0
          ? `${list.length} checks executados, ${failed} com falha ( SNMP/alvo inacessível )`
          : `${list.length} checks executados com sucesso`,
      );
      void results.refetch();
    },
    onError: (error) => setRunMessage(mutationErrorMessage(error)),
  });
  const [runMessage, setRunMessage] = useState<string | null>(null);

  const locations = locationsQuery.items;
  const locationName = (id: string): string =>
    locations.find((entry) => entry.id === id)?.name ?? id;

  const serversError = mutationErrorMessage(servers.error as Error | null);
  const resultsError = mutationErrorMessage(results.error as Error | null);
  const selected = servers.items.find((entry) => entry.id === selectedId) ?? null;

  const history = results.data?.results ?? [];
  const latest = useMemo(() => latestByCheck(history), [history]);

  const cpuPercent = valueOf(latest.cpu, "cpuPercent");
  const cpuAverage = valueOf(latest.cpu, "averagePercent");
  const coreCount = valueOf(latest.cpu, "coreCount");
  const processCount = valueOf(latest.cpu, "processCount");
  const corePercents = latest.cpu?.values?.corePercents ?? null;

  const memoryUsedPercent = valueOf(latest.memory, "usedPercent");
  const memoryTotal = valueOf(latest.memory, "totalMB");
  const memoryUsed = valueOf(latest.memory, "usedMB");
  const memoryAvailable = valueOf(latest.memory, "availableMB");
  const swapPercent = valueOf(latest.memory, "swapPercent");

  const disks = useMemo(() => parseDisks(latest.disk), [latest.disk]);
  const diskCount = valueOf(latest.disk, "diskCount");
  const worstDisk = latest.disk?.values?.worstDisk ?? null;

  const hasAnyData = history.length > 0;

  if (servers.isPending || locationsQuery.isPending) {
    return <LoadingBlock />;
  }

  if (servers.items.length === 0 && serversError) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{serversError}</h2>
        <Button variant="primary" onClick={() => void servers.refetch()} style={{ marginTop: 8, padding: "6px 14px" }}>
          Tentar de novo
        </Button>
      </div>
    );
  }

  return (
    <ListPageShell
      title="Servidores"
      singular="servidor"
      plural="servidores"
      total={servers.totalCount}
      error={serversError}
      onRetry={() => void servers.refetch()}
      actions={
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <ViewSwitcher mode={view} onChange={setView} />
        </div>
      }
    >
      <EntityView<DeviceItem>
        mode={view}
        items={servers.items}
        getKey={(server) => server.id}
        emptyMessage={serversError ? "—" : "Nenhum servidor cadastrado"}
        kanban={{
          groupOf: (server) => server.locationId || "unknown",
          columns: [
            ...locations.map((location) => ({ key: location.id, label: location.name })),
            { key: "unknown", label: "Sem localização" },
          ],
          overflowColumnKey: "unknown",
        }}
        renderCard={(server) => (
          <div
            onClick={() => setSelectedId(server.id)}
            style={{
              padding: 14,
              borderRadius: 8,
              border: `2px solid ${server.id === selectedId ? "#2563eb" : "#e5e7eb"}`,
              background: server.id === selectedId ? "#eff6ff" : "#fff",
              cursor: "pointer",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
              <i className="pi pi-desktop" style={{ color: "#6366f1" }} />
              <span style={{ fontWeight: 700 }}>{server.name}</span>
            </div>
            <div style={{ fontSize: 11, color: "#64748b" }}>
              {server.model ?? "modelo —"} · {server.ip ?? "sem IP"}
            </div>
            <div style={{ fontSize: 11, color: "#94a3b8" }}>{locationName(server.locationId)}</div>
          </div>
        )}
        table={
          <DataTablePanel
            value={servers.items}
            dataKey="id"
            emptyMessage={serversError ? "—" : "Nenhum servidor cadastrado"}
            onRowClick={(event) => setSelectedId(String(event.data.id))}
          >
            <Column header="Servidor" body={(server: DeviceItem) => (
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <i className="pi pi-desktop" style={{ color: "#6366f1" }} />
                <span style={{ fontWeight: 600 }}>{server.name}</span>
                {server.id === selectedId && (
                  <i className="pi pi-angle-right" style={{ color: "#2563eb", fontSize: 12 }} />
                )}
              </div>
            )} />
            <Column header="Modelo" body={(server: DeviceItem) => server.model ?? "—"} />
            <Column header="Fabricante" body={(server: DeviceItem) => server.vendor ?? "—"} />
            <Column header="IP" body={(server: DeviceItem) => server.ip ?? "—"} style={{ width: 130 }} />
            <Column header="Localização" body={(server: DeviceItem) => locationName(server.locationId)} />
          </DataTablePanel>
        }
      />

      {/* Painel — saúde (CPU / memória / discos) */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Saúde do Servidor — {selected?.name ?? "—"}</h2>
          {selected && (
            <Button
              variant="ghost"
              disabled={runMutation.isPending}
              onClick={() => {
                setRunMessage(null);
                runMutation.mutate(selected.id);
              }}
              style={{ marginLeft: "auto" }}
            >
              {runMutation.isPending ? "Executando…" : "Executar checks"}
            </Button>
          )}
        </div>

        {runMessage && (
          <div
            style={{
              padding: "6px 16px",
              fontSize: 12,
              background: runMutation.isError ? "#fef2f2" : "#f0fdf4",
              color: runMutation.isError ? "#dc2626" : "#16a34a",
              borderBottom: "1px solid #e5e7eb",
            }}
          >
            {runMessage}
          </div>
        )}

        {resultsError && (
          <ErrorBanner
            message={resultsError}
            onRetry={() => void results.refetch()}
            style={{ padding: "10px 16px", borderRadius: 0 }}
          />
        )}

        {results.isFetching && !hasAnyData && <LoadingBlock />}

        {!results.isFetching && !resultsError && !hasAnyData && (
          <div style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
            Nenhum resultado de monitoramento ainda. Clique em{" "}
            <strong>Executar checks</strong> para consultar o servidor via SNMP.
          </div>
        )}

        {hasAnyData && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: 16, padding: 16 }}>
            {/* CPU */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>CPU e processamento</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.cpu)}
                {latest.cpu?.succeeded === false && latest.cpu.error
                  ? ` · ${latest.cpu.error}`
                  : ""}
              </div>
              <div style={{ display: "grid", gap: 10 }}>
                <UsageBar label="Pior core" percent={cpuPercent} color="#6366f1" />
                {cpuAverage != null && (
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <span>Média</span>
                    <span style={{ fontWeight: 700 }}>{cpuAverage}%</span>
                  </div>
                )}
                {coreCount != null && (
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <span>Cores</span>
                    <span style={{ fontWeight: 700 }}>{coreCount}</span>
                  </div>
                )}
                {corePercents && (
                  <div style={{ fontSize: 10, color: "#64748b" }}>
                    por core: {corePercents}
                  </div>
                )}
                {processCount != null && (
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <span>Processos</span>
                    <span style={{ fontWeight: 700 }}>{processCount}</span>
                  </div>
                )}
              </div>
            </div>

            {/* Memória */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Memória</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.memory)}
                {latest.memory?.succeeded === false && latest.memory.error
                  ? ` · ${latest.memory.error}`
                  : ""}
              </div>
              <div style={{ display: "grid", gap: 10 }}>
                <UsageBar label="RAM usada" percent={memoryUsedPercent} color="#8b5cf6" />
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Total</span>
                  <span style={{ fontWeight: 700 }}>{mb(memoryTotal)}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Usada</span>
                  <span style={{ fontWeight: 700 }}>{mb(memoryUsed)}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Livre</span>
                  <span style={{ fontWeight: 700 }}>{mb(memoryAvailable)}</span>
                </div>
                {swapPercent != null && (
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <span>Swap</span>
                    <span style={{ fontWeight: 700 }}>{swapPercent}%</span>
                  </div>
                )}
              </div>
            </div>

            {/* Discos */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Discos</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.disk)}
                {latest.disk?.succeeded === false && latest.disk.error
                  ? ` · ${latest.disk.error}`
                  : ""}
                {diskCount != null ? ` · ${diskCount} partição(ões)` : ""}
              </div>
              {disks.length > 0 ? (
                <div style={{ display: "grid", gap: 10 }}>
                  {disks.map((disk) => (
                    <UsageBar
                      key={disk.name}
                      label={
                        worstDisk === disk.name && disks.length > 1
                          ? `${disk.name} (pior)`
                          : disk.name
                      }
                      percent={disk.usedPercent}
                      color="#0ea5e9"
                    />
                  ))}
                </div>
              ) : (
                <div style={{ fontSize: 12, color: "#94a3b8" }}>
                  {latest.disk?.error ?? "Sem leitura de discos ainda."}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </ListPageShell>
  );
}
