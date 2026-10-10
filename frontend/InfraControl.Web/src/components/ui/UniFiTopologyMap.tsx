import {
  useMemo,
  useRef,
  useState,
  type PointerEvent as RPointerEvent,
  type WheelEvent as RWheelEvent,
} from "react";
import { Button } from "@/components/ui/Button";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import type { UniFiTopology, UniFiTopologyNode } from "@/services/api/unifi";

const NODE_W = 130;
const NODE_H = 46;
const LAYER_GAP = 130;
const COL_GAP = 160;
const KIND_ORDER: Record<string, number> = { gateway: 0, switch: 1, ap: 2, other: 3, external: 4 };

interface Positioned extends UniFiTopologyNode {
  x: number;
  y: number;
}

function layout(nodes: UniFiTopologyNode[], topo: UniFiTopology): Positioned[] {
  const macs = new Set(nodes.map((n) => n.mac));
  const hasUplink = new Set(topo.links.map((l) => l.sourceMac));
  const roots = nodes.filter((n) => !hasUplink.has(n.mac));
  const depth = new Map<string, number>();
  const queue = roots.map((n) => n.mac);
  queue.forEach((m) => depth.set(m, 0));
  const edges = topo.links.filter((l) => macs.has(l.sourceMac) && macs.has(l.targetMac));
  while (queue.length > 0) {
    const current = queue.shift() as string;
    const d = depth.get(current) ?? 0;
    for (const l of edges.filter((e) => e.targetMac === current)) {
      if (!depth.has(l.sourceMac)) {
        depth.set(l.sourceMac, d + 1);
        queue.push(l.sourceMac);
      }
    }
  }
  const byLayer = new Map<number, UniFiTopologyNode[]>();
  for (const n of nodes) {
    const d = depth.get(n.mac) ?? (KIND_ORDER[n.kind] ?? 5);
    const layer = byLayer.get(d) ?? [];
    layer.push(n);
    byLayer.set(d, layer);
  }
  const result: Positioned[] = [];
  const layers = [...byLayer.keys()].sort((a, b) => a - b);
  for (const d of layers) {
    const layer = (byLayer.get(d) ?? []).sort((a, b) => {
      const ka = KIND_ORDER[a.kind] ?? 5;
      const kb = KIND_ORDER[b.kind] ?? 5;
      return ka === kb ? a.mac.localeCompare(b.mac) : ka - kb;
    });
    layer.forEach((n, i) => {
      result.push({ ...n, x: 40 + i * COL_GAP, y: 40 + d * LAYER_GAP });
    });
  }
  return result;
}

const CONFIDENCE_COLOR: Record<string, string> = {
  confirmed: "#15803d",
  inferred: "#854d0e",
  unknown: "#64748b",
};

const KIND_COLOR: Record<string, string> = {
  gateway: "#1d4ed8",
  switch: "#0f766e",
  ap: "#7c3aed",
  external: "#64748b",
  other: "#475569",
};

export function UniFiTopologyMap({
  topology,
  macToId,
}: {
  topology: UniFiTopology;
  macToId: Map<string, string>;
}) {
  const positioned = useMemo(
    () => layout(topology.nodes, topology),
    [topology],
  );
  const posByMac = useMemo(
    () => new Map(positioned.map((p) => [p.mac, p])),
    [positioned],
  );
  const [selected, setSelected] = useState<string | null>(null);
  const [term, setTerm] = useState("");
  const [scale, setScale] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const drag = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);

  const width = Math.max(...positioned.map((p) => p.x), 0) + NODE_W + 80;
  const height = Math.max(...positioned.map((p) => p.y), 0) + NODE_H + 80;
  const normalizedTerm = term.trim().toLowerCase().replace(/:/g, "");
  const selectedNode = positioned.find((n) => n.mac === selected) ?? null;
  const selectedLinks = topology.links.filter(
    (l) => l.sourceMac === selected || l.targetMac === selected,
  );

  const onWheel = (e: RWheelEvent) => {
    e.preventDefault();
    setScale((s) => Math.min(3, Math.max(0.4, s * (e.deltaY < 0 ? 1.1 : 0.9))));
  };
  const onPointerDown = (e: RPointerEvent) => {
    drag.current = { x: e.clientX, y: e.clientY, ox: offset.x, oy: offset.y };
    (e.target as Element).setPointerCapture?.(e.pointerId);
  };
  const onPointerMove = (e: RPointerEvent) => {
    if (!drag.current) return;
    setOffset({
      x: drag.current.ox + (e.clientX - drag.current.x),
      y: drag.current.oy + (e.clientY - drag.current.y),
    });
  };
  const onPointerUp = () => {
    drag.current = null;
  };

  return (
    <div>
      <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap", marginBottom: 8 }}>
        <span style={{ minWidth: 220 }}>
          <SearchInput value={term} onChange={setTerm} placeholder="Buscar device, MAC ou tipo" />
        </span>
        <Button variant="ghost" onClick={() => { setScale(1); setOffset({ x: 0, y: 0 }); }}>
          Resetar vista
        </Button>
        <span style={{ fontSize: 12, color: "#64748b" }}>
          zoom {Math.round(scale * 100)}% — role para ampliar, arraste para deslocar
        </span>
      </div>

      <div
        style={{
          border: "1px solid #e5e7eb",
          borderRadius: 8,
          overflow: "hidden",
          background: "#f8fafc",
          cursor: drag.current ? "grabbing" : "grab",
          height: 420,
        }}
      >
        <svg
          width="100%"
          height="420"
          viewBox={`0 0 ${Math.max(width / scale, 100)} ${Math.max(height / scale, 100)}`}
          onWheel={onWheel}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
        >
          <g transform={`translate(${offset.x},${offset.y}) scale(${scale})`}>
            {topology.links.map((l, i) => {
              const a = posByMac.get(l.sourceMac);
              const b = posByMac.get(l.targetMac);
              if (!a || !b) return null;
              const color = CONFIDENCE_COLOR[l.confidence] ?? CONFIDENCE_COLOR.unknown;
              return (
                <g key={`${l.sourceMac}-${l.targetMac}-${i}`}>
                  <line
                    x1={a.x + NODE_W / 2}
                    y1={a.y + NODE_H / 2}
                    x2={b.x + NODE_W / 2}
                    y2={b.y + NODE_H / 2}
                    stroke={color}
                    strokeWidth={selected && (l.sourceMac === selected || l.targetMac === selected) ? 3 : 1.5}
                    strokeDasharray={l.confidence === "confirmed" ? undefined : "6 4"}
                  />
                  <text
                    x={(a.x + b.x) / 2 + NODE_W / 2}
                    y={(a.y + b.y) / 2 + NODE_H / 2 - 4}
                    fontSize={10}
                    fill={color}
                    textAnchor="middle"
                  >
                    {l.sourcePort ? `P${l.sourcePort}` : l.confidence}
                  </text>
                </g>
              );
            })}
            {positioned.map((n) => {
              const matched =
                normalizedTerm === "" ||
                n.mac.replace(/:/g, "").includes(normalizedTerm) ||
                (n.name ?? "").toLowerCase().includes(normalizedTerm);
              const dim = normalizedTerm !== "" && !matched;
              return (
                <g
                  key={n.mac}
                  opacity={dim ? 0.25 : 1}
                  onPointerUp={() => setSelected(n.mac)}
                  style={{ cursor: "pointer" }}
                >
                  <rect
                    x={n.x}
                    y={n.y}
                    width={NODE_W}
                    height={NODE_H}
                    rx={8}
                    fill={selected === n.mac ? "#eff6ff" : "#ffffff"}
                    stroke={selected === n.mac ? "#3b82f6" : (KIND_COLOR[n.kind] ?? "#94a3b8")}
                    strokeWidth={n.isKnownDevice ? 1.5 : 1}
                    strokeDasharray={n.isKnownDevice ? undefined : "4 3"}
                  />
                  <text x={n.x + 8} y={n.y + 18} fontSize={12} fontWeight={600} fill="#0f172a">
                    {(n.name ?? n.mac).slice(0, 16)}
                  </text>
                  <text x={n.x + 8} y={n.y + 33} fontSize={10} fill="#64748b">
                    {n.kind} · {n.mac.replace(/:/g, "").match(/.{1,2}/g)?.join(":")}
                  </text>
                </g>
              );
            })}
          </g>
        </svg>
      </div>

      {selectedNode && (
        <div
          style={{
            marginTop: 8,
            padding: 12,
            border: "1px solid #e5e7eb",
            borderRadius: 8,
            background: "#fff",
            fontSize: 13,
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <strong>{selectedNode.name ?? selectedNode.mac}</strong>
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <StatusChip label={selectedNode.kind} background="#f1f5f9" color={KIND_COLOR[selectedNode.kind] ?? "#475569"} />
              {macToId.get(selectedNode.mac) && (
                <a href={`/devices/${macToId.get(selectedNode.mac)}`} style={{ fontSize: 12 }}>
                  Ir para detalhes →
                </a>
              )}
              <Button variant="ghost" onClick={() => setSelected(null)}>Fechar</Button>
            </div>
          </div>
          <p style={{ margin: "6px 0 0", color: "#64748b" }}>
            {selectedNode.type} · {selectedNode.mac} ·{" "}
            {selectedNode.isKnownDevice ? "device do controller" : "alvo externo (não é device do controller)"}
          </p>
          <ul style={{ margin: "8px 0 0", paddingLeft: 18 }}>
            {selectedLinks.length === 0 && <li>Sem relações conhecidas.</li>}
            {selectedLinks.map((l, i) => (
              <li key={i}>
                {l.sourceMac === selectedNode.mac
                  ? `→ ${l.targetName ?? l.targetMac}${l.sourcePort ? ` (P${l.sourcePort})` : ""}`
                  : `← ${l.sourceName ?? l.sourceMac}${l.sourcePort ? ` (P${l.sourcePort})` : ""}`}
                {" — "}
                <span style={{ color: CONFIDENCE_COLOR[l.confidence] }}>{l.confidence}</span>
                {" / "}
                {l.source}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
