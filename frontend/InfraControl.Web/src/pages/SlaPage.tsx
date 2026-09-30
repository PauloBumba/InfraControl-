import { services, sloBudgets } from "@/data/dashboardData";
import { Can } from "@/components/auth/Can";

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  operational: { label: "Operacional", bg: "#dcfce7", color: "#16a34a" },
  degraded: { label: "Degradado", bg: "#fef3c7", color: "#d97706" },
  down: { label: "Fora do ar", bg: "#fee2e2", color: "#dc2626" },
};

const burnConfig: Record<string, { label: string; color: string }> = {
  normal: { label: "Normal", color: "#16a34a" },
  fast: { label: "Rápido", color: "#dc2626" },
};

export function SlaPage() {
  const avgUptime = (services.reduce((s, svc) => s + svc.uptime, 0) / services.length).toFixed(2);
  const openIncidents = services.reduce((s, svc) => s + svc.openIncidents, 0);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>SLA/SLO & Services</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {services.length} serviços · uptime médio {avgUptime}% · {openIncidents} incidentes abertos
          </p>
        </div>
        <Can level="operate">
          <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 13, fontWeight: 500, border: "none" }}>
            <i className="pi pi-plus" style={{ fontSize: 12 }} /> Novo Serviço
          </button>
        </Can>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Serviços</h2></div>
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th>Serviço</th>
              <th style={{ width: 110 }}>Status</th>
              <th style={{ width: 90 }}>Uptime</th>
              <th style={{ width: 90 }}>SLA</th>
              <th style={{ width: 90 }}>SLO</th>
              <th style={{ width: 70 }}>Incidentes</th>
              <th style={{ width: 100 }}>Owner</th>
            </tr>
          </thead>
          <tbody>
            {services.map((svc) => {
              const st = statusConfig[svc.status] ?? { label: svc.status, bg: "#f1f5f9", color: "#64748b" };
              const meetsSla = svc.uptime >= svc.slaTarget;
              return (
                <tr key={svc.id}>
                  <td style={{ fontWeight: 500, fontSize: 13 }}>{svc.name}</td>
                  <td>
                    <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4, background: st.bg, color: st.color }}>
                      {st.label}
                    </span>
                  </td>
                  <td style={{ fontSize: 13, fontWeight: 600, color: meetsSla ? "#16a34a" : "#dc2626" }}>
                    {svc.uptime}%
                  </td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{svc.slaTarget}%</td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{svc.sloTarget}%</td>
                  <td style={{ fontSize: 13, fontWeight: svc.openIncidents > 0 ? 600 : 400 }}>
                    {svc.openIncidents}
                  </td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{svc.owner}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Error Budget (SLO)</h2></div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: 16, padding: 16 }}>
          {sloBudgets.map((b) => {
            const burn = burnConfig[b.burnRate] ?? { label: b.burnRate, color: "#64748b" };
            return (
              <div key={b.service} style={{ padding: 12, borderRadius: 8, border: "1px solid #e5e7eb" }}>
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                  <span style={{ fontSize: 13, fontWeight: 500 }}>{b.service}</span>
                  <span style={{ fontSize: 11, fontWeight: 600, color: burn.color }}>{burn.label}</span>
                </div>
                <div style={{ height: 6, background: "#f1f5f9", borderRadius: 3, overflow: "hidden", marginBottom: 6 }}>
                  <div style={{ height: "100%", width: `${b.budgetRemaining}%`, background: b.budgetRemaining < 50 ? "#dc2626" : "#22c55e", borderRadius: 3 }} />
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#64748b" }}>
                  <span>{b.budgetRemaining}% restante</span>
                  <span>{b.window}</span>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
