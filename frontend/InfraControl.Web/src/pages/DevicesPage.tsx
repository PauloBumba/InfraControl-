import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { allDevices, switchPortMap, sectorMap } from "@/data/deviceData";

const typeIcons: Record<string, string> = {
  gateway: "pi pi-th-large", switch: "pi pi-server", ap: "pi pi-wifi",
  printer: "pi pi-print", camera: "pi pi-video", phone: "pi pi-phone",
  server: "pi pi-desktop", vm: "pi pi-box", nas: "pi pi-database",
  ups: "pi pi-bolt", storage: "pi pi-database",
};
const typeColors: Record<string, string> = {
  gateway: "#3b82f6", switch: "#8b5cf6", ap: "#22c55e",
  printer: "#f59e0b", camera: "#ec4899", phone: "#06b6d4",
  server: "#3b82f6", vm: "#8b5cf6", nas: "#22c55e",
  ups: "#f59e0b", storage: "#3b82f6",
};
const typeNames: Record<string, string> = {
  gateway: "Gateway", switch: "Switch", ap: "Access Point",
  printer: "Impressora", camera: "Câmera", phone: "Telefone",
  server: "Servidor", vm: "Máquina Virtual", nas: "NAS",
  ups: "UPS", storage: "Armazenamento",
};
const portTypeColors: Record<string, string> = {
  gateway: "#3b82f6", switch: "#8b5cf6", ap: "#22c55e",
  printer: "#f59e0b", camera: "#ec4899", phone: "#06b6d4",
  server: "#3b82f6", nas: "#22c55e", ups: "#f59e0b", storage: "#3b82f6",
  client: "#94a3b8", empty: "#e5e7eb",
};

function TypeFilter({ types, active, onSelect }: { types: { key: string; label: string; count: number; color: string }[]; active: string; onSelect: (key: string) => void }) {
  return (
    <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
      <button onClick={() => onSelect("all")} style={{ padding: "5px 10px", borderRadius: 6, border: `1.5px solid ${active === "all" ? "#3b82f6" : "#e5e7eb"}`, background: active === "all" ? "#3b82f612" : "white", color: active === "all" ? "#3b82f6" : "#6b7280", fontSize: 11, fontWeight: 500, cursor: "pointer" }}>
        Todos ({allDevices.length})
      </button>
      {types.map(t => (
        <button key={t.key} onClick={() => onSelect(t.key)} style={{ padding: "5px 10px", borderRadius: 6, border: `1.5px solid ${active === t.key ? t.color : "#e5e7eb"}`, background: active === t.key ? `${t.color}12` : "white", color: active === t.key ? t.color : "#6b7280", fontSize: 11, fontWeight: 500, cursor: "pointer" }}>
          {t.label} ({t.count})
        </button>
      ))}
    </div>
  );
}

// ============ INVENTÁRIO TAB ============
function InventoryTab() {
  const navigate = useNavigate();
  const [filter, setFilter] = useState("all");

  const typeCounts: Record<string, number> = {};
  allDevices.forEach(d => { typeCounts[d.type] = (typeCounts[d.type] || 0) + 1; });
  const types = Object.entries(typeCounts).map(([key, count]) => ({ key, label: typeNames[key] || key, count, color: typeColors[key] || "#94a3b8" }));

  const filtered = filter === "all" ? allDevices : allDevices.filter(d => d.type === filter);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
      <TypeFilter types={types} active={filter} onSelect={setFilter} />
      <div className="uni-devices-grid">
        {filtered.map(device => (
          <div key={device.id} className="uni-device-card" onClick={() => navigate(`/devices/${device.id}`)}>
            <div className="uni-device-top">
              <div className={`uni-device-icon ${device.type}`} style={{ background: `${typeColors[device.type]}15`, color: typeColors[device.type] }}>
                <i className={typeIcons[device.type] || "pi pi-circle"} />
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div className="uni-device-name">{device.name}</div>
                <div className="uni-device-model">{device.model}</div>
              </div>
              <div className={`uni-device-status ${device.status}`}>
                <span className={`uni-device-status-dot ${device.status}`} />
                {device.status === "online" ? "Online" : device.status === "warning" ? "Atenção" : "Offline"}
              </div>
            </div>
            <div className="uni-device-details">
              <div className="uni-device-detail"><i className="pi pi-desktop" />{device.ip}</div>
              <div className="uni-device-detail"><i className="pi pi-map-marker" />{device.sector}</div>
              <div className="uni-device-detail"><i className="pi pi-clock" />{device.uptime}</div>
              {"vlan" in device && device.vlan && <div className="uni-device-detail"><i className="pi pi-tag" />VLAN {device.vlan}</div>}
            </div>
            <div className="uni-device-bottom">
              <div className="uni-device-stat"><i className="pi pi-code" />v{device.firmware}</div>
              {"switchName" in device && device.switchName && <div className="uni-device-stat"><i className="pi pi-arrow-right" />{device.switchName} P{"switchPort" in device ? device.switchPort : ""}</div>}
              {"storageName" in device && device.storageName && <div className="uni-device-stat"><i className="pi pi-database" />{device.storageName}</div>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ============ POR SETOR TAB ============
function SectorTab() {
  const navigate = useNavigate();
  const sectors = Object.entries(sectorMap);

  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(340px, 1fr))", gap: 12 }}>
      {sectors.map(([name, data]) => (
        <div key={name} className="uni-panel" style={{ overflow: "hidden" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "12px 16px", borderBottom: "1px solid #f3f4f6", background: `${data.color}06` }}>
            <div style={{ width: 32, height: 32, borderRadius: 6, background: `${data.color}15`, color: data.color, display: "flex", alignItems: "center", justifyContent: "center" }}>
              <i className={data.icon} />
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 13, fontWeight: 600 }}>{name}</div>
              <div style={{ fontSize: 10, color: "#9ca3af" }}>{data.description}</div>
            </div>
            <div style={{ display: "flex", gap: 6 }}>
              {data.online > 0 && <span style={{ fontSize: 10, padding: "2px 5px", borderRadius: 4, background: "#f0fdf4", color: "#16a34a" }}>{data.online} ●</span>}
              {data.warning > 0 && <span style={{ fontSize: 10, padding: "2px 5px", borderRadius: 4, background: "#fefce8", color: "#ca8a04" }}>{data.warning} ◐</span>}
              {data.offline > 0 && <span style={{ fontSize: 10, padding: "2px 5px", borderRadius: 4, background: "#fef2f2", color: "#dc2626" }}>{data.offline} ○</span>}
            </div>
          </div>
          <div className="uni-panel-body" style={{ padding: "10px 16px" }}>
            {data.devices.length > 0 && (
              <div style={{ marginBottom: 8 }}>
                <div style={{ fontSize: 10, color: "#9ca3af", textTransform: "uppercase", letterSpacing: 0.5, marginBottom: 6 }}>Dispositivos ({data.devices.length})</div>
                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                  {data.devices.map(d => (
                    <div key={d.id} onClick={() => navigate(`/devices/${d.id}`)} style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 8px", borderRadius: 4, cursor: "pointer", background: "#f9fafb", border: "1px solid #f3f4f6" }}>
                      <span style={{ width: 6, height: 6, borderRadius: "50%", background: d.status === "online" ? "#22c55e" : d.status === "warning" ? "#f59e0b" : "#ef4444", flexShrink: 0 }} />
                      <div style={{ width: 22, height: 22, borderRadius: 4, background: `${typeColors[d.type]}12`, color: typeColors[d.type], display: "flex", alignItems: "center", justifyContent: "center", fontSize: 10, flexShrink: 0 }}>
                        <i className={typeIcons[d.type]} />
                      </div>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontSize: 11, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{d.name}</div>
                        <div style={{ fontSize: 9, color: "#9ca3af", fontFamily: "monospace" }}>{d.ip}</div>
                      </div>
                      <span style={{ fontSize: 9, color: "#9ca3af" }}>{typeNames[d.type]}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
            {data.clients.length > 0 && (
              <div>
                <div style={{ fontSize: 10, color: "#9ca3af", textTransform: "uppercase", letterSpacing: 0.5, marginBottom: 6 }}>Clientes ({data.clients.length})</div>
                <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
                  {data.clients.map((c, i) => (
                    <div key={i} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 11 }}>
                      <i className={c.connection === "WiFi" ? "pi pi-wifi" : "pi pi-desktop"} style={{ fontSize: 9, color: c.connection === "WiFi" ? "#22c55e" : "#3b82f6" }} />
                      <span style={{ fontWeight: 500 }}>{c.name}</span>
                      <span style={{ color: "#9ca3af", fontFamily: "monospace", fontSize: 10 }}>{c.ip}</span>
                      {c.signal && <span style={{ fontSize: 9, color: c.signal > -50 ? "#22c55e" : c.signal > -65 ? "#f59e0b" : "#ef4444" }}>{c.signal} dBm</span>}
                      {c.port && <span style={{ fontSize: 9, color: "#9ca3af" }}>{c.port}</span>}
                    </div>
                  ))}
                </div>
              </div>
            )}
            {data.totalClients > 0 && data.clients.length === 0 && (
              <div style={{ fontSize: 11, color: "#9ca3af", textAlign: "center", padding: 8 }}>{data.totalClients} clientes via AP</div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

// ============ POR SWITCH TAB ============
function SwitchTab() {
  const switches = Object.entries(switchPortMap);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {switches.map(([swId, sw]) => (
        <div key={swId} className="uni-panel" style={{ overflow: "hidden" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "12px 16px", borderBottom: "1px solid #f3f4f6" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <div style={{ width: 36, height: 36, borderRadius: 8, background: "#8b5cf612", color: "#8b5cf6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <i className="pi pi-server" />
              </div>
              <div>
                <div style={{ fontSize: 14, fontWeight: 600 }}>{sw.name}</div>
                <div style={{ fontSize: 11, color: "#9ca3af" }}>{sw.model} · {sw.location} · {sw.rack}</div>
              </div>
            </div>
            <div style={{ display: "flex", gap: 12, fontSize: 11 }}>
              <span style={{ color: "#22c55e", fontWeight: 500 }}>{sw.activePorts} ativas</span>
              <span style={{ color: "#6b7280" }}>/ {sw.totalPorts} total</span>
              {sw.poePorts > 0 && <span style={{ color: "#f59e0b", fontWeight: 500 }}>⚡ {sw.poePorts} PoE</span>}
            </div>
          </div>
          <div className="uni-panel-body">
            <div className="uni-switch-grid">
              {sw.ports.map(p => {
                const isActive = p.status === "active";
                const isDisabled = p.status === "disabled";
                const hasPoe = p.poe;
                const borderColor = isDisabled ? "#f59e0b" : isActive ? portTypeColors[p.type] || "#22c55e" : "#d1d5db";
                const bgColor = isActive ? `${portTypeColors[p.type] || "#22c55e"}08` : isDisabled ? "#fefce8" : "#f9fafb";

                return (
                  <div key={p.port} className="uni-switch-port" style={{ borderColor, background: bgColor }}>
                    <div style={{ fontSize: 10, fontWeight: 700, color: isActive ? "#111827" : "#9ca3af" }}>P{p.port}</div>
                    {isActive && (
                      <>
                        <div style={{ width: 6, height: 6, borderRadius: "50%", background: "#22c55e", margin: "2px 0" }} />
                        <div style={{ fontSize: 8, fontWeight: 600, color: "#374151", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", maxWidth: "100%" }}>{p.device}</div>
                        <div style={{ fontSize: 7, color: "#9ca3af" }}>{p.speed}</div>
                        {hasPoe && <div style={{ fontSize: 7, color: "#f59e0b", fontWeight: 600 }}>⚡ PoE</div>}
                        <div style={{ fontSize: 7, color: "#6b7280" }}>VLAN {p.vlan}</div>
                      </>
                    )}
                    {isDisabled && <div style={{ fontSize: 7, color: "#f59e0b" }}>Disabled</div>}
                    {!isActive && !isDisabled && <div style={{ fontSize: 7, color: "#d1d5db" }}>Vazio</div>}
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ============ MAIN DEVICES PAGE ============
export function DevicesPage() {
  const [tab, setTab] = useState<"inventory" | "sector" | "switch">("inventory");

  const online = allDevices.filter(d => d.status === "online").length;
  const warning = allDevices.filter(d => d.status === "warning").length;
  const offline = allDevices.filter(d => d.status === "offline").length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Dispositivos</h1>
          <div style={{ display: "flex", gap: 12, marginTop: 4, fontSize: 12, color: "#6b7280" }}>
            <span>{allDevices.length} total</span>
            <span style={{ color: "#16a34a" }}>{online} online</span>
            {warning > 0 && <span style={{ color: "#ca8a04" }}>{warning} atenção</span>}
            {offline > 0 && <span style={{ color: "#dc2626" }}>{offline} offline</span>}
          </div>
        </div>
        <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          <i className="pi pi-plus" style={{ fontSize: 11 }} /> Adicionar
        </button>
      </div>

      {/* Tabs */}
      <div style={{ display: "flex", gap: 4, borderBottom: "2px solid #f3f4f6", paddingBottom: 0 }}>
        {[
          { key: "inventory" as const, label: "Inventário", icon: "pi pi-list" },
          { key: "sector" as const, label: "Por Setor", icon: "pi pi-map" },
          { key: "switch" as const, label: "Por Switch", icon: "pi pi-server" },
        ].map(t => (
          <button key={t.key} onClick={() => setTab(t.key)} style={{ display: "flex", alignItems: "center", gap: 6, padding: "8px 14px", border: "none", borderBottom: tab === t.key ? "2px solid #3b82f6" : "2px solid transparent", marginBottom: -2, background: "transparent", color: tab === t.key ? "#3b82f6" : "#6b7280", fontSize: 12, fontWeight: tab === t.key ? 600 : 400, cursor: "pointer" }}>
            <i className={t.icon} style={{ fontSize: 12 }} /> {t.label}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      {tab === "inventory" && <InventoryTab />}
      {tab === "sector" && <SectorTab />}
      {tab === "switch" && <SwitchTab />}
    </div>
  );
}
