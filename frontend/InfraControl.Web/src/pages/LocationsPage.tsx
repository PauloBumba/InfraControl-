import { useState } from "react";
import { locations, racks } from "@/data/dashboardData";

export function LocationsPage() {
  const [tab, setTab] = useState<"locations" | "racks">("locations");

  const totalDevices = locations.reduce((s, l) => s + l.devices, 0);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Locations & Racks</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {locations.length} locais · {racks.length} racks · {totalDevices} dispositivos
          </p>
        </div>
        <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
          {([
            { id: "locations", label: "Locations" },
            { id: "racks", label: "Racks" },
          ] as const).map((t) => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              style={{
                padding: "8px 14px",
                fontSize: 13,
                fontWeight: 500,
                color: tab === t.id ? "#3b82f6" : "#64748b",
                background: "none",
                border: "none",
                borderBottom: tab === t.id ? "2px solid #3b82f6" : "2px solid transparent",
                marginBottom: -1,
                cursor: "pointer",
              }}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      {tab === "locations" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 16 }}>
          {locations.map((loc) => (
            <div key={loc.id} className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
                <div style={{ width: 36, height: 36, borderRadius: 8, background: "#eff6ff", color: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  <i className="pi pi-map-marker" />
                </div>
                <div>
                  <div style={{ fontWeight: 600, fontSize: 14 }}>{loc.name}</div>
                  <div style={{ fontSize: 11, color: "#94a3b8" }}>{loc.type}</div>
                </div>
              </div>
              <p style={{ fontSize: 12, color: "#64748b", marginBottom: 12 }}>{loc.description}</p>
              <div style={{ display: "flex", gap: 16 }}>
                <div>
                  <div style={{ fontSize: 10, color: "#94a3b8" }}>Racks</div>
                  <div style={{ fontSize: 16, fontWeight: 700 }}>{loc.racks}</div>
                </div>
                <div>
                  <div style={{ fontSize: 10, color: "#94a3b8" }}>Dispositivos</div>
                  <div style={{ fontSize: 16, fontWeight: 700 }}>{loc.devices}</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {tab === "racks" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 16 }}>
          {racks.map((rack) => {
            const usage = Math.round((rack.usedUnits / rack.units) * 100);
            return (
              <div key={rack.id} className="uni-panel" style={{ padding: 16 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <i className="pi pi-server" style={{ color: "#8b5cf6" }} />
                    <span style={{ fontWeight: 600, fontSize: 14 }}>{rack.name}</span>
                  </div>
                  <span style={{ fontSize: 11, color: "#94a3b8" }}>{rack.location}</span>
                </div>
                <div style={{ marginBottom: 10 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#64748b", marginBottom: 4 }}>
                    <span>Uso de U</span>
                    <span>{rack.usedUnits}/{rack.units} ({usage}%)</span>
                  </div>
                  <div style={{ height: 6, background: "#f1f5f9", borderRadius: 3, overflow: "hidden" }}>
                    <div style={{ height: "100%", width: `${usage}%`, background: usage > 80 ? "#dc2626" : "#3b82f6", borderRadius: 3 }} />
                  </div>
                </div>
                <div style={{ display: "flex", gap: 16, marginBottom: 10, fontSize: 12 }}>
                  <span style={{ color: "#64748b" }}><i className="pi pi-bolt" style={{ marginRight: 4, color: "#f59e0b" }} />{rack.power}</span>
                  <span style={{ color: "#64748b" }}><i className="pi pi-thermometer" style={{ marginRight: 4, color: "#06b6d4" }} />{rack.temp}</span>
                </div>
                <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
                  {rack.devices.map((d) => (
                    <span key={d} style={{ fontSize: 10, fontFamily: "monospace", padding: "2px 6px", borderRadius: 4, background: "#f1f5f9", color: "#475569" }}>
                      {d}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
