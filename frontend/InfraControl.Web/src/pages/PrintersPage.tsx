import { useEffect, useMemo, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { StatusChip } from "@/components/ui/StatusChip";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { listPrinters, runAllPrinterChecks } from "@/services/api/printers";
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
 * F30-T002d — Impressoras & Suprimentos (docs/43). Padrão SwitchesPage:
 * lista (DeviceType.Printer, inventário é System of Record) + painel de
 * detalhe com os 3 blocos novos da Printer MIB lidos do histórico de
 * monitoramento (getDeviceResults, mais recente por check):
 *
 *  - Consumíveis: SnmpToner/SnmpDrum (nível %) + SnmpColorToner (C/M/Y);
 *  - Estado: SnmpPrinterStatus (tampa/bandejas/alertas);
 *  - Contadores: SnmpPrinterCounters (total de páginas).
 *
 * Botão "Executar checks" roda a bateria SNMP ao vivo (MON-006, Operate) e
 * refaz o histórico. Checks novos são opt-in no perfil — sem resultado
 * ainda = estado vazio com dica, não erro.
 */

interface LatestByCheck {
  toner?: MonitoringResultItem;
  drum?: MonitoringResultItem;
  color?: MonitoringResultItem;
  status?: MonitoringResultItem;
  counters?: MonitoringResultItem;
}

function latestByCheck(results: MonitoringResultItem[]): LatestByCheck {
  const latest: LatestByCheck = {};
  for (const result of results) {
    switch (result.check) {
      case "SnmpToner":
        latest.toner ??= result;
        break;
      case "SnmpDrum":
        latest.drum ??= result;
        break;
      case "SnmpColorToner":
        latest.color ??= result;
        break;
      case "SnmpPrinterStatus":
        latest.status ??= result;
        break;
      case "SnmpPrinterCounters":
        latest.counters ??= result;
        break;
    }
  }
  return latest;
}

function valueOf(result: MonitoringResultItem | undefined, key: string): number | null {
  const raw = result?.values?.[key];
  return raw != null && Number.isFinite(Number(raw)) ? Number(raw) : null;
}

function SupplyBar({ label, percent, color }: { label: string; percent: number | null; color: string }) {
  if (percent == null) {
    return (
      <div style={{ fontSize: 12, color: "#94a3b8" }}>
        {label}: — <span style={{ fontSize: 10 }}>(sem dado)</span>
      </div>
    );
  }
  const tone = percent < 20 ? "#dc2626" : percent < 50 ? "#f59e0b" : "#16a34a";
  return (
    <div style={{ fontSize: 12 }}>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 2 }}>
        <span style={{ fontWeight: 600 }}>{label}</span>
        <span style={{ color: tone, fontWeight: 700 }}>{percent}%</span>
      </div>
      <div style={{ height: 8, borderRadius: 4, background: "#f1f5f9", overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${percent}%`, background: color, borderRadius: 4 }} />
      </div>
      {percent < 20 && (
        <div style={{ fontSize: 10, color: "#dc2626", fontWeight: 700, marginTop: 2 }}>
          Reposição sugerida (limiar 20%)
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

export function PrintersPage() {
  const [view, setView] = useEntityViewMode("printers", "grid");

  const printers = usePagedQuery<DeviceItem>(["printers"], () => listPrinters());
  const locationsQuery = useListQuery<LocationItem>(["locations"], () =>
    listLocations().then((page) => page.items),
  );

  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedId && printers.items.length > 0) {
      setSelectedId(printers.items[0]?.id ?? null);
    }
  }, [printers.items, selectedId]);

  const results = useObjectQuery<DeviceResults | null>(["printerResults", selectedId ?? ""], () =>
    getDeviceResults(selectedId as string, 100),
    { enabled: selectedId !== null },
  );

  // Execução ao vivo da bateria SNMP (MON-006, Operate).
  const runMutation = useCreateMutation({
    invalidate: ["printerResults"],
    mutationFn: (deviceId: string) => runAllPrinterChecks(deviceId),
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

  const printersError = mutationErrorMessage(printers.error as Error | null);
  const resultsError = mutationErrorMessage(results.error as Error | null);
  const selected = printers.items.find((entry) => entry.id === selectedId) ?? null;

  const history = results.data?.results ?? [];
  const latest = useMemo(() => latestByCheck(history), [history]);

  const tonerPercent = valueOf(latest.toner, "levelPercent");
  const drumPercent = valueOf(latest.drum, "levelPercent");
  const cyanPercent = valueOf(latest.color, "cyanPercent");
  const magentaPercent = valueOf(latest.color, "magentaPercent");
  const yellowPercent = valueOf(latest.color, "yellowPercent");
  const totalPages = valueOf(latest.counters, "totalPages");

  const statusValues = latest.status?.values;
  const coverOpen = statusValues?.coverOpen === "true";
  const inputCount = valueOf(latest.status, "inputCount");
  const inputEmpty = valueOf(latest.status, "inputEmptyCount");
  const inputJammed = valueOf(latest.status, "inputJammedCount");
  const activeAlerts = valueOf(latest.status, "activeAlertCount");

  const hasAnyData = history.length > 0;

  if (printers.isPending || locationsQuery.isPending) {
    return <LoadingBlock />;
  }

  if (printers.items.length === 0 && printersError) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{printersError}</h2>
        <Button variant="primary" onClick={() => void printers.refetch()} style={{ marginTop: 8, padding: "6px 14px" }}>
          Tentar de novo
        </Button>
      </div>
    );
  }

  return (
    <ListPageShell
      title="Impressoras"
      singular="impressora"
      plural="impressoras"
      total={printers.totalCount}
      error={printersError}
      onRetry={() => void printers.refetch()}
      actions={
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <ViewSwitcher mode={view} onChange={setView} />
        </div>
      }
    >
      <EntityView<DeviceItem>
        mode={view}
        items={printers.items}
        getKey={(printer) => printer.id}
        emptyMessage={printersError ? "—" : "Nenhuma impressora cadastrada"}
        kanban={{
          groupOf: (printer) => printer.locationId || "unknown",
          columns: [
            ...locations.map((location) => ({ key: location.id, label: location.name })),
            { key: "unknown", label: "Sem localização" },
          ],
          overflowColumnKey: "unknown",
        }}
        renderCard={(printer) => (
          <div
            onClick={() => setSelectedId(printer.id)}
            style={{
              padding: 14,
              borderRadius: 8,
              border: `2px solid ${printer.id === selectedId ? "#2563eb" : "#e5e7eb"}`,
              background: printer.id === selectedId ? "#eff6ff" : "#fff",
              cursor: "pointer",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
              <i className="pi pi-print" style={{ color: "#0ea5e9" }} />
              <span style={{ fontWeight: 700 }}>{printer.name}</span>
            </div>
            <div style={{ fontSize: 11, color: "#64748b" }}>
              {printer.model ?? "modelo —"} · {printer.ip ?? "sem IP"}
            </div>
            <div style={{ fontSize: 11, color: "#94a3b8" }}>{locationName(printer.locationId)}</div>
          </div>
        )}
        table={
          <DataTablePanel
            value={printers.items}
            dataKey="id"
            emptyMessage={printersError ? "—" : "Nenhuma impressora cadastrada"}
            onRowClick={(event) => setSelectedId(String(event.data.id))}
          >
            <Column header="Impressora" body={(printer: DeviceItem) => (
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <i className="pi pi-print" style={{ color: "#0ea5e9" }} />
                <span style={{ fontWeight: 600 }}>{printer.name}</span>
                {printer.id === selectedId && (
                  <i className="pi pi-angle-right" style={{ color: "#2563eb", fontSize: 12 }} />
                )}
              </div>
            )} />
            <Column header="Modelo" body={(printer: DeviceItem) => printer.model ?? "—"} />
            <Column header="Fabricante" body={(printer: DeviceItem) => printer.vendor ?? "—"} />
            <Column header="IP" body={(printer: DeviceItem) => printer.ip ?? "—"} style={{ width: 130 }} />
            <Column header="Localização" body={(printer: DeviceItem) => locationName(printer.locationId)} />
          </DataTablePanel>
        }
      />

      {/* Painel — consumíveis + estado + contadores */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Suprimentos & Estado — {selected?.name ?? "—"}</h2>
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
            <strong>Executar checks</strong> para consultar a impressora via SNMP.
          </div>
        )}

        {hasAnyData && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: 16, padding: 16 }}>
            {/* Consumíveis */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Consumíveis</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.toner ?? latest.color)}
                {latest.toner?.succeeded === false && latest.toner.error
                  ? ` · ${latest.toner.error}`
                  : ""}
              </div>
              <div style={{ display: "grid", gap: 10 }}>
                <SupplyBar label="Toner preto" percent={tonerPercent} color="#334155" />
                <SupplyBar label="Cilindro (Drum)" percent={drumPercent} color="#8b5cf6" />
                {(cyanPercent != null || magentaPercent != null || yellowPercent != null) && (
                  <>
                    <div style={{ fontSize: 11, fontWeight: 700, color: "#64748b", marginTop: 4 }}>
                      Toner colorido
                    </div>
                    <SupplyBar label="Ciano" percent={cyanPercent} color="#06b6d4" />
                    <SupplyBar label="Magenta" percent={magentaPercent} color="#ec4899" />
                    <SupplyBar label="Amarelo" percent={yellowPercent} color="#eab308" />
                  </>
                )}
              </div>
            </div>

            {/* Estado */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Estado</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.status)}
                {latest.status?.succeeded === false && latest.status.error
                  ? ` · ${latest.status.error}`
                  : ""}
              </div>
              {latest.status && latest.status.succeeded ? (
                <div style={{ display: "grid", gap: 8, fontSize: 12 }}>
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span>Tampa</span>
                    <StatusChip
                      label={coverOpen ? "Aberta" : "Fechada"}
                      background={coverOpen ? "#fef3c7" : "#dcfce7"}
                      color={coverOpen ? "#b45309" : "#16a34a"}
                    />
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span>Bandejas</span>
                    <StatusChip
                      label={
                        inputJammed != null && inputJammed > 0
                          ? `${inputJammed} travada(s)`
                          : inputEmpty != null && inputCount != null && inputEmpty > 0
                            ? `${inputEmpty}/${inputCount} sem mídia`
                            : inputCount != null
                              ? `${inputCount} ok`
                              : "—"
                      }
                      background={
                        inputJammed != null && inputJammed > 0
                          ? "#fee2e2"
                          : inputEmpty != null && inputEmpty > 0
                            ? "#fef3c7"
                            : "#dcfce7"
                      }
                      color={
                        inputJammed != null && inputJammed > 0
                          ? "#dc2626"
                          : inputEmpty != null && inputEmpty > 0
                            ? "#b45309"
                            : "#16a34a"
                      }
                    />
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span>Alertas ativos</span>
                    <StatusChip
                      label={activeAlerts != null ? String(activeAlerts) : "—"}
                      background={activeAlerts != null && activeAlerts > 0 ? "#fee2e2" : "#f1f5f9"}
                      color={activeAlerts != null && activeAlerts > 0 ? "#dc2626" : "#64748b"}
                    />
                  </div>
                </div>
              ) : (
                <div style={{ fontSize: 12, color: "#94a3b8" }}>
                  {latest.status?.error ?? "Sem leitura de estado ainda."}
                </div>
              )}
            </div>

            {/* Contadores */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Contadores</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.counters)}
                {latest.counters?.succeeded === false && latest.counters.error
                  ? ` · ${latest.counters.error}`
                  : ""}
              </div>
              <div style={{ textAlign: "center", padding: "8px 0" }}>
                <div style={{ fontSize: 28, fontWeight: 800, color: "#0f172a" }}>
                  {totalPages != null ? totalPages.toLocaleString("pt-BR") : "—"}
                </div>
                <div style={{ fontSize: 11, color: "#64748b" }}>páginas impressas (total)</div>
                <div style={{ fontSize: 10, color: "#94a3b8", marginTop: 6 }}>
                  Volume mensal = diferença entre leituras (histórico no Zabbix)
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </ListPageShell>
  );
}
