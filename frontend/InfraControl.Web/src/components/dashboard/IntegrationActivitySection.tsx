import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { MetricChart } from "@/components/ui/MetricChart";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { getZabbixEventStats } from "@/services/api/zabbix";
import { listUniFiSyncRuns } from "@/services/api/unifi";
import { formatDateTimeBR } from "@/utils/format";

const SEVERITY_NAMES = [
  "Não classificado",
  "Informativo",
  "Aviso",
  "Médio",
  "Alto",
  "Desastre",
];

const SEVERITY_COLORS = [
  "#64748b",
  "#22c55e",
  "#3b82f6",
  "#eab308",
  "#f97316",
  "#dc2626",
];

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="uni-panel" style={{ padding: 14 }}>
      <div style={{ fontSize: 12, fontWeight: 700, color: "#334155", marginBottom: 8 }}>
        {title}
      </div>
      {children}
    </div>
  );
}

function EmptyNote({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ fontSize: 12, color: "#94a3b8", padding: "12px 0" }}>{children}</div>
  );
}

/**
 * Gráficos de atividade das integrações no dashboard principal:
 * eventos Zabbix por dia/severidade (14d) e resultado das syncs UniFi
 * (últimas 15). Dados reais — painel some com aviso honesto quando a
 * fonte não tem dados ou falha (isolado por painel).
 */
export function IntegrationActivitySection() {
  const zabbixStats = useQuery({
    queryKey: ["dash-zabbix-event-stats", 14],
    queryFn: () => getZabbixEventStats(14),
    staleTime: 30_000,
    retry: false,
  });

  const syncRuns = useQuery({
    queryKey: ["dash-unifi-sync-runs"],
    queryFn: () => listUniFiSyncRuns(15),
    staleTime: 60_000,
    retry: false,
  });

  const zabbixChart = useMemo(() => {
    const points = (zabbixStats.data?.points ?? []).filter((p) => p.status === "Problem");
    const dates = [...new Set(points.map((p) => p.date))].sort();
    const severities = [...new Set(points.map((p) => p.severity))].sort((a, b) => a - b);

    const datasets = severities.map((severity) => ({
      label: SEVERITY_NAMES[severity] ?? `Sev ${severity}`,
      data: dates.map((date) =>
        points
          .filter((p) => p.date === date && p.severity === severity)
          .reduce((sum, p) => sum + p.count, 0),
      ),
      backgroundColor: SEVERITY_COLORS[severity] ?? "#64748b",
      borderColor: SEVERITY_COLORS[severity] ?? "#64748b",
    }));

    return {
      labels: dates.map((iso) => {
        const [, m, d] = iso.split("-");
        return `${d}/${m}`;
      }),
      datasets,
    };
  }, [zabbixStats.data]);

  const unifiChart = useMemo(() => {
    const runs = [...(syncRuns.data ?? [])].reverse().slice(-15);
    return {
      labels: runs.map((run) => formatDateTimeBR(run.startedAtUtc)),
      datasets: [
        { label: "Criados", data: runs.map((r) => r.created), backgroundColor: "#16a34a", borderColor: "#16a34a" },
        { label: "Atualizados", data: runs.map((r) => r.updated), backgroundColor: "#3b82f6", borderColor: "#3b82f6" },
        { label: "Erros", data: runs.map((r) => r.errors), backgroundColor: "#dc2626", borderColor: "#dc2626" },
      ],
    };
  }, [syncRuns.data]);

  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(340px, 1fr))",
        gap: 12,
      }}
    >
      <Panel title="Zabbix — eventos por dia (14d, por severidade)">
        {zabbixStats.isLoading ? (
          <LoadingBlock />
        ) : zabbixStats.isError ? (
          <EmptyNote>Integração Zabbix indisponível ou sem dados.</EmptyNote>
        ) : zabbixChart.labels.length === 0 ? (
          <EmptyNote>Nenhum evento ingerido no período.</EmptyNote>
        ) : (
          <MetricChart
            type="bar"
            height={200}
            labels={zabbixChart.labels}
            datasets={zabbixChart.datasets}
            stacked
            ariaLabel="Eventos Zabbix por dia e severidade"
          />
        )}
      </Panel>

      <Panel title="UniFi — sincronizações (últimas 15)">
        {syncRuns.isLoading ? (
          <LoadingBlock />
        ) : syncRuns.isError ? (
          <EmptyNote>Integração UniFi indisponível.</EmptyNote>
        ) : unifiChart.labels.length === 0 ? (
          <EmptyNote>Nenhuma sincronização registrada.</EmptyNote>
        ) : (
          <MetricChart
            type="bar"
            height={200}
            labels={unifiChart.labels}
            datasets={unifiChart.datasets}
            stacked
            ariaLabel="Resultado das sincronizações UniFi"
          />
        )}
      </Panel>
    </div>
  );
}
