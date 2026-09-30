import { useState } from "react";
import type { CSSProperties } from "react";
import { logsData } from "@/data/dashboardData";

// ADR-021/ADR-022: a plataforma de rastreio é o Seq, servido pelo nginx com
// basic auth (SEQ_AUTH_USER/SEQ_AUTH_PASSWORD no .env, dev: admin/admin).
// A URL padrão aponta para o host local na porta exposta do nginx; em
// produção/homologação sobrescreva com VITE_LOGS_URL no build do frontend.
const LOGS_URL = import.meta.env.VITE_LOGS_URL ?? "http://localhost:5341";

export function LogsPage() {
  const [correlationId, setCorrelationId] = useState("");

  const openPlatform = () => window.open(LOGS_URL, "_blank", "noopener,noreferrer");

  const traceCorrelation = () => {
    const base = LOGS_URL;
    if (!correlationId.trim()) {
      window.open(base, "_blank", "noopener,noreferrer");
      return;
    }
    // Seq: filtro fuzzy direto na view de eventos — acha a requisição original
    // e todos os eventos que ela gerou (API, outbox, workers) sob o mesmo id.
    const filter = `CorrelationId = '${correlationId.trim()}'`;
    window.open(`${base}/#/events?filter=${encodeURIComponent(filter)}`, "_blank", "noopener,noreferrer");
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Logs e rastreio</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            Backend, workers e frontend publicam em uma plataforma única — o rastro de cada requisição acompanha todos os eventos.
          </p>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
          <button
            onClick={openPlatform}
            style={buttonStyle}
            title="Abrir a plataforma de rastreio em nova aba"
          >
            <i className="pi pi-external-link" style={{ fontSize: 12 }} />
            Abrir plataforma de logs
          </button>
        </div>
      </div>

      <div className="uni-panel" style={{ background: "#f8fafc", border: "1px solid #e2e8f0" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Rastrear uma requisição pelo CorrelationId</div>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Toda requisição recebe um identificador no header <code style={{ fontSize: 11 }}>X-Correlation-Id</code>
            (gerado com a chamada ou respeitado se você enviar um). Cole abaixo para abrir a plataforma já filtrada —
            a requisição original e tudo que ela disparou na API, no outbox e nos workers aparecem sob o mesmo id.
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#fff", borderRadius: 6, padding: "6px 12px", border: "1px solid #e2e8f0" }}>
              <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
              <input
                type="text"
                value={correlationId}
                onChange={(e) => setCorrelationId(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && traceCorrelation()}
                placeholder="ex.: 0HMJ9Q1T5VML1:00000003"
                style={{ border: "none", background: "none", outline: "none", fontSize: 13, width: 260, fontFamily: "monospace" }}
              />
            </div>
            <button onClick={traceCorrelation} style={buttonStyle}>
              <i className="pi pi-send" style={{ fontSize: 12 }} />
              Rastrear
            </button>
          </div>
        </div>
      </div>

      <div className="uni-panel">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", paddingBottom: 8 }}>
          <span style={{ fontSize: 13, fontWeight: 600 }}>Últimos eventos (amostra local)</span>
          <span style={{ fontSize: 11, color: "#94a3b8" }}>
            Pré-visualização de desenvolvimento — o histórico completo e filtrável está na plataforma de logs.
          </span>
        </div>
        <table className="uni-logs-table">
          <thead>
            <tr>
              <th style={{ width: 140 }}>Data/Hora</th>
              <th style={{ width: 80 }}>Nível</th>
              <th>Mensagem</th>
              <th style={{ width: 100 }}>Fonte</th>
            </tr>
          </thead>
          <tbody>
            {logsData.map((log, i) => (
              <tr key={i}>
                <td style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>{log.time}</td>
                <td>
                  <span className={`uni-log-level ${log.level}`}>
                    {log.level === "info" ? "INFO" : log.level === "warning" ? "WARN" : "ERR"}
                  </span>
                </td>
                <td style={{ fontSize: 13 }}>{log.message}</td>
                <td style={{ fontSize: 12, color: "#64748b" }}>{log.source}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const buttonStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: 6,
  border: "none",
  borderRadius: 6,
  background: "#2563eb",
  color: "#fff",
  fontSize: 13,
  fontWeight: 600,
  padding: "8px 14px",
  cursor: "pointer",
};