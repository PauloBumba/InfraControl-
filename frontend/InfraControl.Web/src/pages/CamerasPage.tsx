import { useEffect, useMemo, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { listCameras, runAllCameraChecks } from "@/services/api/cameras";
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
 * F30-T005d — Câmeras & Gravação (docs/46). Padrão ServersPage: lista
 * (DeviceType.Camera, inventário é System of Record) + painel com os 2
 * blocos ONVIF lidos do histórico de monitoramento (getDeviceResults,
 * mais recente por check):
 *
 *  - Gravação: OnvifRecording (chip Gravando/Parado + videoLoss/tamper);
 *  - Armazenamento: OnvifStorage (barra usedPercent 80/90 + total/usado/livre);
 *  - Retenção: retentionDays (informativo — sem regra de alerta ainda).
 *
 * Botão "Executar checks" roda a bateria ONVIF ao vivo (MON-006, Operate) e
 * refaz o histórico. Checks novos são opt-in no perfil — sem resultado
 * ainda = estado vazio com dica, não erro.
 */

interface LatestByCheck {
  storage?: MonitoringResultItem;
  recording?: MonitoringResultItem;
}

function latestByCheck(results: MonitoringResultItem[]): LatestByCheck {
  const latest: LatestByCheck = {};
  for (const result of results) {
    switch (result.check) {
      case "OnvifStorage":
        latest.storage ??= result;
        break;
      case "OnvifRecording":
        latest.recording ??= result;
        break;
    }
  }
  return latest;
}

function valueOf(result: MonitoringResultItem | undefined, key: string): number | null {
  const raw = result?.values?.[key];
  return raw != null && Number.isFinite(Number(raw)) ? Number(raw) : null;
}

function boolOf(result: MonitoringResultItem | undefined, key: string): boolean | null {
  const raw = result?.values?.[key];
  return raw == null ? null : raw === "true";
}

/** Barra de uso (armazenamento) — limiares 80/90 iguais ao AlertEvaluator. */
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

/** Chip de estado da gravação — gravado (verde) / parado (vermelho). */
function RecordingChip({ recordingActive }: { recordingActive: boolean | null }) {
  if (recordingActive == null) {
    return (
      <span style={{ fontSize: 12, color: "#94a3b8" }}>sem dado</span>
    );
  }
  const tone = recordingActive ? "#16a34a" : "#dc2626";
  return (
    <span
      style={{
        display: "inline-block",
        padding: "3px 10px",
        borderRadius: 999,
        fontSize: 12,
        fontWeight: 700,
        color: tone,
        background: recordingActive ? "#f0fdf4" : "#fef2f2",
        border: `1px solid ${tone}`,
      }}
    >
      {recordingActive ? "Gravando" : "Parado"}
    </span>
  );
}

/** Badges de videoLoss / tamper (só quando true). */
function RecordingFlags({ videoLoss, tamper }: { videoLoss: boolean | null; tamper: boolean | null }) {
  const flags: Array<{ label: string; tone: string }> = [];
  if (videoLoss) flags.push({ label: "Perda de vídeo", tone: "#dc2626" });
  if (tamper) flags.push({ label: "Tamper", tone: "#f59e0b" });
  if (flags.length === 0) return null;
  return (
    <div style={{ display: "flex", gap: 6, flexWrap: "wrap", marginTop: 8 }}>
      {flags.map((flag) => (
        <span
          key={flag.label}
          style={{
            padding: "2px 8px",
            borderRadius: 4,
            fontSize: 11,
            fontWeight: 700,
            color: flag.tone,
            border: `1px solid ${flag.tone}`,
          }}
        >
          {flag.label}
        </span>
      ))}
    </div>
  );
}

export function CamerasPage() {
  const [view, setView] = useEntityViewMode("cameras", "grid");

  const cameras = usePagedQuery<DeviceItem>(["cameras"], () => listCameras());
  const locationsQuery = useListQuery<LocationItem>(["locations"], () =>
    listLocations().then((page) => page.items),
  );

  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedId && cameras.items.length > 0) {
      setSelectedId(cameras.items[0]?.id ?? null);
    }
  }, [cameras.items, selectedId]);

  const results = useObjectQuery<DeviceResults | null>(["cameraResults", selectedId ?? ""], () =>
    getDeviceResults(selectedId as string, 100),
    { enabled: selectedId !== null },
  );

  // Execução ao vivo da bateria ONVIF (MON-006, Operate).
  const runMutation = useCreateMutation({
    invalidate: ["cameraResults"],
    mutationFn: (deviceId: string) => runAllCameraChecks(deviceId),
    onSuccess: (data) => {
      const list = data ?? [];
      const failed = list.filter((entry) => !entry.succeeded).length;
      setRunMessage(
        failed > 0
          ? `${list.length} checks executados, ${failed} com falha ( ONVIF/alvo inacessível )`
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

  const camerasError = mutationErrorMessage(cameras.error as Error | null);
  const resultsError = mutationErrorMessage(results.error as Error | null);
  const selected = cameras.items.find((entry) => entry.id === selectedId) ?? null;

  const history = useMemo(() => results.data?.results ?? [], [results.data]);
  const latest = useMemo(() => latestByCheck(history), [history]);

  const recordingActive = boolOf(latest.recording, "recordingActive");
  const videoLoss = boolOf(latest.recording, "videoLoss");
  const tamper = boolOf(latest.recording, "tamper");

  const usedPercent = valueOf(latest.storage, "usedPercent");
  const totalMB = valueOf(latest.storage, "totalMB");
  const usedMB = valueOf(latest.storage, "usedMB");
  const availableMB = valueOf(latest.storage, "availableMB");
  const retentionDays = valueOf(latest.storage, "retentionDays");
  const manufacturer = latest.storage?.values?.manufacturer ?? null;
  const model = latest.storage?.values?.model ?? null;

  const hasAnyData = history.length > 0;

  if (cameras.isPending || locationsQuery.isPending) {
    return <LoadingBlock />;
  }

  if (cameras.items.length === 0 && camerasError) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{camerasError}</h2>
        <Button variant="primary" onClick={() => void cameras.refetch()} style={{ marginTop: 8, padding: "6px 14px" }}>
          Tentar de novo
        </Button>
      </div>
    );
  }

  return (
    <ListPageShell
      title="Câmeras"
      singular="câmera"
      plural="câmeras"
      total={cameras.totalCount}
      error={camerasError}
      onRetry={() => void cameras.refetch()}
      actions={
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <ViewSwitcher mode={view} onChange={setView} />
        </div>
      }
    >
      <EntityView<DeviceItem>
        mode={view}
        items={cameras.items}
        getKey={(camera) => camera.id}
        emptyMessage={camerasError ? "—" : "Nenhuma câmera cadastrada"}
        kanban={{
          groupOf: (camera) => camera.locationId || "unknown",
          columns: [
            ...locations.map((location) => ({ key: location.id, label: location.name })),
            { key: "unknown", label: "Sem localização" },
          ],
          overflowColumnKey: "unknown",
        }}
        renderCard={(camera) => (
          <div
            onClick={() => setSelectedId(camera.id)}
            style={{
              padding: 14,
              borderRadius: 8,
              border: `2px solid ${camera.id === selectedId ? "#2563eb" : "#e5e7eb"}`,
              background: camera.id === selectedId ? "#eff6ff" : "#fff",
              cursor: "pointer",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
              <i className="pi pi-camera" style={{ color: "#6366f1" }} />
              <span style={{ fontWeight: 700 }}>{camera.name}</span>
            </div>
            <div style={{ fontSize: 11, color: "#64748b" }}>
              {camera.model ?? "modelo —"} · {camera.ip ?? "sem IP"}
            </div>
            <div style={{ fontSize: 11, color: "#94a3b8" }}>{locationName(camera.locationId)}</div>
          </div>
        )}
        table={
          <DataTablePanel
            value={cameras.items}
            dataKey="id"
            emptyMessage={camerasError ? "—" : "Nenhuma câmera cadastrada"}
            onRowClick={(event) => setSelectedId(String(event.data.id))}
          >
            <Column header="Câmera" body={(camera: DeviceItem) => (
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <i className="pi pi-camera" style={{ color: "#6366f1" }} />
                <span style={{ fontWeight: 600 }}>{camera.name}</span>
                {camera.id === selectedId && (
                  <i className="pi pi-angle-right" style={{ color: "#2563eb", fontSize: 12 }} />
                )}
              </div>
            )} />
            <Column header="Modelo" body={(camera: DeviceItem) => camera.model ?? "—"} />
            <Column header="Fabricante" body={(camera: DeviceItem) => camera.vendor ?? "—"} />
            <Column header="IP" body={(camera: DeviceItem) => camera.ip ?? "—"} style={{ width: 130 }} />
            <Column header="Localização" body={(camera: DeviceItem) => locationName(camera.locationId)} />
          </DataTablePanel>
        }
      />

      {/* Painel — gravação & armazenamento (ONVIF) */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Gravação &amp; Armazenamento — {selected?.name ?? "—"}</h2>
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
            <strong>Executar checks</strong> para consultar a câmera via ONVIF.
          </div>
        )}

        {hasAnyData && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: 16, padding: 16 }}>
            {/* Gravação */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Estado da gravação</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.recording)}
                {latest.recording?.succeeded === false && latest.recording.error
                  ? ` · ${latest.recording.error}`
                  : ""}
              </div>
              <RecordingChip recordingActive={recordingActive} />
              <RecordingFlags videoLoss={videoLoss} tamper={tamper} />
            </div>

            {/* Armazenamento */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Armazenamento</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.storage)}
                {latest.storage?.succeeded === false && latest.storage.error
                  ? ` · ${latest.storage.error}`
                  : ""}
              </div>
              <div style={{ display: "grid", gap: 10 }}>
                <UsageBar label="Uso do armazenamento" percent={usedPercent} color="#8b5cf6" />
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Total</span>
                  <span style={{ fontWeight: 700 }}>{mb(totalMB)}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Usado</span>
                  <span style={{ fontWeight: 700 }}>{mb(usedMB)}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Livre</span>
                  <span style={{ fontWeight: 700 }}>{mb(availableMB)}</span>
                </div>
                {(manufacturer || model) && (
                  <div style={{ fontSize: 10, color: "#64748b" }}>
                    {[manufacturer, model].filter(Boolean).join(" · ")}
                  </div>
                )}
              </div>
            </div>

            {/* Retenção */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Retenção</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.storage)}
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                <span>Dias retenção</span>
                <span style={{ fontWeight: 700 }}>
                  {retentionDays != null ? `${retentionDays} dia(s)` : "—"}
                </span>
              </div>
              <div style={{ fontSize: 10, color: "#94a3b8", marginTop: 6 }}>
                Informativo — derivado do armazenamento, sem regra de alerta.
              </div>
            </div>
          </div>
        )}
      </div>
    </ListPageShell>
  );
}
