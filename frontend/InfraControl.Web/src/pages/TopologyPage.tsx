import { useState } from "react";
import { topologyNodes, topologyEdges } from "@/data/dashboardData";

const nodeColors: Record<string, string> = {
  wan: "#3b82f6",
  gateway: "#3b82f6",
  switch: "#8b5cf6",
  ap: "#22c55e",
  client: "#94a3b8",
};

export function TopologyPage() {
  const [filter, setFilter] = useState<string>("all");
  const [zoom] = useState(100);

  const filteredNodes = filter === "all" ? topologyNodes : topologyNodes.filter(n => n.type === filter || n.type === "wan" || n.type === "gateway");

  return (
    <div className="uni-topology-container">
      <div className="uni-topo-controls">
        <div className="uni-topo-filter-group">
          {["all", "gateway", "switch", "ap", "client"].map((f) => (
            <button
              key={f}
              className={`uni-topo-filter ${filter === f ? "active" : ""}`}
              onClick={() => setFilter(f)}
            >
              {f === "all" ? "Todos" : f === "gateway" ? "Gateway" : f === "switch" ? "Switches" : f === "ap" ? "APs" : "Clientes"}
            </button>
          ))}
        </div>
        <div className="uni-topo-zoom">
          <button><i className="pi pi-minus" /></button>
          <span>{zoom}%</span>
          <button><i className="pi pi-plus" /></button>
        </div>
      </div>

      <div className="uni-topo-canvas">
        <svg viewBox="0 0 800 600" preserveAspectRatio="xMidYMid meet">
          {/* Edges */}
          {topologyEdges.map((edge, i) => {
            const from = topologyNodes.find(n => n.id === edge.from);
            const to = topologyNodes.find(n => n.id === edge.to);
            if (!from || !to) return null;
            if (!filteredNodes.find(n => n.id === edge.from) || !filteredNodes.find(n => n.id === edge.to)) return null;
            return (
              <line
                key={i}
                x1={from.x}
                y1={from.y}
                x2={to.x}
                y2={to.y}
                stroke="#e2e8f0"
                strokeWidth="2"
                strokeDasharray={edge.from === "wan" ? "6,4" : "none"}
              />
            );
          })}

          {/* Nodes */}
          {filteredNodes.map((node) => {
            const color = nodeColors[node.type] || "#94a3b8";
            const isSmall = node.type === "client";
            const r = isSmall ? 16 : 24;

            return (
              <g key={node.id} style={{ cursor: "pointer" }}>
                {/* Glow for online */}
                {node.status === "online" && (
                  <circle cx={node.x} cy={node.y} r={r + 4} fill={color} opacity="0.1">
                    <animate attributeName="r" values={`${r + 2};${r + 8};${r + 2}`} dur="3s" repeatCount="indefinite" />
                    <animate attributeName="opacity" values="0.15;0.05;0.15" dur="3s" repeatCount="indefinite" />
                  </circle>
                )}

                {/* Node circle */}
                <circle
                  cx={node.x}
                  cy={node.y}
                  r={r}
                  fill="white"
                  stroke={color}
                  strokeWidth="2"
                />

                {/* Icon text */}
                <text
                  x={node.x}
                  y={node.y + 1}
                  textAnchor="middle"
                  dominantBaseline="central"
                  fill={color}
                  fontSize={isSmall ? 10 : 14}
                  fontFamily="PrimeIcons"
                >
                  {node.type === "wan" ? "🌐" : node.type === "gateway" ? "🏠" : node.type === "switch" ? "🔌" : node.type === "ap" ? "📡" : "👤"}
                </text>

                {/* Label */}
                <text
                  x={node.x}
                  y={node.y + r + 14}
                  textAnchor="middle"
                  fill="#475569"
                  fontSize="11"
                  fontWeight="500"
                  fontFamily="Inter, sans-serif"
                >
                  {node.label}
                </text>

                {/* Model subtitle */}
                {"model" in node && node.model && (
                  <text
                    x={node.x}
                    y={node.y + r + 26}
                    textAnchor="middle"
                    fill="#94a3b8"
                    fontSize="9"
                    fontFamily="Inter, sans-serif"
                  >
                    {node.model}
                  </text>
                )}
              </g>
            );
          })}
        </svg>

        <div className="uni-topo-legend">
          <div className="uni-topo-legend-item">
            <span className="uni-topo-legend-dot" style={{ background: "#3b82f6" }} /> Gateway
          </div>
          <div className="uni-topo-legend-item">
            <span className="uni-topo-legend-dot" style={{ background: "#8b5cf6" }} /> Switch
          </div>
          <div className="uni-topo-legend-item">
            <span className="uni-topo-legend-dot" style={{ background: "#22c55e" }} /> AP
          </div>
          <div className="uni-topo-legend-item">
            <span className="uni-topo-legend-dot" style={{ background: "#94a3b8" }} /> Cliente
          </div>
        </div>
      </div>
    </div>
  );
}
