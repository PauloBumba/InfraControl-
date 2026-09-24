import { useState } from "react";
import { topologyNodes, topologyEdges, deviceSummary, rackLayout, storageConnections, vlanBadges } from "@/data/topologyData";

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
const rackTypeColors: Record<string, string> = {
  gateway: "#3b82f6", switch: "#8b5cf6", ap: "#22c55e", camera: "#ec4899",
  phone: "#06b6d4", server: "#3b82f6", vm: "#8b5cf6", nas: "#22c55e",
  ups: "#f59e0b", printer: "#f59e0b", storage: "#3b82f6", client: "#94a3b8",
};

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

function RackVisual({ rackId, rack }: { rackId: string; rack: typeof rackLayout[string] }) {
  return (
    <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, overflow: "hidden" }}>
      <div style={{ padding: "10px 14px", background: "#f9fafb", borderBottom: "1px solid #e5e7eb", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <div style={{ fontSize: 12, fontWeight: 600 }}>{rackId} — {rack.label}</div>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>{rack.location}</div>
        </div>
        <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f3f4f6", color: "#6b7280" }}>{rack.units.length} devices</span>
      </div>
      <div style={{ padding: 8 }}>
        {Array.from({ length: rack.totalUnits }, (_, i) => {
          const unit = rack.units.find(u => u.u === i);
          return (
            <div key={i} style={{ display: "flex", alignItems: "center", gap: 8, padding: "4px 8px", marginBottom: 2, borderRadius: 4, background: unit ? `${rackTypeColors[unit.type]}08` : "#fafafa", border: unit ? `1px solid ${rackTypeColors[unit.type]}30` : "1px solid #f3f4f6" }}>
              <span style={{ fontSize: 9, color: "#9ca3af", minWidth: 20, fontFamily: "monospace" }}>U{i}</span>
              {unit ? (
                <>
                  <span style={{ width: 6, height: 6, borderRadius: "50%", background: unit.status === "online" ? "#22c55e" : unit.status === "warning" ? "#f59e0b" : "#ef4444", flexShrink: 0 }} />
                  <span style={{ fontSize: 10, fontWeight: 500, flex: 1 }}>{unit.name}</span>
                  <span style={{ fontSize: 8, color: "#9ca3af" }}>{unit.type}</span>
                </>
              ) : (
                <span style={{ fontSize: 9, color: "#d1d5db" }}>vazio</span>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

export function TopologyPage() {
  const [filter, setFilter] = useState<string>("all");
  const [zoom, setZoom] = useState(100);
  const [hoveredNode, setHoveredNode] = useState<string | null>(null);
  const [showRacks, setShowRacks] = useState(true);

  const summary = deviceSummary;
  const racks = Object.entries(rackLayout);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Topologia de Rede</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{summary.total} dispositivos mapeados · {storageConnections.length} conexões de storage</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button onClick={() => setShowRacks(!showRacks)} style={{ padding: "6px 12px", borderRadius: 6, border: `1px solid ${showRacks ? "#8b5cf6" : "#e5e7eb"}`, fontSize: 12, color: showRacks ? "#8b5cf6" : "#6b7280", background: showRacks ? "#f5f3ff" : "white", cursor: "pointer" }}>
            <i className="pi pi-th-large" style={{ marginRight: 4 }} /> Racks
          </button>
        </div>
      </div>

      {/* Stats */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12 }}>
        <div className="uni-panel" style={{ padding: 14, display: "flex", gap: 10 }}>
          <div style={{ width: 32, height: 32, borderRadius: 6, background: "#f0fdf4", color: "#16a34a", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-check-circle" /></div>
          <div><div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1 }}>{summary.byStatus.online}</div><div style={{ fontSize: 10, color: "#9ca3af" }}>Online</div></div>
        </div>
        <div className="uni-panel" style={{ padding: 14, display: "flex", gap: 10 }}>
          <div style={{ width: 32, height: 32, borderRadius: 6, background: "#fefce8", color: "#ca8a04", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-exclamation-triangle" /></div>
          <div><div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1 }}>{summary.byStatus.warning}</div><div style={{ fontSize: 10, color: "#9ca3af" }}>Atenção</div></div>
        </div>
        <div className="uni-panel" style={{ padding: 14, display: "flex", gap: 10 }}>
          <div style={{ width: 32, height: 32, borderRadius: 6, background: "#fef2f2", color: "#dc2626", display: "flex", alignItems: "center", justifyContent: "center" }}><i className="pi pi-times-circle" /></div>
          <div><div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1 }}>{summary.byStatus.offline}</div><div style={{ fontSize: 10, color: "#9ca3af" }}>Offline</div></div>
        </div>
      </div>

      {/* Category Filters */}
      <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
        <CategoryCard label="Todos" count={summary.total} icon="pi pi-th-large" color="#3b82f6" onClick={() => setFilter("all")} active={filter === "all"} />
        {summary.byType.map(t => (
          <CategoryCard key={t.type} label={t.label} count={t.count} icon={t.icon} color={t.color} onClick={() => setFilter(t.type)} active={filter === t.type} />
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
          <div style={{ padding: 10, overflow: "auto", height: "calc(100% - 40px)", position: "relative" }}>
            <svg style={{ width: "100%", height: "100%", transform: `scale(${zoom / 100})`, transformOrigin: "top left" }} viewBox="0 0 1100 760">
              <defs>
                <filter id="glow"><feGaussianBlur stdDeviation="2" result="blur" /><feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge></filter>
                <marker id="arrow" markerWidth="6" markerHeight="4" refX="6" refY="2" orient="auto"><polygon points="0 0, 6 2, 0 4" fill="#94a3b8" /></marker>
              </defs>

              {/* Storage connections (dashed) */}
              {storageConnections.map((conn, i) => {
                const from = topologyNodes.find(n => n.id === conn.from);
                const to = topologyNodes.find(n => n.id === conn.to);
                if (!from || !to) return null;
                return (
                  <g key={`storage-${i}`}>
                    <line x1={from.x} y1={from.y} x2={to.x} y2={to.y} stroke={conn.color} strokeWidth="1.5" strokeDasharray="4,4" opacity="0.5" />
                    <text x={(from.x + to.x) / 2} y={(from.y + to.y) / 2 - 4} textAnchor="middle" fill={conn.color} fontSize="7" fontFamily="Inter, sans-serif" opacity="0.7">{conn.label}</text>
                  </g>
                );
              })}

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
                    <line x1={from.x} y1={from.y} x2={to.x} y2={to.y} stroke={color} strokeWidth={edge.type === "backbone" ? 2.5 : 1.2} strokeDasharray={edge.type === "wifi" ? "4,3" : edge.type === "virtual" ? "2,2" : "none"} opacity={0.5} />
                    {edge.type !== "wifi" && edge.type !== "virtual" && (
                      <text x={(from.x + to.x) / 2} y={(from.y + to.y) / 2 - 4} textAnchor="middle" fill="#9ca3af" fontSize="7" fontFamily="Inter, sans-serif">{edge.label}</text>
                    )}
                  </g>
                );
              })}

              {/* Nodes */}
              {topologyNodes.filter(n => filter === "all" || n.type === filter || n.type === "wan" || n.type === "gateway").map((node) => {
                const color = nodeColors[node.type] || "#94a3b8";
                const isSmall = node.type === "client";
                const r = isSmall ? 12 : node.type === "wan" ? 16 : 20;
                const vlan = vlanBadges.find(v => v.deviceIds.includes(node.id));

                return (
                  <g key={node.id} style={{ cursor: "pointer" }} onMouseEnter={() => setHoveredNode(node.id)} onMouseLeave={() => setHoveredNode(null)}>
                    {node.status === "online" && (
                      <circle cx={node.x} cy={node.y} r={r + 5} fill={color} opacity="0.08">
                        <animate attributeName="r" values={`${r + 3};${r + 8};${r + 3}`} dur="3s" repeatCount="indefinite" />
                        <animate attributeName="opacity" values="0.1;0.03;0.1" dur="3s" repeatCount="indefinite" />
                      </circle>
                    )}
                    <circle cx={node.x} cy={node.y} r={r} fill="white" stroke={node.status === "offline" ? "#ef4444" : color} strokeWidth={node.status === "warning" ? 2.5 : 1.5} filter={node.status === "online" ? "url(#glow)" : undefined} />
                    <text x={node.x} y={node.y + 1} textAnchor="middle" dominantBaseline="central" fill={color} fontSize={isSmall ? 9 : 13}>{nodeIcons[node.type] || "❓"}</text>
                    <text x={node.x} y={node.y + r + 10} textAnchor="middle" fill="#374151" fontSize="9" fontWeight="600" fontFamily="Inter, sans-serif">{node.label}</text>
                    {"model" in node && node.model && <text x={node.x} y={node.y + r + 19} textAnchor="middle" fill="#9ca3af" fontSize="7" fontFamily="Inter, sans-serif">{node.model}</text>}
                    {"ip" in node && node.ip && <text x={node.x} y={node.y + r + 28} textAnchor="middle" fill="#6b7280" fontSize="7" fontFamily="monospace">{node.ip}</text>}

                    {/* VLAN badge */}
                    {vlan && (
                      <g>
                        <rect x={node.x + r - 2} y={node.y - r - 4} width={22} height={12} rx={3} fill={vlan.color} opacity="0.9" />
                        <text x={node.x + r + 9} y={node.y - r + 4} textAnchor="middle" fill="white" fontSize="6" fontWeight="600">V{vlan.id}</text>
                      </g>
                    )}

                    {/* Tooltip */}
                    {hoveredNode === node.id && (
                      <g>
                        <rect x={node.x + r + 8} y={node.y - 40} width={160} height={72} rx={6} fill="white" stroke="#e5e7eb" strokeWidth="1" filter="url(#glow)" />
                        <text x={node.x + r + 16} y={node.y - 24} fill="#111827" fontSize="9" fontWeight="600">{node.label}</text>
                        <text x={node.x + r + 16} y={node.y - 12} fill="#6b7280" fontSize="7">{"model" in node ? node.model : ""} · {node.ip || ""}</text>
                        <line x1={node.x + r + 12} y1={node.y - 6} x2={node.x + r + 156} y2={node.y - 6} stroke="#f3f4f6" />
                        <text x={node.x + r + 16} y={node.y + 6} fill="#6b7280" fontSize="7">Local: {"location" in node ? node.location : ""}</text>
                        <text x={node.x + r + 16} y={node.y + 16} fill="#6b7280" fontSize="7">Rack: {"rack" in node ? node.rack : ""}</text>
                        {vlan && <text x={node.x + r + 16} y={node.y + 26} fill={vlan.color} fontSize="7" fontWeight="500">VLAN {vlan.id} ({vlan.name})</text>}
                      </g>
                    )}
                  </g>
                );
              })}
            </svg>
          </div>
        </div>

        {/* Sidebar */}
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {/* Legend */}
          <div className="uni-panel" style={{ padding: 12 }}>
            <h3 style={{ fontSize: 11, fontWeight: 600, marginBottom: 8 }}>Legenda</h3>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 4 }}>
              {Object.entries(nodeColors).map(([type, color]) => (
                <div key={type} style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}>
                  <span style={{ width: 7, height: 7, borderRadius: "50%", background: color, flexShrink: 0 }} />
                  <span style={{ textTransform: "capitalize" }}>{type}</span>
                </div>
              ))}
            </div>
            <div style={{ marginTop: 8, paddingTop: 8, borderTop: "1px solid #f3f4f6" }}>
              <div style={{ fontSize: 9, fontWeight: 600, color: "#9ca3af", marginBottom: 4 }}>Conexões</div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: 3 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#3b82f6", flexShrink: 0 }} /> Backbone</div>
                <div style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#f59e0b", flexShrink: 0 }} /> PoE</div>
                <div style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#22c55e", flexShrink: 0 }} /> WiFi</div>
                <div style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 9, color: "#6b7280" }}><span style={{ width: 12, height: 2, background: "#ec4899", flexShrink: 0, borderTop: "2px dashed #ec4899" }} /> Storage</div>
              </div>
            </div>
          </div>

          {/* VLANs */}
          <div className="uni-panel" style={{ padding: 12 }}>
            <h3 style={{ fontSize: 11, fontWeight: 600, marginBottom: 8 }}>VLANs</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
              {vlanBadges.map(v => (
                <div key={v.id} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10 }}>
                  <span style={{ width: 8, height: 8, borderRadius: 2, background: v.color, flexShrink: 0 }} />
                  <span style={{ fontWeight: 500 }}>VLAN {v.id}</span>
                  <span style={{ color: "#9ca3af" }}>{v.name}</span>
                  <span style={{ marginLeft: "auto", fontSize: 9, color: "#9ca3af" }}>{v.deviceIds.length} devs</span>
                </div>
              ))}
            </div>
          </div>

          {/* Storage */}
          <div className="uni-panel" style={{ padding: 12 }}>
            <h3 style={{ fontSize: 11, fontWeight: 600, marginBottom: 8 }}>Storage</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
              {storageConnections.map((c, i) => (
                <div key={i} style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 9 }}>
                  <span style={{ width: 6, height: 6, borderRadius: "50%", background: c.color, flexShrink: 0 }} />
                  <span style={{ fontWeight: 500 }}>{topologyNodes.find(n => n.id === c.from)?.label}</span>
                  <i className="pi pi-arrow-right" style={{ fontSize: 7, color: "#9ca3af" }} />
                  <span style={{ fontWeight: 500 }}>{topologyNodes.find(n => n.id === c.to)?.label}</span>
                  <span style={{ marginLeft: "auto", color: "#9ca3af" }}>{c.label}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Rack View */}
      {showRacks && (
        <div>
          <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 12 }}>Visualização por Rack</h2>
          <div style={{ display: "grid", gridTemplateColumns: `repeat(${racks.length}, 1fr)`, gap: 12 }}>
            {racks.map(([id, rack]) => (
              <RackVisual key={id} rackId={id} rack={rack} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
