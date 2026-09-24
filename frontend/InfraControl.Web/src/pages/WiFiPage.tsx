import { wifiNetworks, apData } from "@/data/dashboardData";

export function WiFiPage() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>WiFi</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{wifiNetworks.length} redes · {apData.length} access points</p>
        </div>
        <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          <i className="pi pi-plus" style={{ fontSize: 11 }} /> Criar Rede
        </button>
      </div>

      {/* WiFi Networks */}
      <div>
        <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>Redes WiFi</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 12 }}>
          {wifiNetworks.map((net) => (
            <div key={net.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
                <div>
                  <div style={{ fontSize: 14, fontWeight: 600, fontFamily: "monospace" }}>{net.ssid}</div>
                  <div style={{ fontSize: 11, color: "#9ca3af" }}>{net.security}</div>
                </div>
                <div style={{ width: 32, height: 18, borderRadius: 9, background: "#3b82f6", position: "relative" }}>
                  <div style={{ position: "absolute", top: 2, left: 16, width: 14, height: 14, borderRadius: "50%", background: "white" }} />
                </div>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, fontSize: 11 }}>
                <div><span style={{ color: "#9ca3af" }}>Banda:</span> <span style={{ fontWeight: 500 }}>{net.band}</span></div>
                <div><span style={{ color: "#9ca3af" }}>Canal:</span> <span style={{ fontWeight: 500 }}>{net.channel}</span></div>
                <div><span style={{ color: "#9ca3af" }}>Largura:</span> <span style={{ fontWeight: 500 }}>{net.width}</span></div>
                <div><span style={{ color: "#9ca3af" }}>VLAN:</span> <span style={{ fontWeight: 500 }}>{net.vlan}</span></div>
                <div><span style={{ color: "#9ca3af" }}>Clientes:</span> <span style={{ fontWeight: 500 }}>{net.clients}</span></div>
                <div><span style={{ color: "#9ca3af" }}>APs:</span> <span style={{ fontWeight: 500 }}>{net.aps.length}</span></div>
              </div>
              <div style={{ marginTop: 10, paddingTop: 10, borderTop: "1px solid #f3f4f6" }}>
                <div style={{ fontSize: 10, color: "#9ca3af", marginBottom: 4 }}>APs:</div>
                <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
                  {net.aps.map((ap) => (
                    <span key={ap} style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f3f4f6", color: "#6b7280" }}>{ap}</span>
                  ))}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Access Points */}
      <div>
        <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>Access Points</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 12 }}>
          {apData.map((ap) => (
            <div key={ap.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <div style={{ width: 36, height: 36, borderRadius: 8, background: ap.status === "online" ? "#f0fdf4" : "#fefce8", color: ap.status === "online" ? "#22c55e" : "#eab308", display: "flex", alignItems: "center", justifyContent: "center" }}>
                    <i className="pi pi-wifi" />
                  </div>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>{ap.name}</div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>{ap.model} · {ap.ip}</div>
                  </div>
                </div>
                <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: ap.status === "online" ? "#f0fdf4" : "#fefce8", color: ap.status === "online" ? "#16a34a" : "#ca8a04" }}>
                  {ap.status === "online" ? "Online" : "Atenção"}
                </span>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8, fontSize: 11, marginBottom: 10 }}>
                <div style={{ textAlign: "center" }}><div style={{ color: "#9ca3af" }}>Clientes</div><div style={{ fontWeight: 600, fontSize: 16 }}>{ap.clients}</div></div>
                <div style={{ textAlign: "center" }}><div style={{ color: "#9ca3af" }}>CPU</div><div style={{ fontWeight: 600, fontSize: 16 }}>{ap.cpu}%</div></div>
                <div style={{ textAlign: "center" }}><div style={{ color: "#9ca3af" }}>Mem</div><div style={{ fontWeight: 600, fontSize: 16 }}>{ap.mem}%</div></div>
              </div>
              <div style={{ fontSize: 11, color: "#6b7280", display: "flex", flexDirection: "column", gap: 4 }}>
                {ap.channels.map((ch) => <div key={ch}><i className="pi pi-wifi" style={{ fontSize: 10, marginRight: 4 }} />{ch}</div>)}
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: 4, paddingTop: 4, borderTop: "1px solid #f3f4f6" }}>
                  <span>Tx: {ap.txPower}</span>
                  <span>↑ {ap.txBytes} · ↓ {ap.rxBytes}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
