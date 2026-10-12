import { useCallback, useEffect, useRef, useState } from "react";
import { Column } from "primereact/column";
import { DataTable } from "primereact/datatable";
import { StatusChip } from "@/components/ui/StatusChip";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { useNavigate, useParams } from "react-router-dom";
import { BreadCrumb } from "primereact/breadcrumb";
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
import { errorMessage } from "@/hooks/queries";

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
      else setError(errorMessage(caught));
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
      setError(errorMessage(caught));
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
      setError(errorMessage(caught));
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
      setError(errorMessage(caught));
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
        <Button variant="primary" onClick={() => navigate("/devices")} style={{ marginTop: 8, padding: "6px 14px" }}>
          Voltar para Dispositivos
        </Button>
      </div>
    );
  }

  if (loading && !device) {
    return <LoadingBlock />;
  }

  if (!device) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{error ?? "Erro ao carregar o dispositivo"}</h2>
        <Button variant="primary" onClick={() => void loadDevice()} style={{ marginTop: 8, padding: "6px 14px" }}>
          Tentar de novo
        </Button>
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
      <BreadCrumb className="uni-breadcrumb" home={{ icon: "pi pi-home", command: () => navigate("/") }} model={[{ label: "Dispositivos", command: () => navigate("/devices") }, { label: device.name }]} />
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
            <DataTable value={results} dataKey="id" loading={monitoringLoading} size="small" emptyMessage={error ? "—" : "Nenhum poll registrado ainda — o histórico nasce com a execução dos checks"} tableStyle={{ width: "100%" }}>
              <Column header="Check" field="check" style={{ fontWeight: 500, fontSize: 12 }} />
              <Column header="Status" body={(result: MonitoringResultItem) => <StatusChip label={result.succeeded ? "OK" : "Falhou"} background={result.succeeded ? "#f0fdf4" : "#fef2f2"} color={result.succeeded ? "#15803d" : "#dc2626"} fontWeight={600} fontSize={10} />} style={{ width: 90 }} />
              <Column header="Duração" body={(result: MonitoringResultItem) => `${result.durationMs} ms`} style={{ fontSize: 12, color: "#64748b", width: 100 }} />
              <Column header="Detalhes" body={(result: MonitoringResultItem) => result.error ? <span style={{ color: "#dc2626" }}>{result.error}</span> : result.values ? <span style={{ fontFamily: "monospace", color: "#64748b", overflowWrap: "anywhere" }}>{Object.entries(result.values).map(([key, value]) => `${key}=${value}`).join(" · ")}</span> : <span style={{ color: "#94a3b8" }}>—</span>} style={{ fontSize: 11 }} />
              <Column header="Quando" body={(result: MonitoringResultItem) => formatDateTime(new Date(result.checkedAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
            </DataTable>
          </div>

          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Alertas deste dispositivo</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>{alertsTotal} no total · mostrando até 5</span>
            </div>
            <DataTable value={alerts} dataKey="id" loading={monitoringLoading} size="small" emptyMessage={error ? "—" : "Nenhum alerta neste dispositivo"} tableStyle={{ width: "100%" }}>
              <Column header="Severidade" body={(alert: AlertItem) => <StatusChip label={alert.severity === "Critical" ? "Crítico" : "Atenção"} background={alert.severity === "Critical" ? "#fef2f2" : "#fef3c7"} color={alert.severity === "Critical" ? "#dc2626" : "#d97706"} fontWeight={600} fontSize={10} />} style={{ width: 100 }} />
              <Column header="Check" body={(alert: AlertItem) => alert.check ?? alert.source} style={{ fontSize: 12, fontWeight: 500 }} />
              <Column header="Mensagem" field="message" style={{ fontSize: 12, color: "#64748b" }} />
              <Column header="Status" body={(alert: AlertItem) => alert.status === "Open" ? "Aberto" : alert.status === "Resolved" ? "Resolvido" : "Suprimido"} style={{ fontSize: 12, width: 105 }} />
              <Column header="Aberto em" body={(alert: AlertItem) => formatDateTime(new Date(alert.openedAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
            </DataTable>
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
            <DataTable value={interfaces} dataKey="id" loading={addrLoading} size="small" emptyMessage={error ? "—" : "Nenhuma interface cadastrada"} tableStyle={{ width: "100%" }}>
              <Column header="Nome" field="name" style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }} />
              <Column header="Descrição" body={(entry: NetworkInterfaceItem) => entry.description ?? "—"} style={{ fontSize: 12, color: "#64748b" }} />
            </DataTable>
          </div>

          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Endereços IP</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>
                {addrTotals.ipAddresses > ips.length ? `mostrando ${ips.length} de ${addrTotals.ipAddresses}` : `${ips.length}`}
              </span>
            </div>
            <DataTable value={ips} dataKey="id" loading={addrLoading} size="small" emptyMessage={error ? "—" : "Nenhum endereço IP cadastrado"} tableStyle={{ width: "100%" }}>
              <Column header="IP" field="ip" style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }} />
              <Column header="Interface" body={(entry: IpAddressItem) => interfaceName(entry.interfaceId)} style={{ fontSize: 12, color: "#64748b", fontFamily: "monospace" }} />
            </DataTable>
          </div>

          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Endereços MAC</h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>
                {addrTotals.macAddresses > macs.length ? `mostrando ${macs.length} de ${addrTotals.macAddresses}` : `${macs.length}`}
              </span>
            </div>
            <DataTable value={macs} dataKey="id" loading={addrLoading} size="small" emptyMessage={error ? "—" : "Nenhum endereço MAC cadastrado"} tableStyle={{ width: "100%" }}>
              <Column header="MAC" field="mac" style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }} />
              <Column header="Interface" body={(entry: MacAddressItem) => interfaceName(entry.interfaceId)} style={{ fontSize: 12, color: "#64748b", fontFamily: "monospace" }} />
            </DataTable>
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
          <DataTable value={ports} dataKey="id" loading={portsLoading} size="small" emptyMessage={error ? "—" : "Nenhuma porta cadastrada"} tableStyle={{ width: "100%" }}>
            <Column header="Nome" field="name" style={{ fontWeight: 500, fontSize: 12, fontFamily: "monospace" }} />
            <Column header="Descrição" body={(port: SwitchPortItem) => port.description ?? "—"} style={{ fontSize: 12, color: "#64748b" }} />
            <Column header="Link" body={(port: SwitchPortItem) => <StatusChip label={port.linkUp === true ? "Up" : port.linkUp === false ? "Down" : "—"} background={port.linkUp === true ? "#f0fdf4" : port.linkUp === false ? "#fef2f2" : "#f1f5f9"} color={port.linkUp === true ? "#15803d" : port.linkUp === false ? "#dc2626" : "#64748b"} fontWeight={600} fontSize={10} />} style={{ width: 90 }} />
          </DataTable>
        </div>
      )}
    </div>
  );
}
