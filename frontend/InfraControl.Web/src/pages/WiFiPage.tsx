import { useCallback, useEffect, useState } from "react";
import { Column } from "primereact/column";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { errorMessage, useObjectQuery } from "@/hooks/queries";
import {
  getUnifiWifi,
  type UnifiAccessPointItem,
  type UnifiSsidItem,
} from "@/services/api/integrations";
import { getUnifiLiveClients } from "@/services/api/unifi";
import { listDevices, type DeviceItem } from "@/services/api/inventory";
import { formatDateTime } from "@/utils/format";
import { formatSignal, summarizeLiveClients } from "@/utils/wifiClients";

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

/** Colunas do quadro de SSIDs — o mesmo recorte da coluna "Status". */
const SSID_STATUS_COLUMNS = [
  { key: "on", label: "Ativa" },
  { key: "off", label: "Desativa" },
];

function ssidStatusKey(ssid: UnifiSsidItem): string {
  return ssid.enabled ? "on" : "off";
}

/** Colunas do quadro de APs — o mesmo recorte da coluna "Estado". */
const AP_STATE_COLUMNS = [
  { key: "up", label: "Conectado" },
  { key: "down", label: "Desconectado" },
];

function apStateKey(ap: UnifiAccessPointItem): string {
  return ap.connected ? "up" : "down";
}

export function WiFiPage() {
  const wifi = useObjectQuery(["unifi-wifi"], getUnifiWifi);
  const wifiLoading = wifi.isLoading;
  const wifiError = wifi.error ? wifi.error.message : null;
  const wifiData = wifi.data;

  // F30-T004 S1 — clientes AO VIVO (GET /integrations/unifi/clients, que já
  // existe): agregados por SSID/RSSI. Erro aqui NÃO entra na casca (um slot,
  // fonte é a sondagem de WiFi) — o painel de clientes cai em "—" honesto.
  const clients = useObjectQuery(["unifi-live-clients"], getUnifiLiveClients);
  const clientsError = clients.error ? clients.error.message : null;
  const clientSummary = summarizeLiveClients(clients.data ?? []);

  const [aps, setAps] = useState<DeviceItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Adoção do padrão de visões (backlog Operations UX 2026-10-11): a página
  // tem 2 tabelas distintas, então cada seção ganha seu PRÓPRIO seletor no
  // cabeçalho, com preferência própria no localStorage — um seletor global
  // não teria sobre o que agir. O inventário de APs já é grid de cards e
  // fica como está (não é tabela).
  const [ssidView, setSsidView] = useEntityViewMode("wifi-ssids", "table");
  const [apView, setApView] = useEntityViewMode("wifi-aps", "table");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listDevices({ type: "AccessPoint" });
      setAps(result.items);
      setTotalCount(result.totalCount);
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const truncated = totalCount > aps.length;

  /** Card de SSID — usado nas visões grid E quadro (mesmos campos da tabela). */
  function renderSsidCard(ssid: UnifiSsidItem) {
    return (
      <div
        style={{
          background: "white",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
          padding: 12,
          display: "flex",
          flexDirection: "column",
          gap: 4,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
          <span style={{ fontSize: 13, fontWeight: 600 }}>{ssid.name}</span>
          <StatusChip
            label={ssid.enabled ? "Ativa" : "Desativa"}
            background={ssid.enabled ? "#f0fdf4" : "#f1f5f9"}
            color={ssid.enabled ? "#16a34a" : "#94a3b8"}
            fontSize={10}
            fontWeight={600}
          />
        </div>
        <div style={{ fontFamily: "monospace", fontSize: 11, color: "#64748b" }}>
          {ssid.security || "—"}
        </div>
        <div style={{ fontSize: 11, color: "#64748b", display: "flex", gap: 12, flexWrap: "wrap" }}>
          <span>VLAN: {ssid.vlan > 0 ? ssid.vlan : "—"}</span>
          <span>{ssid.bands || "—"}</span>
          {ssid.isGuest && <span>Convidados</span>}
        </div>
      </div>
    );
  }

  /** Card de AP — usado nas visões grid E quadro (mesmos campos da tabela). */
  function renderApCard(ap: UnifiAccessPointItem) {
    return (
      <div
        style={{
          background: "white",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
          padding: 12,
          display: "flex",
          flexDirection: "column",
          gap: 4,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
          <span style={{ fontSize: 13, fontWeight: 600 }}>{ap.name || ap.mac}</span>
          <StatusChip
            label={ap.connected ? "Conectado" : "Desconectado"}
            background={ap.connected ? "#f0fdf4" : "#fef2f2"}
            color={ap.connected ? "#16a34a" : "#dc2626"}
            fontSize={10}
            fontWeight={600}
          />
        </div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>
          {ap.model || "—"} · {ap.ip || ap.mac}
        </div>
        <div style={{ fontSize: 11, color: "#64748b", display: "flex", gap: 12, flexWrap: "wrap" }}>
          <span>Clientes: {ap.clients}</span>
          <span>CPU/Mem: {ap.cpuPercent}% / {ap.memPercent}%</span>
          <span>Uptime: {formatUptime(ap.uptimeSeconds)}</span>
        </div>
        <div style={{ fontSize: 11, color: "#64748b" }}>
          ↓ {formatBytes(ap.rxBytes)} · ↑ {formatBytes(ap.txBytes)}
        </div>
        {ap.radios.length > 0 && (
          <div style={{ fontSize: 11, color: "#6b7280", display: "flex", flexDirection: "column", gap: 2 }}>
            {ap.radios.map((radio, index) => (
              <span key={`${ap.mac}-radio-${index}`}>
                {radio.band} · canal {radio.channel} · {radio.width} MHz · {radio.txPower} dBm
              </span>
            ))}
          </div>
        )}
      </div>
    );
  }

  // A casca monta `contagem · nota`. Aqui a "nota" é o resto do subtítulo de
  // sondagem ao vivo, byte a byte — o que muda é só quem compõe a string.
  const live = wifiData != null;

  return (
    <ListPageShell
      title="WiFi"
      singular={live ? "rede" : "access point"}
      plural={live ? "redes" : "access points"}
      total={live ? wifiData.ssids.length : totalCount}
      loading={wifiLoading || (!wifiData && loading)}
      note={
        live
          ? `${wifiData.accessPoints.length} ${wifiData.accessPoints.length === 1 ? "AP" : "APs"} · sondagem ao vivo em ${formatDateTime(new Date(wifiData.capturedAtUtc))}`
          : "inventário (DeviceType.AccessPoint)"
      }
      // Um só slot: com a sondagem e o inventário fora ao mesmo tempo a tela
      // mostrava duas caixas vermelhas coladas. O retry aponta para a fonte do
      // erro que está sendo exibido.
      error={wifiError ?? error ?? null}
      onRetry={wifiError ? () => void wifi.refetch() : () => void load()}
    >

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Redes (SSID)</h2>
          <ViewSwitcher mode={ssidView} onChange={setSsidView} />
        </div>
        <div className="uni-panel-body">
          {wifiLoading ? (
            <LoadingBlock />
          ) : (
            <EntityView<UnifiSsidItem>
              mode={ssidView}
              items={wifiData?.ssids ?? []}
              getKey={(ssid) => ssid.name}
              emptyMessage={wifiError ? "—" : "Nenhuma rede SSID configurada no Controller"}
              kanban={{ groupOf: ssidStatusKey, columns: SSID_STATUS_COLUMNS }}
              renderCard={renderSsidCard}
              table={
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
              }
            />
          )}
        </div>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Access Points — telemetria</h2>
          <ViewSwitcher mode={apView} onChange={setApView} />
        </div>
        <div className="uni-panel-body">
          {wifiLoading ? (
            <LoadingBlock />
          ) : (
            <EntityView<UnifiAccessPointItem>
              mode={apView}
              items={wifiData?.accessPoints ?? []}
              getKey={(ap) => ap.mac}
              emptyMessage={wifiError ? "—" : "Nenhum AP respondendo no Controller"}
              kanban={{ groupOf: apStateKey, columns: AP_STATE_COLUMNS }}
              renderCard={renderApCard}
              table={
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
              }
            />
          )}
        </div>
      </div>

      {/* F30-T004 S1 — clientes ao vivo: agregados do
          GET /integrations/unifi/clients (ficha 45 áreas C/E: clientes por
          SSID e RSSI médio). Sem tabela — e cada TABELA desta página é que
          tem seletor de visão. Label e valor no mesmo text node (getByText
          não junta irmãos). */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Clientes ao vivo</h2>
        </div>
        <div className="uni-panel-body">
          {clients.isLoading ? (
            <LoadingBlock />
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <div
                style={{
                  fontSize: 12,
                  color: clientsError ? "#94a3b8" : "#64748b",
                  display: "flex",
                  gap: 16,
                  flexWrap: "wrap",
                }}
              >
                <span>{`Total: ${clientsError ? "—" : clientSummary.total}`}</span>
                <span>{`Com fio: ${clientsError ? "—" : clientSummary.wired}`}</span>
                <span>{`Sem fio: ${clientsError ? "—" : clientSummary.wireless}`}</span>
                <span>
                  {`RSSI médio: ${
                    clientsError ? "—" : formatSignal(clientSummary.averageSignal)
                  }`}
                </span>
                {/* S1b: banda do sem-fio derivada do rádio BRUTO
                    (ng→2.4, na→5) no utilitário; sem rádio conhecido
                    agrupa sob "—". Tudo num text node único. */}
                <span>
                  {`Por banda: ${
                    clientsError || clientSummary.byBand.length === 0
                      ? "—"
                      : clientSummary.byBand
                          .map(
                            (row) =>
                              `${row.band ? `${row.band} GHz` : "—"} ${row.count}`
                          )
                          .join(" · ")
                  }`}
                </span>
              </div>
              {clientsError ? (
                <span style={{ fontSize: 12, color: "#94a3b8" }}>—</span>
              ) : clientSummary.bySsid.length === 0 ? (
                <span style={{ fontSize: 12, color: "#94a3b8" }}>
                  Nenhum cliente sem-fio no Controller
                </span>
              ) : (
                clientSummary.bySsid.map((row) => (
                  <span
                    key={row.ssid ?? "sem-ssid"}
                    style={{ fontSize: 12, color: "#64748b" }}
                  >
                    {`${row.ssid ?? "—"}: ${row.count}`}
                  </span>
                ))
              )}
            </div>
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
    </ListPageShell>
  );
}
