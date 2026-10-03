import { useCallback, useEffect, useRef, useState } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { useNavigate, useParams } from "react-router-dom";
import { ApiError } from "@/services/api/client";
import {
  getDevice,
  listDeviceIps,
  listDeviceMacs,
  listLocations,
  listNetworkInterfaces,
  listSwitchPorts,
  type DeviceItem,
  type IpAddressItem,
  type MacAddressItem,
  type NetworkInterfaceItem,
  type SwitchPortItem,
} from "@/services/api/inventory";
import {
  getDeviceResults,
  type MonitoringResultItem,
} from "@/services/api/monitoring";
import { listAlerts, type AlertItem } from "@/services/api/alerts";
import { formatDateTime } from "@/utils/format";

/**
 * F17-T017 — DeviceDetailPage conectada ao backend (substitui
 * `allDevices` + `printerDetails/cameraDetails/phoneDetails/serverDetails/
 * vmDetails/switchPorts/nasDetails/upsDetails` de `data/deviceData`).
 *
 * O protótipo tinha uma visão por TIPO com telemetria de agente (toner de
 * impressora, linhas de telefone, CPU/memória/discos/serviços, volumes do
 * NAS, bateria da UPS…) — **nada disso tem backend** (não existe coleta de
 * agente; o histórico real é o poll de `MonitoringResult`), então todos os
 * `*View` por tipo saíram. A página virou abas sobre contratos reais:
 *
 * - **Geral** — `GET /inventory/devices/{id}` (404 → tela "não encontrado")
 *   + localização resolvida por `GET /inventory/locations`; checks do
 *   perfil vêm no próprio `DeviceDto.monitoringChecks` (sem pedido extra).
 * - **Monitoramento** — `GET /monitoring/results/{id}?limit=20` (polls com
 *   check/status/duração/erro/`values`) + `GET /monitoring/alerts?deviceId=`
 *   (5 últimos; total real).
 * - **Endereços** — `GET /inventory/network-interfaces|ip-addresses|
 *   mac-addresses?deviceId=` (IP/MAC pertencem a interface → join local).
 * - **Portas** (só `DeviceType.Switch`) — `GET /network/switches/{id}/ports`
 *   (reusa o serviço do T007), carregada só ao abrir a aba.
 *
 * Divergências do protótipo: chip Online/Offline (sem endpoint de estado
 * atual — decisão do T007), `department`/`firmware`/`uptime` saíram (não
 * existem no `DeviceDto`), ícones/cores agora usam o enum real
 * `DeviceType` (Server/Switch/… — os tipos minúsculos do mock `vm`/`nas`/
 * `gateway` não existem). Sem mutations nesta página.
 */

type Tab = "geral" | "monitoramento" | "enderecos" | "portas";

const typeIcon: Record<string, string> = {
  Server: "pi pi-server",
  Workstation: "pi pi-desktop",
  Switch: "pi pi-clone",
  Router: "pi pi-compass",
  Firewall: "pi pi-shield",
  AccessPoint: "pi pi-wifi",
  Printer: "pi pi-print",
  Camera: "pi pi-video",
  Phone: "pi pi-phone",
  UPS: "pi pi-bolt",
  Other: "pi pi-question",
};

const typeColor: Record<string, string> = {
  Server: "#3b82f6",
  Workstation: "#06b6d4",
  Switch: "#8b5cf6",
  Router: "#3b82f6",
  Firewall: "#ef4444",
  AccessPoint: "#22c55e",
  Printer: "#f59e0b",
  Camera: "#ec4899",
  Phone: "#06b6d4",
  UPS: "#f59e0b",
  Other: "#94a3b8",
};

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", gap: 16, padding: "6px 0", borderBottom: "1px solid #f3f4f6", fontSize: 12 }}>
      <span style={{ color: "#6b7280" }}>{label}</span>
      <span style={{ fontWeight: 500, fontFamily: "monospace", textAlign: "right", overflowWrap: "anywhere" }}>{value}</span>
    </div>
  );
}

const tabStyle = (active: boolean) => ({
  padding: "6px 14px",
  fontSize: 12,
  fontWeight: 500,
  borderRadius: 5,
  background: active ? "white" : "transparent",
  color: active ? "#1a1a1a" : "#6b7280",
  boxShadow: active ? "0 1px 2px rgba(0,0,0,0.06)" : "none",
  border: "none",
  cursor: "pointer",
});

export function DeviceDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [device, setDevice] = useState<DeviceItem | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [locations, setLocations] = useState<Record<string, string>>({});

  const [tab, setTab] = useState<Tab>("geral");

  const [results, setResults] = useState<MonitoringResultItem[]>([]);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [alertsTotal, setAlertsTotal] = useState(0);
  const [monitoringLoading, setMonitoringLoading] = useState(false);

  const [interfaces, setInterfaces] = useState<NetworkInterfaceItem[]>([]);
  const [ips, setIps] = useState<IpAddressItem[]>([]);
  const [macs, setMacs] = useState<MacAddressItem[]>([]);
  const [addrTotals, setAddrTotals] = useState({ networkInterfaces: 0, ipAddresses: 0, macAddresses: 0 });
  const [addrLoading, setAddrLoading] = useState(false);

  const [ports, setPorts] = useState<SwitchPortItem[]>([]);
  const [portsTotal, setPortsTotal] = useState(0);
  const [portsLoading, setPortsLoading] = useState(false);

  // Abas carregam uma vez (ref síncrono protege do duplo-effect do StrictMode).
  const requested = useRef<Set<Tab>>(new Set());

  const loadDevice = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    setNotFound(false);
    try {
      const data = await getDevice(id);
      if (data) setDevice(data);
      else setNotFound(true);
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 404) setNotFound(true);
      else setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void loadDevice();
  }, [loadDevice]);

  useEffect(() => {
    let cancelled = false;
    listLocations()
      .then((page) => {
        if (cancelled) return;
        const map: Record<string, string> = {};
        page.items.forEach((entry) => { map[entry.id] = entry.name; });
        setLocations(map);
      })
      .catch(() => { /* rótulo de localização é auxiliar — em falha mostra o Guid */ });
    return () => { cancelled = true; };
  }, []);

  const loadMonitoring = useCallback(async () => {
    if (!id) return;
    setMonitoringLoading(true);
    setError(null);
    try {
      const [deviceResults, deviceAlerts] = await Promise.all([
        getDeviceResults(id, 20),
        listAlerts({ deviceId: id, page: 1, pageSize: 5 }),
      ]);
      setResults(deviceResults?.results ?? []);
      setAlerts(deviceAlerts.items);
      setAlertsTotal(deviceAlerts.totalCount);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setMonitoringLoading(false);
    }
  }, [id]);

  const loadAddresses = useCallback(async () => {
    if (!id) return;
    setAddrLoading(true);
    setError(null);
    try {
      const [networkInterfaces, ipAddresses, macAddresses] = await Promise.all([
        listNetworkInterfaces(id),
        listDeviceIps(id),
        listDeviceMacs(id),
      ]);
      setInterfaces(networkInterfaces.items);
      setIps(ipAddresses.items);
      setMacs(macAddresses.items);
      setAddrTotals({
        networkInterfaces: networkInterfaces.totalCount,
        ipAddresses: ipAddresses.totalCount,
        macAddresses: macAddresses.totalCount,
      });
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setAddrLoading(false);
    }
  }, [id]);

  const loadPorts = useCallback(async () => {
    if (!id) return;
    setPortsLoading(true);
    setError(null);
    try {
      const page = await listSwitchPorts(id);
      setPorts(page.items);
      setPortsTotal(page.totalCount);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setPortsLoading(false);
    }
  }, [id]);

  function requestTab(name: Tab, force = false) {
    if (!force && requested.current.has(name)) return;
    requested.current.add(name);
    if (name === "monitoramento") void loadMonitoring();
    else if (name === "enderecos") void loadAddresses();
    else if (name === "portas") void loadPorts();
  }

  useEffect(() => {
    if (device) requestTab(tab);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, device]);

  if (notFound) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>Dispositivo não encontrado</h2>
        <button onClick={() => navigate("/devices")} style={{ marginTop: 8, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          Voltar para Dispositivos
        </button>
      </div>
    );
  }

  if (loading && !device) {
    return <div style={{ padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</div>;
  }

  if (!device) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{error ?? "Erro ao carregar o dispositivo"}</h2>
        <button onClick={() => void loadDevice()} style={{ marginTop: 8, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          Tentar de novo
        </button>
      </div>
    );
  }

  const icon = typeIcon[device.type] ?? "pi pi-question";
  const color = typeColor[device.type] ?? "#3b82f6";
  const isSwitch = device.type === "Switch";
  const tabs: Array<{ id: Tab; label: string }> = [
    { id: "geral", label: "Geral" },
    { id: "monitoramento", label: "Monitoramento" },
    { id: "enderecos", label: "Endereços" },
    ...(isSwitch ? [{ id: "portas" as Tab, label: "Portas" }] : []),
  ];

  const interfaceName = (interfaceId: string): string =>
    interfaces.find((entry) => entry.id === interfaceId)?.name ?? "—";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <button onClick={() => navigate("/devices")} style={{ width: 32, height: 32, borderRadius: 6, border: "1px solid #e2e8f0", display: "flex", alignItems: "center", justifyContent: "center", background: "white", cursor: "pointer" }}>
          <i className="pi pi-arrow-left" style={{ fontSize: 12 }} />
        </button>
        <div style={{ width: 40, height: 40, borderRadius: 8, background: `${color}15`, color, display: "flex", alignItems: "center", justifyContent: "center" }}>
          <i className={icon} />
        </div>
        <div>
          <h1 style={{ fontSize: 18, fontWeight: 700 }}>{device.name}</h1>
          <p style={{ fontSize: 12, color: "#6b7280" }}>
            {device.model ?? device.type} · {device.ip ?? "sem IP"}
          </p>
        </div>
        <span style={{ marginLeft: "auto", fontSize: 10, fontWeight: 600, padding: "3px 8px", borderRadius: 4, background: `${color}15`, color }}>
          {device.type}
        </span>
      </div>

      {/* Info bar */}
      <div className="uni-panel" style={{ padding: 12 }}>
        <div style={{ display: "flex", gap: 20, fontSize: 11, color: "#6b7280", flexWrap: "wrap" }}>
          <span><i className="pi pi-map-marker" style={{ marginRight: 4 }} />{locations[device.locationId] ?? device.locationId}</span>
          {device.serial && <span><i className="pi pi-tag" style={{ marginRight: 4 }} />{device.serial}</span>}
          {device.vendor && <span><i className="pi pi-credit-card" style={{ marginRight: 4 }} />{device.vendor}</span>}
          {device.mac && <span style={{ fontFamily: "monospace" }}>{device.mac}</span>}
        </div>
      </div>

      {error && (
        <ErrorBanner
          message={error}
          onRetry={() => {
            setError(null);
            requestTab(tab, true);
          }}
        />
      )}

      {/* Abas */}
      <div style={{ display: "flex", gap: 4, background: "#f3f4f6", padding: 2, borderRadius: 6, alignSelf: "flex-start" }}>
        {tabs.map((entry) => (
          <button key={entry.id} onClick={() => setTab(entry.id)} style={tabStyle(tab === entry.id)}>
            {entry.label}
          </button>
        ))}
      </div>

      {/* Geral */}
      {tab === "geral" && (
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>Identificação</h2></div>
          <div className="uni-panel-body">
            <InfoRow label="Nome" value={device.name} />
            <InfoRow label="Tipo" value={device.type} />
            <InfoRow label="IP" value={device.ip ?? "—"} />
            <InfoRow label="MAC" value={device.mac ?? "—"} />
            <InfoRow label="Serial" value={device.serial ?? "—"} />
            <InfoRow label="Modelo" value={device.model ?? "—"} />
            <InfoRow label="Vendor" value={device.vendor ?? "—"} />
            <InfoRow label="Localização" value={locations[device.locationId] ?? device.locationId} />
            <InfoRow label="Descrição" value={device.description ?? "—"} />
            <InfoRow label="Perfil de checks" value={device.profileIsCustom ? "personalizado (override)" : "padrão do tipo"} />
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 16, padding: "8px 0", fontSize: 12 }}>
              <span style={{ color: "#6b7280" }}>Checks ativos</span>
              <div style={{ display: "flex", gap: 4, flexWrap: "wrap", justifyContent: "flex-end" }}>
                {device.monitoringChecks.length > 0 ? (
                  device.monitoringChecks.map((check) => (
                    <span key={check} style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: "#eff6ff", color: "#2563eb" }}>
                      {check}
                    </span>
                  ))
                ) : (
                  <span style={{ fontFamily: "monospace" }}>—</span>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Monitoramento */}
      {tab === "monitoramento" && (
        <>
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Últimos polls</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>até 20, mais recentes primeiro</span>
            </div>
            <table className="uni-clients-table">
              <thead>
                <tr>
                  <th>Check</th>
                  <th>Status</th>
                  <th>Duração</th>
                  <th>Detalhes</th>
                  <th>Quando</th>
                </tr>
              </thead>
              <tbody>
                {results.map((result) => (
                  <tr key={result.id}>
                    <td style={{ fontWeight: 500, fontSize: 12 }}>{result.check}</td>
                    <td>
                      <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: result.succeeded ? "#f0fdf4" : "#fef2f2", color: result.succeeded ? "#15803d" : "#dc2626" }}>
                        {result.succeeded ? "OK" : "Falhou"}
                      </span>
                    </td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{result.durationMs} ms</td>
                    <td style={{ fontSize: 11, maxWidth: 260 }}>
                      {result.error ? (
                        <span style={{ color: "#dc2626" }}>{result.error}</span>
                      ) : result.values ? (
                        <span style={{ fontFamily: "monospace", color: "#64748b", overflowWrap: "anywhere" }}>
                          {Object.entries(result.values).map(([key, value]) => `${key}=${value}`).join(" · ")}
                        </span>
                      ) : (
                        <span style={{ color: "#94a3b8" }}>—</span>
                      )}
                    </td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(result.checkedAt))}</td>
                  </tr>
                ))}
                {!monitoringLoading && results.length === 0 && !error && (
                  <tr>
                    <td colSpan={5} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                      Nenhum poll registrado ainda — o histórico nasce com a execução dos checks
                    </td>
                  </tr>
                )}
                {monitoringLoading && (
                  <tr>
                    <td colSpan={5} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                      Carregando…
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Alertas deste dispositivo</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>{alertsTotal} no total · mostrando até 5</span>
            </div>
            <table className="uni-clients-table">
              <thead>
                <tr>
                  <th>Severidade</th>
                  <th>Check</th>
                  <th>Mensagem</th>
                  <th>Status</th>
                  <th>Aberto em</th>
                </tr>
              </thead>
              <tbody>
                {alerts.map((alert) => (
                  <tr key={alert.id}>
                    <td>
                      <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: alert.severity === "Critical" ? "#fef2f2" : "#fef3c7", color: alert.severity === "Critical" ? "#dc2626" : "#d97706" }}>
                        {alert.severity === "Critical" ? "Crítico" : "Atenção"}
                      </span>
                    </td>
                    <td style={{ fontSize: 12, fontWeight: 500 }}>{alert.check ?? alert.source}</td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{alert.message}</td>
                    <td style={{ fontSize: 12 }}>{alert.status === "Open" ? "Aberto" : alert.status === "Resolved" ? "Resolvido" : "Suprimido"}</td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(alert.openedAt))}</td>
                  </tr>
                ))}
                {!monitoringLoading && alerts.length === 0 && !error && (
                  <tr>
                    <td colSpan={5} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                      Nenhum alerta neste dispositivo
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}

      {/* Endereços */}
      {tab === "enderecos" && (
        <>
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Interfaces de rede</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>
                {addrTotals.networkInterfaces > interfaces.length
                  ? `mostrando ${interfaces.length} de ${addrTotals.networkInterfaces}`
                  : `${interfaces.length}`}
              </span>
            </div>
            <table className="uni-clients-table">
              <thead>
                <tr><th>Nome</th><th>Descrição</th></tr>
              </thead>
              <tbody>
                {interfaces.map((entry) => (
                  <tr key={entry.id}>
                    <td style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }}>{entry.name}</td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{entry.description ?? "—"}</td>
                  </tr>
                ))}
                {!addrLoading && interfaces.length === 0 && !error && (
                  <tr>
                    <td colSpan={2} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                      Nenhuma interface cadastrada
                    </td>
                  </tr>
                )}
                {addrLoading && (
                  <tr>
                    <td colSpan={2} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Endereços IP</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>
                {addrTotals.ipAddresses > ips.length ? `mostrando ${ips.length} de ${addrTotals.ipAddresses}` : `${ips.length}`}
              </span>
            </div>
            <table className="uni-clients-table">
              <thead>
                <tr><th>IP</th><th>Interface</th></tr>
              </thead>
              <tbody>
                {ips.map((entry) => (
                  <tr key={entry.id}>
                    <td style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }}>{entry.ip}</td>
                    <td style={{ fontSize: 12, color: "#64748b", fontFamily: "monospace" }}>{interfaceName(entry.interfaceId)}</td>
                  </tr>
                ))}
                {!addrLoading && ips.length === 0 && !error && (
                  <tr>
                    <td colSpan={2} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                      Nenhum endereço IP cadastrado
                    </td>
                  </tr>
                )}
                {addrLoading && (
                  <tr>
                    <td colSpan={2} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Endereços MAC</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>
                {addrTotals.macAddresses > macs.length ? `mostrando ${macs.length} de ${addrTotals.macAddresses}` : `${macs.length}`}
              </span>
            </div>
            <table className="uni-clients-table">
              <thead>
                <tr><th>MAC</th><th>Interface</th></tr>
              </thead>
              <tbody>
                {macs.map((entry) => (
                  <tr key={entry.id}>
                    <td style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }}>{entry.mac}</td>
                    <td style={{ fontSize: 12, color: "#64748b", fontFamily: "monospace" }}>{interfaceName(entry.interfaceId)}</td>
                  </tr>
                ))}
                {!addrLoading && macs.length === 0 && !error && (
                  <tr>
                    <td colSpan={2} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                      Nenhum endereço MAC cadastrado
                    </td>
                  </tr>
                )}
                {addrLoading && (
                  <tr>
                    <td colSpan={2} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}

      {/* Portas (só Switch) */}
      {tab === "portas" && isSwitch && (
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Portas</h2>
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              {portsTotal > ports.length ? `mostrando ${ports.length} de ${portsTotal}` : `${ports.length}`}
            </span>
          </div>
          <table className="uni-clients-table">
            <thead>
              <tr><th>Nome</th><th>Descrição</th><th>Link</th></tr>
            </thead>
            <tbody>
              {ports.map((port) => (
                <tr key={port.id}>
                  <td style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }}>{port.name}</td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{port.description ?? "—"}</td>
                  <td>
                    <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: port.linkUp === true ? "#f0fdf4" : port.linkUp === false ? "#fef2f2" : "#f1f5f9", color: port.linkUp === true ? "#15803d" : port.linkUp === false ? "#dc2626" : "#64748b" }}>
                      {port.linkUp === true ? "Up" : port.linkUp === false ? "Down" : "—"}
                    </span>
                  </td>
                </tr>
              ))}
              {!portsLoading && ports.length === 0 && !error && (
                <tr>
                  <td colSpan={3} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                    Nenhuma porta cadastrada
                  </td>
                </tr>
              )}
              {portsLoading && (
                <tr>
                  <td colSpan={3} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
