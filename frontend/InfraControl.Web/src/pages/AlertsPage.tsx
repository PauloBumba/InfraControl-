import { useCallback, useEffect, useState } from "react";
import {
  listAlerts,
  type AlertItem,
  type AlertStatusFilter,
} from "@/services/api/alerts";
import { formatDateTime } from "@/utils/format";

/**
 * F17-T011 — AlertsPage conectada ao backend (substitui `alertsData` de
 * `data/dashboardData`). Contrato real: `GET /monitoring/alerts`
 * (`PagedResult<AlertDto>`; serviço já criado em F17-T009). O botão
 * "Filtrar" morto do protótipo virou o segmented control do **parâmetro
 * real** `status` (open|resolved|suppressed — vazio = todos; valor fora
 * responde 400 pt-BR, por isso só mandamos os três do contrato).
 *
 * Divergências do protótipo: severidade só tem Warning/Critical no enum
 * (chip "Info" saiu); `title`/`detail` não existem — o título real é
 * `deviceName · check` e o detalhe é a `message`; entrou `status`
 * (Aberto/Resolvido/Suprimido) e os horários (`openedAt`/`resolvedAt`).
 * Sem mutations: alerta é criado automático pós-check e resolvido pelo
 * sistema (F6) — não há POST/resolve público.
 */

const PAGE_SIZE = 20;

const severityConfig: Record<string, { label: string; bg: string; color: string; icon: string }> = {
  Critical: { label: "Crítico", bg: "#fef2f2", color: "#dc2626", icon: "pi pi-exclamation-circle" },
  Warning: { label: "Atenção", bg: "#fef3c7", color: "#d97706", icon: "pi pi-exclamation-triangle" },
};

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  Open: { label: "Aberto", bg: "#fef2f2", color: "#dc2626" },
  Resolved: { label: "Resolvido", bg: "#f0fdf4", color: "#15803d" },
  Suppressed: { label: "Suprimido", bg: "#f1f5f9", color: "#64748b" },
};

const filters: Array<{ key: "" | AlertStatusFilter; label: string }> = [
  { key: "", label: "Todos" },
  { key: "open", label: "Abertos" },
  { key: "resolved", label: "Resolvidos" },
  { key: "suppressed", label: "Suprimidos" },
];

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function AlertsPage() {
  const [items, setItems] = useState<AlertItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [status, setStatus] = useState<"" | AlertStatusFilter>("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listAlerts({
        status: status || undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [status, page]);

  useEffect(() => {
    void load();
  }, [load]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Alertas</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "alerta" : "alertas"}`}
          </p>
        </div>
        <div style={{ display: "flex", gap: 4, background: "#f3f4f6", padding: 2, borderRadius: 6 }}>
          {filters.map((entry) => (
            <button
              key={entry.key || "all"}
              onClick={() => { setStatus(entry.key); setPage(1); }}
              style={{
                padding: "5px 12px", borderRadius: 5, fontSize: 12, fontWeight: 500,
                background: status === entry.key ? "white" : "transparent",
                color: status === entry.key ? "#1a1a1a" : "#6b7280",
                boxShadow: status === entry.key ? "0 1px 2px rgba(0,0,0,0.06)" : "none",
                border: "none", cursor: "pointer",
              }}
            >
              {entry.label}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
          <button onClick={() => void load()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
            Tentar de novo
          </button>
        </div>
      )}

      <div className="uni-panel">
        <div className="uni-alerts-list">
          {items.map((alert) => {
            const severity = severityConfig[alert.severity] ?? {
              label: alert.severity, bg: "#f1f5f9", color: "#64748b", icon: "pi pi-info-circle",
            };
            const statusStyle = statusConfig[alert.status] ?? {
              label: alert.status, bg: "#f1f5f9", color: "#64748b",
            };
            return (
              <div className="uni-alert-row" key={alert.id}>
                <div className={`uni-alert-icon ${alert.severity === "Critical" ? "critical" : "warning"}`}>
                  <i className={severity.icon} />
                </div>
                <div className="uni-alert-body">
                  <div className="uni-alert-title">{alert.deviceName ?? "Sem device"} · {alert.check ?? alert.source}</div>
                  <div className="uni-alert-detail">{alert.message}</div>
                </div>
                <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4 }}>
                  <span style={{ fontSize: 11, fontWeight: 600, padding: "2px 8px", borderRadius: 4, background: severity.bg, color: severity.color }}>
                    {severity.label}
                  </span>
                  <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4, background: statusStyle.bg, color: statusStyle.color }}>
                    {statusStyle.label}
                  </span>
                  <span style={{ fontSize: 10, color: "#94a3b8" }}>
                    {formatDateTime(new Date(alert.openedAt))}
                    {alert.resolvedAt ? ` → ${formatDateTime(new Date(alert.resolvedAt))}` : ""}
                  </span>
                </div>
              </div>
            );
          })}
          {!loading && items.length === 0 && (
            <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              {error ? "—" : "Nenhum alerta neste filtro"}
            </div>
          )}
          {loading && items.length === 0 && (
            <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              Carregando…
            </div>
          )}
        </div>
      </div>

      {totalCount > PAGE_SIZE && (
        <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 10, fontSize: 12, color: "#64748b" }}>
          <span>Página {page} de {totalPages}</span>
          <button
            onClick={() => setPage((value) => Math.max(1, value - 1))}
            disabled={page <= 1 || loading}
            style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: page <= 1 ? "default" : "pointer", opacity: page <= 1 ? 0.5 : 1 }}
          >
            Anterior
          </button>
          <button
            onClick={() => setPage((value) => Math.min(totalPages, value + 1))}
            disabled={page >= totalPages || loading}
            style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: page >= totalPages ? "default" : "pointer", opacity: page >= totalPages ? 0.5 : 1 }}
          >
            Próxima
          </button>
        </div>
      )}
    </div>
  );
}
