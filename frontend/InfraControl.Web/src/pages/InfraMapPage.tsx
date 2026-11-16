import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { StatusChip } from "@/components/ui/StatusChip";
import { getTopology, type TopologyGraph } from "@/services/api/network";
import { listDevices } from "@/services/api/inventory";

/**
 * Mapa ERD da infraestrutura (pedido do usuário): a topologia real da
 * empresa desenhada como diagrama entidade-relacionamento — cada
 * equipamento é uma "entidade" (caixa com atributos) e cada conexão é
 * um "relacionamento" rotulado. Layout por camadas (BFS do backend:
 * core → distribuição → acesso); arestas = Connections reais
 * (LLDP/CDP/UniFi/manual). Dados 100% do que está cadastrado na
 * plataforma — nada inventado; device sem caminho até uma raiz cai na
 * faixa "Sem hierarquia".
 */

interface InfraNode {
  deviceId: string;
  name: string;
  deviceType: string;
  ip: string | null;
  status: "up" | "down" | null;
  layer: number | null;
  interfaceCount: number;
  interfaceNames: string[];
}

interface InfraEdge {
  id: string;
  sourceDeviceId: string;
  targetDeviceId: string;
  linkCount: number;
  sources: string[];
  confidence: string | null;
}

const BOX_W = 250;
const HEADER_H = 34;
const ROW_H = 19;
const BOX_PAD = 10;
const COL_GAP = 130;
const ROW_GAP = 46;
const LAYER_LABEL_H = 46;
const MARGIN = 30;
const MAX_IFACE_ROWS = 4;

const TYPE_COLORS: Record<string, { bg: string; fg: string }> = {
  Router: { bg: "#dbeafe", fg: "#1d4ed8" },
  Firewall: { bg: "#fee2e2", fg: "#b91c1c" },
  Switch: { bg: "#ede9fe", fg: "#6d28d9" },
  AccessPoint: { bg: "#dcfce7", fg: "#15803d" },
  Server: { bg: "#cffafe", fg: "#0e7490" },
  Printer: { bg: "#fef9c3", fg: "#854d0e" },
  Camera: { bg: "#fce7f3", fg: "#be185d" },
  WirelessController: { bg: "#ffedd5", fg: "#c2410c" },
};
const DEFAULT_TYPE_COLOR = { bg: "#f1f5f9", fg: "#475569" };

const STATUS_STYLE: Record<string, { bg: string; color: string }> = {
  up: { bg: "#dcfce7", color: "#15803d" },
  down: { bg: "#fee2e2", color: "#b91c1c" },
};
const STATUS_LABEL: Record<string, string> = { up: "online", down: "offline" };

function typeColor(type: string) {
  return TYPE_COLORS[type] ?? DEFAULT_TYPE_COLOR;
}

function buildInfra(graph: TopologyGraph): { nodes: InfraNode[]; edges: InfraEdge[] } {
  const byDevice = new Map<string, InfraNode>();
  for (const n of graph.nodes) {
    if (n.isGhost) continue;
    const existing = byDevice.get(n.deviceId);
    if (!existing) {
      byDevice.set(n.deviceId, {
        deviceId: n.deviceId,
        name: n.deviceName,
        deviceType: n.deviceType,
        ip: n.ip,
        status: n.status,
        layer: n.layer ?? null,
        interfaceCount: 1,
        interfaceNames: [n.name],
      });
    } else {
      existing.interfaceCount += 1;
      existing.interfaceNames.push(n.name);
      if (!existing.ip && n.ip) existing.ip = n.ip;
      // status do device = pior caso entre interfaces (down vence up).
      if (n.status === "down") existing.status = "down";
      else if (existing.status === null) existing.status = n.status;
      if (existing.layer == null && n.layer != null) existing.layer = n.layer;
    }
  }

  const pairKey = (a: string, b: string) => (a < b ? `${a}|${b}` : `${b}|${a}`);
  const nodeByIface = new Map(graph.nodes.map((n) => [n.id, n]));
  const edgesByPair = new Map<string, InfraEdge>();
  for (const e of graph.edges) {
    if (e.isGhost) continue;
    const src = nodeByIface.get(e.sourceInterfaceId);
    const tgt = nodeByIface.get(e.targetInterfaceId);
    if (!src || !tgt || src.deviceId === tgt.deviceId) continue;
    const key = pairKey(src.deviceId, tgt.deviceId);
    const [sourceDeviceId, targetDeviceId] =
      src.deviceId < tgt.deviceId
        ? [src.deviceId, tgt.deviceId]
        : [tgt.deviceId, src.deviceId];
    const existing = edgesByPair.get(key);
    if (existing) {
      existing.linkCount += 1;
      if (e.source && !existing.sources.includes(e.source)) existing.sources.push(e.source);
      if (!existing.confidence && e.confidence) existing.confidence = e.confidence;
    } else {
      edgesByPair.set(key, {
        id: `e-${key}`,
        sourceDeviceId,
        targetDeviceId,
        linkCount: 1,
        sources: e.source ? [e.source] : [],
        confidence: e.confidence ?? null,
      });
    }
  }

  return {
    nodes: [...byDevice.values()].sort(
      (a, b) => a.name.localeCompare(b.name, "pt-BR"),
    ),
    edges: [...edgesByPair.values()],
  };
}

export function InfraMapPage() {
  const topology = useQuery({
    queryKey: ["network-topology-erd"],
    queryFn: getTopology,
    staleTime: 60_000,
  });
  const devices = useQuery({
    queryKey: ["infra-map-devices"],
    queryFn: () => listDevices({ pageSize: 200 }).then((p) => p.items),
    staleTime: 60_000,
  });

  const { nodes, edges, offMap } = useMemo(() => {
    if (!topology.data) return { nodes: [], edges: [], offMap: [] };
    const built = buildInfra(topology.data);
    const mapped = new Set(built.nodes.map((n) => n.deviceId));
    const off = (devices.data ?? []).filter((d) => !mapped.has(d.id));
    return { ...built, offMap: off };
  }, [topology.data, devices.data]);

  const layout = useMemo(() => {
    // Colunas por camada (null por último), ordenadas por tipo+nome.
    const layers = [...new Set(nodes.map((n) => n.layer ?? -1))].sort((a, b) => a - b);
    const columns = layers.map((layer) => {
      const items = nodes
        .filter((n) => (n.layer ?? -1) === layer)
        .sort(
          (a, b) =>
            a.deviceType.localeCompare(b.deviceType) ||
            a.name.localeCompare(b.name, "pt-BR"),
        );
      return { layer, items };
    });

    const positions = new Map<string, { x: number; y: number; w: number; h: number }>();
    let maxRows = 1;
    columns.forEach((col, colIdx) => {
      maxRows = Math.max(maxRows, col.items.length);
      col.items.forEach((node, rowIdx) => {
        const h =
          HEADER_H +
          (2 + Math.min(node.interfaceCount, MAX_IFACE_ROWS)) * ROW_H +
          BOX_PAD;
        positions.set(node.deviceId, {
          x: MARGIN + colIdx * (BOX_W + COL_GAP),
          y:
            MARGIN +
            LAYER_LABEL_H +
            rowIdx * (h + ROW_GAP),
          w: BOX_W,
          h,
        });
      });
    });

    const width = MARGIN * 2 + columns.length * BOX_W + Math.max(0, columns.length - 1) * COL_GAP;
    const colHeights = columns.map((col) =>
      col.items.reduce((sum, n) => {
        const p = positions.get(n.deviceId)!;
        return sum + p.h + ROW_GAP;
      }, 0),
    );
    const height =
      MARGIN * 2 + LAYER_LABEL_H + Math.max(...colHeights, 80);

    return { positions, columns, width, height };
  }, [nodes]);

  if (topology.isLoading) return <LoadingBlock />;
  if (topology.isError)
    return (
      <ErrorBanner
        message="Falha ao carregar a topologia."
        onRetry={() => void topology.refetch()}
      />
    );

  const layerTitle = (layer: number) =>
    layer < 0 ? "Sem hierarquia" : `Camada ${layer}`;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Mapa ERD da infraestrutura"
        subtitle="Topologia real como diagrama entidade-relacionamento — caixas = equipamentos, linhas = conexões cadastradas"
      />

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        <StatusChip label={`${nodes.length} equipamentos`} background="#dbeafe" color="#1d4ed8" />
        <StatusChip label={`${edges.length} relacionamentos`} background="#ede9fe" color="#6d28d9" />
        <StatusChip
          label={`${offMap.length} fora do mapa`}
          background="#f1f5f9"
          color="#475569"
        />
      </div>

      {nodes.length === 0 ? (
        <div className="uni-panel" style={{ padding: 24, textAlign: "center", color: "#64748b", fontSize: 13 }}>
          Nenhuma interface mapeada ainda — cadastre devices com interfaces e conexões
          (ou execute o discovery) para o mapa aparecer.
        </div>
      ) : (
        <div className="uni-panel" style={{ padding: 8, overflow: "auto" }}>
          <svg
            width={layout.width}
            height={layout.height}
            role="img"
            aria-label="Diagrama entidade-relacionamento da topologia de rede"
          >
            {/* arestas primeiro (ficam atrás das caixas) */}
            {edges.map((edge) => {
              const a = layout.positions.get(edge.sourceDeviceId);
              const b = layout.positions.get(edge.targetDeviceId);
              if (!a || !b) return null;
              const aCol = layout.columns.findIndex((c) =>
                c.items.some((n) => n.deviceId === edge.sourceDeviceId),
              );
              const bCol = layout.columns.findIndex((c) =>
                c.items.some((n) => n.deviceId === edge.targetDeviceId),
              );
              // liga pela horizontal quando as colunas diferem; vertical
              // (topo→base) quando na mesma camada.
              const horizontal = aCol !== bCol;
              const x1 = horizontal ? a.x + a.w : a.x + a.w / 2;
              const y1 = horizontal ? a.y + a.h / 2 : a.y + a.h;
              const x2 = horizontal ? b.x : b.x + b.w / 2;
              const y2 = horizontal ? b.y + b.h / 2 : b.y;
              const dx = Math.max(50, Math.abs(x2 - x1) / 2) * (x2 >= x1 ? 1 : -1);
              const dy = horizontal ? 0 : Math.max(40, Math.abs(y2 - y1) / 2);
              const cx1 = horizontal ? x1 + dx : x1;
              const cy1 = horizontal ? y1 : y1 + dy;
              const cx2 = horizontal ? x2 - dx : x2;
              const cy2 = horizontal ? y2 : y2 - dy;
              const mx = (x1 + x2) / 2;
              const my = (y1 + y2) / 2;
              return (
                <g key={edge.id}>
                  <path
                    d={`M ${x1} ${y1} C ${cx1} ${cy1}, ${cx2} ${cy2}, ${x2} ${y2}`}
                    fill="none"
                    stroke="#94a3b8"
                    strokeWidth={1.4}
                  />
                  <text
                    x={mx}
                    y={my - 4}
                    textAnchor="middle"
                    fontSize={10}
                    fill="#64748b"
                  >
                    {edge.linkCount > 1 ? `${edge.linkCount} links` : "link"}
                    {edge.sources.length > 0 ? ` · ${edge.sources[0]}` : ""}
                  </text>
                </g>
              );
            })}

            {/* cabeçalhos de camada */}
            {layout.columns.map((col, idx) => (
              <text
                key={`layer-${col.layer}`}
                x={MARGIN + idx * (BOX_W + COL_GAP) + BOX_W / 2}
                y={MARGIN + 18}
                textAnchor="middle"
                fontSize={12}
                fontWeight={700}
                fill="#334155"
              >
                {layerTitle(col.layer)} · {col.items.length}
              </text>
            ))}

            {/* caixas-entidade */}
            {nodes.map((node) => {
              const p = layout.positions.get(node.deviceId);
              if (!p) return null;
              const color = typeColor(node.deviceType);
              const status = STATUS_STYLE[node.status ?? ""];
              const ifaceRows = node.interfaceNames.slice(0, MAX_IFACE_ROWS);
              const extra = node.interfaceCount - ifaceRows.length;
              let rowY = p.y + HEADER_H + BOX_PAD / 2;
              return (
                <g key={node.deviceId}>
                  <rect
                    x={p.x}
                    y={p.y}
                    width={p.w}
                    height={p.h}
                    rx={8}
                    fill="#ffffff"
                    stroke="#e2e8f0"
                  />
                  <rect
                    x={p.x}
                    y={p.y}
                    width={p.w}
                    height={HEADER_H}
                    rx={8}
                    fill={color.bg}
                  />
                  <rect
                    x={p.x}
                    y={p.y + HEADER_H - 8}
                    width={p.w}
                    height={8}
                    fill={color.bg}
                  />
                  <text
                    x={p.x + 12}
                    y={p.y + 22}
                    fontSize={12}
                    fontWeight={700}
                    fill={color.fg}
                  >
                    {node.name.length > 30 ? `${node.name.slice(0, 29)}…` : node.name}
                  </text>
                  <text
                    x={p.x + p.w - 12}
                    y={p.y + 22}
                    textAnchor="end"
                    fontSize={9}
                    fill={color.fg}
                  >
                    {node.deviceType}
                  </text>

                  {/* atributos (estilo DER) */}
                  <text x={p.x + 12} y={rowY + 10} fontSize={10} fill="#64748b">
                    ip: {node.ip ?? "—"}
                  </text>
                  <text x={p.x + 12} y={rowY + ROW_H + 10} fontSize={10} fill="#64748b">
                    status:{" "}
                    {node.status ? (
                      <tspan fontWeight={700} fill={status?.color ?? "#64748b"}>
                        {STATUS_LABEL[node.status] ?? node.status}
                      </tspan>
                    ) : (
                      "desconhecido"
                    )}
                  </text>
                  {ifaceRows.map((iface, i) => (
                    <text
                      key={iface}
                      x={p.x + 12}
                      y={rowY + (i + 2) * ROW_H + 10}
                      fontSize={9.5}
                      fill="#94a3b8"
                      fontFamily="monospace"
                    >
                      {iface}
                    </text>
                  ))}
                  {extra > 0 && (
                    <text
                      x={p.x + 12}
                      y={rowY + (ifaceRows.length + 2) * ROW_H + 10}
                      fontSize={9.5}
                      fill="#94a3b8"
                    >
                      +{extra} interface(s)
                    </text>
                  )}
                </g>
              );
            })}
          </svg>
        </div>
      )}

      {offMap.length > 0 && (
        <div className="uni-panel" style={{ padding: 14 }}>
          <div style={{ fontSize: 12, fontWeight: 700, color: "#334155", marginBottom: 8 }}>
            Equipamentos sem interfaces no mapa ({offMap.length})
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
            {offMap.slice(0, 40).map((d) => (
              <StatusChip
                key={d.id}
                label={`${d.name}${d.ip ? ` · ${d.ip}` : ""}`}
                background="#f1f5f9"
                color="#475569"
                fontSize={10}
              />
            ))}
            {offMap.length > 40 && (
              <StatusChip
                label={`+${offMap.length - 40} outros`}
                background="#f1f5f9"
                color="#64748b"
                fontSize={10}
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
}

