import { useState } from "react";
import { switches, switchPortDetail } from "@/data/dashboardData";

export function SwitchesPage() {
  const [selectedSwitch, setSelectedSwitch] = useState("Switch-Core-01");

  const ports = switchPortDetail.filter((p) => p.switch === selectedSwitch);
  const active = ports.filter((p) => p.status === "active").length;
  const poe = ports.filter((p) => p.poe).length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Switches & Portas</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {switches.length} switches · {switches.filter((s) => s.status === "online").length} online
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <select
            value={selectedSwitch}
            onChange={(e) => setSelectedSwitch(e.target.value)}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            {switches.map((s) => (
              <option key={s.id} value={s.name}>{s.name}</option>
            ))}
          </select>
        </div>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 16 }}>
        {switches.map((sw) => (
          <div
            key={sw.id}
            className="uni-panel"
            style={{
              padding: 16,
              cursor: "pointer",
              border: selectedSwitch === sw.name ? "2px solid #3b82f6" : "1px solid #e5e7eb",
            }}
            onClick={() => setSelectedSwitch(sw.name)}
          >
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <i className="pi pi-server" style={{ color: "#8b5cf6" }} />
                <span style={{ fontWeight: 600, fontSize: 14 }}>{sw.name}</span>
              </div>
              <span
                style={{
                  fontSize: 11,
                  fontWeight: 500,
                  padding: "2px 8px",
                  borderRadius: 4,
                  background: sw.status === "online" ? "#dcfce7" : "#fee2e2",
                  color: sw.status === "online" ? "#16a34a" : "#dc2626",
                }}
              >
                {sw.status === "online" ? "Online" : "Offline"}
              </span>
            </div>
            <div style={{ fontSize: 12, color: "#64748b", marginBottom: 8 }}>
              {sw.model} · {sw.ip} · {sw.location}
            </div>
            <div style={{ display: "flex", gap: 16, fontSize: 12 }}>
              <span style={{ color: "#64748b" }}>Portas <strong style={{ color: "#0f172a" }}>{sw.activePorts}/{sw.totalPorts}</strong></span>
              <span style={{ color: "#64748b" }}>PoE <strong style={{ color: "#f59e0b" }}>{sw.poePorts}</strong></span>
              <span style={{ color: "#64748b" }}>FW <strong style={{ color: "#0f172a" }}>{sw.firmware}</strong></span>
            </div>
          </div>
        ))}
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Portas — {selectedSwitch}</h2>
          <span style={{ fontSize: 12, color: "#64748b" }}>{active} ativas · {poe} PoE</span>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(90px, 1fr))", gap: 8, padding: 16 }}>
          {ports.map((port) => (
            <div
              key={`${port.switch}-${port.port}`}
              style={{
                padding: "10px 6px",
                borderRadius: 6,
                border: `2px solid ${port.status === "active" ? "#22c55e" : port.status === "disabled" ? "#f59e0b" : "#e5e7eb"}`,
                background: port.status === "active" ? "#f0fdf4" : port.status === "disabled" ? "#fefce8" : "#f9fafb",
                textAlign: "center",
                fontSize: 10,
              }}
            >
              <div style={{ fontSize: 15, fontWeight: 700 }}>{port.port}</div>
              {port.poe && <div style={{ fontSize: 8, color: "#f59e0b", fontWeight: 700 }}>PoE</div>}
              {port.device !== "—" && (
                <div style={{ fontSize: 8, color: "#6b7280", marginTop: 2, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                  {port.device}
                </div>
              )}
              {port.vlan > 0 && <div style={{ fontSize: 8, color: "#94a3b8" }}>VLAN {port.vlan}</div>}
            </div>
          ))}
        </div>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Detalhe de portas</h2></div>
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th style={{ width: 60 }}>Porta</th>
              <th>Dispositivo</th>
              <th style={{ width: 90 }}>Tipo</th>
              <th style={{ width: 90 }}>Velocidade</th>
              <th style={{ width: 60 }}>PoE</th>
              <th style={{ width: 80 }}>VLAN</th>
              <th style={{ width: 90 }}>Status</th>
            </tr>
          </thead>
          <tbody>
            {ports.map((port) => (
              <tr key={`${port.switch}-${port.port}-row`}>
                <td style={{ fontFamily: "monospace", fontWeight: 600, fontSize: 12 }}>{port.port}</td>
                <td style={{ fontSize: 13 }}>{port.device}</td>
                <td style={{ fontSize: 12, color: "#64748b", textTransform: "capitalize" }}>{port.type}</td>
                <td style={{ fontSize: 12 }}>{port.speed}</td>
                <td>{port.poe ? <i className="pi pi-bolt" style={{ color: "#f59e0b", fontSize: 13 }} /> : <span style={{ color: "#cbd5e1" }}>—</span>}</td>
                <td style={{ fontSize: 12, color: "#64748b" }}>{port.vlan || "—"}</td>
                <td>
                  <span
                    style={{
                      fontSize: 11,
                      fontWeight: 500,
                      padding: "2px 8px",
                      borderRadius: 4,
                      background: port.status === "active" ? "#dcfce7" : port.status === "disabled" ? "#fef3c7" : "#f1f5f9",
                      color: port.status === "active" ? "#16a34a" : port.status === "disabled" ? "#d97706" : "#64748b",
                    }}
                  >
                    {port.status === "active" ? "Ativa" : port.status === "disabled" ? "Desativada" : "Inativa"}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
