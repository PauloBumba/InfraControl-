/**
 * F54 — blocos do dashboard principal (dados reais apenas):
 * status da frota, gráficos com seletor de período, topologia UniFi
 * embutida, resumo UniFi e central de eventos (alertas + problemas Zabbix).
 * Cada bloco busca seus próprios dados com falha isolada — um endpoint
 * fora do ar nunca derruba os demais.
 */
import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { MetricChart } from "@/components/ui/MetricChart";
import { UniFiTopologyMap } from "@/components/ui/UniFiTopologyMap";
import { StatusChip } from "@/components/ui/StatusChip";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import {
  getFleetStatusOverview,
  listDevices,
  type FleetStatusOverview,
} from "@/services/api/inventory";
import {
  getMonitoringAvailability,
  type MonitoringAvailability,
} from "@/services/api/monitoring";
import {
  getUnifiTopology,
  getUniFiDashboard,
  type UniFiDashboard,
  type UniFiTopology,
} from "@/services/api/unifi";
import {
  getZabbixHostItems,
  getZabbixHostSeries,
  getZabbixHosts,
  getZabbixProblems,
  type ZabbixHostItem,
  type ZabbixHostSeries,
  type ZabbixHostView,
  type ZabbixProblemView,
} from "@/services/api/zabbix";
import { listAlerts, type AlertItem } from "@/services/api/alerts";

/* ------------------------------------------------------------------ */
/* Períodos compartilhados dos gráficos (T003)                          */
/* ------------------------------------------------------------------ */

export const PERIODS = [
  { key: "1h", label: "1h", hours: 1 },
  { key: "24h", label: "24h", hours: 24 },
  { key: "7d", label: "7d", hours: 24 * 7 },
  { key: "30d", label: "30d", hours: 24 * 30 },
] as const;

export type PeriodKey = (typeof PERIODS)[number]["key"];

function PeriodSelector({
  value,
  onChange,
}: {
  value: PeriodKey;
  onChange: (key: PeriodKey) => void;
}) {
  return (
    <div className="uni-dash-periods" role="group" aria-label="Período dos gráficos">
      {PERIODS.map((p) => (
        <button
          key={p.key}
          type="button"
          className={p.key === value ? "uni-dash-period active" : "uni-dash-period"}
          onClick={() => onChange(p.key)}
        >
          {p.label}
        </button>
      ))}
    </div>
  );
}

function formatBucket(iso: string, hours: number): string {
  const d = new Date(iso);
  if (hours <= 24) return d.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
  return d.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit" }) +
    " " + d.toLocaleTimeString("pt-BR", { hour: "2-digit" });
}

/* ------------------------------------------------------------------ */
/* T002 — faixa de status da frota (status-overview, F54-T001)          */
/* ------------------------------------------------------------------ */

const STATUS_LABEL: Record<string, string> = {
  Available: "Disponíveis",
  Degraded: "Degradados",
  Unavailable: "Indisponíveis",
  Unknown: "Sem dados",
};

export function FleetStatusStrip() {
  const [data, setData] = useState<FleetStatusOverview | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    (async () => {
      try {
        const result = await getFleetStatusOverview();
        if (alive) setData(result);
      } catch {
        if (alive) setFailed(true);
      }
    })();
    return () => {
      alive = false;
    };
  }, []);

  if (failed) return null; // endpoint novo indisponível — página segue sem a faixa
  if (!data) return <LoadingBlock />;

  return (
    <section className="uni-panel uni-dash-fleet" aria-label="Status da frota">
      <header className="uni-panel-header">
        <h2>Frota</h2>
        <Link className="uni-dash-view-all" to="/devices">Ver devices</Link>
      </header>
      <div className="uni-dash-fleet-stats">
        <Link className="uni-dash-fleet-kpi" to="/devices">
          <span className="uni-dash-fleet-value">{data.totalDevices}</span>
          <span className="uni-dash-fleet-label">Dispositivos</span>
        </Link>
        {data.byStatus.map((s) => (
          <Link
            key={s.status}
            className="uni-dash-fleet-kpi"
            to={`/devices?status=${s.status}`}
          >
            <span className="uni-dash-fleet-value">{s.count}</span>
            <span className="uni-dash-fleet-label">
              {STATUS_LABEL[s.status] ?? s.status}
            </span>
          </Link>
        ))}
        <Link className="uni-dash-fleet-kpi" to="/alerts">
          <span className="uni-dash-fleet-value">{data.openAlerts}</span>
          <span className="uni-dash-fleet-label">
            Alertas abertos ({data.openCriticalAlerts} críticos)
          </span>
        </Link>
      </div>
      {data.byType.length > 0 && (
        <p className="uni-dash-fleet-types">
          {data.byType.map((t) => `${t.count} ${t.type}`).join(" · ")}
        </p>
      )}
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* T003 — disponibilidade dos checks internos + métrica Zabbix           */
/* ------------------------------------------------------------------ */

function AvailabilityChart({ periodKey }: { periodKey: PeriodKey }) {
  const [data, setData] = useState<MonitoringAvailability | null>(null);
  const [failed, setFailed] = useState(false);
  const period = PERIODS.find((p) => p.key === periodKey) ?? PERIODS[1];

  useEffect(() => {
    let alive = true;
    const toUtc = new Date();
    const fromUtc = new Date(toUtc.getTime() - period.hours * 3600_000);
    (async () => {
      try {
        const result = await getMonitoringAvailability({
          fromUtc: fromUtc.toISOString(),
          toUtc: toUtc.toISOString(),
        });
        if (alive) setData(result);
      } catch {
        if (alive) setFailed(true);
      }
    })();
    return () => {
      alive = false;
    };
  }, [period.hours]);

  if (failed) return null;
  if (!data) return <LoadingBlock />;

  const labels = data.points.map((p) => formatBucket(p.bucketStartUtc, period.hours));
  // % de sucesso por bucket; bucket sem polls fica null (lacuna honesta).
  const values = data.points.map((p) =>
    p.total > 0 ? Math.round((p.succeeded / p.total) * 1000) / 10 : null,
  ) as unknown as number[];

  return (
    <MetricChart
      type="line"
      labels={labels}
      values={values}
      label="Disponibilidade (%)"
      ariaLabel="Disponibilidade dos checks de monitoramento no período"
      height={200}
    />
  );
}

function ZabbixMetricChart({ periodKey }: { periodKey: PeriodKey }) {
  const [hosts, setHosts] = useState<ZabbixHostView[]>([]);
  const [hostId, setHostId] = useState<string>("");
  const [items, setItems] = useState<ZabbixHostItem[]>([]);
  const [itemId, setItemId] = useState<string>("");
  const [series, setSeries] = useState<ZabbixHostSeries | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "empty" | "error">("loading");
  const period = PERIODS.find((p) => p.key === periodKey) ?? PERIODS[1];

  // Hosts Zabbix (read-model local) — falha → painel some.
  useEffect(() => {
    let alive = true;
    (async () => {
      try {
        const page = await getZabbixHosts({ pageSize: 50, enabled: true });
        const usable = page.items.filter((h) => !h.missing);
        if (!alive) return;
        setHosts(usable);
        setHostId((prev) => prev || usable[0]?.hostId || "");
      } catch {
        if (alive) setHosts([]);
      }
    })();
    return () => {
      alive = false;
    };
  }, []);

  // Itens do host selecionado.
  useEffect(() => {
    if (!hostId) return;
    let alive = true;
    setItems([]);
    setItemId("");
    setSeries(null);
    (async () => {
      try {
        const list = await getZabbixHostItems(hostId);
        const usable = list.filter((i) => i.enabled && !i.unsupported);
        if (!alive) return;
        setItems(usable);
        setItemId(usable[0]?.itemId ?? "");
      } catch {
        if (alive) setItems([]);
      }
    })();
    return () => {
      alive = false;
    };
  }, [hostId]);

  // Série do item no período.
  useEffect(() => {
    if (!hostId || !itemId) return;
    let alive = true;
    setState("loading");
    const toUtc = new Date();
    const fromUtc = new Date(toUtc.getTime() - period.hours * 3600_000);
    (async () => {
      try {
        const result = await getZabbixHostSeries({
          hostId,
          itemIds: itemId,
          fromUtc: fromUtc.toISOString(),
          toUtc: toUtc.toISOString(),
        });
        if (!alive) return;
        const found = result.series.find((s) => s.itemId === itemId) ?? result.series[0] ?? null;
        setSeries(found);
        setState(found && found.points.length > 0 ? "ready" : "empty");
      } catch {
        if (alive) setState("error");
      }
    })();
    return () => {
      alive = false;
    };
  }, [hostId, itemId, period.hours]);

  const chart = useMemo(() => {
    if (!series || series.points.length === 0) return null;
    const labels = series.points.map((p) => formatBucket(p.clockUtc, period.hours));
    const values = series.points.map((p) => {
      const n = Number(p.value);
      return Number.isFinite(n) ? n : null;
    }) as unknown as number[];
    const hasData = values.some((v) => v !== null);
    if (!hasData) return null;
    return { labels, values };
  }, [series, period.hours]);

  const selectedItem = items.find((i) => i.itemId === itemId);

  return (
    <div className="uni-dash-zmetric">
      <div className="uni-dash-zmetric-controls">
        <select
          className="uni-dash-select"
          aria-label="Host Zabbix"
          value={hostId}
          onChange={(e) => setHostId(e.target.value)}
          disabled={hosts.length === 0}
        >
          {hosts.length === 0 && <option value="">Nenhum host disponível</option>}
          {hosts.map((h) => (
            <option key={h.hostId} value={h.hostId}>
              {h.name || h.host}
            </option>
          ))}
        </select>
        <select
          className="uni-dash-select"
          aria-label="Métrica"
          value={itemId}
          onChange={(e) => setItemId(e.target.value)}
          disabled={items.length === 0}
        >
          {items.length === 0 && <option value="">Sem métricas</option>}
          {items.map((i) => (
            <option key={i.itemId} value={i.itemId}>
              {i.name}
              {i.lastValue ? ` — ${i.lastValue}${i.unit ? ` ${i.unit}` : ""}` : ""}
            </option>
          ))}
        </select>
      </div>
      {state === "loading" && <LoadingBlock />}
      {state === "empty" && (
        <p className="uni-dash-empty">Sem histórico no período para esta métrica.</p>
      )}
      {state === "error" && (
        <p className="uni-dash-empty">Histórico indisponível no momento.</p>
      )}
      {state === "ready" && chart && (
        <MetricChart
          type="line"
          labels={chart.labels}
          values={chart.values}
          label={selectedItem ? `${selectedItem.name}${selectedItem.unit ? ` (${selectedItem.unit})` : ""}` : "Métrica"}
          ariaLabel={`Série histórica de ${selectedItem?.name ?? "métrica"} no período`}
          height={200}
        />
      )}
    </div>
  );
}

export function PerformanceSection() {
  const [period, setPeriod] = useState<PeriodKey>("24h");
  return (
    <section className="uni-panel uni-dash-performance" aria-label="Desempenho e disponibilidade">
      <header className="uni-panel-header">
        <h2>Desempenho</h2>
        <PeriodSelector value={period} onChange={setPeriod} />
      </header>
      <div className="uni-dash-charts">
        <div className="uni-dash-chart-card">
          <h3 className="uni-dash-chart-title">Disponibilidade dos checks internos</h3>
          <AvailabilityChart periodKey={period} />
        </div>
        <div className="uni-dash-chart-card">
          <h3 className="uni-dash-chart-title">Métrica Zabbix</h3>
          <ZabbixMetricChart periodKey={period} />
        </div>
      </div>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* T004 — topologia UniFi embutida (resumo, link para a página cheia)    */
/* ------------------------------------------------------------------ */

export function TopologyEmbed() {
  const [topology, setTopology] = useState<UniFiTopology | null>(null);
  const [macToId, setMacToId] = useState<Map<string, string>>(new Map());
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    (async () => {
      try {
        const [topo, devices] = await Promise.all([
          getUnifiTopology(),
          listDevices({ pageSize: 200 }),
        ]);
        if (!alive) return;
        setTopology(topo.nodes.length > 0 ? topo : null);
        setMacToId(
          new Map(
            (devices.items ?? [])
              .filter((d) => d.mac)
              .map((d) => [d.mac as string, d.id]),
          ),
        );
      } catch {
        if (alive) setFailed(true);
      }
    })();
    return () => {
      alive = false;
    };
  }, []);

  if (failed) return null;

  return (
    <section className="uni-panel uni-dash-topology" aria-label="Topologia UniFi">
      <header className="uni-panel-header">
        <h2>Topologia (UniFi ao vivo)</h2>
        <Link className="uni-dash-view-all" to="/unifi">Abrir mapa completo</Link>
      </header>
      {topology === null ? (
        <LoadingBlock />
      ) : (
        <div className="uni-dash-topology-map">
          <UniFiTopologyMap topology={topology} macToId={macToId} />
        </div>
      )}
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* T005 — resumo UniFi                                                  */
/* ------------------------------------------------------------------ */

export function UniFiSummaryPanel() {
  const [data, setData] = useState<UniFiDashboard | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    (async () => {
      try {
        const result = await getUniFiDashboard();
        if (alive) setData(result);
      } catch {
        if (alive) setFailed(true);
      }
    })();
    return () => {
      alive = false;
    };
  }, []);

  if (failed) return null;
  if (!data) return <LoadingBlock />;

  const down = Object.entries(data.deviceStates ?? {})
    .filter(([k]) => /off|disconnect|adopt|provision|error/i.test(k))
    .reduce((acc, [, v]) => acc + v, 0);

  return (
    <section className="uni-panel" aria-label="Resumo UniFi">
      <header className="uni-panel-header">
        <h2>UniFi</h2>
        <Link className="uni-dash-view-all" to="/unifi/dashboard">Detalhes</Link>
      </header>
      {!data.controllerReachable && (
        <ErrorBanner message={data.controllerError ?? "Controlador UniFi inalcançável."} />
      )}
      {data.stale && (
        <p className="uni-dash-empty">Dados do último sync{data.lastSyncAtUtc ? ` (${new Date(data.lastSyncAtUtc).toLocaleString("pt-BR")})` : ""} — pode estar desatualizado.</p>
      )}
      <div className="uni-dash-unifi-grid">
        <div><strong>{data.sites}</strong><span>Sites</span></div>
        <div><strong>{data.switches}</strong><span>Switches</span></div>
        <div><strong>{data.accessPoints}</strong><span>Access Points</span></div>
        <div><strong>{data.clients}</strong><span>Clientes</span></div>
        <div>
          <strong>{data.ports ? `${data.ports.up}/${data.ports.total}` : "—"}</strong>
          <span>Portas up</span>
        </div>
        <div><strong>{down}</strong><span>Devices offline</span></div>
      </div>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* T006 — central de eventos: alertas + problemas Zabbix ao vivo         */
/* ------------------------------------------------------------------ */

interface FeedItem {
  key: string;
  at: string;
  severity: string;
  title: string;
  detail: string;
  source: string;
  resolved: boolean;
}

const SEVERITY_RANK: Record<string, number> = {
  critical: 0, disaster: 0, high: 1, warning: 2, average: 2, information: 3, not_classified: 4,
};

function severityChip(severity: string, resolved: boolean) {
  if (resolved) return { background: "#f1f5f9", color: "#64748b" };
  const s = severity.toLowerCase();
  if (s.includes("crit") || s.includes("disaster") || s === "high")
    return { background: "var(--uni-chip-red-bg)", color: "#b91c1c" };
  if (s.includes("warn") || s.includes("average") || s.includes("médio"))
    return { background: "var(--uni-chip-yellow-bg)", color: "#b45309" };
  return { background: "var(--uni-chip-blue-bg)", color: "var(--uni-text-secondary)" };
}

export function EventsCenter() {
  const [items, setItems] = useState<FeedItem[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    (async () => {
      const feed: FeedItem[] = [];
      let anyOk = false;
      try {
        const page = await listAlerts({ status: "open", pageSize: 10 });
        anyOk = true;
        for (const a of page.items as AlertItem[]) {
          feed.push({
            key: `alert-${a.id}`,
            at: a.openedAt,
            severity: a.severity,
            title: a.message,
            detail: [a.deviceName, a.check].filter(Boolean).join(" · ") || a.source,
            source: a.source === "Zabbix" ? "Zabbix" : "Interno",
            resolved: a.status !== "Open",
          });
        }
      } catch { /* alertas indisponíveis — segue com o que houver */ }
      try {
        const problems = await getZabbixProblems({ includeRecovered: false });
        anyOk = true;
        for (const p of problems.items.slice(0, 10) as ZabbixProblemView[]) {
          feed.push({
            key: `zbx-${p.eventId}`,
            at: p.clockUtc,
            severity: p.severityLabel,
            title: p.name,
            detail: p.hostNames.join(", "),
            source: "Zabbix",
            resolved: p.recovered,
          });
        }
      } catch { /* Zabbix indisponível/não configurado */ }
      if (!alive) return;
      if (!anyOk) {
        setFailed(true);
        return;
      }
      feed.sort((a, b) => {
        const sev = (SEVERITY_RANK[a.severity.toLowerCase()] ?? 5) - (SEVERITY_RANK[b.severity.toLowerCase()] ?? 5);
        return sev !== 0 ? sev : b.at.localeCompare(a.at);
      });
      setItems(feed.slice(0, 15));
    })();
    return () => {
      alive = false;
    };
  }, []);

  if (failed) return null;
  if (!items) return <LoadingBlock />;

  return (
    <section className="uni-panel" aria-label="Central de eventos">
      <header className="uni-panel-header">
        <h2>Eventos recentes</h2>
        <Link className="uni-dash-view-all" to="/alerts">Ver alertas</Link>
      </header>
      {items.length === 0 ? (
        <p className="uni-dash-empty">Nenhum evento recente.</p>
      ) : (
        <ul className="uni-dash-events">
          {items.map((item) => (
            <li key={item.key} className="uni-dash-event">
              <StatusChip
                {...severityChip(item.severity, item.resolved)}
                label={item.severity}
                fontSize={9}
              />
              <div className="uni-dash-event-body">
                <span className="uni-dash-event-title">{item.title}</span>
                <span className="uni-dash-event-meta">
                  {item.detail} · {item.source} · {new Date(item.at).toLocaleString("pt-BR")}
                  {item.resolved ? " · resolvido" : ""}
                </span>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
