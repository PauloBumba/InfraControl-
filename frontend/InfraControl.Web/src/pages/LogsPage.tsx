import { useState } from "react";
import type { CSSProperties } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { Button } from "@/components/ui/Button";
import { SearchInput } from "@/components/ui/SearchInput";

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
      <PageHeader
        title="Logs e rastreio"
        subtitle="Backend, workers e frontend publicam em uma plataforma única — o rastro de cada requisição acompanha todos os eventos."
        actions={
          <Button
            onClick={openPlatform}
            style={buttonStyle}
            title="Abrir a plataforma de rastreio em nova aba"
          >
            <i className="pi pi-external-link" style={{ fontSize: 12 }} />
            Abrir plataforma de logs
          </Button>
        }
      />

      <div className="uni-panel" style={{ background: "#f8fafc", border: "1px solid #e2e8f0" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Rastrear uma requisição pelo CorrelationId</div>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Toda requisição recebe um identificador no header <code style={{ fontSize: 11 }}>X-Correlation-Id</code>
            (gerado com a chamada ou respeitado se você enviar um). Cole abaixo para abrir a plataforma já filtrada —
            a requisição original e tudo que ela disparou na API, no outbox e nos workers aparecem sob o mesmo id.
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <SearchInput
              value={correlationId}
              onChange={setCorrelationId}
              placeholder="ex.: 0HMJ9Q1T5VML1:00000003"
              variant="outlined"
              width={260}
              mono
              onEnter={traceCorrelation}
            />
            <Button onClick={traceCorrelation} style={buttonStyle}>
              <i className="pi pi-send" style={{ fontSize: 12 }} />
              Rastrear
            </Button>
          </div>
        </div>
      </div>

      {/* F17-T019: a tabela "Últimos eventos (amostra local)" de `logsData`
          saiu — não existe endpoint de logs da aplicação na API. Os logs
          estruturados (nível/fonte/mensagem) são publicados no Seq
          (ADR-021/ADR-022) e a API não os expõe; auditoria de ações de
          usuário tem página própria em /audit. */}
      <div className="uni-panel" style={{ padding: 20, display: "flex", alignItems: "center", gap: 12 }}>
        <i className="pi pi-database" style={{ fontSize: 18, color: "#94a3b8" }} />
        <div>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Eventos da aplicação ficam na plataforma de logs</div>
          <div style={{ fontSize: 12, color: "#64748b", marginTop: 2 }}>
            A API não expõe o histórico de logs (níveis, fontes, mensagens) — ele é gravado no Seq, junto com workers e
            outbox. Use os botões acima para abrir a plataforma ou rastrear um <code style={{ fontSize: 11 }}>CorrelationId</code>.
            Ações de usuário auditadas estão em <strong>Auditoria</strong>.
          </div>
        </div>
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