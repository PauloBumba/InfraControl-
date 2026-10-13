import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { MeterGroup } from "primereact/metergroup";
import { listAlerts, type AlertItem } from "@/services/api/alerts";
import { getZabbixSummary, type ZabbixSummary } from "@/services/api/zabbix";
import { getUniFiDashboard, type UniFiDashboard } from "@/services/api/unifi";
import { formatDateTime } from "@/utils/format";

/**
 * Fase 06 (Plano Mestre — lacuna da auditoria Fase 00): HealthPage religada
 * aos dados REAIS do backend — saúde das integrações (Zabbix + UniFi) e
 * alertas abertos do monitoramento. Substitui o mock de serviços de rede
 * (WAN/DNS/DHCP) que não existiam no domínio.
 */
export function HealthPage() {
  const navigate = useNavigate();
  const [zabbix, setZabbix] = useState<ZabbixSummary | null>(null);
  const [unifi, setUnifi] = useState<UniFiDashboard | null>(null);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [alertsTotal, setAlertsTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [z, u, a] = await Promise.all([
        getZabbixSummary(),
        getUniFiDashboard(),
        listAlerts({ status: "open", pageSize: 10 }),
      ]);
      setZabbix(z);
      setUnifi(u);
      setAlerts(a.items);
      setAlertsTotal(a.totalCount);
    } catch {
      setError("Não foi possível carregar a saúde das integrações.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) return <LoadingBlock />;

  const zabbixHealthy = zabbix?.isConfigured === true && zabbix.problemsAvailable;
  const unifiHealthy = unifi?.controllerReachable === true && unifi.lastSyncSucceeded !== false;
  const criticalAlerts = alerts.filter((a) => a.severity === "Critical").length;

  const cards: Array<{
    name: string;
    healthy: boolean;
    metrics: Array<{ label: string; value: string }>;
    detail: string;
    link?: string;
  }> = [
    {
      name: "Zabbix",
      healthy: zabbixHealthy,
      detail: zabbix?.isConfigured
        ? `Versão ${zabbix.detectedVersion ?? "?"} · ${zabbix.totalHosts} hosts`
        : "Não configurado",
      metrics: [
        { label: "Hosts com asset", value: String(zabbix?.hostsWithAsset ?? 0) },
        { label: "Problemas ativos", value: String(zabbix?.problemsTotal ?? 0) },
        {
          label: "Último sync",
          value: zabbix?.lastInventorySyncAtUtc
            ? formatDateTime(new Date(zabbix.lastInventorySyncAtUtc))
            : "—",
        },
      ],
      link: "/zabbix",
    },
    {
      name: "UniFi",
      healthy: unifiHealthy,
      detail: unifi?.controllerReachable === false
        ? `Controller inacessível${unifi.controllerError ? `: ${unifi.controllerError}` : ""}`
        : `${unifi?.sites ?? 0} sites · ${unifi?.switches ?? 0} switches · ${unifi?.accessPoints ?? 0} APs`,
      metrics: [
        { label: "Clientes", value: String(unifi?.clients ?? 0) },
        {
          label: "Último sync",
          value: unifi?.lastSyncAtUtc ? formatDateTime(new Date(unifi.lastSyncAtUtc)) : "—",
        },
        {
          label: "Erros recentes",
          value: String(unifi?.recentSyncErrors ?? 0),
        },
      ],
      link: "/integrations",
    },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Saúde do sistema"
        subtitle="Estado das integrações e alertas abertos do monitoramento."
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div className="uni-health-grid">
        {cards.map((card) => (
          <div
            className="uni-health-card"
            key={card.name}
            style={{ cursor: card.link ? "pointer" : "default" }}
            onClick={() => card.link && navigate(card.link)}
          >
            <div className="uni-health-header">
              <h3>{card.name}</h3>
              <StatusChip
                label={card.healthy ? "Saudável" : "Atenção"}
                background={card.healthy ? "#dcfce7" : "#fef3c7"}
                color={card.healthy ? "#16a34a" : "#d97706"}
                fontWeight={600}
              />
            </div>
            <div className="uni-health-metrics">
              {card.metrics.map((metric) => (
                <div className="uni-health-metric" key={metric.label}>
                  <span>{metric.label}</span>
                  <strong>{metric.value}</strong>
                </div>
              ))}
            </div>
            <div style={{ marginTop: 8, fontSize: 11, color: "#64748b" }}>
              {card.detail}
            </div>
          </div>
        ))}
      </div>

      {/* Alertas abertos — resumo + top 5 */}
      <div className="uni-panel" style={{ padding: 20 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
          <div>
            <h3 style={{ fontSize: 14, fontWeight: 700, margin: 0 }}>
              Alertas abertos
            </h3>
            <p style={{ fontSize: 12, color: "#6b7280", margin: "2px 0 0" }}>
              {alertsTotal === 0
                ? "Nenhum alerta aberto."
                : `${alertsTotal} alerta(s) · ${criticalAlerts} crítico(s)`}
            </p>
          </div>
          <StatusChip
            label={criticalAlerts > 0 ? `${criticalAlerts} crítico(s)` : "Normal"}
            background={criticalAlerts > 0 ? "#fef2f2" : "#dcfce7"}
            color={criticalAlerts > 0 ? "#dc2626" : "#16a34a"}
            fontWeight={600}
          />
        </div>
        {alertsTotal > 0 && (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {alerts.slice(0, 5).map((alert) => (
              <div
                key={alert.id}
                style={{
                  display: "flex", alignItems: "center", gap: 10,
                  padding: "8px 10px", borderRadius: 6, background: "#f8fafc",
                  cursor: "pointer",
                }}
                onClick={() => navigate(alert.deviceId ? `/devices/${alert.deviceId}` : "/alerts")}
              >
                <StatusChip
                  label={alert.severity}
                  background={alert.severity === "Critical" ? "#fef2f2" : "#fef3c7"}
                  color={alert.severity === "Critical" ? "#dc2626" : "#d97706"}
                  fontWeight={600}
                  fontSize={10}
                />
                <span style={{ fontSize: 12, flex: 1, overflowWrap: "anywhere" }}>
                  {alert.message}
                </span>
                <span style={{ fontSize: 11, color: "#94a3b8", whiteSpace: "nowrap" }}>
                  {alert.deviceName ?? alert.source}
                </span>
              </div>
            ))}
            {alertsTotal > 5 && (
              <button
                onClick={() => navigate("/alerts")}
                style={{
                  alignSelf: "flex-start", background: "none", border: "none",
                  color: "#3b82f6", fontSize: 12, cursor: "pointer", padding: 0,
                }}
              >
                Ver todos ({alertsTotal}) →
              </button>
            )}
          </div>
        )}
      </div>

      {/* Uptime estimado do UniFi (quando disponível) */}
      {unifi && unifi.ports && unifi.ports.total > 0 && (
        <div className="uni-panel" style={{ padding: 16 }}>
          <div style={{ fontSize: 12, fontWeight: 600, marginBottom: 6 }}>
            Portas UniFi com link up
          </div>
          <MeterGroup
            values={[{
              label: "Up",
              value: Math.round((unifi.ports.up / unifi.ports.total) * 100),
              color: "#22c55e",
            }]}
            max={100}
          />
          <div style={{ fontSize: 11, color: "#6b7280", marginTop: 4 }}>
            {unifi.ports.up} de {unifi.ports.total} portas com link ativo
          </div>
        </div>
      )}
    </div>
  );
}
