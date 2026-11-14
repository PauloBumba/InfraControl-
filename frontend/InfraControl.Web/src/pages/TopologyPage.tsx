import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import {
  getDeviceSystemInfo,
  listDevices,
  listLocations,
  listSwitchPorts,
  type DeviceItem,
  type DeviceSystemInfoItem,
  type SwitchPortItem,
} from "@/services/api/inventory";
import {
  getTopology,
  listVlans,
  type TopologyGraph,
  type TopologyNodeItem,
  type VlanItem,
} from "@/services/api/network";
import { getDeviceResults } from "@/services/api/monitoring";
import { listAlerts, type AlertItem } from "@/services/api/alerts";
import { getZabbixHostItems, getZabbixHosts, type ZabbixHostItem, type ZabbixHostView } from "@/services/api/zabbix";
import { getUniFiCorrelation, type UniFiCorrelation } from "@/services/api/unifi";
import { errorMessage } from "@/hooks/queries";
import { formatDateTime, formatPortDuplex, formatPortSpeed, formatUptime } from "@/utils/format";

/**
 * F17-T021 — Topologia de Rede conectada ao backend (substitui
 * `topologyNodes`/`topologyEdges`/`deviceSummary`/`rackLayout`/
 * `storageConnections`/`vlanBadges` de `data/topologyData`).
 *
 * Fontes reais (4 leituras em paralelo):
 * - `GET /network/topology` (F3-T006) — nodes = **interfaces** (1 device
 *   pode ter N), edges = Connections dirigidas; `status` = último Ping do
 *   device dono (`"up"`/`"down"`/`null`); **sem coordenadas x/y** → o
 *   layout do mapa é calculado no cliente em **faixas por `layer`**
 *   (F53-T003 — hierarquia BFS do backend, devices ordenados por tipo,
 *   interfaces em satélite ao redor do device);
 * - `GET /inventory/devices` — junção para o "Local" do tooltip
 *   (device.locationId → `GET /inventory/locations`);
 * - `GET /network/vlans` (serviço do T008) — lista lateral.
 *
 * Divergências do protótipo: tipos `wan`/`gateway`/`vm`/`nas`/`client`
 * (não existem no enum `DeviceType` — Router/Firewall são o análogo);
 * status "Atenção"/"warning" (não existe — só up/down/nunca verificado);
 * arestas tipadas (backbone/PoE/WiFi/virtual — `TopologyEdgeDto` não tem
 * tipo, cor nem label; todas são traços neutros); painel "Storage"
 * (sem contrato); badges de VLAN por device (VLAN↔porta exige
 * `GET /network/switch-ports/{id}/vlans` = 1 requisição POR PORTA — a
 * sidebar mostra só o inventário de VLANs, sem contagem de devices);
 * **vista por Rack removida** (o DTO não tem posição em U nem rackId —
 * racks e contagens vivem na LocationsPage, T018). Sem mutations.
 *
 * F53-T003 — layout hierárquico: faixas por `layer` (Camada 1 no topo até
 * "Sem hierarquia" embaixo) no lugar da grade fixa de 7 colunas; pan por
 * arrasto (cliques pós-arrasto são suprimidos); "Centralizar" reseta
 * pan+zoom; toggle por faixa (`aria-expanded` na barra do mapa) recolhe
 * o grupo — some da altura do SVG e esconde devices/arestas/ghosts da
 * camada.
 *
 * F53-T004 — busca (nome/IP) e filtros combináveis (categoria ∧ local ∧
 * VLAN ∧ só problemas): `filteredDeviceIds` dimensiona as faixas e
 * `visibleDeviceIds` (filtrados ∧ camada aberta) o que desenha; VLAN do
 * device = união das VLANs nas pontas das Connections dele; destaque do
 * selecionado: conexões com traço forte, vizinhos com anel `#93c5fd`,
 * resto esmaecido.
 *
 * F53-T005 — painel de detalhes: ficha do equipamento (11.5) com dado
 * real do inventário + `system-info` (uptime) + último poll (última
 * comunicação) + host/métricas Zabbix + vínculo UniFi por MAC + alertas
 * (eventos) e, em switch, portas com o que está conectado (match da
 * interface nas duas pontas das Connections); conexão (11.3) com portas,
 * velocidade/duplex, VLANs, estado da porta (`linkUp`), origem e
 * confiança — onde o contrato não tem dado (tipo da conexão) a tela
 * mostra "—" (nunca inventado). Falha de integração degrada só a linha.
 */

const typeMeta: Record<string, { color: string; icon: string; emoji: string }> = {
  Router: { color: "#3b82f6", icon: "pi pi-compass", emoji: "🌐" },
  Firewall: { color: "#ef4444", icon: "pi pi-shield", emoji: "🛡️" },
  Switch: { color: "#8b5cf6", icon: "pi pi-clone", emoji: "🔌" },
  AccessPoint: { color: "#22c55e", icon: "pi pi-wifi", emoji: "📡" },
  Server: { color: "#3b82f6", icon: "pi pi-server", emoji: "🖥️" },
  Workstation: { color: "#06b6d4", icon: "pi pi-desktop", emoji: "💻" },
  Printer: { color: "#f59e0b", icon: "pi pi-print", emoji: "🖨️" },
  Camera: { color: "#ec4899", icon: "pi pi-video", emoji: "📷" },
  Phone: { color: "#06b6d4", icon: "pi pi-phone", emoji: "📞" },
  UPS: { color: "#f59e0b", icon: "pi pi-bolt", emoji: "🔋" },
  Other: { color: "#94a3b8", icon: "pi pi-question", emoji: "❓" },
};

const typeOrder = [
  "Router", "Firewall", "Switch", "AccessPoint", "Server", "Workstation",
  "Printer", "Camera", "Phone", "UPS", "Other",
];

const statusColor = (status: "up" | "down" | null): string =>
  status === "up" ? "#22c55e" : status === "down" ? "#ef4444" : "#cbd5e1";

const statusLabel = (status: "up" | "down" | null): string =>
  status === "up" ? "Up" : status === "down" ? "Down" : "Nunca verificado";

/** Chave da faixa de camada (F53-T003): "1"/"2"/… ou "none" p/ null. */
const layerKeyOf = (layer: number | null): string =>
  layer === null ? "none" : String(layer);

/**
 * F53-T005 — rótulos de exibição do painel da conexão. Wire minúsculo do
 * backend (`ToWire`); valor fora do conjunto conhecido aparece cru
 * (nunca traduzido para algo que o backend não disse).
 */
const sourceLabel = (value: string | null | undefined): string => {
  if (!value) return "—";
  const labels: Record<string, string> = {
    manual: "Manual", lldp: "LLDP", cdp: "CDP", unifi: "UniFi",
  };
  return labels[value] ?? value;
};

const confidenceLabel = (value: string | null | undefined): string => {
  if (!value) return "—";
  const labels: Record<string, string> = {
    confirmed: "Confirmada", estimated: "Estimada",
  };
  return labels[value] ?? value;
};

/** F53-T005 — estado do vínculo UniFi por MAC (correlação do controller). */
const unifiLabel = (correlation: UniFiCorrelation | null, mac: string | null | undefined): string => {
  if (!correlation || !mac) return "—";
  const needle = mac.toLowerCase();
  const matched = correlation.matched.find((link) => link.mac.toLowerCase() === needle);
  if (matched) {
    return `Vinculado (${matched.source})${matched.controllerPresent ? " · controller acessível" : " · controller inacessível"}`;
  }
  if (correlation.conflicts.some((link) => link.mac.toLowerCase() === needle)) {
    return "Conflito de vínculo";
  }
  if (correlation.unlinked.some((entry) => entry.mac.toLowerCase() === needle)) {
    return "No controller · sem vínculo";
  }
  return "Sem vínculo UniFi";
};

/** F53-T005 — porta casada por nome (mesmo critério do backend: minúsculas). */
const matchPort = (
  ports: SwitchPortItem[] | null | undefined,
  interfaceName: string | null | undefined,
): SwitchPortItem | null => {
  if (!ports || !interfaceName) return null;
  const needle = interfaceName.toLowerCase();
  return ports.find((port) => port.name.toLowerCase() === needle) ?? null;
};

/** F53-T005 — bundle do painel do device (fetch na seleção; null = carregando). */
interface DeviceDetail {
  systemInfo: DeviceSystemInfoItem | null;
  lastPollAt: string | null;
  alerts: AlertItem[];
  alertsTotal: number;
  zabbixHost: ZabbixHostView | null;
  zabbixItems: ZabbixHostItem[];
  unifi: UniFiCorrelation | null;
  ports: SwitchPortItem[] | null;
}

/** Device agrupado a partir dos nodes (interfaces) do grafo. */
interface DeviceGroup {
  deviceId: string;
  deviceName: string;
  deviceType: string;
  ip: string | null;
  status: "up" | "down" | null;
  locationId: string | null;
  /** Camada herdada dos nodes (F53-T002); null = fora da hierarquia. */
  layer: number | null;
  nodes: TopologyNodeItem[];
}

function CategoryCard({ label, count, icon, color, onClick, active }: { label: string; count: number; icon: string; color: string; onClick: () => void; active: boolean }) {
  return (
    <div onClick={onClick} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 12px", borderRadius: 8, border: `2px solid ${active ? color : "#e5e7eb"}`, background: active ? `${color}10` : "white", cursor: "pointer", transition: "all 0.12s" }}>
      <div style={{ width: 28, height: 28, borderRadius: 6, background: `${color}15`, color, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 12 }}>
        <i className={icon} />
      </div>
      <div>
        <div style={{ fontSize: 14, fontWeight: 700 }}>{count}</div>
        <div style={{ fontSize: 9, color: "#9ca3af" }}>{label}</div>
      </div>
    </div>
  );
}

export function TopologyPage() {
  const navigate = useNavigate();
  const [graph, setGraph] = useState<TopologyGraph | null>(null);
  const [deviceItems, setDeviceItems] = useState<DeviceItem[]>([]);
  const [locations, setLocations] = useState<Record<string, string>>({});
  const [vlans, setVlans] = useState<VlanItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [filter, setFilter] = useState<string>("all");
  const [zoom, setZoom] = useState(100);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [collapsedLayers, setCollapsedLayers] = useState<Set<string>>(() => new Set());
  const [dragging, setDragging] = useState(false);
  const [search, setSearch] = useState("");
  const [locationFilter, setLocationFilter] = useState("all");
  const [vlanFilter, setVlanFilter] = useState("all");
  const [problemsOnly, setProblemsOnly] = useState(false);
  const [hoveredId, setHoveredId] = useState<string | null>(null);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  // F53-T005 — bundle do painel de detalhes (device ou conexão selecionada).
  const [deviceDetail, setDeviceDetail] = useState<DeviceDetail | null>(null);
  const [deviceDetailError, setDeviceDetailError] = useState<string | null>(null);
  const [edgePorts, setEdgePorts] = useState<{ source: SwitchPortItem[] | null; target: SwitchPortItem[] | null } | null>(null);
  const dragRef = useRef<{ startX: number; startY: number; panX: number; panY: number } | null>(null);
  const didDragRef = useRef(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [topology, devices, locationPage, vlanPage] = await Promise.all([
        getTopology(),
        listDevices(),
        listLocations(),
        listVlans(),
      ]);
      setGraph(topology ?? { nodes: [], edges: [] });
      setDeviceItems(devices.items);
      const map: Record<string, string> = {};
      locationPage.items.forEach((entry) => { map[entry.id] = entry.name; });
      setLocations(map);
      setVlans(vlanPage.items);
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  /** Agrupa as interfaces por device e ordena por tipo (tipo → nome). */
  const groups = useMemo<DeviceGroup[]>(() => {
    if (!graph) return [];
    const byDevice = new Map<string, DeviceGroup>();
    graph.nodes.forEach((node) => {
      // F29-5: nó ghost (vizinho não gerenciado) fica FORA do cluster do
      // device — é renderizado à parte, tracejado, para não poluir contagens.
      if (node.isGhost) return;
      const existing = byDevice.get(node.deviceId);
      if (existing) {
        existing.nodes.push(node);
        if (existing.layer === null && node.layer != null) existing.layer = node.layer;
      } else {
        const device = deviceItems.find((entry) => entry.id === node.deviceId);
        byDevice.set(node.deviceId, {
          deviceId: node.deviceId,
          deviceName: node.deviceName,
          deviceType: node.deviceType,
          ip: node.ip,
          status: null,
          locationId: device?.locationId ?? null,
          layer: node.layer ?? null,
          nodes: [node],
        });
      }
    });
    byDevice.forEach((group) => {
      const statuses = group.nodes.map((node) => node.status);
      group.status = statuses.includes("up")
        ? "up"
        : statuses.includes("down")
          ? "down"
          : null;
    });
    const orderIndex = (type: string): number => {
      const index = typeOrder.indexOf(type);
      return index === -1 ? typeOrder.length : index;
    };
    return [...byDevice.values()].sort(
      (a, b) =>
        orderIndex(a.deviceType) - orderIndex(b.deviceType) ||
        a.deviceName.localeCompare(b.deviceName),
    );
  }, [graph, deviceItems]);

  /** Interface → device, direto dos nodes (sem depender do layout). */
  const deviceByInterface = useMemo(
    () => new Map(graph?.nodes.map((node) => [node.id, node.deviceId]) ?? []),
    [graph],
  );

  /**
   * F53-T004 — VLANs por device: união das VLANs nas pontas das
   * Connections dele (sourceVlans p/ ponta origem, targetVlans p/ ponta
   * destino — aresta ghost não tem ponta gerenciada no destino). Device
   * sem Connection não tem VLAN no grafo → fica de fora do filtro.
   */
  const vlanByDevice = useMemo(() => {
    const map = new Map<string, Set<number>>();
    const addVlans = (
      deviceId: string | undefined,
      vlans: { number: number }[] | null | undefined,
    ) => {
      if (!deviceId || !vlans?.length) return;
      const set = map.get(deviceId) ?? new Set<number>();
      vlans.forEach((vlan) => set.add(vlan.number));
      map.set(deviceId, set);
    };
    graph?.edges.forEach((edge) => {
      addVlans(deviceByInterface.get(edge.sourceInterfaceId), edge.sourceVlans);
      if (!edge.isGhost) {
        addVlans(deviceByInterface.get(edge.targetInterfaceId), edge.targetVlans);
      }
    });
    return map;
  }, [graph, deviceByInterface]);

  /**
   * F53-T004 — devices que passam nos FILTROS (categoria ∧ local ∧ VLAN
   * ∧ só problemas ∧ busca). Dimensiona quais faixas existem no layout —
   * colapso não entra aqui (recolher esconde o corpo, não a faixa).
   */
  const filteredDeviceIds = useMemo(() => {
    const query = search.trim().toLowerCase();
    const matches = (group: DeviceGroup) => {
      if (filter !== "all" && group.deviceType !== filter) return false;
      if (locationFilter !== "all" && group.locationId !== locationFilter) return false;
      if (problemsOnly && group.status !== "down") return false;
      if (
        query &&
        !group.deviceName.toLowerCase().includes(query) &&
        !(group.ip ?? "").toLowerCase().includes(query)
      ) {
        return false;
      }
      if (
        vlanFilter !== "all" &&
        !vlanByDevice.get(group.deviceId)?.has(Number(vlanFilter))
      ) {
        return false;
      }
      return true;
    };
    return new Set(groups.filter(matches).map((group) => group.deviceId));
  }, [groups, filter, search, locationFilter, vlanFilter, problemsOnly, vlanByDevice]);

  /** F53-T003/T004 — filtrados ∧ camada não recolhida: o que desenha. */
  const visibleDeviceIds = useMemo(
    () =>
      new Set(
        groups
          .filter(
            (group) =>
              filteredDeviceIds.has(group.deviceId) &&
              !collapsedLayers.has(layerKeyOf(group.layer)),
          )
          .map((group) => group.deviceId),
      ),
    [groups, filteredDeviceIds, collapsedLayers],
  );

  /**
   * F53-T003 — faixas por camada no lugar da grade fixa de 7 colunas:
   * Camada 1 no topo até "Sem hierarquia" embaixo; dentro da faixa os
   * devices seguem a ordem tipo → nome em colunas, com interfaces em
   * satélite. Faixa recolhida ocupa só o cabeçalho. F53-T004: faixas
   * seguem os filtros (a recolhida mantém a faixa com o toggle).
   */
  const layout = useMemo(() => {
    const cols = 7;
    const slotW = 148;
    const slotH = 116;
    const padX = 74;
    const headerH = 30;
    const nodePos = new Map<string, { x: number; y: number }>();
    const nodeDevice = new Map<string, string>();
    const slots: { group: DeviceGroup; cx: number; cy: number }[] = [];
    const bands: { key: string; label: string; title: string; headerY: number; collapsed: boolean }[] = [];

    const filteredGroups = groups.filter((group) => filteredDeviceIds.has(group.deviceId));
    const layerValues = [...new Set(filteredGroups.map((group) => group.layer))].sort(
      (a, b) => (a ?? Number.MAX_SAFE_INTEGER) - (b ?? Number.MAX_SAFE_INTEGER),
    );

    let y = 10;
    for (const layer of layerValues) {
      const key = layerKeyOf(layer);
      const label = layer === null ? "Sem hierarquia" : `Camada ${layer}`;
      const members = filteredGroups.filter((group) => group.layer === layer);
      const headerY = y + 18;
      const bodyTop = y + headerH;

      members.forEach((group, index) => {
        const cx = padX + (index % cols) * slotW;
        const cy = bodyTop + Math.floor(index / cols) * slotH + 46;
        group.nodes.forEach((node, nodeIndex) => {
          nodeDevice.set(node.id, group.deviceId);
          if (group.nodes.length === 1) {
            nodePos.set(node.id, { x: cx, y: cy });
          } else {
            const angle = -Math.PI / 2 + (nodeIndex * 2 * Math.PI) / group.nodes.length;
            nodePos.set(node.id, {
              x: cx + Math.cos(angle) * 30,
              y: cy + Math.sin(angle) * 30,
            });
          }
        });
        slots.push({ group, cx, cy });
      });

      const rows = Math.max(1, Math.ceil(members.length / cols));
      const collapsed = collapsedLayers.has(key);
      const count = members.length;
      bands.push({
        key,
        label,
        title: `${label} · ${count} ${count === 1 ? "device" : "devices"}`,
        headerY,
        collapsed,
      });
      y += headerH + (collapsed ? 0 : rows * slotH);
    }

    const height = Math.max(420, y + 90);
    return { slots, bands, nodePos, nodeDevice, height };
  }, [groups, filteredDeviceIds, collapsedLayers]);

  /** F29-5: nós ghost (vizinho não gerenciado) — fora dos clusters. */
  const ghostNodes = useMemo(
    () => graph?.nodes.filter((node) => node.isGhost) ?? [],
    [graph],
  );

  /** Posição dos ghosts: deslocados do slot do device local que os descobriu. */
  const ghostPos = useMemo(() => {
    const map = new Map<string, { x: number; y: number }>();
    const perDevice = new Map<string, number>();
    ghostNodes.forEach((ghost) => {
      const slot = layout.slots.find((s) => s.group.deviceId === ghost.deviceId);
      const base = slot ? { x: slot.cx, y: slot.cy } : { x: 48, y: 48 };
      const index = perDevice.get(ghost.deviceId) ?? 0;
      perDevice.set(ghost.deviceId, index + 1);
      map.set(ghost.id, {
        x: base.x + 52 + (index % 3) * 22,
        y: base.y + 46 + Math.floor(index / 3) * 20,
      });
    });
    return map;
  }, [ghostNodes, layout]);

  /** Posição de um node (real no cluster, ou ghost avulso). */
  const positionOf = useCallback(
    (interfaceId: string) => layout.nodePos.get(interfaceId) ?? ghostPos.get(interfaceId),
    [layout, ghostPos],
  );

  const typeCounts = useMemo(() => {
    const counts = new Map<string, number>();
    groups.forEach((group) => {
      counts.set(group.deviceType, (counts.get(group.deviceType) ?? 0) + 1);
    });
    return counts;
  }, [groups]);

  const presentTypes = useMemo(
    () => typeOrder.filter((type) => typeCounts.has(type)),
    [typeCounts],
  );

  const clearSelection = useCallback(() => {
    setSelectedDeviceId(null);
    setSelectedEdgeId(null);
  }, []);

  /**
   * F53-T003 — pan: mousedown no mapa ancora a arrasto; o move do
   * window desloca o canvas; qualquer deslocamento > 4px marca
   * `didDragRef` e os handlers de clique checam a ref (clique limpo
   * seguinte volta a funcionar porque o próximo mousedown zera a ref).
   */
  const startDrag = useCallback((event: React.MouseEvent) => {
    if (event.button !== 0) return;
    event.preventDefault();
    setDragging(true);
    dragRef.current = {
      startX: event.clientX,
      startY: event.clientY,
      panX: pan.x,
      panY: pan.y,
    };
    didDragRef.current = false;
  }, [pan]);

  useEffect(() => {
    const onMove = (event: MouseEvent) => {
      const drag = dragRef.current;
      if (!drag) return;
      const dx = event.clientX - drag.startX;
      const dy = event.clientY - drag.startY;
      if (Math.abs(dx) > 4 || Math.abs(dy) > 4) didDragRef.current = true;
      setPan({ x: drag.panX + dx, y: drag.panY + dy });
    };
    const endDrag = () => {
      dragRef.current = null;
      setDragging(false);
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", endDrag);
    window.addEventListener("blur", endDrag);
    return () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", endDrag);
      window.removeEventListener("blur", endDrag);
    };
  }, []);

  const toggleLayer = useCallback((key: string) => {
    setCollapsedLayers((previous) => {
      const next = new Set(previous);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }, []);

  /** F53-T003 — "Centralizar": volta pan e zoom ao estado inicial. */
  const centralize = useCallback(() => {
    setPan({ x: 0, y: 0 });
    setZoom(100);
  }, []);

  /** F53-T004 — limpa todos os filtros de uma vez (volta ao "Todos"). */
  const clearFilters = useCallback(() => {
    setFilter("all");
    setSearch("");
    setLocationFilter("all");
    setVlanFilter("all");
    setProblemsOnly(false);
  }, []);

  const hasActiveFilters =
    filter !== "all" ||
    search !== "" ||
    locationFilter !== "all" ||
    vlanFilter !== "all" ||
    problemsOnly;

  // Esc limpa a seleção (nó ou conexão) — a mesma saída do tooltip de hover.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") clearSelection();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [clearSelection]);

  const nodeById = useMemo(
    () => new Map(graph?.nodes.map((node) => [node.id, node]) ?? []),
    [graph],
  );

  /** Devices vizinhos do selecionado (ponta de alguma Connection). */
  const neighborDeviceIds = useMemo(() => {
    const neighbors = new Set<string>();
    if (!selectedDeviceId) return neighbors;
    graph?.edges.forEach((edge) => {
      const sourceDevice = layout.nodeDevice.get(edge.sourceInterfaceId);
      const targetDevice = layout.nodeDevice.get(edge.targetInterfaceId);
      if (!sourceDevice || !targetDevice) return;
      if (sourceDevice === selectedDeviceId) neighbors.add(targetDevice);
      if (targetDevice === selectedDeviceId) neighbors.add(sourceDevice);
    });
    return neighbors;
  }, [selectedDeviceId, graph, layout]);

  const selectedEdge = useMemo(() => {
    if (!selectedEdgeId) return null;
    const edge = graph?.edges.find((entry) => entry.id === selectedEdgeId);
    if (!edge) return null;
    return {
      edge,
      sourceInterfaceId: edge.sourceInterfaceId,
      targetInterfaceId: edge.targetInterfaceId,
      sourceDeviceId: layout.nodeDevice.get(edge.sourceInterfaceId) ?? null,
      targetDeviceId: layout.nodeDevice.get(edge.targetInterfaceId) ?? null,
    };
  }, [selectedEdgeId, graph, layout]);

  const selectedGroup = useMemo(
    () => groups.find((group) => group.deviceId === selectedDeviceId) ?? null,
    [groups, selectedDeviceId],
  );

  const selectedGroupConnections = useMemo(() => {
    if (!selectedGroup) return 0;
    return (
      graph?.edges.filter((edge) => {
        const sourceDevice = layout.nodeDevice.get(edge.sourceInterfaceId);
        const targetDevice = layout.nodeDevice.get(edge.targetInterfaceId);
        return (
          sourceDevice === selectedGroup.deviceId ||
          targetDevice === selectedGroup.deviceId
        );
      }).length ?? 0
    );
  }, [selectedGroup, graph, layout]);

  /**
   * F53-T005 — bundle do painel do device (11.5): system-info + último
   * poll + alertas em paralelo; Zabbix/UniFi/portas só quando o device
   * tem os dados (nome/MAC/tipo). Falha de integração degrada a linha
   * (catch → null), não o painel inteiro.
   */
  useEffect(() => {
    if (!selectedDeviceId) {
      setDeviceDetail(null);
      setDeviceDetailError(null);
      return;
    }
    const device = deviceItems.find((entry) => entry.id === selectedDeviceId) ?? null;
    let alive = true;
    setDeviceDetail(null);
    setDeviceDetailError(null);
    (async () => {
      try {
        const [systemInfo, results, alerts] = await Promise.all([
          getDeviceSystemInfo(selectedDeviceId).catch(() => null),
          getDeviceResults(selectedDeviceId, 5).catch(() => null),
          listAlerts({ deviceId: selectedDeviceId, page: 1, pageSize: 5 }).catch(() => null),
        ]);
        const [hostsPage, unifi, ports] = await Promise.all([
          device?.name
            ? getZabbixHosts({ term: device.name }).catch(() => null)
            : Promise.resolve(null),
          device?.mac
            ? getUniFiCorrelation().catch(() => null)
            : Promise.resolve(null),
          device?.type === "Switch"
            ? listSwitchPorts(selectedDeviceId).catch(() => null)
            : Promise.resolve(null),
        ]);

        // Host Zabbix = match exato de host/nome/IP (sem match, sem vínculo).
        let zabbixHost: ZabbixHostView | null = null;
        let zabbixItems: ZabbixHostItem[] = [];
        if (hostsPage && device) {
          const name = device.name.toLowerCase();
          const ip = device.ip?.toLowerCase() ?? "";
          zabbixHost =
            hostsPage.items.find(
              (host) =>
                host.host.toLowerCase() === name ||
                host.name.toLowerCase() === name ||
                (!!ip && (host.ip ?? "").toLowerCase() === ip),
            ) ?? null;
          if (zabbixHost) {
            try {
              const items = await getZabbixHostItems(zabbixHost.hostId);
              // Até 5 leituras úteis; chaves sem índice descoberto primeiro.
              zabbixItems = items
                .filter((item) => item.enabled && !item.unsupported && item.lastValue != null)
                .sort((a, b) => Number(a.key.includes("[")) - Number(b.key.includes("[")))
                .slice(0, 5);
            } catch {
              zabbixItems = [];
            }
          }
        }

        if (!alive) return;
        setDeviceDetail({
          systemInfo: systemInfo ?? null,
          lastPollAt: results?.results?.[0]?.checkedAt ?? null,
          alerts: alerts?.items ?? [],
          alertsTotal: alerts?.totalCount ?? 0,
          zabbixHost,
          zabbixItems,
          unifi,
          ports: ports?.items ?? null,
        });
      } catch (caught) {
        if (!alive) return;
        setDeviceDetailError(errorMessage(caught));
      }
    })();
    return () => {
      alive = false;
    };
  }, [selectedDeviceId, deviceItems]);

  /**
   * F53-T005 — portas das pontas da conexão selecionada (11.3): fonte do
   * "Estado" (linkUp real da SwitchPort casada). Ghost não tem ponta
   * gerenciada no destino; falha de leitura → sem estado, sem inventar.
   */
  useEffect(() => {
    if (!selectedEdge || !selectedEdgeId) {
      setEdgePorts(null);
      return;
    }
    const edge = selectedEdge.edge;
    const sourceDevice = deviceByInterface.get(edge.sourceInterfaceId) ?? null;
    const targetDevice = edge.isGhost
      ? null
      : deviceByInterface.get(edge.targetInterfaceId) ?? null;
    let alive = true;
    setEdgePorts(null);
    (async () => {
      const [source, target] = await Promise.all([
        sourceDevice ? listSwitchPorts(sourceDevice).catch(() => null) : Promise.resolve(null),
        targetDevice && targetDevice !== sourceDevice
          ? listSwitchPorts(targetDevice).catch(() => null)
          : Promise.resolve(null),
      ]);
      if (!alive) return;
      setEdgePorts({ source: source?.items ?? null, target: target?.items ?? null });
    })();
    return () => {
      alive = false;
    };
  }, [selectedEdge, selectedEdgeId, deviceByInterface]);

  /**
   * F53-T005 — valores derivados dos painéis de detalhe (fora do JSX
   * para o restante da página não encher de ternários de loading).
   */
  const selectedDevice = selectedGroup
    ? deviceItems.find((entry) => entry.id === selectedGroup.deviceId) ?? null
    : null;
  const detailLoading =
    selectedDeviceId !== null && deviceDetail === null && deviceDetailError === null;
  /** Valor que só existe depois do fetch: "…" carregando, "—" sem dado. */
  const asyncValue = (loaded: string): string =>
    deviceDetail ? loaded : detailLoading ? "…" : "—";

  const vlanRef = selectedDevice?.vlanId
    ? vlans.find((entry) => entry.id === selectedDevice.vlanId) ?? null
    : null;
  const vlanText = vlanRef ? `VLAN ${vlanRef.vlanNumber} — ${vlanRef.name}` : "—";
  const uptimeText = asyncValue(
    deviceDetail ? formatUptime(deviceDetail.systemInfo?.uptimeSeconds) : "",
  );
  const lastPollText = asyncValue(
    deviceDetail
      ? deviceDetail.lastPollAt
        ? formatDateTime(new Date(deviceDetail.lastPollAt))
        : "—"
      : "",
  );
  const zabbixText = asyncValue(
    deviceDetail
      ? deviceDetail.zabbixHost
        ? deviceDetail.zabbixHost.name
        : "Sem host Zabbix vinculado"
      : "",
  );
  const unifiText = asyncValue(
    deviceDetail ? unifiLabel(deviceDetail.unifi, selectedDevice?.mac) : "",
  );

  const detailEdge = selectedEdge?.edge ?? null;
  const edgeSpeedText = detailEdge
    ? `${formatPortSpeed(detailEdge.sourceSpeed)} / ${formatPortSpeed(detailEdge.targetSpeed)}`
    : "";
  const edgeDuplexText = detailEdge
    ? `${formatPortDuplex(detailEdge.sourceDuplex)} / ${formatPortDuplex(detailEdge.targetDuplex)}`
    : "";
  const edgeVlanText = (() => {
    if (!detailEdge) return "";
    const byNumber = new Map<number, string>();
    [...(detailEdge.sourceVlans ?? []), ...(detailEdge.targetVlans ?? [])].forEach((vlan) => {
      if (!byNumber.has(vlan.number)) byNumber.set(vlan.number, vlan.name);
    });
    return byNumber.size
      ? [...byNumber.entries()].map(([number, name]) => `${number} ${name}`).join(", ")
      : "—";
  })();
  const edgeStateText = (() => {
    if (!detailEdge) return "";
    if (!edgePorts) return "…"; // porta ainda sendo lida
    const sourcePort = matchPort(edgePorts.source, detailEdge.sourceInterfaceName);
    const targetPort = detailEdge.isGhost
      ? null
      : matchPort(edgePorts.target, detailEdge.targetInterfaceName);
    if (sourcePort?.linkUp != null) {
      return `${sourcePort.linkUp ? "Up" : "Down"} (ponta origem)`;
    }
    if (targetPort?.linkUp != null) {
      return `${targetPort.linkUp ? "Up" : "Down"} (ponta destino)`;
    }
    return "—";
  })();

  /**
   * F53-T005 — o que está conectado numa porta do switch: match do nome
   * da interface nas DUAS pontas das Connections (a porta local pode ser
   * origem ou destino); ghost = vizinho não gerenciado.
   */
  const connectedTextFor = (deviceId: string, portName: string): string => {
    const needle = portName.toLowerCase();
    for (const entry of graph?.edges ?? []) {
      const sourceNode = nodeById.get(entry.sourceInterfaceId);
      const targetNode = nodeById.get(entry.targetInterfaceId);
      const sourceName = (entry.sourceInterfaceName ?? sourceNode?.name ?? "").toLowerCase();
      const targetName = (entry.targetInterfaceName ?? targetNode?.name ?? "").toLowerCase();
      if (deviceByInterface.get(entry.sourceInterfaceId) === deviceId && sourceName === needle) {
        return entry.isGhost
          ? `Vizinho não gerenciado · ${targetNode?.port ?? entry.targetInterfaceName ?? "—"}`
          : `${targetNode?.deviceName ?? "—"} · ${entry.targetInterfaceName ?? targetNode?.name ?? "—"}`;
      }
      if (
        !entry.isGhost &&
        deviceByInterface.get(entry.targetInterfaceId) === deviceId &&
        targetName === needle
      ) {
        return `${sourceNode?.deviceName ?? "—"} · ${entry.sourceInterfaceName ?? sourceNode?.name ?? "—"}`;
      }
    }
    return "—";
  };

  const upCount = groups.filter((group) => group.status === "up").length;
  const downCount = groups.filter((group) => group.status === "down").length;
  const unknownCount = groups.filter((group) => group.status === null).length;

  if (loading) {
    return <LoadingBlock />;
  }

  // Erro inicial NÃO é estado vazio (F29-T004): "não há dados" e "não
  // consegui perguntar" pedem ações diferentes. A página continua renderizando
  // com o cabeçalho e o ErrorBanner cuida do aviso (role="alert" + retry).
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Topologia de Rede"
        subtitle={`${groups.length} dispositivos · ${graph?.nodes.length ?? 0} interfaces · ${graph?.edges.length ?? 0} conexões`}
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {/* Stats — estado do device = último Ping (up/down/nunca verificado) */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12 }}>
        <div className="uni-panel" style={{ padding: 14, display: "flex", gap: 10 }}>
          <div style={{ width: 32, height: 32, borderRadius: 6, background: "#f0fdf4", color: "#16a34a", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-check-circle" /></div>
          <div><div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1 }}>{upCount}</div><div style={{ fontSize: 10, color: "#9ca3af" }}>Up</div></div>
        </div>
        <div className="uni-panel" style={{ padding: 14, display: "flex", gap: 10 }}>
          <div style={{ width: 32, height: 32, borderRadius: 6, background: "#fef2f2", color: "#dc2626", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-times-circle" /></div>
          <div><div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1 }}>{downCount}</div><div style={{ fontSize: 10, color: "#9ca3af" }}>Down</div></div>
        </div>
        <div className="uni-panel" style={{ padding: 14, display: "flex", gap: 10 }}>
          <div style={{ width: 32, height: 32, borderRadius: 6, background: "#f1f5f9", color: "#64748b", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-question-circle" /></div>
          <div><div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1 }}>{unknownCount}</div><div style={{ fontSize: 10, color: "#9ca3af" }}>Nunca verificado</div></div>
        </div>
      </div>

      {/* Category filters */}
      <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
        <CategoryCard label="Todos" count={groups.length} icon="pi pi-th-large" color="#3b82f6" onClick={() => setFilter("all")} active={filter === "all"} />
        {presentTypes.map((type) => (
          <CategoryCard
            key={type}
            label={type}
            count={typeCounts.get(type) ?? 0}
            icon={typeMeta[type]?.icon ?? "pi pi-question"}
            color={typeMeta[type]?.color ?? "#94a3b8"}
            onClick={() => setFilter(type)}
            active={filter === type}
          />
        ))}
      </div>

      {/* F53-T004 — filtros combináveis (AND sobre o mesmo visibleDeviceIds) */}
      {graph && graph.nodes.length > 0 && (
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
          <input
            aria-label="Buscar device por nome ou IP"
            placeholder="Buscar por nome ou IP…"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e5e7eb", fontSize: 11, width: 200 }}
          />
          <select
            aria-label="Filtrar por localização"
            value={locationFilter}
            onChange={(event) => setLocationFilter(event.target.value)}
            style={{ padding: "6px 8px", borderRadius: 6, border: "1px solid #e5e7eb", fontSize: 11, background: "white" }}
          >
            <option value="all">Todos os locais</option>
            {Object.entries(locations)
              .sort(([, a], [, b]) => a.localeCompare(b))
              .map(([id, name]) => (
                <option key={id} value={id}>{name}</option>
              ))}
          </select>
          <select
            aria-label="Filtrar por VLAN"
            value={vlanFilter}
            onChange={(event) => setVlanFilter(event.target.value)}
            style={{ padding: "6px 8px", borderRadius: 6, border: "1px solid #e5e7eb", fontSize: 11, background: "white" }}
          >
            <option value="all">Todas as VLANs</option>
            {vlans.map((vlan) => (
              <option key={vlan.id} value={String(vlan.vlanNumber)}>
                VLAN {vlan.vlanNumber} — {vlan.name}
              </option>
            ))}
          </select>
          <button
            onClick={() => setProblemsOnly((value) => !value)}
            aria-pressed={problemsOnly}
            style={{
              display: "inline-flex", alignItems: "center", gap: 4,
              padding: "5px 10px", borderRadius: 6,
              border: `1px solid ${problemsOnly ? "#dc2626" : "#e5e7eb"}`,
              background: problemsOnly ? "#fef2f2" : "white",
              color: problemsOnly ? "#dc2626" : "#374151",
              fontSize: 11, fontWeight: 600, cursor: "pointer",
            }}
          >
            <i className="pi pi-exclamation-triangle" style={{ fontSize: 10 }} />
            Só problemas
          </button>
          {hasActiveFilters && (
            <button
              aria-label="Limpar filtros"
              onClick={clearFilters}
              style={{
                display: "inline-flex", alignItems: "center", gap: 4,
                padding: "5px 10px", borderRadius: 6, border: "1px solid #e5e7eb",
                background: "white", color: "#6b7280", fontSize: 11, cursor: "pointer",
              }}
            >
              <i className="pi pi-times" style={{ fontSize: 10 }} />
              Limpar filtros
            </button>
          )}
        </div>
      )}

      {/* Main: Map + Sidebar */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 280px", gap: 12, minHeight: 500 }}>
        {/* Map */}
        <div className="uni-panel" style={{ overflow: "hidden" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "10px 16px", borderBottom: "1px solid #f3f4f6" }}>
            <h2 style={{ fontSize: 13, fontWeight: 600 }}>Mapa de Rede</h2>
            <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <button aria-label="Diminuir zoom" onClick={() => setZoom(z => Math.max(z - 10, 50))} style={{ width: 22, height: 22, borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-minus" style={{ fontSize: 9 }} /></button>
              <span style={{ fontSize: 10, fontWeight: 500, minWidth: 28, textAlign: "center" }}>{zoom}%</span>
              <button aria-label="Aumentar zoom" onClick={() => setZoom(z => Math.min(z + 10, 150))} style={{ width: 22, height: 22, borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-plus" style={{ fontSize: 9 }} /></button>
              <button
                onClick={centralize}
                style={{ display: "inline-flex", alignItems: "center", gap: 4, marginLeft: 6, padding: "3px 8px", borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", fontSize: 10, fontWeight: 500, color: "#374151" }}
              >
                <i className="pi pi-map-marker" style={{ fontSize: 10 }} />
                Centralizar
              </button>
            </div>
          </div>

          {/* F53-T003 — toggles de recolhimento por faixa de camada */}
          {graph && graph.nodes.length > 0 && layout.bands.length > 0 && (
            <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap", padding: "8px 16px", borderBottom: "1px solid #f3f4f6" }}>
              <span style={{ fontSize: 10, color: "#9ca3af" }}>Camadas</span>
              {layout.bands.map((band) => (
                <button
                  key={band.key}
                  onClick={() => toggleLayer(band.key)}
                  aria-expanded={!band.collapsed}
                  style={{
                    display: "inline-flex", alignItems: "center", gap: 4,
                    padding: "3px 8px", borderRadius: 999,
                    border: "1px solid #e5e7eb",
                    background: band.collapsed ? "#f9fafb" : "#eff6ff",
                    color: band.collapsed ? "#6b7280" : "#1d4ed8",
                    fontSize: 10, fontWeight: 600, cursor: "pointer",
                  }}
                >
                  <i className={band.collapsed ? "pi pi-chevron-right" : "pi pi-chevron-down"} style={{ fontSize: 8 }} />
                  {band.label}
                </button>
              ))}
            </div>
          )}

          {graph && graph.nodes.length === 0 && (
            <div style={{ padding: 40, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
              Nenhuma interface mapeada — a topologia nasce das Connections cadastradas entre interfaces (F3-T005)
            </div>
          )}

          {/* F53-T004 — filtro sem resultado ≠ sem dados */}
          {graph && graph.nodes.length > 0 && filteredDeviceIds.size === 0 && (
            <div style={{ padding: 40, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
              Nenhum device corresponde aos filtros
            </div>
          )}

          {graph && graph.nodes.length > 0 && filteredDeviceIds.size > 0 && (
            <div
              data-testid="topology-map"
              onMouseDown={startDrag}
              style={{ padding: 10, overflow: "hidden", height: 640, userSelect: "none", cursor: dragging ? "grabbing" : "grab" }}
            >
              <div
                data-testid="topology-canvas"
                style={{ transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom / 100})`, transformOrigin: "top left", width: "100%" }}
              >
                <svg
                  style={{ display: "block", width: "100%", height: "auto" }}
                  viewBox={`0 0 1100 ${layout.height}`}
                  onClick={clearSelection}
                >
                  <defs>
                    <filter id="glow"><feGaussianBlur stdDeviation="2" result="blur" /><feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge></filter>
                  </defs>

                  {/* F53-T003 — faixas por camada: cabeçalho sempre visível
                      (mesmo recolhida, o corpo da faixa some da altura). */}
                  {layout.bands.map((band) => (
                    <g key={band.key}>
                      <text
                        x={16}
                        y={band.headerY}
                        fill={band.collapsed ? "#9ca3af" : "#475569"}
                        fontSize="11"
                        fontWeight="700"
                        fontFamily="Inter, sans-serif"
                      >
                        {band.title}
                      </text>
                      <line
                        x1={16}
                        y1={band.headerY + 8}
                        x2={1084}
                        y2={band.headerY + 8}
                        stroke="#e2e8f0"
                        strokeWidth={1}
                      />
                    </g>
                  ))}

                  {/* Edges — Connections sem tipo no contrato: traço neutro.
                      Seleção: device → destaca as conexões dele; edge → destaca
                      só ela; o resto perde opacidade (foco visual). */}
                  {graph.edges.map((edge) => {
                    const from = positionOf(edge.sourceInterfaceId);
                    const to = positionOf(edge.targetInterfaceId);
                    if (!from || !to) return null;
                    const sourceDevice = layout.nodeDevice.get(edge.sourceInterfaceId);
                    const targetDevice = edge.isGhost
                      ? edge.targetInterfaceId // ghost não tem device próprio no mapa
                      : layout.nodeDevice.get(edge.targetInterfaceId);
                    if (!sourceDevice || !targetDevice) return null;
                    if (!visibleDeviceIds.has(sourceDevice) || (!edge.isGhost && !visibleDeviceIds.has(targetDevice))) return null;

                    const isHighlighted =
                      (selectedDeviceId !== null &&
                        (sourceDevice === selectedDeviceId ||
                          targetDevice === selectedDeviceId)) ||
                      selectedEdgeId === edge.id;
                    const isDimmed =
                      (selectedDeviceId !== null && !isHighlighted) ||
                      (selectedEdgeId !== null && !isHighlighted);

                    return (
                      <g key={edge.id}>
                        <line
                          x1={from.x} y1={from.y} x2={to.x} y2={to.y}
                          stroke={selectedEdgeId === edge.id ? "#3b82f6" : edge.isGhost ? "#a78bfa" : "#94a3b8"}
                          strokeWidth={isHighlighted ? 2.2 : 1.2}
                          strokeDasharray={edge.isGhost ? "4 3" : undefined}
                          opacity={isDimmed ? 0.12 : isHighlighted ? 0.95 : 0.5}
                        />
                        {/* hit-area invisível: a linha visível tem 1,2px de alvo */}
                        <line
                          x1={from.x} y1={from.y} x2={to.x} y2={to.y}
                          stroke="transparent" strokeWidth={12}
                          style={{ cursor: "pointer" }}
                          onClick={(event) => {
                            if (didDragRef.current) return;
                            event.stopPropagation();
                            setSelectedEdgeId(edge.id);
                            setSelectedDeviceId(null);
                          }}
                        />
                      </g>
                    );
                  })}

                  {/* Nodes = interfaces */}
                  {layout.slots
                    .filter((slot) => visibleDeviceIds.has(slot.group.deviceId))
                    .map((slot) => {
                      const { group } = slot;
                      const meta = typeMeta[group.deviceType] ?? { color: "#94a3b8", icon: "pi pi-question", emoji: "❓" };
                      const isSelected = selectedDeviceId === group.deviceId;
                      const isNeighbor = neighborDeviceIds.has(group.deviceId);
                      const isInSelectedEdge =
                        selectedEdge !== null &&
                        (group.deviceId === selectedEdge.sourceDeviceId ||
                          group.deviceId === selectedEdge.targetDeviceId);
                      const dimmed =
                        (selectedDeviceId !== null && !isSelected && !isNeighbor) ||
                        (selectedEdge !== null && !isInSelectedEdge);
                      return (
                        <g
                          key={group.deviceId}
                          opacity={dimmed ? 0.32 : 1}
                          style={{ cursor: "pointer" }}
                          onClick={(event) => {
                            if (didDragRef.current) return;
                            event.stopPropagation();
                            setSelectedDeviceId(group.deviceId);
                            setSelectedEdgeId(null);
                          }}
                        >
                        {/* anel de seleção do device */}
                        {isSelected && (
                          <circle
                            cx={slot.cx} cy={slot.cy} r={20}
                            fill="none" stroke="#3b82f6" strokeWidth={1.5}
                            strokeDasharray="4 3"
                          />
                        )}
                        {/* F53-T004 — anel de destaque nos vizinhos associados */}
                        {isNeighbor && !isSelected && (
                          <circle
                            cx={slot.cx} cy={slot.cy} r={20}
                            fill="none" stroke="#93c5fd" strokeWidth={1.5}
                            strokeDasharray="2 3"
                          />
                        )}
                          {/* nome do device sob o cluster */}
                          <text x={slot.cx} y={slot.cy + 52} textAnchor="middle" fill={isSelected ? "#1d4ed8" : "#374151"} fontSize="9" fontWeight="600" fontFamily="Inter, sans-serif">
                            {group.deviceName}
                          </text>

                          {group.nodes.map((node) => {
                            const pos = layout.nodePos.get(node.id);
                            if (!pos) return null;
                            const color = statusColor(group.status);
                            return (
                              <g
                                key={node.id}
                                style={{ cursor: "pointer" }}
                                onMouseEnter={() => setHoveredId(node.id)}
                                onMouseLeave={() => setHoveredId(null)}
                              >
                                <circle cx={pos.x} cy={pos.y} r={13} fill="white" stroke={isSelected ? "#3b82f6" : color} strokeWidth={isSelected || group.status === "up" ? 2 : 1.5} filter={group.status === "up" ? "url(#glow)" : undefined} />
                                <text x={pos.x} y={pos.y + 1} textAnchor="middle" dominantBaseline="central" fontSize="10">{meta.emoji}</text>

                                {hoveredId === node.id && (
                                  <g>
                                    <rect x={pos.x + 16} y={pos.y - 46} width={168} height={84} rx={6} fill="white" stroke="#e5e7eb" strokeWidth="1" filter="url(#glow)" />
                                    <text x={pos.x + 24} y={pos.y - 30} fill="#111827" fontSize="9" fontWeight="600">{group.deviceName}</text>
                                    <text x={pos.x + 24} y={pos.y - 18} fill="#6b7280" fontSize="7">{group.deviceType} · {group.ip ?? "sem IP"}</text>
                                    <line x1={pos.x + 20} y1={pos.y - 12} x2={pos.x + 180} y2={pos.y - 12} stroke="#f3f4f6" />
                                    <text x={pos.x + 24} y={pos.y} fill="#6b7280" fontSize="7">Interface: {node.name}</text>
                                    <text x={pos.x + 24} y={pos.y + 12} fill={color} fontSize="7" fontWeight="600">{statusLabel(group.status)}</text>
                                    <text x={pos.x + 24} y={pos.y + 24} fill="#6b7280" fontSize="7">
                                      Local: {group.locationId ? locations[group.locationId] ?? "—" : "—"}
                                    </text>
                                  </g>
                                )}
                              </g>
                            );
                          })}
                        </g>
                      );
                    })}
                  {/* F29-5: nós ghost — vizinho LLDP/CDP não gerenciado,
                      renderizado tracejado com o rótulo da porta remota. */}
                  {ghostNodes
                    .filter((ghost) => visibleDeviceIds.has(ghost.deviceId))
                    .map((ghost) => {
                    const pos = ghostPos.get(ghost.id);
                    if (!pos) return null;
                    const isSelected = selectedEdgeId !== null &&
                      selectedEdge?.targetInterfaceId === ghost.id;
                    return (
                      <g
                        key={ghost.id}
                        style={{ cursor: "pointer" }}
                        onMouseEnter={() => setHoveredId(ghost.id)}
                        onMouseLeave={() => setHoveredId(null)}
                      >
                        <circle
                          cx={pos.x} cy={pos.y} r={13}
                          fill="#faf5ff" stroke="#a78bfa" strokeWidth={1.5}
                          strokeDasharray="3 3"
                          opacity={isSelected ? 1 : 0.9}
                        />
                        <text x={pos.x} y={pos.y + 1} textAnchor="middle" dominantBaseline="central" fontSize="10">👻</text>
                        <text x={pos.x} y={pos.y + 26} textAnchor="middle" fill="#7c3aed" fontSize="8" fontWeight="600" fontFamily="Inter, sans-serif">
                          {ghost.name}
                        </text>
                        {ghost.port && (
                          <text x={pos.x} y={pos.y + 36} textAnchor="middle" fill="#a78bfa" fontSize="7" fontFamily="Inter, sans-serif">
                            {ghost.port}
                          </text>
                        )}
                        {hoveredId === ghost.id && (
                          <g>
                            <rect x={pos.x + 16} y={pos.y - 30} width={158} height={48} rx={6} fill="white" stroke="#e5e7eb" strokeWidth="1" filter="url(#glow)" />
                            <text x={pos.x + 24} y={pos.y - 16} fill="#111827" fontSize="9" fontWeight="600">{ghost.name}</text>
                            <text x={pos.x + 24} y={pos.y - 4} fill="#7c3aed" fontSize="7">Vizinho não gerenciado</text>
                            <text x={pos.x + 24} y={pos.y + 8} fill="#6b7280" fontSize="7">Porta remota: {ghost.port ?? "—"}</text>
                          </g>
                        )}
                      </g>
                    );
                  })}
                </svg>
              </div>
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {/* Detalhes da seleção — nó (device) ou aresta (Connection) */}
          {(selectedGroup || selectedEdge) && (
            <div className="uni-panel" style={{ padding: 12 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
                <h3 style={{ fontSize: 11, fontWeight: 600 }}>Detalhes</h3>
                <button
        onClick={() => {
          if (didDragRef.current) return;
          clearSelection();
        }}
                  aria-label="Limpar seleção"
                  style={{ width: 20, height: 20, borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}
                >
                  <i className="pi pi-times" style={{ fontSize: 9 }} />
                </button>
              </div>

              {selectedGroup && (
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
                    <div style={{ fontSize: 13, fontWeight: 700, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {selectedGroup.deviceName}
                    </div>
                    <StatusChip
                      label={statusLabel(selectedGroup.status)}
                      background={selectedGroup.status === "up" ? "#f0fdf4" : selectedGroup.status === "down" ? "#fef2f2" : "#f1f5f9"}
                      color={selectedGroup.status === "up" ? "#16a34a" : selectedGroup.status === "down" ? "#dc2626" : "#64748b"}
                      fontSize={9}
                      style={{ flexShrink: 0 }}
                    />
                  </div>
                  {deviceDetailError ? (
                    <div style={{ fontSize: 10, color: "#dc2626" }}>{deviceDetailError}</div>
                  ) : (
                    <div style={{ display: "flex", flexDirection: "column", gap: 3, fontSize: 10, color: "#6b7280" }}>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Tipo: </span>
                        {selectedGroup.deviceType}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>IP: </span>
                        {selectedGroup.ip ?? "—"}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>MAC: </span>
                        {selectedDevice?.mac ?? "—"}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Série: </span>
                        {selectedDevice?.serial ?? "—"}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Modelo: </span>
                        {selectedDevice?.model ?? "—"}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Fabricante: </span>
                        {selectedDevice?.vendor ?? "—"}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Local: </span>
                        {selectedGroup.locationId ? locations[selectedGroup.locationId] ?? "—" : "—"}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>VLAN: </span>
                        {vlanText}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Uptime: </span>
                        {uptimeText}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Última comunicação: </span>
                        {lastPollText}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Zabbix: </span>
                        {zabbixText}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>UniFi: </span>
                        {unifiText}
                      </div>
                      <div>
                        <span style={{ color: "#9ca3af" }}>Interfaces: </span>
                        {selectedGroup.nodes.length} ·{" "}
                        <span style={{ color: "#9ca3af" }}>Conexões: </span>
                        {selectedGroupConnections}
                      </div>
                    </div>
                  )}

                  {/* F53-T005 — métricas Zabbix do host vinculado (leituras
                      úteis; sem host ou sem leitura = linha honesta acima). */}
                  {deviceDetail?.zabbixHost && !deviceDetailError && (
                    <div style={{ display: "flex", flexDirection: "column", gap: 2, fontSize: 9, color: "#6b7280", borderTop: "1px solid #f3f4f6", paddingTop: 6 }}>
                      <div style={{ fontWeight: 600, color: "#9ca3af" }}>Métricas Zabbix</div>
                      {deviceDetail.zabbixItems.length > 0 ? (
                        deviceDetail.zabbixItems.map((item) => (
                          <div key={item.itemId}>
                            {item.name}: {item.lastValue}
                            {item.unit ? ` ${item.unit}` : ""}
                          </div>
                        ))
                      ) : (
                        <div>Sem métricas disponíveis</div>
                      )}
                    </div>
                  )}

                  {/* F53-T005 — histórico de eventos (alertas do device). */}
                  {!deviceDetailError && (
                    <div style={{ display: "flex", flexDirection: "column", gap: 2, fontSize: 9, color: "#6b7280", borderTop: "1px solid #f3f4f6", paddingTop: 6 }}>
                      <div style={{ fontWeight: 600, color: "#9ca3af" }}>
                        Eventos recentes{deviceDetail ? ` (${deviceDetail.alertsTotal})` : ""}
                      </div>
                      {deviceDetail && deviceDetail.alerts.length === 0 && (
                        <div>Nenhum evento registrado</div>
                      )}
                      {deviceDetail?.alerts.map((alert) => (
                        <div key={alert.id}>
                          <span style={{ color: "#9ca3af" }}>
                            {formatDateTime(new Date(alert.openedAt))} · {alert.severity} ·{" "}
                          </span>
                          {alert.message}
                        </div>
                      ))}
                      {detailLoading && <div>…</div>}
                    </div>
                  )}

                  {/* F53-T005 — switch: portas com o que está conectado
                      (match da interface nas duas pontas das Connections). */}
                  {selectedDevice?.type === "Switch" && !deviceDetailError && (
                    <div style={{ display: "flex", flexDirection: "column", gap: 2, fontSize: 9, color: "#6b7280", borderTop: "1px solid #f3f4f6", paddingTop: 6 }}>
                      <div style={{ fontWeight: 600, color: "#9ca3af" }}>
                        Portas{deviceDetail?.ports ? ` (${deviceDetail.ports.length})` : ""}
                      </div>
                      {deviceDetail?.ports && deviceDetail.ports.length === 0 && (
                        <div>Nenhuma porta cadastrada</div>
                      )}
                      {deviceDetail?.ports?.map((port) => (
                        <div key={port.id}>
                          {port.name} · {port.linkUp == null ? "—" : port.linkUp ? "Up" : "Down"} ·{" "}
                          {formatPortSpeed(port.speed)} ·{" "}
                          {connectedTextFor(selectedGroup.deviceId, port.name)}
                        </div>
                      ))}
                      {detailLoading && <div>…</div>}
                    </div>
                  )}

                  <Button
                    variant="primary"
                    icon="pi pi-arrow-right"
                    style={{ marginTop: 4, padding: "6px 12px", fontSize: 11 }}
                    onClick={() => navigate(`/devices/${selectedGroup.deviceId}`)}
                  >
                    Ver device
                  </Button>
                </div>
              )}

              {selectedEdge && detailEdge && (
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <div style={{ fontSize: 11, color: "#6b7280" }}>
                    Connection (interface → interface)
                  </div>
                  {([["Origem", "source"], ["Destino", "target"]] as const).map(([label, side]) => {
                    const interfaceId =
                      side === "source" ? selectedEdge.sourceInterfaceId : selectedEdge.targetInterfaceId;
                    const deviceId =
                      side === "source" ? selectedEdge.sourceDeviceId : selectedEdge.targetDeviceId;
                    const node = nodeById.get(interfaceId);
                    const isGhostSide = side === "target" && detailEdge.isGhost;
                    // Ghost: a ponta gerenciada é o vizinho descoberto —
                    // o rótulo útil é a porta REMOTA (node.port), não o nome.
                    const interfaceName = isGhostSide
                      ? node?.port ?? node?.name ?? "—"
                      : (side === "source"
                          ? detailEdge.sourceInterfaceName
                          : detailEdge.targetInterfaceName) ??
                        node?.name ??
                        "—";
                    return (
                      <div key={label} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, fontSize: 10 }}>
                        <div style={{ minWidth: 0 }}>
                          <div>
                            <span style={{ color: "#9ca3af" }}>{label}: </span>
                            <span style={{ fontWeight: 600 }}>
                              {isGhostSide ? "Vizinho não gerenciado" : node?.deviceName ?? "—"}
                            </span>
                          </div>
                          <div style={{ color: "#9ca3af" }}>
                            <span>Porta: </span>
                            {interfaceName}
                          </div>
                        </div>
                        {deviceId && !isGhostSide && (
                          <Button
                            variant="ghost"
                            icon="pi pi-arrow-right"
                            style={{ padding: "4px 8px", fontSize: 10, flexShrink: 0 }}
                            onClick={() => navigate(`/devices/${deviceId}`)}
                          >
                            Abrir
                          </Button>
                        )}
                      </div>
                    );
                  })}
                  <div style={{ display: "flex", flexDirection: "column", gap: 3, fontSize: 10, color: "#6b7280", borderTop: "1px solid #f3f4f6", paddingTop: 6 }}>
                    <div>
                      <span style={{ color: "#9ca3af" }}>Velocidade: </span>
                      {edgeSpeedText}
                    </div>
                    <div>
                      <span style={{ color: "#9ca3af" }}>Duplex: </span>
                      {edgeDuplexText}
                    </div>
                    <div>
                      <span style={{ color: "#9ca3af" }}>Tipo: </span>
                      —
                    </div>
                    <div>
                      <span style={{ color: "#9ca3af" }}>VLANs: </span>
                      {edgeVlanText}
                    </div>
                    <div>
                      <span style={{ color: "#9ca3af" }}>Estado: </span>
                      {edgeStateText}
                    </div>
                    <div>
                      <span style={{ color: "#9ca3af" }}>Origem: </span>
                      {sourceLabel(detailEdge.source)}
                    </div>
                    <div>
                      <span style={{ color: "#9ca3af" }}>Confiança: </span>
                      {confidenceLabel(detailEdge.confidence)}
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          <div className="uni-panel" style={{ padding: 12 }}>
            <h3 style={{ fontSize: 11, fontWeight: 600, marginBottom: 8 }}>Legenda</h3>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 4 }}>
              {presentTypes.map((type) => (
                <div key={type} style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}>
                  <span style={{ width: 7, height: 7, borderRadius: "50%", background: typeMeta[type]?.color ?? "#94a3b8", flexShrink: 0 }} />
                  <span>{type}</span>
                </div>
              ))}
            </div>
            <div style={{ marginTop: 8, paddingTop: 8, borderTop: "1px solid #f3f4f6", display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}>
              <span style={{ width: 12, height: 2, background: "#94a3b8", flexShrink: 0 }} /> Connection (interface → interface)
            </div>
            <div style={{ marginTop: 4, display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}>
              <span style={{ width: 12, height: 0, borderTop: "2px dashed #a78bfa", flexShrink: 0 }} /> Vizinho não gerenciado (ghost)
            </div>
            <div style={{ marginTop: 4, display: "flex", gap: 8, fontSize: 9 }}>
              <span style={{ color: "#22c55e", fontWeight: 600 }}>● Up</span>
              <span style={{ color: "#ef4444", fontWeight: 600 }}>● Down</span>
              <span style={{ color: "#94a3b8" }}>● Nunca verificado</span>
            </div>
            <div style={{ marginTop: 8, paddingTop: 8, borderTop: "1px solid #f3f4f6", fontSize: 9, color: "#94a3b8" }}>
              Clique em um device ou conexão para ver os detalhes · Esc limpa a seleção
            </div>
          </div>

          <div className="uni-panel" style={{ padding: 12 }}>
            <h3 style={{ fontSize: 11, fontWeight: 600, marginBottom: 8 }}>VLANs</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
              {vlans.map((vlan) => (
                <div key={vlan.id} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10 }}>
                  <span style={{ fontWeight: 500 }}>VLAN {vlan.vlanNumber}</span>
                  <span style={{ color: "#94a3b8" }}>{vlan.name}</span>
                </div>
              ))}
              {vlans.length === 0 && (
                <div style={{ fontSize: 10, color: "#94a3b8" }}>Nenhuma VLAN cadastrada</div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
