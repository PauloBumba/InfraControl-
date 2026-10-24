import { useCallback, useEffect, useState } from "react";
import {
  listAlerts,
  type AlertItem,
  type AlertStatusFilter,
} from "@/services/api/alerts";
import { formatDateTime } from "@/utils/format";
import { Pagination } from "@/components/ui/Pagination";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { StatusChip } from "@/components/ui/StatusChip";
import { SelectButton } from "primereact/selectbutton";
import { errorMessage } from "@/hooks/queries";
import { useOperationalRefresh } from "@/hooks/useOperationalRefresh";
import { useAutoRefresh } from "@/hooks/useAutoRefresh";

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
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, [status, page]);

  useEffect(() => {
    void load();
  }, [load]);

  // F44-T002/T003: push operacional + botão global + polling (autoRefresh)
  useOperationalRefresh(load);
  useAutoRefresh(load);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <ListPageShell
      title="Alertas"
      singular="alerta"
      plural="alertas"
      total={totalCount}
      loading={loading}
      error={error}
      onRetry={() => void load()}
      actions={
        <SelectButton className="uni-select-button" value={status} options={filters} optionLabel="label" optionValue="key" allowEmpty={false} onChange={(event) => { setStatus(event.value); setPage(1); }} />
      }
      footer={
        totalCount > PAGE_SIZE ? (
          <Pagination page={page} totalPages={totalPages} disabled={loading} onPage={setPage} />
        ) : undefined
      }
    >
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
                  <StatusChip label={severity.label} background={severity.bg} color={severity.color} fontWeight={600} />
                  <StatusChip label={statusStyle.label} background={statusStyle.bg} color={statusStyle.color} />
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
          {loading && items.length === 0 && <LoadingBlock />}
        </div>
      </div>
    </ListPageShell>
  );
}
