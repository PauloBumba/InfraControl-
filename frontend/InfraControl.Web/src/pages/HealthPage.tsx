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
import { onHealthStatusChange } from "@/services/signalr/notifications";
import { formatDateTime } from "@/utils/format";

interface HealthCheckItem {
  name: string;
  status: string;
  description: string | null;
  duration: number;
  /** F46-T006: console do serviço irmão (quando o check declara link). */
  link?: string | null;
}

interface HealthResponse {
  status: string;
  checks: HealthCheckItem[];
  totalDuration: number;
}

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
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [healthNotice, setHealthNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [z, u, a, h] = await Promise.all([
        getZabbixSummary(),
        getUniFiDashboard(),
        listAlerts({ status: "open", pageSize: 10 }),
        fetch("/health").then((r) => r.json() as Promise<HealthResponse>).catch(() => null),
      ]);
      setZabbix(z);
      setUnifi(u);
      setAlerts(a.items);
      setAlertsTotal(a.totalCount);
      setHealth(h);
    } catch {
      setError("Não foi possível carregar a saúde das integrações.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  // F46-T006: aviso em tempo real quando o status agregado muda
  // (HealthStatusNotifier publica "health-status" no hub).
  useEffect(
    () =>
      onHealthStatusChange((event) => {
        setHealthNotice(
          `Status da saúde mudou: ${event.previous} → ${event.status}` +
            (event.unhealthy.length > 0
              ? ` (problemas: ${event.unhealthy.join(", ")})`
              : ""),
        );
        void load();
      }),
    [load],
  );

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

  const overallHealthy = health?.status === "Healthy";
  const overallDegraded = health?.status === "Degraded";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Saúde do sistema"
        subtitle="Estado dos containers/serviços, integrações e alertas abertos do monitoramento."
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {healthNotice && (
        <div
          className="uni-panel"
          style={{
            padding: "10px 14px",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 12,
            borderLeft: "3px solid #d97706",
          }}
        >
          <span style={{ fontSize: 12, color: "#92400e" }}>{healthNotice}</span>
          <button
            onClick={() => setHealthNotice(null)}
            style={{
              background: "none", border: "none", color: "#92400e",
              fontSize: 12, cursor: "pointer", padding: 0,
            }}
          >
            Dispensar
          </button>
        </div>
      )}

      <div className="uni-panel" style={{ padding: 20 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 14 }}>
          <div>
            <h3 style={{ fontSize: 14, fontWeight: 700, margin: 0 }}>Serviços e containers</h3>
            <p style={{ fontSize: 12, color: "#6b7280", margin: "2px 0 0" }}>
              Verificado via <code>/health</code>
            </p>
          </div>
          <StatusChip
            label={health?.status === "Healthy" ? "Tudo saudável" : health?.status === "Degraded" ? "Degradado" : health ? health.status : "Indisponível"}
            background={overallHealthy ? "#dcfce7" : overallDegraded ? "#fef3c7" : "#f1f5f9"}
            color={overallHealthy ? "#16a34a" : overallDegraded ? "#d97706" : "#475569"}
            fontWeight={600}
          />
        </div>
        {health?.checks?.length ? (
          <div className="uni-health-grid">
            {health.checks.map((check) => {
              const ok = check.status === "Healthy";
              const degraded = check.status === "Degraded";
              return (
                <div className="uni-health-card" key={check.name}>
                  <div className="uni-health-header">
                    <h3 style={{ textTransform: "capitalize" }}>{check.name.replace(/-/g, " ")}</h3>
                    <StatusChip
                      label={check.status === "Healthy" ? "Saudável" : check.status === "Degraded" ? "Degradado" : check.status}
                      background={ok ? "#dcfce7" : degraded ? "#fef3c7" : "#fef2f2"}
                      color={ok ? "#16a34a" : degraded ? "#d97706" : "#dc2626"}
                      fontWeight={600}
                    />
                  </div>
                  <div className="uni-health-metrics">
                    <div className="uni-health-metric">
                      <span>Tempo</span>
                      <strong>{check.duration.toFixed(0)} ms</strong>
                    </div>
                  </div>
                  {check.description && (
                    <div style={{ marginTop: 8, fontSize: 11, color: "#64748b" }}>
                      {check.description}
                    </div>
                  )}
                  {check.link && (
                    <a
                      href={check.link}
                      target="_blank"
                      rel="noreferrer"
                      onClick={(event) => event.stopPropagation()}
                      style={{
                        display: "inline-block", marginTop: 6, fontSize: 11,
                        color: "#3b82f6", textDecoration: "none",
                      }}
                    >
                      Abrir console ↗
                    </a>
                  )}
                </div>
              );
            })}
          </div>
        ) : (
          <div style={{ fontSize: 12, color: "#94a3b8" }}>Não foi possível carregar o status dos containers.</div>
        )}
      </div>

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
