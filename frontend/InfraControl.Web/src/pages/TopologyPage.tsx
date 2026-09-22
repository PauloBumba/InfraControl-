import { useState } from "react";
import { topologyNodes, topologyEdges, deviceSummary } from "@/data/topologyData";

const nodeIcons: Record<string, string> = {
  wan: "🌐", gateway: "🏠", switch: "🔌", ap: "📡", camera: "📷", phone: "📞",
  server: "🖥️", vm: "📦", nas: "💾", ups: "🔋", printer: "🖨️", client: "👤",
};
const nodeColors: Record<string, string> = {
  wan: "#64748b", gateway: "#3b82f6", switch: "#8b5cf6", ap: "#22c55e",
  camera: "#ec4899", phone: "#06b6d4", server: "#3b82f6", vm: "#8b5cf6",
  nas: "#22c55e", ups: "#f59e0b", printer: "#f59e0b", client: "#94a3b8",
};
const edgeColors: Record<string, string> = {
  wan: "#64748b", backbone: "#3b82f6", poe: "#f59e0b", access: "#94a3b8",
  server: "#3b82f6", virtual: "#8b5cf6", wifi: "#22c55e",
};

function CategoryCard({ label, count, icon, color, onClick, active }: { label: string; count: number; icon: string; color: string; onClick: () => void; active: boolean }) {
  return (
    <div onClick={onClick} style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 14px", borderRadius: 8, border: `2px solid ${active ? color : "#e5e7eb"}`, background: active ? `${color}10` : "white", cursor: "pointer", transition: "all 0.12s", minWidth: 0 }}>
      <div style={{ width: 32, height: 32, borderRadius: 6, background: `${color}15`, color, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 14, flexShrink: 0 }}>
        <i className={icon} />
      </div>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 16, fontWeight: 700 }}>{count}</div>
        <div style={{ fontSize: 10, color: "#9ca3af", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{label}</div>
      </div>
    </div>
  );
}

function LocationRow({ label, devices, count, color, expanded, onToggle }: { label: string; devices: string[]; count: number; color: string; expanded: boolean; onToggle: () => void }) {
  return (
    <div style={{ borderRadius: 6, border: "1px solid #f3f4f6", overflow: "hidden" }}>
      <div onClick={onToggle} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 12px", cursor: "pointer", background: expanded ? `${color}08` : "white", transition: "background 0.12s" }}>
        <span style={{ width: 8, height: 8, borderRadius: "50%", background: color, flexShrink: 0 }} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12, fontWeight: 500 }}>{label}</div>
        </div>
        <span style={{ fontSize: 11, padding: "2px 6px", borderRadius: 4, background: "#f3f4f6", color: "#6b7280", fontWeight: 500 }}>{count}</span>
        <i className={`pi pi-chevron-${expanded ? "up" : "down"}`} style={{ fontSize: 10, color: "#9ca3af" }} />
      </div>
      {expanded && (
        <div style={{ padding: "6px 12px 10px 30px", display: "flex", flexWrap: "wrap", gap: 4 }}>
          {devices.map((d, i) => (
            <span key={i} style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f9fafb", border: "1px solid #f3f4f6", color: "#6b7280" }}>{d}</span>
          ))}
        </div>
      )}
    </div>
  );
}

export function TopologyPage() {
  const [filter, setFilter] = useState<string>("all");
  const [zoom, setZoom] = useState(100);
  const [showPhysical, setShowPhysical] = useState(true);
  const [expandedLoc, setExpandedLoc] = useState<string | null>(null);

  const filteredNodes = filter === "all"
    ? topologyNodes
    : topologyNodes.filter(n => n.type === filter || n.type === "wan" || n.type === "gateway");

  const summary = deviceSummary;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Topologia de Rede</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{summary.total} dispositivos mapeados</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button onClick={() => setShowPhysical(!showPhysical)} style={{ padding: "6px 12px", borderRadius: 6, border: `1px solid ${showPhysical ? "#3b82f6" : "#e5e7eb"}`, fontSize: 12, color: showPhysical ? "#3b82f6" : "#6b7280", background: showPhysical ? "#eff6ff" : "white", cursor: "pointer" }}>
            <i className="pi pi-map" style={{ marginRight: 4 }} /> Físico
          </button>
          <button style={{ padding: "6px 12px", borderRadius: 6, border: "1px solid #e5e7eb", fontSize: 12, color: "#6b7280", background: "white", cursor: "pointer" }}>
            <i className="pi pi-refresh" style={{ marginRight: 4 }} /> Atualizar
          </button>
        </div>
      </div>

      {/* Stats */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12 }}>
        <div className="uni-panel" style={{ padding: 16, display: "flex", gap: 12 }}>
          <div style={{ width: 36, height: 36, borderRadius: 8, background: "#f0fdf4", color: "#16a34a", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-check-circle" /></div>
          <div><div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1 }}>{summary.byStatus.online}</div><div style={{ fontSize: 11, color: "#9ca3af" }}>Online</div></div>
        </div>
        <div className="uni-panel" style={{ padding: 16, display: "flex", gap: 12 }}>
          <div style={{ width: 36, height: 36, borderRadius: 8, background: "#fefce8", color: "#ca8a04", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-exclamation-triangle" /></div>
          <div><div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1 }}>{summary.byStatus.warning}</div><div style={{ fontSize: 11, color: "#9ca3af" }}>Atenção</div></div>
        </div>
        <div className="uni-panel" style={{ padding: 16, display: "flex", gap: 12 }}>
          <div style={{ width: 36, height: 36, borderRadius: 8, background: "#fef2f2", color: "#dc2626", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-times-circle" /></div>
          <div><div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1 }}>{summary.byStatus.offline}</div><div style={{ fontSize: 11, color: "#9ca3af" }}>Offline</div></div>
        </div>
      </div>

      {/* Category Filters */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))", gap: 8 }}>
        <CategoryCard label="Todos" count={summary.total} icon="pi pi-th-large" color="#0088ff" onClick={() => setFilter("all")} active={filter === "all"} />
        {summary.byType.map(t => (
          <CategoryCard key={t.type} label={t.label} count={t.count} icon={t.icon} color={t.color} onClick={() => setFilter(t.type)} active={filter === t.type} />
        ))}
      </div>

      {/* Main Area: Map + Sidebar */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 300px", gap: 12, minHeight: 500 }}>
        {/* Map */}
        <div className="uni-panel" style={{ overflow: "hidden" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "10px 16px", borderBottom: "1px solid #f3f4f6" }}>
            <h2 style={{ fontSize: 13, fontWeight: 600 }}>Mapa de Rede</h2>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <button onClick={() => setZoom(z => Math.max(z - 10, 50))} style={{ width: 24, height: 24, borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-minus" style={{ fontSize: 10 }} /></button>
              <span style={{ fontSize: 11, fontWeight: 500, minWidth: 32, textAlign: "center" }}>{zoom}%</span>
              <button onClick={() => setZoom(z => Math.min(z + 10, 150))} style={{ width: 24, height: 24, borderRadius: 4, border: "1px solid #e5e7eb", background: "white", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-plus" style={{ fontSize: 10 }} /></button>
            </div>
          </div>
          <div style={{ padding: 12, overflow: "auto", height: "calc(100% - 42px)" }}>
            <svg style={{ width: "100%", height: "100%", transform: `scale(${zoom / 100})`, transformOrigin: "top left" }} viewBox="0 0 1100 760">
              <defs>
                <filter id="glow"><feGaussianBlur stdDeviation="3" result="blur" /><feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge></filter>
              </defs>

              {/* Edges */}
              {topologyEdges.map((edge, i) => {
                const from = topologyNodes.find(n => n.id === edge.from);
                const to = topologyNodes.find(n => n.id === edge.to);
                if (!from || !to) return null;
                const show = filter === "all" || from.type === filter || to.type === filter || from.type === "wan" || to.type === "wan" || from.type === "gateway" || to.type === "gateway";
                if (!show) return null;
                const color = edgeColors[edge.type] || "#e2e8f0";
                return (
                  <g key={i}>
                    <line x1={from.x} y1={from.y} x2={to.x} y2={to.y} stroke={color} strokeWidth={edge.type === "backbone" ? 3 : 1.5} strokeDasharray={edge.type === "wifi" ? "4,4" : edge.type === "virtual" ? "2,3" : "none"} opacity={0.6} />
                    {edge.type !== "wifi" && edge.type !== "virtual" && (
                      <text x={(from.x + to.x) / 2} y={(from.y + to.y) / 2 - 5} textAnchor="middle" fill="#9ca3af" fontSize="8" fontFamily="Inter, sans-serif">{edge.label}</text>
                    )}
                  </g>
                );
              })}

              {/* Nodes */}
              {filteredNodes.map((node) => {
                const color = nodeColors[node.type] || "#94a3b8";
                const isSmall = node.type === "client";
                const r = isSmall ? 14 : node.type === "wan" ? 18 : 22;
                return (
                  <g key={node.id} style={{ cursor: "pointer" }}>
                    {node.status === "online" && (
                      <circle cx={node.x} cy={node.y} r={r + 6} fill={color} opacity="0.1">
                        <animate attributeName="r" values={`${r + 4};${r + 10};${r + 4}`} dur="3s" repeatCount="indefinite" />
                        <animate attributeName="opacity" values="0.12;0.04;0.12" dur="3s" repeatCount="indefinite" />
                      </circle>
                    )}
                    <circle cx={node.x} cy={node.y} r={r} fill="white" stroke={node.status === "offline" ? "#ef4444" : color} strokeWidth={node.status === "warning" ? 3 : 2} filter={node.status === "online" ? "url(#glow)" : undefined} />
                    <text x={node.x} y={node.y + 1} textAnchor="middle" dominantBaseline="central" fill={color} fontSize={isSmall ? 10 : 14}>
                      {nodeIcons[node.type] || "❓"}
                    </text>
                    <text x={node.x} y={node.y + r + 12} textAnchor="middle" fill="#374151" fontSize="10" fontWeight="600" fontFamily="Inter, sans-serif">
                      {node.label}
                    </text>
                    {"model" in node && node.model && (
                      <text x={node.x} y={node.y + r + 22} textAnchor="middle" fill="#9ca3af" fontSize="8" fontFamily="Inter, sans-serif">
                        {node.model}
                      </text>
                    )}
                    {"ip" in node && node.ip && (
                      <text x={node.x} y={node.y + r + 32} textAnchor="middle" fill="#6b7280" fontSize="8" fontFamily="monospace">
                        {node.ip}
                      </text>
                    )}
                  </g>
                );
              })}
            </svg>
          </div>
        </div>

        {/* Sidebar: Locations */}
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {/* Legend */}
          <div className="uni-panel" style={{ padding: 14 }}>
            <h3 style={{ fontSize: 12, fontWeight: 600, marginBottom: 10 }}>Legenda</h3>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 6 }}>
              {Object.entries(nodeColors).filter(([k]) => k !== "wan" || true).map(([type, color]) => (
                <div key={type} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10, color: "#6b7280" }}>
                  <span style={{ width: 8, height: 8, borderRadius: "50%", background: color, flexShrink: 0 }} />
                  <span style={{ textTransform: "capitalize" }}>{type}</span>
                </div>
              ))}
            </div>
            <div style={{ marginTop: 10, paddingTop: 10, borderTop: "1px solid #f3f4f6", display: "grid", gridTemplateColumns: "1fr 1fr", gap: 6 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#3b82f6", flexShrink: 0 }} /> Backbone</div>
              <div style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#f59e0b", flexShrink: 0 }} /> PoE</div>
              <div style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#22c55e", flexShrink: 0, borderTop: "2px dashed #22c55e" }} /> WiFi</div>
              <div style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#94a3b8", flexShrink: 0 }} /> Acesso</div>
            </div>
          </div>

          {/* Physical Locations */}
          <div className="uni-panel" style={{ padding: 14, flex: 1, overflow: "auto" }}>
            <h3 style={{ fontSize: 12, fontWeight: 600, marginBottom: 10 }}>Localização Física</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {summary.byLocation.map(loc => (
                <LocationRow
                  key={loc.location}
                  label={`${loc.label} — ${loc.location}`}
                  devices={loc.devices}
                  count={loc.count}
                  color={loc.color}
                  expanded={expandedLoc === loc.location}
                  onToggle={() => setExpandedLoc(expandedLoc === loc.location ? null : loc.location)}
                />
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
