import { useCallback, useEffect, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { useObjectQuery } from "@/hooks/queries";
import {
  getUnifiWifi,
  type UnifiAccessPointItem,
  type UnifiSsidItem,
} from "@/services/api/integrations";
import { listDevices, type DeviceItem } from "@/services/api/inventory";
import { formatDateTime } from "@/utils/format";

/**
 * F18-T006 (decisão 2026-10-10) — WiFiPage com dados REAIS do UniFi
 * Controller: `GET /integrations/unifi/wifi` (docs/29 §WiFi) devolve as
 * redes SSID (`/rest/ssidconf`) e a telemetria dos APs (`/stat/device`:
 * estado/uptime, CPU/mem, clientes, rádios e tráfego), servidos com
 * cache Redis de 60s — nada é persistido (regra 8 da integração).
 *
 * A seção de INVENTÁRIO do F17-T014 permanece: lista os APs cadastrados
 * (`GET /inventory/devices?type=AccessPoint`) e funciona mesmo com a
 * integração UniFi desligada — nesse caso o painel de cima mostra o
 * motivo honesto (503 com envelope) em vez de dado inventado.
 */

/** Bytes → texto legível (KB/MB/GB/TB, base 1024). */
function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  const index = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    units.length - 1
  );
  const value = bytes / Math.pow(1024, index);
  return `${value >= 100 ? Math.round(value) : value.toFixed(1)} ${units[index]}`;
}

/** Segundos → "3d 4h" / "5h 12min" / "9min"; 0/negativo = "—". */
function formatUptime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds <= 0) return "—";
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  if (days > 0) return `${days}d ${hours}h`;
  const minutes = Math.floor((seconds % 3600) / 60);
  return hours > 0 ? `${hours}h ${minutes}min` : `${minutes}min`;
}

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function WiFiPage() {
  const wifi = useObjectQuery(["unifi-wifi"], getUnifiWifi);
  const wifiLoading = wifi.isLoading;
  const wifiError = wifi.error ? wifi.error.message : null;
  const wifiData = wifi.data;

  const [aps, setAps] = useState<DeviceItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listDevices({ type: "AccessPoint" });
      setAps(result.items);
      setTotalCount(result.totalCount);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const truncated = totalCount > aps.length;

  const subtitle = wifiLoading
    ? "Carregando…"
    : wifiData
      ? `${wifiData.ssids.length} ${wifiData.ssids.length === 1 ? "rede" : "redes"} · ${wifiData.accessPoints.length} ${wifiData.accessPoints.length === 1 ? "AP" : "APs"} · sondagem ao vivo em ${formatDateTime(new Date(wifiData.capturedAtUtc))}`
      : loading
        ? "Carregando…"
        : `${totalCount} ${totalCount === 1 ? "access point" : "access points"} · inventário (DeviceType.AccessPoint)`;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader title="WiFi" subtitle={subtitle} />

      {wifiError && (
        <ErrorBanner
          message={wifiError}
          onRetry={() => void wifi.refetch()}
        />
      )}
      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Redes (SSID)</h2>
        </div>
        <div className="uni-panel-body">
          {wifiLoading ? (
            <LoadingBlock />
          ) : (
            <DataTablePanel
              panel={false}
              value={wifiData?.ssids ?? []}
              dataKey="name"
              emptyMessage={
                wifiError
                  ? "—"
                  : "Nenhuma rede SSID configurada no Controller"
              }
            >
              <Column
                header="Rede"
                body={(ssid: UnifiSsidItem) => (
                  <div style={{ fontWeight: 500, fontSize: 13 }}>{ssid.name}</div>
                )}
              />
              <Column
                header="Segurança"
                body={(ssid: UnifiSsidItem) => ssid.security || "—"}
                style={{ width: 120, fontFamily: "monospace", fontSize: 12 }}
              />
              <Column
                header="VLAN"
                body={(ssid: UnifiSsidItem) => (ssid.vlan > 0 ? ssid.vlan : "—")}
                style={{ width: 80, fontSize: 12 }}
              />
              <Column
                header="Bandas"
                body={(ssid: UnifiSsidItem) => ssid.bands || "—"}
                style={{ width: 150, fontSize: 12 }}
              />
              <Column
                header="Status"
                body={(ssid: UnifiSsidItem) => (
                  <span
                    style={{
                      fontSize: 12,
                      fontWeight: 500,
                      color: ssid.enabled ? "#16a34a" : "#94a3b8",
                    }}
                  >
                    {ssid.enabled ? "Ativa" : "Desativa"}
                  </span>
                )}
                style={{ width: 90 }}
              />
              <Column
                header="Convidados"
                body={(ssid: UnifiSsidItem) => (ssid.isGuest ? "Sim" : "Não")}
                style={{ width: 100, fontSize: 12 }}
              />
            </DataTablePanel>
          )}
        </div>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Access Points — telemetria</h2>
        </div>
        <div className="uni-panel-body">
          {wifiLoading ? (
            <LoadingBlock />
          ) : (
            <DataTablePanel
              panel={false}
              value={wifiData?.accessPoints ?? []}
              dataKey="mac"
              emptyMessage={
                wifiError
                  ? "—"
                  : "Nenhum AP respondendo no Controller"
              }
            >
              <Column
                header="AP"
                body={(ap: UnifiAccessPointItem) => (
                  <>
                    <div style={{ fontWeight: 500, fontSize: 13 }}>
                      {ap.name || ap.mac}
                    </div>
                    <div style={{ fontSize: 11, color: "#94a3b8" }}>
                      {ap.model || "—"} · {ap.ip || ap.mac}
                    </div>
                  </>
                )}
              />
              <Column
                header="Estado"
                body={(ap: UnifiAccessPointItem) => (
                  <span
                    style={{
                      fontSize: 12,
                      fontWeight: 500,
                      color: ap.connected ? "#16a34a" : "#dc2626",
                    }}
                  >
                    {ap.connected ? "Conectado" : "Desconectado"}
                  </span>
                )}
                style={{ width: 120 }}
              />
              <Column
                header="Clientes"
                body={(ap: UnifiAccessPointItem) => ap.clients}
                style={{ width: 90, textAlign: "right", fontSize: 12 }}
              />
              <Column
                header="CPU / Mem"
                body={(ap: UnifiAccessPointItem) => (
                  <span style={{ fontSize: 12 }}>
                    {ap.cpuPercent}% / {ap.memPercent}%
                  </span>
                )}
                style={{ width: 110, textAlign: "right" }}
              />
              <Column
                header="Uptime"
                body={(ap: UnifiAccessPointItem) => formatUptime(ap.uptimeSeconds)}
                style={{ width: 90, fontSize: 12, color: "#64748b" }}
              />
              <Column
                header="Tráfego"
                body={(ap: UnifiAccessPointItem) => (
                  <span style={{ fontSize: 11 }}>
                    ↓ {formatBytes(ap.rxBytes)} · ↑ {formatBytes(ap.txBytes)}
                  </span>
                )}
                style={{ width: 170, textAlign: "right" }}
              />
              <Column
                header="Rádios"
                body={(ap: UnifiAccessPointItem) =>
                  ap.radios.length === 0 ? (
                    "—"
                  ) : (
                    <div
                      style={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 3,
                        fontSize: 11,
                        color: "#6b7280",
                      }}
                    >
                      {ap.radios.map((radio, index) => (
                        <div key={`${ap.mac}-radio-${index}`}>
                          {radio.band} · canal {radio.channel} · {radio.width} MHz
                          {" · "}
                          {radio.txPower} dBm
                          {radio.utilization > 0
                            ? ` · util ${radio.utilization}%`
                            : ""}
                        </div>
                      ))}
                    </div>
                  )
                }
                style={{ width: 230 }}
              />
            </DataTablePanel>
          )}
        </div>
      </div>

      <div>
        <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 10 }}>
          <h2 style={{ fontSize: 14, fontWeight: 600 }}>Inventário de APs</h2>
          {truncated && (
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              mostrando {aps.length} de {totalCount} — página completa em Dispositivos
            </span>
          )}
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 12 }}>
          {aps.map((ap) => (
            <div key={ap.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                <div style={{ width: 36, height: 36, borderRadius: 8, background: "#f0fdf4", color: "#22c55e", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  <i className="pi pi-wifi" />
                </div>
                <div>
                  <div style={{ fontSize: 13, fontWeight: 600 }}>{ap.name}</div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>
                    {ap.model ?? ap.type} · {ap.ip ?? "sem IP"}
                  </div>
                </div>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, fontSize: 11, color: "#6b7280" }}>
                <div><span style={{ color: "#9ca3af" }}>Vendor:</span> <span style={{ fontWeight: 500 }}>{ap.vendor ?? "—"}</span></div>
                <div><span style={{ color: "#9ca3af" }}>Serial:</span> <span style={{ fontWeight: 500, fontFamily: "monospace" }}>{ap.serial ?? "—"}</span></div>
              </div>
              {ap.description && (
                <div style={{ marginTop: 10, paddingTop: 10, borderTop: "1px solid #f3f4f6", fontSize: 11, color: "#6b7280" }}>
                  {ap.description}
                </div>
              )}
            </div>
          ))}
          {!loading && aps.length === 0 && !error && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              Nenhum Access Point no inventário — cadastre devices do tipo AccessPoint em Dispositivos
            </div>
          )}
          {loading && aps.length === 0 && (
            <LoadingBlock style={{ gridColumn: "1 / -1" }} />
          )}
        </div>
      </div>
    </div>
  );
}
