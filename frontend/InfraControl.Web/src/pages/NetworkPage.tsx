import { useState } from "react";
import { vlans, dhcpLeases, routes, firewallRules } from "@/data/dashboardData";

const tabs = ["VLANs", "DHCP", "Rotas", "Firewall"];

export function NetworkPage() {
  const [activeTab, setActiveTab] = useState("VLANs");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Rede</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>VLANs, DHCP, rotas e firewall</p>
        </div>
        <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          <i className="pi pi-plus" style={{ fontSize: 11 }} /> Adicionar
        </button>
      </div>

      <div style={{ display: "flex", gap: 2, background: "#f3f4f6", padding: 2, borderRadius: 6, width: "fit-content" }}>
        {tabs.map((tab) => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            style={{
              padding: "5px 14px", borderRadius: 5, fontSize: 12, fontWeight: 500,
              background: activeTab === tab ? "white" : "transparent",
              color: activeTab === tab ? "#1a1a1a" : "#6b7280",
              boxShadow: activeTab === tab ? "0 1px 2px rgba(0,0,0,0.06)" : "none",
            }}
          >
            {tab}
          </button>
        ))}
      </div>

      {activeTab === "VLANs" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 12 }}>
          {vlans.map((vlan) => (
            <div key={vlan.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16, cursor: "pointer", transition: "all 0.12s" }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <div style={{ width: 10, height: 10, borderRadius: "50%", background: vlan.color }} />
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>{vlan.name}</div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>VLAN {vlan.vlanId}</div>
                  </div>
                </div>
                <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: "#f3f4f6", color: "#6b7280" }}>
                  {vlan.devices} dispositivos
                </span>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 6, fontSize: 11, color: "#6b7280" }}>
                <div style={{ display: "flex", justifyContent: "space-between" }}><span>Subnet</span><span style={{ fontFamily: "monospace", color: "#1a1a1a" }}>{vlan.subnet}</span></div>
                <div style={{ display: "flex", justifyContent: "space-between" }}><span>Gateway</span><span style={{ fontFamily: "monospace", color: "#1a1a1a" }}>{vlan.gateway}</span></div>
                <div style={{ display: "flex", justifyContent: "space-between" }}><span>DHCP</span><span style={{ fontFamily: "monospace", color: "#1a1a1a" }}>{vlan.dhcp}</span></div>
              </div>
            </div>
          ))}
        </div>
      )}

      {activeTab === "DHCP" && (
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>IP</th>
                <th>MAC</th>
                <th>Hostname</th>
                <th>VLAN</th>
                <th>Expira</th>
              </tr>
            </thead>
            <tbody>
              {dhcpLeases.map((lease, i) => (
                <tr key={i}>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{lease.ip}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 11, color: "#6b7280" }}>{lease.mac}</td>
                  <td style={{ fontWeight: 500 }}>{lease.hostname}</td>
                  <td><span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f3f4f6", color: "#6b7280" }}>{lease.vlan}</span></td>
                  <td style={{ fontSize: 12, color: lease.expires === "Reservado" ? "#3b82f6" : "#6b7280" }}>{lease.expires}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {activeTab === "Rotas" && (
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Destino</th>
                <th>Gateway</th>
                <th>Interface</th>
                <th>Métrica</th>
                <th>Tipo</th>
              </tr>
            </thead>
            <tbody>
              {routes.map((route, i) => (
                <tr key={i}>
                  <td style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 500 }}>{route.destination}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{route.gateway}</td>
                  <td>{route.interface}</td>
                  <td>{route.metric}</td>
                  <td><span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: route.type === "Conectada" ? "#f0fdf4" : "#eff6ff", color: route.type === "Conectada" ? "#16a34a" : "#3b82f6" }}>{route.type}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {activeTab === "Firewall" && (
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Nome</th>
                <th>Ação</th>
                <th>Protocolo</th>
                <th>Origem</th>
                <th>Destino</th>
                <th>Porta</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {firewallRules.map((rule) => (
                <tr key={rule.id}>
                  <td style={{ fontWeight: 500 }}>{rule.name}</td>
                  <td>
                    <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: rule.action === "Permitir" ? "#f0fdf4" : "#fef2f2", color: rule.action === "Permitir" ? "#16a34a" : "#dc2626" }}>
                      {rule.action}
                    </span>
                  </td>
                  <td style={{ fontSize: 12 }}>{rule.protocol}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 11 }}>{rule.source}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 11 }}>{rule.destination}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 11 }}>{rule.port}</td>
                  <td>
                    <div style={{ width: 32, height: 18, borderRadius: 9, background: rule.enabled ? "#3b82f6" : "#d1d5db", position: "relative", cursor: "pointer" }}>
                      <div style={{ position: "absolute", top: 2, left: rule.enabled ? 16 : 2, width: 14, height: 14, borderRadius: "50%", background: "white", transition: "left 0.2s" }} />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
