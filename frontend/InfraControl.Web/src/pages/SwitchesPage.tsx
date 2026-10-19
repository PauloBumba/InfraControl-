import { useEffect, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { formatPortDuplex, formatPortSpeed, formatUptime } from "@/utils/format";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { SelectField } from "@/components/ui/SelectField";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { StatusChip } from "@/components/ui/StatusChip";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import {
  getDeviceSystemInfo,
  listLocations,
  listSwitchPorts,
  type DeviceItem,
  type DeviceSystemInfoItem,
  type LocationItem,
  type SwitchPortItem,
} from "@/services/api/inventory";
import { listSwitches, getSwitchUnifiPorts, getSwitchZabbixInterfaces, syncSwitchPortsFromUnifi, type ZabbixInterfaceItem } from "@/services/api/network";
import { setUnifiPortConfig } from "@/services/api/unifi";
import type { UniFiSwitchPort } from "@/services/api/unifi";
import { getDeviceResults, type DeviceResults } from "@/services/api/monitoring";
import {
  useListQuery,
  usePagedQuery,
  useObjectQuery,
  useCreateMutation,
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

/**
 * F30-T001 S5c — métrica do SnmpHealth (`values` chega como texto do
 * backend: "12", "41"). Ausente ⇒ "—" — nunca 0 inventado.
 */
function snmpHealthMetric(
  values: Record<string, string> | null,
  key: string,
  suffix: string,
): string {
  const raw = values?.[key];
  return raw ? `${raw}${suffix}` : "—";
}

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

  // Fase 07a — portas UniFi ao vivo (port_table) quando o device tem
  // referência UniFi. Lista vazia = sem referência (não é erro).
  const unifiPorts = useObjectQuery<UniFiSwitchPort[]>(
    ["switchUnifiPorts", selectedId ?? ""],
    () => getSwitchUnifiPorts(selectedId as string),
    { enabled: selectedId !== null },
  );

  // Fase 07b — interfaces SNMP monitoradas pelo Zabbix. Lista vazia =
  // sem referência Zabbix ou Zabbix não configurado (não é erro).
  const zabbixIfaces = useObjectQuery<ZabbixInterfaceItem[]>(
    ["switchZabbixInterfaces", selectedId ?? ""],
    () => getSwitchZabbixInterfaces(selectedId as string),
    { enabled: selectedId !== null },
  );

  // F30-T001 S5b — uptime + sysDescr (S3). Erro esconde a faixa: device sem
  // system-info não polui a tela do switch. Envelope null (defensivo) vira
  // "sem leitura" — mesmo resultado visual, nada inventado.
  const systemInfo = useObjectQuery<DeviceSystemInfoItem>(
    ["deviceSystemInfo", selectedId ?? ""],
    async () =>
      (await getDeviceSystemInfo(selectedId as string)) ?? {
        uptimeSeconds: null,
        sysDescr: null,
        capturedAtUtc: null,
      },
    { enabled: selectedId !== null },
  );

  // F30-T001 S5c — saúde SNMP (S1) na tela do switch. O endpoint não filtra
  // por tipo: pede os últimos 100 polls (SnmpHealth pode ficar no meio de
  // muitos Pings) e o mais recente do tipo já vem primeiro na lista.
  const healthResults = useObjectQuery<DeviceResults>(
    ["switchSnmpHealth", selectedId ?? ""],
    async () =>
      (await getDeviceResults(selectedId as string, 100)) ?? {
        deviceId: "",
        deviceName: "",
        results: [],
      },
    { enabled: selectedId !== null },
  );
  const snmpHealth =
    healthResults.data?.results.find((result) => result.check === "SnmpHealth") ?? null;

  // Fase 07c — sync de portas UniFi → cadastro (RoleAccess.Operate).
  const syncMutation = useCreateMutation({
    invalidate: ["switchPorts"],
    mutationFn: (switchId: string) => syncSwitchPortsFromUnifi(switchId),
    onSuccess: (data) => {
      if (data) {
        setSyncMessage(
          `${data.created} criadas, ${data.updated} atualizadas (${data.totalFromController} no controller)`,
        );
        void ports.refetch();
        void unifiPorts.refetch();
      }
    },
    onError: (error) => {
      setSyncMessage(mutationErrorMessage(error));
    },
  });
  const [syncMessage, setSyncMessage] = useState<string | null>(null);

  // Fase 07e (ADR-051): gravação de configuração de porta (enable/VLAN).
  const portConfigMutation = useCreateMutation({
    invalidate: ["switchUnifiPorts"],
    mutationFn: (vars: { switchId: string; portIndex: number; body: { disabled?: boolean | null; portVlan?: number | null } }) =>
      setUnifiPortConfig(vars.switchId, vars.portIndex, vars.body),
  });
  const [portConfigTarget, setPortConfigTarget] = useState<UniFiSwitchPort | null>(null);
  const [portVlanInput, setPortVlanInput] = useState("");
  const [portConfigMessage, setPortConfigMessage] = useState<string | null>(null);

  const locations = locationsQuery.items;
  const locationName = (id: string): string =>
    locations.find((entry) => entry.id === id)?.name ?? id;

  const switchesTotal = switches.totalCount;
  const switchesError = mutationErrorMessage(switches.error as Error | null);
  const portsError = mutationErrorMessage(ports.error as Error | null);

  const selectedSwitch = switches.items.find((entry) => entry.id === selectedId) ?? null;
  const upCount = ports.items.filter((port) => port.linkUp === true).length;
  const downCount = ports.items.filter((port) => port.linkUp === false).length;

  // Fase 07a/07b — fonte primária das portas: UniFi ao vivo quando
  // existe referência; senão, interfaces Zabbix; senão, cadastro manual.
  const livePorts = unifiPorts.data ?? [];
  const hasLivePorts = livePorts.length > 0;
  const liveUp = livePorts.filter((p) => p.linkUp === true).length;
  const liveDown = livePorts.filter((p) => p.linkUp === false).length;

  const zabbixIfs = zabbixIfaces.data ?? [];
  const hasZabbixIfs = !hasLivePorts && zabbixIfs.length > 0;

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
    <ListPageShell
      title="Switches & Portas"
      // A produção diz "1 switches" (gramática errada, pré-existente). Corrigir
      // para "switch" muda texto visível — decisão humana, fora do C-3.
      // Sem `loading` de propósito: o subtítulo original nunca teve esse ramo.
      singular="switches"
      plural="switches"
      total={switchesTotal}
      error={switchesError}
      onRetry={() => void switches.refetch()}
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
    >

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
          <h2>
            Portas — {selectedSwitch?.name ?? "—"}
            {hasLivePorts && (
              <span
                style={{
                  marginLeft: 8,
                  fontSize: 10,
                  fontWeight: 700,
                  padding: "2px 6px",
                  borderRadius: 4,
                  background: "#dbeafe",
                  color: "#1d4ed8",
                  verticalAlign: "middle",
                }}
              >
                UniFi ao vivo
              </span>
            )}
            {hasZabbixIfs && (
              <span
                style={{
                  marginLeft: 8,
                  fontSize: 10,
                  fontWeight: 700,
                  padding: "2px 6px",
                  borderRadius: 4,
                  background: "#fef3c7",
                  color: "#b45309",
                  verticalAlign: "middle",
                }}
              >
                Zabbix SNMP
              </span>
            )}
          </h2>
          {!ports.isPending && !portsError && !hasLivePorts && !hasZabbixIfs && (
            <span style={{ fontSize: 12, color: "#64748b" }}>
              {ports.totalCount} portas · {upCount} up · {downCount} down
            </span>
          )}
          {hasLivePorts && (
            <span style={{ fontSize: 12, color: "#64748b" }}>
              {livePorts.length} portas · {liveUp} up · {liveDown} down
            </span>
          )}
          {hasZabbixIfs && (
            <span style={{ fontSize: 12, color: "#64748b" }}>
              {zabbixIfs.length} interfaces monitoradas
            </span>
          )}
          {selectedId && (
            <Button
              variant="ghost"
              disabled={syncMutation.isPending}
              onClick={() => {
                setSyncMessage(null);
                syncMutation.mutate(selectedId);
              }}
              style={{ marginLeft: "auto" }}
            >
              {syncMutation.isPending ? "Sincronizando…" : "Sync UniFi"}
            </Button>
          )}
        </div>

        {/* F30-T001 S5b — identificação do sistema vinda do histórico SNMP
            (S3): uptime formatado + sysDescr cru. "—" onde nunca houve
            leitura; a faixa some se o device não responder. */}
        {!systemInfo.isPending && !systemInfo.isError && (
          <div
            style={{
              padding: "6px 16px",
              fontSize: 12,
              color: "#475569",
              borderBottom: "1px solid #e5e7eb",
              display: "flex",
              gap: 16,
              flexWrap: "wrap",
            }}
          >
            <span style={{ fontWeight: 600 }}>
              {`Uptime: ${formatUptime(systemInfo.data?.uptimeSeconds ?? null)}`}
            </span>
            <span style={{ fontWeight: 600, overflowWrap: "anywhere" }}>
              {`Sistema: ${systemInfo.data?.sysDescr ?? "—"}`}
            </span>
            {/* F30-T001 S5c — último SnmpHealth (S1). Sem resultado do tipo
                ⇒ "Saúde: —"; resultado falho ⇒ "—" por métrica. */}
            {!healthResults.isPending && !healthResults.isError && (
              <span style={{ fontWeight: 600 }}>
                {snmpHealth
                  ? `Saúde: CPU ${snmpHealthMetric(snmpHealth.values, "cpuPercent", "%")} · Memória ${snmpHealthMetric(snmpHealth.values, "memoryPercent", "%")} · Temperatura ${snmpHealthMetric(snmpHealth.values, "temperatureCelsius", "°C")}`
                  : "Saúde: —"}
              </span>
            )}
          </div>
        )}

        {syncMessage && (
          <div
            style={{
              padding: "6px 16px",
              fontSize: 12,
              background: syncMutation.isError ? "#fef2f2" : "#f0fdf4",
              color: syncMutation.isError ? "#dc2626" : "#16a34a",
              borderBottom: "1px solid #e5e7eb",
            }}
          >
            {syncMessage}
          </div>
        )}

        {portsError && !hasLivePorts && !hasZabbixIfs && (
          <ErrorBanner
            message={portsError}
            onRetry={() => void ports.refetch()}
            style={{ padding: "10px 16px", borderRadius: 0 }}
          />
        )}

        {(ports.isFetching || unifiPorts.isFetching || zabbixIfaces.isFetching) && !hasLivePorts && !hasZabbixIfs && ports.items.length === 0 && (
          <LoadingBlock />
        )}

        {!unifiPorts.isFetching && !zabbixIfaces.isFetching && (hasLivePorts || hasZabbixIfs || (!ports.isFetching && !portsError)) && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(90px, 1fr))", gap: 8, padding: 16 }}>
            {hasLivePorts
              ? livePorts.map((port) => {
                  const up = port.linkUp === true;
                  const down = port.linkUp === false;
                  return (
                    <div
                      key={port.portIndex}
                      style={{
                        padding: "10px 6px",
                        borderRadius: 6,
                        border: `2px solid ${up ? "#22c55e" : down ? "#ef4444" : "#e5e7eb"}`,
                        background: up ? "#f0fdf4" : down ? "#fef2f2" : "#f9fafb",
                        textAlign: "center",
                        fontSize: 10,
                      }}
                      title={
                        [
                          port.name,
                          port.speed ?? null,
                          port.portVlan != null ? `VLAN ${port.portVlan}` : null,
                          port.poeStatus ? `PoE: ${port.poeStatus}` : null,
                          port.clientMac ? `Cliente: ${port.clientMac}` : null,
                        ]
                          .filter(Boolean)
                          .join(" · ") || port.name
                      }
                    >
                      <div style={{ fontSize: 15, fontWeight: 700 }}>{port.name}</div>
                      <div style={{ fontSize: 8, fontWeight: 700, color: up ? "#16a34a" : down ? "#dc2626" : "#94a3b8" }}>
                        {up ? "Up" : down ? "Down" : "—"}
                      </div>
                      {port.speed && (
                        <div style={{ fontSize: 8, color: "#94a3b8", marginTop: 2 }}>{port.speed}</div>
                      )}
                      {port.poeStatus && port.poeStatus !== "none" && (
                        <div
                          style={{
                            fontSize: 7,
                            fontWeight: 700,
                            marginTop: 2,
                            color: port.poeStatus === "delivering" ? "#16a34a" : "#f59e0b",
                          }}
                        >
                          PoE {port.poeStatus === "delivering" ? "✓" : port.poeStatus}
                        </div>
                      )}
                    </div>
                  );
                })
              : hasZabbixIfs
                ? zabbixIfs.map((iface) => (
                    <div
                      key={iface.itemId}
                      style={{
                        padding: "10px 6px",
                        borderRadius: 6,
                        border: `2px solid ${iface.enabled && !iface.unsupported ? "#e5e7eb" : "#fde68a"}`,
                        background: iface.enabled && !iface.unsupported ? "#f9fafb" : "#fffbeb",
                        textAlign: "center",
                        fontSize: 10,
                      }}
                      title={`${iface.name}\n${iface.key}`}
                    >
                      <div style={{ fontSize: 12, fontWeight: 700, wordBreak: "break-all" }}>
                        {iface.interfaceName}
                      </div>
                      <div
                        style={{
                          fontSize: 7,
                          fontWeight: 700,
                          marginTop: 2,
                          color: iface.unsupported ? "#d97706" : iface.enabled ? "#64748b" : "#94a3b8",
                        }}
                      >
                        {iface.unsupported ? "Unsupported" : iface.enabled ? "Monitorada" : "Desabilitada"}
                      </div>
                    </div>
                  ))
                : ports.items.map((port) => {
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
            {hasLivePorts && livePorts.length === 0 && ports.items.length === 0 && (
              <div style={{ gridColumn: "1 / -1", padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
                Nenhuma porta disponível
              </div>
            )}
            {!hasLivePorts && !hasZabbixIfs && ports.items.length === 0 && !ports.isFetching && (
              <div style={{ gridColumn: "1 / -1", padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
                Nenhuma porta cadastrada neste switch
              </div>
            )}
          </div>
        )}
      </div>

      {/* Detalhe — UniFi ao vivo > Zabbix SNMP > cadastro manual */}
      {hasLivePorts ? (
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Detalhe de portas (UniFi)</h2>
          </div>
          <DataTablePanel
            panel={false}
            value={livePorts}
            dataKey="portIndex"
            emptyMessage="—"
          >
            <Column
              header="Porta"
              body={(port: UniFiSwitchPort) => port.name}
              style={{ fontFamily: "monospace", fontWeight: 600, fontSize: 12, width: 100 }}
            />
            <Column header="Idx" field="portIndex" style={{ width: 50 }} />
            <Column header="Link" body={(port: UniFiSwitchPort) => (
              <StatusChip
                label={port.linkUp === true ? "Up" : port.linkUp === false ? "Down" : "—"}
                background={port.linkUp === true ? "#dcfce7" : port.linkUp === false ? "#fee2e2" : "#f1f5f9"}
                color={port.linkUp === true ? "#16a34a" : port.linkUp === false ? "#dc2626" : "#64748b"}
              />
            )} style={{ width: 80 }} />
            <Column header="Velocidade" body={(port: UniFiSwitchPort) => port.speed ?? "—"} style={{ width: 90 }} />
            <Column header="VLAN" body={(port: UniFiSwitchPort) => port.portVlan ?? "—"} style={{ width: 70 }} />
            <Column header="PoE" body={(port: UniFiSwitchPort) => {
              if (port.poeEnable == null) return "—";
              if (!port.poeEnable) return "off";
              return (
                <StatusChip
                  label={port.poeStatus ?? "on"}
                  background={port.poeStatus === "delivering" ? "#dcfce7" : "#fef3c7"}
                  color={port.poeStatus === "delivering" ? "#16a34a" : "#d97706"}
                />
              );
            }} style={{ width: 90 }} />
            <Column header="Mídia" body={(port: UniFiSwitchPort) => port.media ?? "—"} style={{ width: 100 }} />
            <Column header="Cliente" body={(port: UniFiSwitchPort) => (
              port.clientMac ? (
                <span style={{ fontFamily: "monospace", fontSize: 11 }}>{port.clientMac}</span>
              ) : "—"
            )} style={{ minWidth: 140 }} />
            <Column header="Ações" body={(port: UniFiSwitchPort) => (
              <div style={{ display: "flex", gap: 4 }}>
                <Button
                  variant="ghost"
                  disabled={portConfigMutation.isPending}
                  onClick={() => {
                    setPortConfigMessage(null);
                    portConfigMutation.mutate(
                      {
                        switchId: selectedId!,
                        portIndex: port.portIndex,
                        // ADR-059 §3 (H6): escrita pelo estado ADMINISTRATIVO
                        // (`enabled`); `linkUp` é telemetria — antes
                        // `disabled: !linkUp` DESABILITAVA porta com link
                        // null ao clicar "Habilitar".
                        body: { disabled: !port.enabled },
                      },
                      {
                        onSuccess: (data) => {
                          setPortConfigMessage(data?.message ?? "OK");
                          void unifiPorts.refetch();
                        },
                        onError: (error) => {
                          setPortConfigMessage(mutationErrorMessage(error));
                        },
                      },
                    );
                  }}
                  style={{ fontSize: 10, padding: "2px 6px" }}
                >
                  {port.enabled ? "Desabilitar" : "Habilitar"}
                </Button>
                <Button
                  variant="ghost"
                  disabled={portConfigMutation.isPending}
                  onClick={() => {
                    setPortConfigTarget(port);
                    setPortVlanInput(port.portVlan?.toString() ?? "");
                    setPortConfigMessage(null);
                  }}
                  style={{ fontSize: 10, padding: "2px 6px" }}
                >
                  VLAN
                </Button>
              </div>
            )} style={{ width: 160 }} />
          </DataTablePanel>

          {portConfigMessage && (
            <div
              style={{
                padding: "6px 16px",
                fontSize: 12,
                background: portConfigMutation.isError ? "#fef2f2" : "#f0fdf4",
                color: portConfigMutation.isError ? "#dc2626" : "#16a34a",
                borderTop: "1px solid #e5e7eb",
              }}
            >
              {portConfigMessage}
            </div>
          )}

          {portConfigTarget && (
            <div
              style={{
                padding: 16,
                borderTop: "1px solid #e5e7eb",
                display: "flex",
                alignItems: "center",
                gap: 8,
              }}
            >
              <span style={{ fontSize: 13, fontWeight: 600 }}>
                VLAN na porta {portConfigTarget.name} (índice {portConfigTarget.portIndex}):
              </span>
              <input
                type="number"
                value={portVlanInput}
                onChange={(e) => setPortVlanInput(e.target.value)}
                placeholder="ex: 10, 20, 99"
                style={{
                  width: 100,
                  padding: "4px 8px",
                  border: "1px solid #d1d5db",
                  borderRadius: 4,
                  fontSize: 13,
                }}
              />
              <Button
                variant="primary"
                disabled={portConfigMutation.isPending || !portVlanInput}
                onClick={() => {
                  const vlan = parseInt(portVlanInput, 10);
                  if (Number.isNaN(vlan)) return;
                  portConfigMutation.mutate(
                    {
                      switchId: selectedId!,
                      portIndex: portConfigTarget.portIndex,
                      body: { portVlan: vlan },
                    },
                    {
                      onSuccess: () => {
                        setPortConfigTarget(null);
                        setPortConfigMessage(`VLAN ${vlan} aplicada na porta ${portConfigTarget.name}.`);
                        void unifiPorts.refetch();
                      },
                      onError: (error) => {
                        setPortConfigMessage(mutationErrorMessage(error));
                      },
                    },
                  );
                }}
                style={{ fontSize: 12, padding: "4px 12px" }}
              >
                {portConfigMutation.isPending ? "Aplicando…" : "Aplicar VLAN"}
              </Button>
              <Button
                variant="ghost"
                onClick={() => setPortConfigTarget(null)}
                style={{ fontSize: 12, padding: "4px 12px" }}
              >
                Cancelar
              </Button>
            </div>
          )}
        </div>
      ) : hasZabbixIfs ? (
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Interfaces monitoradas (Zabbix SNMP)</h2>
          </div>
          <DataTablePanel
            panel={false}
            value={zabbixIfs}
            dataKey="itemId"
            emptyMessage="—"
          >
            <Column
              header="Interface"
              body={(iface: ZabbixInterfaceItem) => iface.interfaceName}
              style={{ fontFamily: "monospace", fontWeight: 600, fontSize: 12, minWidth: 140 }}
            />
            <Column header="Item Zabbix" body={(iface: ZabbixInterfaceItem) => iface.name} style={{ fontSize: 13, color: "#64748b", minWidth: 180 }} />
            <Column header="Key" body={(iface: ZabbixInterfaceItem) => (
              <span style={{ fontFamily: "monospace", fontSize: 11 }}>{iface.key}</span>
            )} style={{ minWidth: 200 }} />
            <Column header="Estado" body={(iface: ZabbixInterfaceItem) => (
              <StatusChip
                label={iface.unsupported ? "Unsupported" : iface.enabled ? "Monitorada" : "Desabilitada"}
                background={iface.unsupported ? "#fef3c7" : iface.enabled ? "#dcfce7" : "#f1f5f9"}
                color={iface.unsupported ? "#d97706" : iface.enabled ? "#16a34a" : "#64748b"}
              />
            )} style={{ width: 120 }} />
          </DataTablePanel>
        </div>
      ) : (
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
            {/* F30-T001 S5a — cadastro semi-estático observado (S4): a coluna
                diz "—" enquanto o device não responder; nunca 0/inventado. */}
            <Column header="Velocidade" body={(port: SwitchPortItem) => (
              <span style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }}>
                {formatPortSpeed(port.speed)}
              </span>
            )} style={{ width: 100 }} />
            <Column header="Duplex" body={(port: SwitchPortItem) => (
              <span style={{ fontSize: 13, color: "#64748b" }}>
                {formatPortDuplex(port.duplex)}
              </span>
            )} style={{ width: 90 }} />
          </DataTablePanel>
        </div>
      )}
    </ListPageShell>
  );
}
