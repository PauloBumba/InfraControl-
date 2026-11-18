import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { MetricChart } from "@/components/ui/MetricChart";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import {
  getDeliveryStats,
  type NotificationDeliveryStatPoint,
} from "@/services/api/notifications";

const STATUS_COLORS: Record<string, string> = {
  Sent: "#16a34a",
  Failed: "#dc2626",
  Pending: "#d97706",
  Processing: "#2563eb",
  Cancelled: "#64748b",
  Suppressed: "#8b5cf6",
};

const STATUS_ORDER = ["Sent", "Failed", "Pending", "Processing", "Cancelled", "Suppressed"];

const CHANNEL_COLORS: Record<string, string> = {
  SignalR: "#3b82f6",
  Email: "#f59e0b",
  Telegram: "#06b6d4",
  WhatsApp: "#16a34a",
  Webhook: "#8b5cf6",
  Slack: "#64748b",
};

const CHANNEL_ORDER = ["WhatsApp", "Email", "SignalR", "Telegram", "Slack", "Webhook"];

function statusLabel(status: string): string {
  return (
    {
      Sent: "Enviadas",
      Failed: "Falhas",
      Pending: "Pendentes",
      Processing: "Processando",
      Cancelled: "Canceladas",
      Suppressed: "Suprimidas",
    }[status] ?? status
  );
}

function dayLabel(iso: string): string {
  const [, m, d] = iso.split("-");
  return `${d}/${m}`;
}

/**
 * Gráficos de entregas de notificação (dados reais de
 * /notifications/delivery-stats): barras por dia empilhadas pela dimensão
 * escolhida + doughnut com o total do período.
 *
 * - `channel="WhatsApp"` → filtra um canal e agrupa por status.
 * - sem `channel` → agrupa por canal (visão da tela de Integrações).
 */
export function DeliveryStatsCharts({
  channel,
  days = 14,
  title,
}: {
  channel?: string;
  days?: number;
  title?: string;
}) {
  const stats = useQuery({
    queryKey: ["notification-delivery-stats", days],
    queryFn: () => getDeliveryStats(days),
    staleTime: 30_000,
  });

  const groupBy: "status" | "channel" = channel ? "status" : "channel";

  const { labels, datasets, totals } = useMemo(() => {
    const points = (stats.data?.points ?? []).filter(
      (p) => !channel || p.channel === channel,
    );
    const dates = [...new Set(points.map((p) => p.date))].sort();
    const groups = [
      ...new Set(points.map((p) => (groupBy === "status" ? p.status : p.channel))),
    ].sort((a, b) => {
      const order = groupBy === "status" ? STATUS_ORDER : CHANNEL_ORDER;
      return order.indexOf(a) - order.indexOf(b);
    });

    const index = (list: NotificationDeliveryStatPoint[], date: string, group: string) =>
      list
        .filter((p) => p.date === date && (groupBy === "status" ? p.status : p.channel) === group)
        .reduce((sum, p) => sum + p.count, 0);

    const colorOf = (group: string) =>
      groupBy === "status"
        ? (STATUS_COLORS[group] ?? "#64748b")
        : (CHANNEL_COLORS[group] ?? "#64748b");

    const labelOf = (group: string) =>
      groupBy === "status" ? statusLabel(group) : group;

    const ds = groups.map((group) => ({
      label: labelOf(group),
      data: dates.map((date) => index(points, date, group)),
      backgroundColor: colorOf(group),
      borderColor: colorOf(group),
      stack: "deliveries",
    }));

    const totalOf = (group: string) =>
      points
        .filter((p) => (groupBy === "status" ? p.status : p.channel) === group)
        .reduce((sum, p) => sum + p.count, 0);

    const totalsDs = groups.map((group) => ({
      label: labelOf(group),
      data: [totalOf(group)],
      backgroundColor: colorOf(group),
      borderColor: colorOf(group),
    }));

    return {
      labels: dates.map(dayLabel),
      datasets: ds,
      totals: totalsDs,
    };
  }, [stats.data, channel, groupBy]);

  if (stats.isLoading) return <LoadingBlock />;

  const empty = labels.length === 0;

  return (
    <div style={{ display: "grid", gap: 12 }}>
      {title && (
        <div style={{ fontSize: 12, fontWeight: 700, color: "#334155" }}>{title}</div>
      )}
      {empty ? (
        <div style={{ fontSize: 12, color: "#94a3b8", padding: "12px 0" }}>
          Nenhuma entrega no período.
        </div>
      ) : (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
            gap: 12,
          }}
        >
          <div className="uni-panel" style={{ padding: 14 }}>
            <MetricChart
              type="bar"
              height={200}
              labels={labels}
              datasets={datasets}
              stacked
              ariaLabel="Entregas por dia"
            />
          </div>
          <div className="uni-panel" style={{ padding: 14 }}>
            <MetricChart
              type="doughnut"
              height={200}
              labels={totals.map((d) => d.label)}
              datasets={totals}
              ariaLabel="Total de entregas por status"
            />
          </div>
        </div>
      )}
    </div>
  );
}
