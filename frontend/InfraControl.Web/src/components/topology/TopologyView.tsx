import { useState, useRef, useCallback, useEffect } from "react";
import { Tag } from "primereact/tag";

type NodeType = "gateway" | "switch" | "ap" | "client";
type NodeStatus = "online" | "offline" | "warning";

interface TopoNode {
  id: string;
  label: string;
  type: NodeType;
  status: NodeStatus;
  x: number;
  y: number;
  details?: string;
}

interface TopoEdge {
  from: string;
  to: string;
  label?: string;
  speed?: string;
  status: "active" | "inactive";
}

const topoNodes: TopoNode[] = [
  { id: "gw", label: "UDM Pro Max", type: "gateway", status: "online", x: 400, y: 60, details: "192.168.1.1" },
  { id: "sw1", label: "US-24-250W", type: "switch", status: "online", x: 250, y: 200, details: "192.168.1.2" },
  { id: "sw2", label: "US-8-150W", type: "switch", status: "online", x: 550, y: 200, details: "192.168.1.3" },
  { id: "ap1", label: "AP-AC-PRO", type: "ap", status: "online", x: 120, y: 360, details: "28 clients" },
  { id: "ap2", label: "AP-AC-LR", type: "ap", status: "online", x: 320, y: 360, details: "15 clients" },
  { id: "ap3", label: "AP-U6-Lite", type: "ap", status: "warning", x: 520, y: 360, details: "Adopting" },
  { id: "c1", label: "iPhone 15", type: "client", status: "online", x: 60, y: 500 },
  { id: "c2", label: "MacBook", type: "client", status: "online", x: 160, y: 500 },
  { id: "c3", label: "Samsung TV", type: "client", status: "online", x: 280, y: 500 },
  { id: "c4", label: "Work PC", type: "client", status: "online", x: 380, y: 500 },
  { id: "c5", label: "iPad", type: "client", status: "offline", x: 500, y: 500 },
  { id: "c6", label: "Ring Camera", type: "client", status: "online", x: 600, y: 500 },
];

const topoEdges: TopoEdge[] = [
  { from: "gw", to: "sw1", label: "10G", speed: "10G SFP+", status: "active" },
  { from: "gw", to: "sw2", label: "10G", speed: "10G SFP+", status: "active" },
  { from: "sw1", to: "ap1", label: "1G", speed: "PoE", status: "active" },
  { from: "sw1", to: "ap2", label: "1G", speed: "PoE", status: "active" },
  { from: "sw2", to: "ap3", label: "1G", speed: "PoE", status: "active" },
  { from: "ap1", to: "c1", status: "active" },
  { from: "ap1", to: "c2", status: "active" },
  { from: "ap2", to: "c3", status: "active" },
  { from: "ap2", to: "c4", status: "active" },
  { from: "ap3", to: "c5", status: "inactive" },
  { from: "ap3", to: "c6", status: "active" },
];

const nodeColors: Record<NodeType, { fill: string; stroke: string; icon: string }> = {
  gateway: { fill: "#3b82f6", stroke: "#2563eb", icon: "M12 2L2 22h20L12 2z" },
  switch: { fill: "#8b5cf6", stroke: "#7c3aed", icon: "M3 3h18v14H3V3zm2 4h2v2H5V7zm4 0h2v2H9V7zm4 0h2v2h-2V7zm4 0h2v2h-2V7zm-8 4h6v2H9v-2z" },
  ap: { fill: "#22c55e", stroke: "#16a34a", icon: "M12 18a2 2 0 100 4 2 2 0 000-4zm-4-4a6 6 0 0112 0" },
  client: { fill: "#64748b", stroke: "#475569", icon: "M4 4h16v12H4V4zm2 2v8h12V6H6z" },
};

const statusGlow: Record<NodeStatus, string> = {
  online: "rgba(34, 197, 94, 0.4)",
  warning: "rgba(245, 158, 11, 0.4)",
  offline: "rgba(239, 68, 68, 0.4)",
};

function TopoNodeElement({ node, onHover, onLeave }: { node: TopoNode; onHover: (n: TopoNode) => void; onLeave: () => void }) {
  const colors = nodeColors[node.type];
  const isSmall = node.type === "client";
  const size = isSmall ? 24 : 40;

  return (
    <g
      transform={`translate(${node.x}, ${node.y})`}
      onMouseEnter={() => onHover(node)}
      onMouseLeave={onLeave}
      className="topo-node"
    >
      {/* Glow */}
      {node.status === "online" && (
        <circle cx={0} cy={0} r={size / 2 + 4} fill={statusGlow[node.status]} className="topo-glow" />
      )}

      {/* Main circle */}
      <circle cx={0} cy={0} r={size / 2} fill={colors.fill} stroke={colors.stroke} strokeWidth="2" />

      {/* Icon */}
      <path d={colors.icon} fill="white" transform={`translate(${-size / 4}, ${-size / 4}) scale(${size / 48})`} />

      {/* Status dot */}
      <circle cx={size / 2 - 2} cy={-size / 2 + 2} r="4" fill={node.status === "online" ? "#22c55e" : node.status === "warning" ? "#f59e0b" : "#ef4444"} stroke="white" strokeWidth="1.5" />

      {/* Label */}
      <text y={size / 2 + 14} textAnchor="middle" fill="var(--text)" fontSize="11" fontWeight="600">
        {node.label}
      </text>
      {node.details && (
        <text y={size / 2 + 26} textAnchor="middle" fill="var(--text-muted)" fontSize="9">
          {node.details}
        </text>
      )}
    </g>
  );
}

function TopoEdgeElement({ from, to, edge }: { from: TopoNode; to: TopoNode; edge: TopoEdge }) {
  return (
    <line
      x1={from.x}
      y1={from.y}
      x2={to.x}
      y2={to.y}
      stroke={edge.status === "active" ? "var(--status-green)" : "var(--text-muted)"}
      strokeWidth="2"
      strokeDasharray={edge.status === "inactive" ? "6,4" : "none"}
      className="topo-edge"
    />
  );
}

export function TopologyView() {
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState(false);
  const [startPos, setStartPos] = useState({ x: 0, y: 0 });
  const [hoveredNode, setHoveredNode] = useState<TopoNode | null>(null);
  const [filter, setFilter] = useState<NodeType | null>(null);
  const svgRef = useRef<SVGSVGElement>(null);

  const filteredNodes = filter ? topoNodes.filter((n) => n.type === filter) : topoNodes;
  const filteredIds = new Set(filteredNodes.map((n) => n.id));
  const filteredEdges = topoEdges.filter((e) => filteredIds.has(e.from) && filteredIds.has(e.to));

  const nodeMap = new Map(topoNodes.map((n) => [n.id, n]));

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    if (e.target === svgRef.current) {
      setIsPanning(true);
      setStartPos({ x: e.clientX - pan.x, y: e.clientY - pan.y });
    }
  }, [pan]);

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    if (isPanning) {
      setPan({ x: e.clientX - startPos.x, y: e.clientY - startPos.y });
    }
  }, [isPanning, startPos]);

  const handleMouseUp = useCallback(() => {
    setIsPanning(false);
  }, []);

  const handleWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.1 : 0.1;
    setZoom((z) => Math.min(Math.max(z + delta, 0.3), 3));
  }, []);

  useEffect(() => {
    const svg = svgRef.current;
    if (svg) {
      svg.addEventListener("wheel", handleWheel as unknown as EventListener, { passive: false });
      return () => svg.removeEventListener("wheel", handleWheel as unknown as EventListener);
    }
  }, [handleWheel]);

  return (
    <article className="panel topology-panel">
      <div className="panel-heading">
        <div>
          <h2>Network Topology</h2>
          <p>Interactive network map</p>
        </div>
        <div className="topology-controls">
          <div className="topo-filters">
            <button className={`topo-filter ${filter === null ? "active" : ""}`} onClick={() => setFilter(null)}>All</button>
            <button className={`topo-filter ${filter === "gateway" ? "active" : ""}`} onClick={() => setFilter("gateway")}>
              <i className="pi pi-direction" /> Gateway
            </button>
            <button className={`topo-filter ${filter === "switch" ? "active" : ""}`} onClick={() => setFilter("switch")}>
              <i className="pi pi-server" /> Switch
            </button>
            <button className={`topo-filter ${filter === "ap" ? "active" : ""}`} onClick={() => setFilter("ap")}>
              <i className="pi pi-wifi" /> AP
            </button>
            <button className={`topo-filter ${filter === "client" ? "active" : ""}`} onClick={() => setFilter("client")}>
              <i className="pi pi-user" /> Client
            </button>
          </div>
          <div className="topo-zoom">
            <button onClick={() => setZoom((z) => Math.min(z + 0.2, 3))} aria-label="Zoom in">
              <i className="pi pi-plus" />
            </button>
            <span>{Math.round(zoom * 100)}%</span>
            <button onClick={() => setZoom((z) => Math.max(z - 0.2, 0.3))} aria-label="Zoom out">
              <i className="pi pi-minus" />
            </button>
            <button onClick={() => { setZoom(1); setPan({ x: 0, y: 0 }); }} aria-label="Reset view">
              <i className="pi pi-refresh" />
            </button>
          </div>
        </div>
      </div>

      <div className="topology-canvas">
        <svg
          ref={svgRef}
          viewBox="0 0 800 560"
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onMouseLeave={handleMouseUp}
          style={{ cursor: isPanning ? "grabbing" : "grab" }}
        >
          <g transform={`translate(${pan.x}, ${pan.y}) scale(${zoom})`}>
            {/* Edges */}
            {filteredEdges.map((edge) => {
              const from = nodeMap.get(edge.from);
              const to = nodeMap.get(edge.to);
              if (!from || !to) return null;
              return <TopoEdgeElement key={`${edge.from}-${edge.to}`} from={from} to={to} edge={edge} />;
            })}

            {/* Nodes */}
            {filteredNodes.map((node) => (
              <TopoNodeElement
                key={node.id}
                node={node}
                onHover={setHoveredNode}
                onLeave={() => setHoveredNode(null)}
              />
            ))}
          </g>
        </svg>

        {/* Hover tooltip */}
        {hoveredNode && (
          <div className="topo-tooltip">
            <Tag value={hoveredNode.type} severity={hoveredNode.status === "online" ? "success" : hoveredNode.status === "warning" ? "warning" : "danger"} />
            <strong>{hoveredNode.label}</strong>
            {hoveredNode.details && <span>{hoveredNode.details}</span>}
          </div>
        )}

        {/* Legend */}
        <div className="topo-legend">
          {Object.entries(nodeColors).map(([type, colors]) => (
            <span key={type} className="topo-legend-item">
              <span className="topo-legend-dot" style={{ background: colors.fill }} />
              {type.charAt(0).toUpperCase() + type.slice(1)}
            </span>
          ))}
        </div>
      </div>
    </article>
  );
}
