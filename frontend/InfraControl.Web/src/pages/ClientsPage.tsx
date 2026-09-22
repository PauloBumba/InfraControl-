import { clients } from "@/data/dashboardData";

export function ClientsPage() {
  const getSignalBars = (signal: number) => {
    const strength = Math.min(4, Math.max(0, Math.round((signal + 90) / 10)));
    return Array.from({ length: 4 }, (_, i) => (
      <span
        key={i}
        className={`uni-signal-seg ${i < strength ? "filled" : ""}`}
        style={{ height: `${6 + i * 3}px` }}
      />
    ));
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Clientes</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>{clients.length} clientes conectados</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 12px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}>
            <i className="pi pi-filter" style={{ fontSize: 12 }} /> Filtrar
          </button>
        </div>
      </div>

      <div className="uni-panel">
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th>Cliente</th>
              <th>IP</th>
              <th>Tipo</th>
              <th>Conexão</th>
              <th>Sinal</th>
              <th>Velocidade</th>
              <th>Uso</th>
              <th>SO</th>
            </tr>
          </thead>
          <tbody>
            {clients.map((client) => (
              <tr key={client.id}>
                <td>
                  <div className="uni-client-table-name">
                    <div className="uni-client-table-icon">
                      <i className={client.type === "WiFi" ? "pi pi-wifi" : "pi pi-desktop"} />
                    </div>
                    <div>
                      <div style={{ fontWeight: 500 }}>{client.name}</div>
                      <div style={{ fontSize: 11, color: "#94a3b8" }}>{client.mac}</div>
                    </div>
                  </div>
                </td>
                <td style={{ fontFamily: "monospace", fontSize: 12 }}>{client.ip}</td>
                <td>
                  <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 6px", borderRadius: 4, background: client.type === "WiFi" ? "#dbeafe" : "#f1f5f9", color: client.type === "WiFi" ? "#2563eb" : "#64748b" }}>
                    {client.type}
                  </span>
                </td>
                <td style={{ fontSize: 12, color: "#64748b" }}>{client.ap || client.port}</td>
                <td>
                  {client.signal ? (
                    <div className="uni-signal-bar">{getSignalBars(client.signal)}</div>
                  ) : (
                    <span style={{ fontSize: 12, color: "#94a3b8" }}>—</span>
                  )}
                </td>
                <td style={{ fontSize: 12, fontWeight: 500 }}>{client.speed}</td>
                <td style={{ fontSize: 12, fontWeight: 500 }}>{client.usage}</td>
                <td style={{ fontSize: 12, color: "#64748b" }}>{client.os}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
