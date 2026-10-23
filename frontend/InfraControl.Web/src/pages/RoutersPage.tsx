import { useEffect, useMemo, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { listRouters, runAllRouterChecks } from "@/services/api/routers";
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
 * F30-T007d — Routers & Firewalls (docs/48). Padrão CamerasPage: lista
 * (DeviceType.Router + Firewall mergeados — o filtro do backend é 1 tipo)
 * + painel "Rotas & Sessões" com 3 blocos do histórico (getDeviceResults,
 * mais recente por check):
 *
 *  - Rotas: SnmpRouterRoutes (contagem + defaultGateway + estáticas/
 *    dinâmicas + top 20 rotas);
 *  - Sessões: SnmpRouterSessions (total/established/listen + top remotos);
 *  - Saúde: SnmpServerCpu/Memory quando presentes no histórico (mesmos
 *    limiares 80/90 do AlertEvaluator — HOST-RESOURCES estendido T007c).
 *
 * Botão "Executar checks" roda a bateria SNMP ao vivo (MON-006, Operate) e
 * refaz o histórico. Checks são opt-in — sem resultado ainda = estado
 * vazio com dica, não erro.
 */

interface LatestByCheck {
  routes?: MonitoringResultItem;
  sessions?: MonitoringResultItem;
  cpu?: MonitoringResultItem;
  memory?: MonitoringResultItem;
}

function latestByCheck(results: MonitoringResultItem[]): LatestByCheck {
  const latest: LatestByCheck = {};
  for (const result of results) {
    switch (result.check) {
      case "SnmpRouterRoutes":
        latest.routes ??= result;
        break;
      case "SnmpRouterSessions":
        latest.sessions ??= result;
        break;
      case "SnmpServerCpu":
        latest.cpu ??= result;
        break;
      case "SnmpServerMemory":
        latest.memory ??= result;
        break;
    }
  }
  return latest;
}

function valueOf(result: MonitoringResultItem | undefined, key: string): number | null {
  const raw = result?.values?.[key];
  return raw != null && Number.isFinite(Number(raw)) ? Number(raw) : null;
}

function textOf(result: MonitoringResultItem | undefined, key: string): string | null {
  const raw = result?.values?.[key];
  return raw == null || raw === "" ? null : raw;
}

function jsonOf<T>(result: MonitoringResultItem | undefined, key: string): T[] {
  const raw = result?.values?.[key];
  if (raw == null || raw === "") return [];
  try {
    const parsed = JSON.parse(raw) as T[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function checkedAtLabel(result: MonitoringResultItem | undefined): string {
  if (!result) return "sem execução";
  const at = new Date(result.checkedAt);
  return `última execução ${at.toLocaleString("pt-BR")}`;
}

function numLabel(value: number | null, unit = ""): string {
  if (value == null) return "—";
  const formatted = value.toLocaleString("pt-BR");
  return unit === "%" ? `${formatted}%` : `${formatted} ${unit}`.trim();
}

/** Uso % com limiares 80/90 iguais ao AlertEvaluator (CPU/memória). */
function UsageBar({ label, percent }: { label: string; percent: number | null }) {
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
        <span style={{ color: tone, fontWeight: 700 }}>{numLabel(percent, "%")}</span>
      </div>
      <div style={{ height: 8, borderRadius: 4, background: "#f1f5f9", overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${Math.min(percent, 100)}%`, background: tone, borderRadius: 4 }} />
      </div>
    </div>
  );
}

interface RouteRow {
  dest: string;
  nextHop: string;
  proto: string;
  type: string;
}

interface RemoteRow {
  ip: string;
  connections: number;
}

export function RoutersPage() {
  const [view, setView] = useEntityViewMode("routers", "grid");

  const routers = usePagedQuery<DeviceItem>(["routers"], () => listRouters());
  const locationsQuery = useListQuery<LocationItem>(["locations"], () =>
    listLocations().then((page) => page.items),
  );

  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedId && routers.items.length > 0) {
      setSelectedId(routers.items[0]?.id ?? null);
    }
  }, [routers.items, selectedId]);

  const results = useObjectQuery<DeviceResults | null>(["routerResults", selectedId ?? ""], () =>
    getDeviceResults(selectedId as string, 100),
    { enabled: selectedId !== null },
  );

  // Execução ao vivo da bateria SNMP (MON-006, Operate).
  const runMutation = useCreateMutation({
    invalidate: ["routerResults"],
    mutationFn: (deviceId: string) => runAllRouterChecks(deviceId),
    onSuccess: (data) => {
      const list = data ?? [];
      const failed = list.filter((entry) => !entry.succeeded).length;
      setRunMessage(
        failed > 0
          ? `${list.length} checks executados, ${failed} com falha (SNMP indisponível ou community errada)`
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

  const routersError = mutationErrorMessage(routers.error as Error | null);
  const resultsError = mutationErrorMessage(results.error as Error | null);
  const selected = routers.items.find((entry) => entry.id === selectedId) ?? null;

  const history = useMemo(() => results.data?.results ?? [], [results.data]);
  const latest = useMemo(() => latestByCheck(history), [history]);

  const routeCount = valueOf(latest.routes, "routeCount");
  const defaultGateway = textOf(latest.routes, "defaultGateway");
  const staticCount = valueOf(latest.routes, "staticCount");
  const dynamicCount = valueOf(latest.routes, "dynamicCount");
  const routes = useMemo(() => jsonOf<RouteRow>(latest.routes, "routes"), [latest.routes]);

  const sessionCount = valueOf(latest.sessions, "sessionCount");
  const establishedCount = valueOf(latest.sessions, "establishedCount");
  const listenCount = valueOf(latest.sessions, "listenCount");
  const topRemotes = useMemo(
    () => jsonOf<RemoteRow>(latest.sessions, "topRemoteIps"),
    [latest.sessions],
  );

  const cpuPercent = valueOf(latest.cpu, "cpuPercent");
  const memoryPercent = valueOf(latest.memory, "usedPercent");

  const hasAnyData = history.length > 0;

  if (routers.isPending || locationsQuery.isPending) {
    return <LoadingBlock />;
  }

  if (routers.items.length === 0 && routersError) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{routersError}</h2>
        <Button variant="primary" onClick={() => void routers.refetch()} style={{ marginTop: 8, padding: "6px 14px" }}>
          Tentar de novo
        </Button>
      </div>
    );
  }

  return (
    <ListPageShell
      title="Roteadores & Firewalls"
      singular="router/firewall"
      plural="routers/firewalls"
      total={routers.totalCount}
      error={routersError}
      onRetry={() => void routers.refetch()}
      actions={
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <ViewSwitcher mode={view} onChange={setView} />
        </div>
      }
    >
      <EntityView<DeviceItem>
        mode={view}
        items={routers.items}
        getKey={(router) => router.id}
        emptyMessage={routersError ? "—" : "Nenhum router/firewall cadastrado"}
        kanban={{
          groupOf: (router) => router.locationId || "unknown",
          columns: [
            ...locations.map((location) => ({ key: location.id, label: location.name })),
            { key: "unknown", label: "Sem localização" },
          ],
          overflowColumnKey: "unknown",
        }}
        renderCard={(router) => (
          <div
            onClick={() => setSelectedId(router.id)}
            style={{
              padding: 14,
              borderRadius: 8,
              border: `2px solid ${router.id === selectedId ? "#2563eb" : "#e5e7eb"}`,
              background: router.id === selectedId ? "#eff6ff" : "#fff",
              cursor: "pointer",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
              <i className="pi pi-globe" style={{ color: "#6366f1" }} />
              <span style={{ fontWeight: 700 }}>{router.name}</span>
            </div>
            <div style={{ fontSize: 11, color: "#64748b" }}>
              {router.type} · {router.model ?? "modelo —"} · {router.ip ?? "sem IP"}
            </div>
            <div style={{ fontSize: 11, color: "#94a3b8" }}>{locationName(router.locationId)}</div>
          </div>
        )}
        table={
          <DataTablePanel
            value={routers.items}
            dataKey="id"
            emptyMessage={routersError ? "—" : "Nenhum router/firewall cadastrado"}
            onRowClick={(event) => setSelectedId(String(event.data.id))}
          >
            <Column header="Dispositivo" body={(router: DeviceItem) => (
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <i className="pi pi-globe" style={{ color: "#6366f1" }} />
                <span style={{ fontWeight: 600 }}>{router.name}</span>
                {router.id === selectedId && (
                  <i className="pi pi-angle-right" style={{ color: "#2563eb", fontSize: 12 }} />
                )}
              </div>
            )} />
            <Column header="Tipo" body={(router: DeviceItem) => router.type} style={{ width: 100 }} />
            <Column header="Modelo" body={(router: DeviceItem) => router.model ?? "—"} />
            <Column header="Fabricante" body={(router: DeviceItem) => router.vendor ?? "—"} />
            <Column header="IP" body={(router: DeviceItem) => router.ip ?? "—"} style={{ width: 130 }} />
            <Column header="Localização" body={(router: DeviceItem) => locationName(router.locationId)} />
          </DataTablePanel>
        }
      />

      {/* Painel — rotas, sessões e saúde (SNMP universal) */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Rotas &amp; Sessões — {selected?.name ?? "—"}</h2>
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
            <strong>Executar checks</strong> para consultar rotas e sessões via SNMP.
          </div>
        )}

        {hasAnyData && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: 16, padding: 16 }}>
            {/* Rotas */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Tabela de rotas</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.routes)}
                {latest.routes?.succeeded === false && latest.routes.error
                  ? ` · ${latest.routes.error}`
                  : ""}
              </div>
              <div style={{ display: "grid", gap: 10 }}>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Total de rotas</span>
                  <span style={{ fontWeight: 700 }}>{numLabel(routeCount)}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Gateway padrão</span>
                  <span style={{ fontWeight: 700 }}>{defaultGateway ?? "—"}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Estáticas / Dinâmicas</span>
                  <span style={{ fontWeight: 700 }}>
                    {numLabel(staticCount)} / {numLabel(dynamicCount)}
                  </span>
                </div>
                {routes.length > 0 && (
                  <div style={{ borderTop: "1px solid #f1f5f9", paddingTop: 8 }}>
                    <div style={{ fontSize: 11, fontWeight: 700, marginBottom: 4 }}>
                      Top rotas (máx. 20)
                    </div>
                    {routes.map((route) => (
                      <div
                        key={`${route.dest}-${route.nextHop}`}
                        style={{ display: "flex", justifyContent: "space-between", fontSize: 11, padding: "2px 0" }}
                      >
                        <span style={{ fontFamily: "monospace" }}>{route.dest}</span>
                        <span style={{ color: "#64748b" }}>
                          → {route.nextHop} · {route.proto}
                        </span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Sessões */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Sessões TCP</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.sessions)}
                {latest.sessions?.succeeded === false && latest.sessions.error
                  ? ` · ${latest.sessions.error}`
                  : ""}
              </div>
              <div style={{ display: "grid", gap: 10 }}>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Total</span>
                  <span style={{ fontWeight: 700 }}>{numLabel(sessionCount)}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Established</span>
                  <span style={{ fontWeight: 700, color: "#16a34a" }}>{numLabel(establishedCount)}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Listen</span>
                  <span style={{ fontWeight: 700 }}>{numLabel(listenCount)}</span>
                </div>
                {topRemotes.length > 0 && (
                  <div style={{ borderTop: "1px solid #f1f5f9", paddingTop: 8 }}>
                    <div style={{ fontSize: 11, fontWeight: 700, marginBottom: 4 }}>
                      Top remotos (máx. 10)
                    </div>
                    {topRemotes.map((remote) => (
                      <div
                        key={remote.ip}
                        style={{ display: "flex", justifyContent: "space-between", fontSize: 11, padding: "2px 0" }}
                      >
                        <span style={{ fontFamily: "monospace" }}>{remote.ip}</span>
                        <span style={{ color: "#64748b" }}>{remote.connections} conexão(ões)</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Saúde (HOST-RESOURCES, quando o check foi configurado) */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Saúde</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.cpu ?? latest.memory)}
              </div>
              <div style={{ display: "grid", gap: 12 }}>
                <UsageBar label="CPU (pior core)" percent={cpuPercent} />
                <UsageBar label="Memória" percent={memoryPercent} />
                {cpuPercent == null && memoryPercent == null && (
                  <div style={{ fontSize: 11, color: "#94a3b8" }}>
                    Configure os checks SnmpServerCpu/SnmpServerMemory no device
                    para ver a saúde (HOST-RESOURCES — MikroTik compatível).
                  </div>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </ListPageShell>
  );
}
