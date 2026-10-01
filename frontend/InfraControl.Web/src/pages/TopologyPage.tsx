import { useCallback, useEffect, useMemo, useState } from "react";
import {
  listDevices,
  listLocations,
  type DeviceItem,
} from "@/services/api/inventory";
import {
  getTopology,
  listVlans,
  type TopologyGraph,
  type TopologyNodeItem,
  type VlanItem,
} from "@/services/api/network";

/**
 * F17-T021 — Topologia de Rede conectada ao backend (substitui
 * `topologyNodes`/`topologyEdges`/`deviceSummary`/`rackLayout`/
 * `storageConnections`/`vlanBadges` de `data/topologyData`).
 *
 * Fontes reais (4 leituras em paralelo):
 * - `GET /network/topology` (F3-T006) — nodes = **interfaces** (1 device
 *   pode ter N), edges = Connections dirigidas; `status` = último Ping do
 *   device dono (`"up"`/`"down"`/`null`); **sem coordenadas x/y** → o
 *   layout do mapa é grade calculada no cliente (devices ordenados por
 *   tipo, interfaces em satélite ao redor do device);
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
 */

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

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

/** Device agrupado a partir dos nodes (interfaces) do grafo. */
interface DeviceGroup {
  deviceId: string;
  deviceName: string;
  deviceType: string;
  ip: string | null;
  status: "up" | "down" | null;
  locationId: string | null;
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
  const [graph, setGraph] = useState<TopologyGraph | null>(null);
  const [deviceItems, setDeviceItems] = useState<DeviceItem[]>([]);
  const [locations, setLocations] = useState<Record<string, string>>({});
  const [vlans, setVlans] = useState<VlanItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [filter, setFilter] = useState<string>("all");
  const [zoom, setZoom] = useState(100);
  const [hoveredId, setHoveredId] = useState<string | null>(null);

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
      setError(messageFor(caught));
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
      const existing = byDevice.get(node.deviceId);
      if (existing) existing.nodes.push(node);
      else {
        const device = deviceItems.find((entry) => entry.id === node.deviceId);
        byDevice.set(node.deviceId, {
          deviceId: node.deviceId,
          deviceName: node.deviceName,
          deviceType: node.deviceType,
          ip: node.ip,
          status: null,
          locationId: device?.locationId ?? null,
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

  /** Grade do mapa: 7 colunas; interfaces em satélite ao redor do device. */
  const layout = useMemo(() => {
    const cols = 7;
    const slotW = 148;
    const slotH = 116;
    const padX = 74;
    const padY = 56;
    const rows = Math.max(1, Math.ceil(groups.length / cols));
    const height = Math.max(760, rows * slotH + 80);
    const nodePos = new Map<string, { x: number; y: number }>();
    const nodeDevice = new Map<string, string>();
    const slots = groups.map((group, index) => {
      const cx = padX + (index % cols) * slotW;
      const cy = padY + Math.floor(index / cols) * slotH;
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
      return { group, cx, cy };
    });
    return { slots, nodePos, nodeDevice, height };
  }, [groups]);

  const visibleDeviceIds = useMemo(() => {
    if (filter === "all") return new Set(groups.map((group) => group.deviceId));
    return new Set(
      groups.filter((group) => group.deviceType === filter).map((group) => group.deviceId),
    );
  }, [groups, filter]);

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

  const upCount = groups.filter((group) => group.status === "up").length;
  const downCount = groups.filter((group) => group.status === "down").length;
  const unknownCount = groups.filter((group) => group.status === null).length;

  if (loading) {
    return <div style={{ padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</div>;
  }

  if (error && !graph) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{error}</h2>
        <button onClick={() => void load()} style={{ marginTop: 8, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          Tentar de novo
        </button>
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Topologia de Rede</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>
            {groups.length} dispositivos · {graph?.nodes.length ?? 0} interfaces · {graph?.edges.length ?? 0} conexões
          </p>
        </div>
      </div>

      {error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
          <button onClick={() => void load()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
            Tentar de novo
          </button>
        </div>
      )}

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

      {/* Main: Map + Sidebar */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 280px", gap: 12, minHeight: 500 }}>
        {/* Map */}
        <div className="uni-panel" style={{ overflow: "hidden" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "10px 16px", borderBottom: "1px solid #f3f4f6" }}>
            <h2 style={{ fontSize: 13, fontWeight: 600 }}>Mapa de Rede</h2>
            <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <button onClick={() => setZoom(z => Math.max(z - 10, 50))} style={{ width: 22, height: 22, borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-minus" style={{ fontSize: 9 }} /></button>
              <span style={{ fontSize: 10, fontWeight: 500, minWidth: 28, textAlign: "center" }}>{zoom}%</span>
              <button onClick={() => setZoom(z => Math.min(z + 10, 150))} style={{ width: 22, height: 22, borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-plus" style={{ fontSize: 9 }} /></button>
            </div>
          </div>

          {graph && graph.nodes.length === 0 && (
            <div style={{ padding: 40, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
              Nenhuma interface mapeada — a topologia nasce das Connections cadastradas entre interfaces (F3-T005)
            </div>
          )}

          {graph && graph.nodes.length > 0 && (
            <div style={{ padding: 10, overflow: "auto", height: "calc(100% - 40px)", position: "relative" }}>
              <svg style={{ width: "100%", height: "100%", transform: `scale(${zoom / 100})`, transformOrigin: "top left" }} viewBox={`0 0 1100 ${layout.height}`}>
                <defs>
                  <filter id="glow"><feGaussianBlur stdDeviation="2" result="blur" /><feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge></filter>
                </defs>

                {/* Edges — Connections sem tipo no contrato: traço neutro */}
                {graph.edges.map((edge) => {
                  const from = layout.nodePos.get(edge.sourceInterfaceId);
                  const to = layout.nodePos.get(edge.targetInterfaceId);
                  if (!from || !to) return null;
                  const sourceDevice = layout.nodeDevice.get(edge.sourceInterfaceId);
                  const targetDevice = layout.nodeDevice.get(edge.targetInterfaceId);
                  if (!sourceDevice || !targetDevice) return null;
                  if (!visibleDeviceIds.has(sourceDevice) || !visibleDeviceIds.has(targetDevice)) return null;
                  return (
                    <line key={edge.id} x1={from.x} y1={from.y} x2={to.x} y2={to.y} stroke="#94a3b8" strokeWidth={1.2} opacity={0.5} />
                  );
                })}

                {/* Nodes = interfaces */}
                {layout.slots
                  .filter((slot) => visibleDeviceIds.has(slot.group.deviceId))
                  .map((slot) => {
                    const { group } = slot;
                    const meta = typeMeta[group.deviceType] ?? { color: "#94a3b8", icon: "pi pi-question", emoji: "❓" };
                    return (
                      <g key={group.deviceId}>
                        {/* nome do device sob o cluster */}
                        <text x={slot.cx} y={slot.cy + 52} textAnchor="middle" fill="#374151" fontSize="9" fontWeight="600" fontFamily="Inter, sans-serif">
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
                              <circle cx={pos.x} cy={pos.y} r={13} fill="white" stroke={color} strokeWidth={group.status === "up" ? 2 : 1.5} filter={group.status === "up" ? "url(#glow)" : undefined} />
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
              </svg>
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
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
            <div style={{ marginTop: 4, display: "flex", gap: 8, fontSize: 9 }}>
              <span style={{ color: "#22c55e", fontWeight: 600 }}>● Up</span>
              <span style={{ color: "#ef4444", fontWeight: 600 }}>● Down</span>
              <span style={{ color: "#94a3b8" }}>● Nunca verificado</span>
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
