import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { listIncidents, type IncidentItem } from "../services/api/incidents";
import { listDevices } from "../services/api/inventory";
import { getZabbixSummary } from "../services/api/zabbix";
import { useAudio } from "../hooks";
import { useOperationalRefresh } from "../hooks/useOperationalRefresh";
import { MetricChart } from "@/components/ui/MetricChart";

type OpsView = "overview" | "incidents" | "network" | "servers" | "cameras";

type NocTheme = "dark" | "light";

const VIEWS: { id: OpsView; label: string }[] = [
  { id: "overview", label: "Visão Geral" },
  { id: "incidents", label: "Incidentes" },
  { id: "network", label: "Rede" },
  { id: "servers", label: "Servidores e Serviços" },
  { id: "cameras", label: "Câmeras e Operações" },
];

const ROTATION_MS = 60000;

function getDurationLabel(iso: string) {
  const start = new Date(iso).getTime();
  const now = Date.now();
  const diffMs = Math.max(0, now - start);
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 60) return `há ${diffMin} min`;
  const diffH = Math.floor(diffMin / 60);
  if (diffH < 24) return `há ${diffH}h ${diffMin % 60}min`;
  const diffD = Math.floor(diffH / 24);
  return `há ${diffD}d ${diffH % 24}h`;
}

function SeverityDot({ severity }: { severity: string }) {
  const sev = severity?.toLowerCase() ?? "low";
  if (sev === "critical") return <span className="noc-sev-dot noc-sev-dot--crit" />;
  if (sev === "high") return <span className="noc-sev-dot noc-sev-dot--warn" />;
  if (sev === "medium") return <span className="noc-sev-dot noc-sev-dot--info" />;
  return <span className="noc-sev-dot noc-sev-dot--ok" />;
}

function CriticalTicker({ items }: { items: IncidentItem[] }) {
  if (items.length === 0) return null;
  const text = items
    .map((item) => `[${item.severity.toUpperCase()}] ${item.title} • ${getDurationLabel(item.openedAt)}`)
    .join("   •   ");
  return (
    <div className="noc-ticker">
      <div className="noc-ticker__label">
        <span className="noc-ticker__dot" />
        <span>CRÍTICO</span>
      </div>
      <div className="noc-ticker__item">
        <span>{text}</span>
        <span className="noc-ticker__sep">•</span>
        <span>{text}</span>
      </div>
    </div>
  );
}

function ViewOverview({
  incidents,
  servers,
  networkCount,
  printers,
  cameras,
  zabbixOk,
}: {
  incidents: IncidentItem[];
  servers: number;
  networkCount: number;
  printers: number;
  cameras: number;
  zabbixOk: boolean;
}) {
  const open = incidents.filter((i) => i.status !== "Resolved");
  const critical = open.filter((i) => i.severity?.toLowerCase() === "critical");
  const high = open.filter((i) => i.severity?.toLowerCase() === "high");
  const top = [...open].sort((a, b) => {
    const order = (s: string) => (s === "Critical" ? 0 : s === "High" ? 1 : s === "Medium" ? 2 : 3);
    return order(a.severity) - order(b.severity);
  }).slice(0, 6);

  return (
    <>
      <div className="noc-kpi-row">
        <div className={`noc-kpi ${critical.length > 0 ? "is-crit" : ""}`}>
          <span className="noc-kpi__value">{critical.length.toString().padStart(2, "0")}</span>
          <span className="noc-kpi__label">Críticos abertos</span>
          <span className="noc-kpi__sub">Prioridade máxima</span>
        </div>
        <div className={`noc-kpi ${high.length > 0 ? "is-warn" : ""}`}>
          <span className="noc-kpi__value">{open.length.toString().padStart(2, "0")}</span>
          <span className="noc-kpi__label">Incidentes abertos</span>
          <span className="noc-kpi__sub">Aguardando ação</span>
        </div>
        <div className="noc-kpi">
          <span className="noc-kpi__value">{servers.toString().padStart(2, "0")}</span>
          <span className="noc-kpi__label">Servidores</span>
          <span className="noc-kpi__sub">Inventariados</span>
        </div>
        <div className="noc-kpi">
          <span className="noc-kpi__value">{networkCount.toString().padStart(2, "0")}</span>
          <span className="noc-kpi__label">Rede</span>
          <span className="noc-kpi__sub">Switch/Router/Firewall/AP</span>
        </div>
        <div className="noc-kpi">
          <span className="noc-kpi__value">{zabbixOk ? "OK" : "N/D"}</span>
          <span className="noc-kpi__label">Monitoramento</span>
          <span className="noc-kpi__sub">{cameras} câmeras • {printers} impressoras</span>
        </div>
      </div>
      <div className="noc-grid">
        <section className="noc-panel noc-panel--wide">
          <header className="noc-panel__head">
            <div className="noc-panel__title">
              <span>Incidentes prioritários</span>
            </div>
            <span className="noc-text-muted">Ordenado por severidade</span>
          </header>
          <div className="noc-panel__body">
            {top.length === 0 ? (
              <div className="noc-empty">Sem incidentes abertos</div>
            ) : (
              <table className="noc-table">
                <thead>
                  <tr>
                    <th>Severidade</th>
                    <th>Ocorrência</th>
                    <th>Status</th>
                    <th>Duração</th>
                  </tr>
                </thead>
                <tbody>
                  {top.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <div className="noc-sev">
                          <SeverityDot severity={item.severity} />
                          <span className="noc-text-strong">{item.severity}</span>
                        </div>
                      </td>
                      <td className="noc-text-strong">{item.title}</td>
                      <td className="noc-text-sub">{item.status}</td>
                      <td className="noc-text-muted">{getDurationLabel(item.openedAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </section>
        <section className="noc-panel">
          <header className="noc-panel__head">
            <div className="noc-panel__title">
              <span>Rede</span>
            </div>
            <span className="noc-text-muted">Visão rápida</span>
          </header>
          <div className="noc-panel__body">
            <div className="noc-list">
              <div className="noc-list-item">
                <span className="noc-text-strong">Dispositivos de rede</span>
                <span className="noc-text-sub">{networkCount} inventariados</span>
                <span className="noc-pill is-ok">Monitoração ativa</span>
              </div>
              <div className="noc-list-item">
                <span className="noc-text-strong">Zabbix</span>
                <span className="noc-text-sub">{zabbixOk ? "Conectado" : "Indisponível"}</span>
                <span className={`noc-pill ${zabbixOk ? "is-ok" : "is-warn"}`}>{zabbixOk ? "OK" : "VERIFICAR"}</span>
              </div>
            </div>
          </div>
        </section>
        <section className="noc-panel">
          <header className="noc-panel__head">
            <div className="noc-panel__title">
              <span>Câmeras e Operações</span>
            </div>
            <span className="noc-text-muted">Inventário</span>
          </header>
          <div className="noc-panel__body">
            <div className="noc-list">
              <div className="noc-list-item">
                <span className="noc-text-strong">Câmeras</span>
                <span className="noc-text-sub">{cameras} registradas</span>
                <span className="noc-pill is-ok">Inventário</span>
              </div>
              <div className="noc-list-item">
                <span className="noc-text-strong">Impressoras</span>
                <span className="noc-text-sub">{printers} registradas</span>
                <span className="noc-pill is-ok">Inventário</span>
              </div>
            </div>
          </div>
        </section>
      </div>
    </>
  );
}

function ViewIncidents({ incidents }: { incidents: IncidentItem[] }) {
  const open = [...incidents].filter((i) => i.status !== "Resolved").sort((a, b) => {
    const order = (s: string) => (s === "Critical" ? 0 : s === "High" ? 1 : s === "Medium" ? 2 : 3);
    return order(a.severity) - order(b.severity);
  });
  return (
    <div className="noc-grid noc-grid--full">
      <section className="noc-panel">
        <header className="noc-panel__head">
          <div className="noc-panel__title">
            <span>Central de Incidentes</span>
          </div>
          <span className="noc-text-muted">{open.length} abertos</span>
        </header>
        <div className="noc-panel__body">
          {open.length === 0 ? (
            <div className="noc-empty">Sem incidentes abertos</div>
          ) : (
            <table className="noc-table">
              <thead>
                <tr>
                  <th>Severidade</th>
                  <th>Título</th>
                  <th>Status</th>
                  <th>Aberto</th>
                  <th>Duração</th>
                </tr>
              </thead>
              <tbody>
                {open.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <div className="noc-sev">
                        <SeverityDot severity={item.severity} />
                        <span className="noc-text-strong">{item.severity}</span>
                      </div>
                    </td>
                    <td className="noc-text-strong">{item.title}</td>
                    <td className="noc-text-sub">{item.status}</td>
                    <td className="noc-text-muted">{new Date(item.openedAt).toLocaleTimeString("pt-BR")}</td>
                    <td className="noc-text-muted">{getDurationLabel(item.openedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </section>
    </div>
  );
}

function ViewNetwork({ switches }: { switches: number }) {
  return (
    <div className="noc-grid noc-grid--full">
      <section className="noc-panel">
        <header className="noc-panel__head">
          <div className="noc-panel__title">
            <span>Rede / Conectividade</span>
          </div>
          <span className="noc-text-muted">Switches, roteadores, firewall e access points</span>
        </header>
        <div className="noc-panel__body">
          <div className="noc-list">
            <div className="noc-list-item">
              <span className="noc-text-strong">Dispositivos de rede</span>
              <span className="noc-text-sub">{switches} inventariados</span>
              <span className="noc-pill is-ok">Monitoração ativa</span>
            </div>
            <div className="noc-list-item">
              <span className="noc-text-strong">UniFi / SNMP</span>
              <span className="noc-text-sub">Integrações disponíveis no módulo de rede</span>
              <span className="noc-pill is-ok">Configurável</span>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

function ViewServers({ servers }: { servers: number }) {
  return (
    <div className="noc-grid noc-grid--full">
      <section className="noc-panel">
        <header className="noc-panel__head">
          <div className="noc-panel__title">
            <span>Servidores e Serviços</span>
          </div>
          <span className="noc-text-muted">{servers} servidores inventariados</span>
        </header>
        <div className="noc-panel__body">
          <div className="noc-list">
            <div className="noc-list-item">
              <span className="noc-text-strong">Servidores</span>
              <span className="noc-text-sub">{servers} registrados no inventário</span>
              <span className="noc-pill is-ok">Ativo</span>
            </div>
            <div className="noc-list-item">
              <span className="noc-text-strong">Serviços críticos</span>
              <span className="noc-text-sub">PostgreSQL • Redis • MassTransit</span>
              <span className="noc-pill is-ok">Ver /health</span>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

function ViewCamerasOps({ cameras, printers, phones }: { cameras: number; printers: number; phones: number }) {
  return (
    <div className="noc-grid noc-grid--full">
      <section className="noc-panel">
        <header className="noc-panel__head">
          <div className="noc-panel__title">
            <span>Câmeras e Operações</span>
          </div>
          <span className="noc-text-muted">Câmeras • Impressoras • Telefonia</span>
        </header>
        <div className="noc-panel__body">
          <div className="noc-list">
            <div className="noc-list-item">
              <span className="noc-text-strong">Câmeras</span>
              <span className="noc-text-sub">{cameras} registradas</span>
              <span className="noc-pill is-ok">Inventário</span>
            </div>
            <div className="noc-list-item">
              <span className="noc-text-strong">Impressoras</span>
              <span className="noc-text-sub">{printers} registradas</span>
              <span className="noc-pill is-ok">Inventário</span>
            </div>
            <div className="noc-list-item">
              <span className="noc-text-strong">Telefones</span>
              <span className="noc-text-sub">{phones} registrados</span>
              <span className="noc-pill is-ok">Inventário</span>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

export function OperationsCenterPage() {
  const [idx, setIdx] = useState(0);
  const [now, setNow] = useState(new Date());
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [lastUpdate, setLastUpdate] = useState(new Date());
  const [theme, setTheme] = useState<NocTheme>(() => {
    if (typeof window === "undefined") return "dark";
    const stored = localStorage.getItem("noc-theme");
    return stored === "light" || stored === "dark" ? stored : "dark";
  });
  const queryClient = useQueryClient();
  const announcedRef = useRef<Set<string>>(new Set());

  const {
    isInitialized: audioInitialized,
    isInitializing: audioInitializing,
    config: audioConfig,
    initialize: initAudio,
    announce,
    updateConfig: updateAudioConfig,
  } = useAudio();

  const view = VIEWS[idx] || VIEWS[0];

  const { data: incidents = [] } = useQuery({
    queryKey: ["ops-incidents"],
    queryFn: () => listIncidents({ page: 1, pageSize: 100 }),
    select: (data) => data.items ?? [],
    refetchInterval: 30000,
  });

  const { data: servers = 0 } = useQuery({
    queryKey: ["ops-servers"],
    queryFn: () => listDevices({ type: "Server", page: 1, pageSize: 1 }),
    select: (data) => data.totalCount ?? 0,
    refetchInterval: 60000,
  });

  const { data: networkCount = 0 } = useQuery({
    queryKey: ["ops-network-count"],
    queryFn: async () => {
      const [sw, rt, fw, ap] = await Promise.all([
        listDevices({ type: "Switch", page: 1, pageSize: 1 }),
        listDevices({ type: "Router", page: 1, pageSize: 1 }),
        listDevices({ type: "Firewall", page: 1, pageSize: 1 }),
        listDevices({ type: "AccessPoint", page: 1, pageSize: 1 }),
      ]);
      return (sw.totalCount ?? 0) + (rt.totalCount ?? 0) + (fw.totalCount ?? 0) + (ap.totalCount ?? 0);
    },
    refetchInterval: 60000,
  });

  const { data: printers = 0 } = useQuery({
    queryKey: ["ops-printers"],
    queryFn: () => listDevices({ type: "Printer", page: 1, pageSize: 1 }),
    select: (data) => data.totalCount ?? 0,
    refetchInterval: 60000,
  });

  const { data: cameras = 0 } = useQuery({
    queryKey: ["ops-cameras"],
    queryFn: () => listDevices({ type: "Camera", page: 1, pageSize: 1 }),
    select: (data) => data.totalCount ?? 0,
    refetchInterval: 60000,
  });

  const { data: phones = 0 } = useQuery({
    queryKey: ["ops-phones"],
    queryFn: () => listDevices({ type: "Phone", page: 1, pageSize: 1 }),
    select: (data) => data.totalCount ?? 0,
    refetchInterval: 60000,
  });

  const { data: zabbixSummary } = useQuery({
    queryKey: ["ops-zabbix"],
    queryFn: getZabbixSummary,
    refetchInterval: 60000,
  });

  useOperationalRefresh(() => {
    queryClient.invalidateQueries({ queryKey: ["ops-incidents"] });
  });

  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(t);
  }, []);

  useEffect(() => {
    const t = setInterval(() => {
      if (document.hidden) return;
      setIdx((i) => (i + 1) % VIEWS.length);
    }, ROTATION_MS);
    return () => clearInterval(t);
  }, []);

  useEffect(() => {
    setLastUpdate(new Date());
  }, [incidents, zabbixSummary]);

  useEffect(() => {
    const handler = () => {
      const fs = !!document.fullscreenElement;
      setIsFullscreen(fs);
      if (fs) {
        document.body.classList.add("noc-fullscreen-active");
      } else {
        document.body.classList.remove("noc-fullscreen-active");
      }
    };
    document.addEventListener("fullscreenchange", handler);
    return () => {
      document.removeEventListener("fullscreenchange", handler);
      document.body.classList.remove("noc-fullscreen-active");
    };
  }, []);

  useEffect(() => {
    localStorage.setItem("noc-theme", theme);
  }, [theme]);

  const open = useMemo(() => incidents.filter((i) => i.status !== "Resolved"), [incidents]);
  const critical = useMemo(() => open.filter((i) => i.severity?.toLowerCase() === "critical"), [open]);
  const high = useMemo(() => open.filter((i) => i.severity?.toLowerCase() === "high"), [open]);

  useEffect(() => {
    if (!audioInitialized) return;
    const newCritical = critical.filter((c) => !announcedRef.current.has(c.id));
    for (const item of newCritical) {
      announcedRef.current.add(item.id);
      announce(`Alerta crítico: ${item.title}`, "critical");
    }
  }, [critical, audioInitialized, announce]);

  const overallState = critical.length > 0 ? "crit" : high.length > 0 ? "warn" : "ok";

  const toggleFullscreen = useCallback(() => {
    if (!document.fullscreenElement) {
      document.documentElement
        .requestFullscreen()
        .catch(() => {
          // ignore
        });
    } else {
      document.exitFullscreen().catch(() => {
        // ignore
      });
    }
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme((t) => (t === "dark" ? "light" : "dark"));
  }, []);

  const handleInitAudio = useCallback(async () => {
    await initAudio();
  }, [initAudio]);

  const toggleAudio = useCallback(() => {
    updateAudioConfig({ enabled: !audioConfig.enabled });
  }, [audioConfig.enabled, updateAudioConfig]);

  const handleTestAudio = useCallback(async () => {
    if (audioInitialized) {
      await announce("Teste de áudio - Central de Operações", "info");
    }
  }, [audioInitialized, announce]);

  const severityChartData = useMemo(() => {
    const crit = open.filter((i) => i.severity?.toLowerCase() === "critical").length;
    const high = open.filter((i) => i.severity?.toLowerCase() === "high").length;
    const med = open.filter((i) => i.severity?.toLowerCase() === "medium").length;
    const low = open.filter((i) => i.severity?.toLowerCase() === "low").length;
    return {
      labels: ["Crítico", "Alto", "Médio", "Baixo"],
      datasets: [
        {
          label: "Incidentes",
          data: [crit, high, med, low],
          backgroundColor: ["rgba(239,68,68,0.9)", "rgba(245,158,11,0.9)", "rgba(56,189,248,0.9)", "rgba(148,163,184,0.8)"],
        },
      ],
    };
  }, [open]);

  const capacityChartData = useMemo(() => {
    const total = servers + networkCount + printers + cameras;
    const safe = Math.max(total - critical.length - Math.floor(high.length / 2), 1);
    const warn = Math.max(critical.length + high.length, 0);
    const down = critical.length;
    return {
      labels: ["Operacional", "Atenção", "Crítico"],
      datasets: [
        {
          label: "Capacidade",
          data: [safe, warn, down],
          backgroundColor: ["rgba(34,197,94,0.9)", "rgba(245,158,11,0.9)", "rgba(239,68,68,0.9)"],
        },
      ],
    };
  }, [servers, networkCount, printers, cameras, critical.length, high.length]);

  // Chart options handled by MetricChart defaults for TV legibility.

  return (
    <div className={`noc-page noc-page--${theme}`}>
      <header className="noc-header">
        <div className="noc-brand">
          <span className="noc-logo">INFRA CONTROLE</span>
          <span className="noc-sep">|</span>
          <span className="noc-title">CENTRAL DE OPERAÇÕES</span>
        </div>
        <div className="noc-meta">
          <span className={`noc-state noc-state--${overallState}`}>ESTADO: {overallState === "crit" ? "CRÍTICO" : overallState === "warn" ? "ATENÇÃO" : "OK"}</span>
          <span className="noc-meta-item">{now.toLocaleString("pt-BR")}</span>
          <span className="noc-meta-item">CONEXÃO: ONLINE</span>
          <span className="noc-meta-item">ÚLTIMA ATUALIZAÇÃO: {lastUpdate.toLocaleTimeString("pt-BR")}</span>
          <span className="noc-pager">{idx + 1} / {VIEWS.length} • {view?.label}</span>
          <div className="noc-tools">
            <button onClick={toggleTheme} className="noc-btn">
              <i className={`pi ${theme === "dark" ? "pi-sun" : "pi-moon"}`} />
              <span>{theme === "dark" ? "CLARO" : "ESCURO"}</span>
            </button>
            {!audioInitialized ? (
              <button onClick={handleInitAudio} disabled={audioInitializing} className="noc-btn">
                <i className={`pi ${audioInitializing ? "pi-spinner pi-spin" : "pi-volume-up"}`} />
                <span>{audioInitializing ? "INICIALIZANDO" : "ATIVAR ÁUDIO"}</span>
              </button>
            ) : (
              <>
                <button onClick={handleTestAudio} className="noc-btn">
                  <i className="pi pi-volume-up" />
                  <span>TESTAR</span>
                </button>
                <button onClick={toggleAudio} className={`noc-btn ${audioConfig.enabled ? "is-active" : ""}`}>
                  <i className={`pi ${audioConfig.enabled ? "pi-volume-up" : "pi-volume-off"}`} />
                  <span>{audioConfig.enabled ? "ÁUDIO ON" : "ÁUDIO OFF"}</span>
                </button>
              </>
            )}
            <button onClick={toggleFullscreen} className="noc-btn is-primary">
              <i className={`pi ${isFullscreen ? "pi-window-minimize" : "pi-window-maximize"}`} />
              <span>{isFullscreen ? "SAIR TELA CHEIA" : "TELA CHEIA"}</span>
            </button>
          </div>
        </div>
      </header>

      <CriticalTicker items={critical} />

      <main className="noc-main">
        {view?.id === "overview" && (
          <>
            <ViewOverview
              incidents={incidents}
              servers={servers}
              networkCount={networkCount}
              printers={printers}
              cameras={cameras}
              zabbixOk={!!zabbixSummary}
            />
            <div className="noc-charts">
              <section className="noc-panel">
                <header className="noc-panel__head">
                  <div className="noc-panel__title">
                    <span>Incidentes por severidade</span>
                  </div>
                  <span className="noc-text-muted">Tempo real</span>
                </header>
                <div className="noc-panel__body noc-panel__body--chart">
                  <MetricChart
                    type="bar"
                    labels={severityChartData.labels}
                    datasets={severityChartData.datasets}
                  />
                </div>
              </section>
              <section className="noc-panel">
                <header className="noc-panel__head">
                  <div className="noc-panel__title">
                    <span>Saúde da infraestrutura</span>
                  </div>
                  <span className="noc-text-muted">Baseado em inventário + incidentes</span>
                </header>
                <div className="noc-panel__body noc-panel__body--chart">
                  <MetricChart
                    type="doughnut"
                    labels={capacityChartData.labels}
                    datasets={capacityChartData.datasets}
                  />
                </div>
              </section>
            </div>
          </>
        )}
        {view?.id === "incidents" && <ViewIncidents incidents={incidents} />}
        {view?.id === "network" && <ViewNetwork switches={networkCount} />}
        {view?.id === "servers" && <ViewServers servers={servers} />}
        {view?.id === "cameras" && <ViewCamerasOps cameras={cameras} printers={printers} phones={phones} />}
      </main>
    </div>
  );
}
