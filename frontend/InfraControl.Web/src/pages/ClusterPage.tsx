import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { Button } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/SelectField";
import { SwitchField } from "@/components/ui/SwitchField";
import { FormField } from "@/components/ui/FormField";
import { StatusChip } from "@/components/ui/StatusChip";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import {
  checkAllInstances,
  checkServiceInstance,
  deleteServiceInstance,
  dockerContainerAction,
  getClusterInsights,
  getClusterOverview,
  getComposeServices,
  getDockerContainerLogs,
  getDockerContainers,
  getDockerLogsInsight,
  getDockerTopology,
  getTrafficMetrics,
  removeDockerContainer,
  scaleComposeService,
  updateDockerResources,
  upsertServiceInstance,
  type ClusterInsight,
  type ClusterOverview,
  type ComposeServiceItem,
  type DockerContainerItem,
  type DockerContainerLogs,
  type DockerContainersResult,
  type DockerTopologyResult,
  type ServiceInstanceItem,
  type TrafficMetrics,
} from "@/services/api/accessPolicies";
import { formatAiDuration, formatBytes, formatDateTime } from "@/utils/format";

/**
 * Administração geral do cluster: instâncias/réplicas registradas pelo admin,
 * containers Docker reais do projeto (estado, CPU, memória, logs, ações de
 * ciclo de vida) e análise assistida por IA — tudo em português.
 */

const KIND_OPTIONS = [
  { label: "API", value: "api" },
  { label: "Frontend", value: "frontend" },
  { label: "Worker", value: "worker" },
  { label: "Banco de dados", value: "database" },
  { label: "Cache", value: "cache" },
  { label: "Fila", value: "queue" },
  { label: "Integração", value: "integration" },
  { label: "Outro", value: "custom" },
];

const KIND_LABEL: Record<string, string> = Object.fromEntries(
  KIND_OPTIONS.map((o) => [o.value, o.label]),
);

const CHIP: Record<string, { background: string; color: string }> = {
  success: { background: "#dcfce7", color: "#166534" },
  danger: { background: "#fee2e2", color: "#dc2626" },
  warning: { background: "#fef3c7", color: "#b45309" },
  secondary: { background: "#f1f5f9", color: "#64748b" },
  info: { background: "#dbeafe", color: "#2563eb" },
  help: { background: "#f5f3ff", color: "#6d28d9" },
};

function chip(label: string, tone: keyof typeof CHIP | string) {
  const style = CHIP[tone] ?? CHIP.secondary!;
  return <StatusChip label={label} background={style.background} color={style.color} />;
}

function statusInstanciaTag(status: string | null) {
  if (status === "up") return chip("Online", "success");
  if (status === "down") return chip("Offline", "danger");
  return chip(status === "unknown" ? "Sem URL" : "Não verificado", "secondary");
}

function estadoDockerTag(state: string) {
  if (state === "running") return chip("Em execução", "success");
  if (state === "exited" || state === "dead") return chip(state === "exited" ? "Parado" : "Falhou", "danger");
  return chip(state, "warning");
}

function saudeDockerTag(health: string | null | undefined) {
  if (!health) return <span style={{ fontSize: 12, color: "#94a3b8" }}>—</span>;
  if (health === "healthy") return chip("Saudável", "success");
  if (health === "unhealthy") return chip("Insalubre", "danger");
  if (health === "starting") return chip("Iniciando", "warning");
  return chip(health, "secondary");
}

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: "100%",
};

const gridStyle: React.CSSProperties = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
  gap: 12,
};

const thStyle: React.CSSProperties = {
  padding: "8px 12px 8px 0",
  fontWeight: 600,
  fontSize: 12,
  color: "#475569",
  textAlign: "left",
};

const tdPad: React.CSSProperties = {
  padding: "8px 12px 8px 0",
  fontSize: 13,
  verticalAlign: "top",
};

const tdMono: React.CSSProperties = {
  ...tdPad,
  fontFamily: "ui-monospace, monospace",
  fontSize: 12,
};

const rowStyle: React.CSSProperties = { borderBottom: "1px solid #f1f5f9" };

const panelStyle: React.CSSProperties = { padding: 16 };

function StatCard({ value, label, color }: { value: number | string; label: string; color: string }) {
  return (
    <div className="uni-panel" style={panelStyle}>
      <div style={{ fontSize: 28, fontWeight: 600, color }}>{value}</div>
      <div style={{ fontSize: 13, color: "#6b7280" }}>{label}</div>
    </div>
  );
}

function Box({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        border: "1px solid #e2e8f0",
        borderRadius: 8,
        padding: 12,
        background: "#f8fafc",
      }}
    >
      {children}
    </div>
  );
}

function PanelTitle({ icon, title, subtitle }: { icon: string; title: string; subtitle?: string }) {
  return (
    <div>
      <h3 style={{ fontSize: 14, fontWeight: 700, margin: 0, display: "flex", alignItems: "center", gap: 8 }}>
        <i className={icon} /> {title}
      </h3>
      {subtitle && (
        <p style={{ fontSize: 12, color: "#6b7280", margin: "2px 0 0" }}>{subtitle}</p>
      )}
    </div>
  );
}

function uptimeDesde(startedAt: string | null | undefined): string {
  if (!startedAt) return "—";
  const start = new Date(startedAt);
  if (Number.isNaN(start.getTime())) return "—";
  const diffMs = Date.now() - start.getTime();
  if (diffMs < 0) return "—";
  const minutes = Math.floor(diffMs / 60000);
  if (minutes < 1) return "<1 min";
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ${minutes % 60}min`;
  const days = Math.floor(hours / 24);
  return `${days}d ${hours % 24}h`;
}

/** Palpite de papel a partir do nome do serviço Compose (para a visão unificada). */
function kindDoServico(service: string): string {
  const s = service.toLowerCase();
  if (s.includes("api")) return "api";
  if (s.includes("front")) return "frontend";
  if (s.includes("worker")) return "worker";
  if (s.includes("postgres") || s.includes("db") || s.includes("mysql") || s.includes("mongo"))
    return "database";
  if (s.includes("redis") || s.includes("cache")) return "cache";
  if (s.includes("rabbit") || s.includes("queue") || s.includes("kafka")) return "queue";
  return "custom";
}

function formatMs(ms: number | null | undefined): string {
  if (ms == null) return "—";
  return ms >= 1000 ? `${(ms / 1000).toFixed(2)}s` : `${Math.round(ms)}ms`;
}

export function ClusterPage() {
  const [overview, setOverview] = useState<ClusterOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [probing, setProbing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ServiceInstanceItem | null>(null);
  const [form, setForm] = useState({
    kind: "custom", name: "", host: "", port: null as number | null,
    healthUrl: "", weight: 1, isActive: true, notes: "",
  });
  const [saving, setSaving] = useState(false);

  const [insight, setInsight] = useState<ClusterInsight | null>(null);
  const [insightLoading, setInsightLoading] = useState(false);
  const [logsInsight, setLogsInsight] = useState<ClusterInsight | null>(null);
  const [logsInsightScope, setLogsInsightScope] = useState<string>("cluster");
  const [logsInsightLoading, setLogsInsightLoading] = useState(false);

  const [docker, setDocker] = useState<DockerContainersResult | null>(null);
  const [dockerLoading, setDockerLoading] = useState(true);
  const [dockerBusy, setDockerBusy] = useState<string | null>(null);
  const [topology, setTopology] = useState<DockerTopologyResult | null>(null);
  const [composeServices, setComposeServices] = useState<ComposeServiceItem[] | null>(null);
  const [composeUnavailable, setComposeUnavailable] = useState(false);
  const [scaleDialog, setScaleDialog] = useState<{
    open: boolean; service: string; replicas: number; saving: boolean;
  }>({ open: false, service: "", replicas: 1, saving: false });
  const [removeDialog, setRemoveDialog] = useState<{
    open: boolean; name: string; confirm: string; reason: string; removing: boolean;
  }>({ open: false, name: "", confirm: "", reason: "", removing: false });
  const [limitsDialog, setLimitsDialog] = useState<{
    open: boolean; container: DockerContainerItem | null;
    memoryMb: number | null; cpuCores: number | null; saving: boolean;
  }>({ open: false, container: null, memoryMb: null, cpuCores: null, saving: false });
  const [confirmRemoveInstance, setConfirmRemoveInstance] = useState<ServiceInstanceItem | null>(null);
  const [confirmAction, setConfirmAction] = useState<{
    c: DockerContainerItem;
    action: "start" | "stop" | "restart";
    label: string;
  } | null>(null);
  const [details, setDetails] = useState<DockerContainerItem | null>(null);
  const [traffic, setTraffic] = useState<TrafficMetrics | null>(null);
  const [trafficWindow, setTrafficWindow] = useState(60);

  const [logsDialog, setLogsDialog] = useState<{
    open: boolean;
    name: string;
    loading: boolean;
    data: DockerContainerLogs | null;
    error: string | null;
    analyzing: boolean;
  }>({ open: false, name: "", loading: false, data: null, error: null, analyzing: false });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setOverview(await getClusterOverview());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar o cluster.");
    } finally {
      setLoading(false);
    }
  }, []);

  const loadDocker = useCallback(async () => {
    setDockerLoading(true);
    try {
      const [containers, topo, traf, services] = await Promise.all([
        getDockerContainers(),
        getDockerTopology().catch(() => null),
        getTrafficMetrics(trafficWindow).catch(() => null),
        getComposeServices().catch(() => null),
      ]);
      setDocker(containers);
      setTopology(topo);
      setTraffic(traf);
      setComposeServices(services);
      setComposeUnavailable(services === null);
    } catch {
      setDocker({ available: false, containers: [] });
    } finally {
      setDockerLoading(false);
    }
  }, [trafficWindow]);

  useEffect(() => {
    void load();
    void loadDocker();
  }, [load, loadDocker]);

  const probeAll = async () => {
    setProbing(true);
    setError(null);
    try {
      setOverview(await checkAllInstances());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao verificar instâncias.");
    } finally {
      setProbing(false);
    }
  };

  const probeOne = async (id: string) => {
    try {
      await checkServiceInstance(id);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha na verificação.");
    }
  };

  const openCreate = () => {
    setEditing(null);
    setForm({ kind: "custom", name: "", host: "", port: null, healthUrl: "", weight: 1, isActive: true, notes: "" });
    setDialogOpen(true);
  };

  const openEdit = (i: ServiceInstanceItem) => {
    setEditing(i);
    setForm({
      kind: i.kind, name: i.name, host: i.host ?? "", port: i.port,
      healthUrl: i.healthUrl ?? "", weight: i.weight, isActive: i.isActive,
      notes: i.notes ?? "",
    });
    setDialogOpen(true);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await upsertServiceInstance({
        id: editing?.id ?? null,
        kind: form.kind,
        name: form.name,
        host: form.host || null,
        port: form.port,
        healthUrl: form.healthUrl || null,
        weight: form.weight,
        isActive: form.isActive,
        notes: form.notes || null,
      });
      setDialogOpen(false);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao salvar instância.");
    } finally {
      setSaving(false);
    }
  };

  const remove = async (i: ServiceInstanceItem) => {
    try {
      await deleteServiceInstance(i.id);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao remover réplica.");
    }
  };

  const runInsights = async () => {
    setInsightLoading(true);
    setError(null);
    try {
      setInsight(await getClusterInsights());
    } catch (err) {
      setError(err instanceof Error ? err.message : "A IA não conseguiu analisar o cluster.");
    } finally {
      setInsightLoading(false);
    }
  };

  const runLogsInsight = async (container?: string, service?: string) => {
    setLogsInsightLoading(true);
    setError(null);
    try {
      setLogsInsight(await getDockerLogsInsight(container, service));
      setLogsInsightScope(
        service ? `serviço ${service}` : container ? `container ${container}` : "cluster",
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "A IA não conseguiu analisar o container.");
    } finally {
      setLogsInsightLoading(false);
    }
  };

  const dockerAction = async (
    c: DockerContainerItem,
    action: "start" | "stop" | "restart",
  ) => {
    setDockerBusy(`${c.id}:${action}`);
    setError(null);
    try {
      await dockerContainerAction(c.name, action);
      await loadDocker();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha na ação do container.");
    } finally {
      setDockerBusy(null);
    }
  };

  const openScale = (service: string, currentTotal: number) => {
    setScaleDialog({ open: true, service, replicas: currentTotal, saving: false });
  };

  const confirmScale = async () => {
    setScaleDialog((s) => ({ ...s, saving: true }));
    setError(null);
    try {
      await scaleComposeService(scaleDialog.service, scaleDialog.replicas);
      setScaleDialog((s) => ({ ...s, open: false, saving: false }));
      await loadDocker();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao escalar serviço.");
      setScaleDialog((s) => ({ ...s, saving: false }));
    }
  };

  const openRemove = (c: DockerContainerItem) => {
    setRemoveDialog({ open: true, name: c.name, confirm: "", reason: "", removing: false });
  };

  const confirmRemove = async () => {
    setRemoveDialog((s) => ({ ...s, removing: true }));
    setError(null);
    try {
      await removeDockerContainer(removeDialog.name, removeDialog.reason, removeDialog.confirm);
      setRemoveDialog((s) => ({ ...s, open: false, removing: false }));
      await loadDocker();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao remover container.");
      setRemoveDialog((s) => ({ ...s, removing: false }));
    }
  };

  const openLimits = (c: DockerContainerItem) => {
    setLimitsDialog({
      open: true,
      container: c,
      memoryMb: c.memoryLimitBytes != null ? Math.round(c.memoryLimitBytes / 1024 / 1024) : null,
      cpuCores: c.cpuLimitCores ?? null,
      saving: false,
    });
  };

  const confirmLimits = async () => {
    const c = limitsDialog.container;
    if (!c) return;
    setLimitsDialog((s) => ({ ...s, saving: true }));
    setError(null);
    try {
      await updateDockerResources(
        c.name,
        limitsDialog.memoryMb != null ? limitsDialog.memoryMb * 1024 * 1024 : null,
        limitsDialog.cpuCores,
      );
      setLimitsDialog((s) => ({ ...s, open: false, saving: false }));
      await loadDocker();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao aplicar limites.");
      setLimitsDialog((s) => ({ ...s, saving: false }));
    }
  };

  const openLogs = async (c: DockerContainerItem) => {
    setLogsDialog({ open: true, name: c.name, loading: true, data: null, error: null, analyzing: false });
    try {
      const data = await getDockerContainerLogs(c.name, 300);
      setLogsDialog((s) => ({ ...s, loading: false, data }));
    } catch (err) {
      setLogsDialog((s) => ({
        ...s,
        loading: false,
        error: err instanceof Error ? err.message : "Falha ao ler logs.",
      }));
    }
  };

  const analyzeContainerLogs = async () => {
    setLogsDialog((s) => ({ ...s, analyzing: true }));
    try {
      const result = await getDockerLogsInsight(logsDialog.name);
      setLogsInsight(result);
      setLogsInsightScope(`container ${logsDialog.name}`);
      setLogsDialog((s) => ({ ...s, analyzing: false }));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha na análise de logs.");
      setLogsDialog((s) => ({ ...s, analyzing: false }));
    }
  };

  if (loading) {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <PageHeader title="Administração do cluster" />
        <LoadingBlock />
      </div>
    );
  }

  const dockerRunning = docker?.containers.filter((c) => c.state === "running").length ?? 0;
  const dockerStopped = (docker?.containers.length ?? 0) - dockerRunning;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Administração do cluster"
        subtitle="Containers reais do projeto, réplicas registradas, distribuição de requisições e análise por IA — tudo gerenciável aqui."
        actions={
          <>
            <Button
              icon="pi pi-refresh"
              variant="ghost"
              disabled={probing}
              onClick={() => void probeAll()}
            >
              {probing ? "Verificando…" : "Verificar réplicas"}
            </Button>
            <Button
              icon="pi pi-sparkles"
              variant="ghost"
              disabled={insightLoading}
              onClick={() => void runInsights()}
            >
              {insightLoading ? "Analisando…" : "Análise IA do cluster"}
            </Button>
            <Button
              icon="pi pi-search"
              variant="info"
              disabled={logsInsightLoading}
              onClick={() => void runLogsInsight()}
            >
              {logsInsightLoading ? "Investigando…" : "Investigar logs com IA"}
            </Button>
            <Button icon="pi pi-plus" onClick={openCreate}>
              Nova réplica
            </Button>
            <Button
              icon="pi pi-chart-bar"
              variant="ghost"
              onClick={() => window.open("/grafana/", "_blank", "noopener")}
              title="Abrir dashboards de observabilidade (Prometheus/Grafana)"
            >
              Grafana
            </Button>
          </>
        }
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div style={gridStyle}>
        <StatCard value={overview?.upCount ?? 0} label="Réplicas online" color="#16a34a" />
        <StatCard value={overview?.downCount ?? 0} label="Réplicas offline" color="#dc2626" />
        <StatCard value={dockerRunning} label="Containers em execução" color="#16a34a" />
        <StatCard value={dockerStopped} label="Containers parados" color="#d97706" />
      </div>

      <div className="uni-panel" style={panelStyle}>
        <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 12, marginBottom: 12 }}>
          <PanelTitle
            icon="pi pi-cog"
            title="Containers do projeto"
            subtitle="Apenas containers deste projeto Compose — estado, CPU e memória reais do host"
          />
          <Button
            icon="pi pi-refresh"
            variant="ghost"
            disabled={dockerLoading}
            onClick={() => void loadDocker()}
            title="Atualizar lista"
            aria-label="Atualizar containers"
          />
        </div>
        {docker === null ? (
          <LoadingBlock />
        ) : !docker.available ? (
          <NoticeBanner
            message="Docker indisponível para a API — verifique a montagem de /var/run/docker.sock no serviço api do compose."
            background="#fef3c7"
            color="#92400e"
          />
        ) : docker.containers.length === 0 ? (
          <div style={{ fontSize: 13, color: "#94a3b8" }}>Nenhum container do projeto encontrado.</div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid #e2e8f0" }}>
                  <th style={thStyle}>Container</th>
                  <th style={thStyle}>Imagem</th>
                  <th style={thStyle}>Estado</th>
                  <th style={thStyle}>Saúde</th>
                  <th style={{ ...thStyle, textAlign: "right" }}>CPU</th>
                  <th style={{ ...thStyle, textAlign: "right" }}>Memória</th>
                  <th style={{ ...thStyle, textAlign: "right" }}>Reinícios</th>
                  <th style={thStyle}>Uptime</th>
                  <th style={thStyle}>Status</th>
                  <th style={{ ...thStyle, textAlign: "right" }}>Ações</th>
                </tr>
              </thead>
              <tbody>
                {docker.containers.map((c) => (
                  <tr key={c.id} style={rowStyle}>
                    <td style={tdMono}>{c.name}</td>
                    <td style={{ ...tdMono, maxWidth: 224, overflow: "hidden", textOverflow: "ellipsis" }}>
                      {c.image}
                    </td>
                    <td style={tdPad}>{estadoDockerTag(c.state)}</td>
                    <td style={tdPad}>{saudeDockerTag(c.healthStatus)}</td>
                    <td style={{ ...tdPad, textAlign: "right" }}>
                      {c.cpuPercent != null ? `${c.cpuPercent}%` : "—"}
                    </td>
                    <td style={{ ...tdPad, textAlign: "right" }}>
                      {c.memoryPercent != null
                        ? `${formatBytes(c.memoryUsageBytes)} (${c.memoryPercent}%)`
                        : formatBytes(c.memoryUsageBytes)}
                    </td>
                    <td style={{ ...tdPad, textAlign: "right" }}>{c.restartCount ?? "—"}</td>
                    <td style={{ ...tdPad, whiteSpace: "nowrap" }}>{uptimeDesde(c.startedAt)}</td>
                    <td style={{ ...tdPad, fontSize: 12, color: "#64748b" }}>{c.status}</td>
                    <td style={{ ...tdPad, textAlign: "right", whiteSpace: "nowrap" }}>
                      {c.state !== "running" && (
                        <Button
                          variant="ghost"
                          icon="pi pi-play"
                          title="Iniciar"
                          aria-label={`Iniciar ${c.name}`}
                          disabled={dockerBusy === `${c.id}:start`}
                          onClick={() => setConfirmAction({ c, action: "start", label: "Iniciar" })}
                        />
                      )}
                      {c.state === "running" && (
                        <Button
                          variant="ghost"
                          icon="pi pi-stop"
                          title="Parar"
                          aria-label={`Parar ${c.name}`}
                          disabled={dockerBusy === `${c.id}:stop`}
                          onClick={() => setConfirmAction({ c, action: "stop", label: "Parar" })}
                        />
                      )}
                      <Button
                        variant="ghost"
                        icon="pi pi-chart-bar"
                        title="Ver métricas detalhadas"
                        aria-label={`Métricas de ${c.name}`}
                        onClick={() => setDetails(c)}
                      />
                      <Button
                        variant="ghost"
                        icon="pi pi-file"
                        title="Ver logs"
                        aria-label={`Logs de ${c.name}`}
                        onClick={() => void openLogs(c)}
                      />
                      <Button
                        variant="ghost"
                        icon="pi pi-search"
                        title="Investigar container com IA (estado, métricas e logs)"
                        aria-label={`Investigar ${c.name}`}
                        disabled={logsInsightLoading}
                        onClick={() => void runLogsInsight(c.name)}
                      />
                      <Button
                        variant="ghost"
                        icon="pi pi-refresh"
                        title="Reiniciar"
                        aria-label={`Reiniciar ${c.name}`}
                        disabled={dockerBusy === `${c.id}:restart`}
                        onClick={() => setConfirmAction({ c, action: "restart", label: "Reiniciar" })}
                      />
                      <Button
                        variant="ghost"
                        icon="pi pi-sliders-h"
                        title="Limites de CPU/memória"
                        aria-label={`Limites de ${c.name}`}
                        onClick={() => openLimits(c)}
                      />
                      {c.state !== "running" && (
                        <Button
                          variant="danger"
                          icon="pi pi-trash"
                          title="Remover container parado"
                          aria-label={`Remover ${c.name}`}
                          onClick={() => openRemove(c)}
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {docker?.available && topology?.available && topology.topology && (
        <div className="uni-panel" style={panelStyle}>
          <div style={{ marginBottom: 12 }}>
            <PanelTitle
              icon="pi pi-sitemap"
              title="Topologia Docker (ao vivo)"
              subtitle="Containers agrupados por rede Docker — dados reais do Engine a cada carregamento."
            />
          </div>
          {topology.topology.networks.length === 0 ? (
            <div style={{ fontSize: 13, color: "#94a3b8" }}>Nenhuma rede Docker detectada.</div>
          ) : (
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
                gap: 12,
              }}
            >
              {topology.topology.networks.map((net) => {
                const members = topology.topology!.containers.filter((n) =>
                  n.networks.includes(net.name),
                );
                return (
                  <Box key={net.name}>
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        marginBottom: 8,
                      }}
                    >
                      <span
                        style={{
                          fontFamily: "ui-monospace, monospace",
                          fontSize: 12,
                          fontWeight: 600,
                        }}
                      >
                        {net.name}
                      </span>
                      <span style={{ fontSize: 12, color: "#94a3b8" }}>
                        {members.length} container(s)
                      </span>
                    </div>
                    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                      {members.map((n) => (
                        <div
                          key={n.id}
                          style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                            fontSize: 13,
                          }}
                        >
                          {estadoDockerTag(n.state)}
                          <span
                            style={{
                              flex: 1,
                              overflow: "hidden",
                              textOverflow: "ellipsis",
                              whiteSpace: "nowrap",
                              fontFamily: "ui-monospace, monospace",
                              fontSize: 12,
                            }}
                            title={n.name}
                          >
                            {n.service}
                          </span>
                          {n.healthStatus && saudeDockerTag(n.healthStatus)}
                        </div>
                      ))}
                      {members.length === 0 && (
                        <div style={{ fontSize: 12, color: "#94a3b8" }}>
                          Sem containers do projeto.
                        </div>
                      )}
                    </div>
                  </Box>
                );
              })}
            </div>
          )}
        </div>
      )}

      {docker?.available && traffic && (
        <div className="uni-panel" style={panelStyle}>
          <div
            style={{
              display: "flex",
              alignItems: "flex-start",
              justifyContent: "space-between",
              gap: 12,
              marginBottom: 12,
              flexWrap: "wrap",
            }}
          >
            <PanelTitle
              icon="pi pi-chart-line"
              title="Tráfego interno da plataforma"
              subtitle="Requisições reais coletadas pelo middleware — por rota, com latência"
            />
            <SelectField
              value={String(trafficWindow)}
              onChange={(v) => setTrafficWindow(Number(v))}
              options={[
                { label: "Últimos 15 min", value: "15" },
                { label: "Última hora", value: "60" },
                { label: "Últimas 6 horas", value: "360" },
                { label: "Últimas 24 horas", value: "1440" },
              ]}
              aria-label="Janela de tráfego"
              style={{ width: 170 }}
            />
          </div>
          <div style={{ ...gridStyle, marginBottom: 16 }}>
            <Box>
              <div style={{ fontSize: 22, fontWeight: 600 }}>{traffic.totalRequests}</div>
              <div style={{ fontSize: 12, color: "#64748b" }}>Requisições na janela</div>
            </Box>
            <Box>
              <div style={{ fontSize: 22, fontWeight: 600, color: "#dc2626" }}>
                {traffic.totalErrors}
              </div>
              <div style={{ fontSize: 12, color: "#64748b" }}>
                Erros ({traffic.errorRatePercent}%)
              </div>
            </Box>
            <Box>
              <div style={{ fontSize: 22, fontWeight: 600 }}>{formatMs(traffic.avgLatencyMs)}</div>
              <div style={{ fontSize: 12, color: "#64748b" }}>Latência média</div>
            </Box>
            <Box>
              <div style={{ fontSize: 18, fontWeight: 600 }}>
                {formatMs(traffic.p50Ms)} / {formatMs(traffic.p95Ms)} / {formatMs(traffic.p99Ms)}
              </div>
              <div style={{ fontSize: 12, color: "#64748b" }}>P50 / P95 / P99</div>
            </Box>
          </div>
          {(traffic.instances?.length ?? 0) > 0 && (
            <div style={{ marginBottom: 16 }}>
              <div
                style={{
                  fontSize: 13,
                  fontWeight: 600,
                  marginBottom: 8,
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                }}
              >
                <i className="pi pi-share-alt" /> Distribuição por instância/réplica
              </div>
              <div style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse" }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid #e2e8f0" }}>
                      <th style={thStyle}>Instância</th>
                      <th style={{ ...thStyle, textAlign: "right" }}>Requisições</th>
                      <th style={{ ...thStyle, textAlign: "right" }}>Erros</th>
                      <th style={{ ...thStyle, textAlign: "right" }}>Latência média</th>
                      <th style={{ ...thStyle, textAlign: "right" }}>Participação</th>
                    </tr>
                  </thead>
                  <tbody>
                    {traffic.instances.map((inst) => {
                      const container = docker?.containers.find(
                        (c) =>
                          c.id.startsWith(inst.instance) ||
                          inst.instance.startsWith(c.id.slice(0, 12)),
                      );
                      return (
                        <tr key={inst.instance} style={rowStyle}>
                          <td style={tdMono}>
                            {container?.name ?? inst.instance}
                            {container && (
                              <span style={{ color: "#94a3b8" }}> ({inst.instance})</span>
                            )}
                          </td>
                          <td style={{ ...tdPad, textAlign: "right" }}>{inst.requests}</td>
                          <td style={{ ...tdPad, textAlign: "right" }}>{inst.errors}</td>
                          <td style={{ ...tdPad, textAlign: "right" }}>
                            {formatMs(inst.avgLatencyMs)}
                          </td>
                          <td style={{ ...tdPad, textAlign: "right" }}>{inst.sharePercent}%</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
          {traffic.routes.length === 0 ? (
            <div style={{ fontSize: 13, color: "#94a3b8" }}>
              Nenhuma requisição registrada na janela — os dados aparecem após o tráfego real
              passar pela API.
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr style={{ borderBottom: "1px solid #e2e8f0" }}>
                    <th style={thStyle}>Rota</th>
                    <th style={{ ...thStyle, textAlign: "right" }}>Reqs</th>
                    <th style={{ ...thStyle, textAlign: "right" }}>Erros</th>
                    <th style={{ ...thStyle, textAlign: "right" }}>Taxa erro</th>
                    <th style={{ ...thStyle, textAlign: "right" }}>Média</th>
                    <th style={{ ...thStyle, textAlign: "right" }}>P50</th>
                    <th style={{ ...thStyle, textAlign: "right" }}>P95</th>
                    <th style={{ ...thStyle, textAlign: "right" }}>P99</th>
                  </tr>
                </thead>
                <tbody>
                  {traffic.routes.map((r) => (
                    <tr key={r.route} style={rowStyle}>
                      <td style={tdMono}>{r.route}</td>
                      <td style={{ ...tdPad, textAlign: "right" }}>{r.requests}</td>
                      <td style={{ ...tdPad, textAlign: "right" }}>{r.errors}</td>
                      <td style={{ ...tdPad, textAlign: "right" }}>{r.errorRatePercent}%</td>
                      <td style={{ ...tdPad, textAlign: "right" }}>{formatMs(r.avgLatencyMs)}</td>
                      <td style={{ ...tdPad, textAlign: "right" }}>{formatMs(r.p50Ms)}</td>
                      <td style={{ ...tdPad, textAlign: "right" }}>{formatMs(r.p95Ms)}</td>
                      <td style={{ ...tdPad, textAlign: "right" }}>{formatMs(r.p99Ms)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {logsInsight && (
        <div
          className="uni-panel"
          style={{ ...panelStyle, borderLeft: "3px solid #3b82f6" }}
        >
          <h3 style={{ fontSize: 14, fontWeight: 700, margin: "0 0 8px" }}>
            Investigação — {logsInsightScope} ({logsInsight.provider}/{logsInsight.model}
            {formatAiDuration(logsInsight.durationMs) && ` · ${formatAiDuration(logsInsight.durationMs)}`})
          </h3>
          <div style={{ fontSize: 13, whiteSpace: "pre-wrap" }}>{logsInsight.analysis}</div>
        </div>
      )}

      {insight && (
        <div
          className="uni-panel"
          style={{ ...panelStyle, borderLeft: "3px solid #8b5cf6" }}
        >
          <h3 style={{ fontSize: 14, fontWeight: 700, margin: "0 0 8px" }}>
            Análise do cluster — {insight.provider}/{insight.model}
            {formatAiDuration(insight.durationMs) && ` · ${formatAiDuration(insight.durationMs)}`}
          </h3>
          <div style={{ fontSize: 13, whiteSpace: "pre-wrap" }}>{insight.analysis}</div>
        </div>
      )}

      {overview && overview.distribution.length > 0 && (
        <div className="uni-panel" style={panelStyle}>
          <div style={{ marginBottom: 12 }}>
            <PanelTitle
              icon="pi pi-share-alt"
              title="Distribuição de requisições por papel"
            />
          </div>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
              gap: 12,
            }}
          >
            {overview.distribution.map((d) => (
              <Box key={d.kind}>
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    marginBottom: 8,
                  }}
                >
                  <span style={{ fontWeight: 600, fontSize: 13 }}>
                    {KIND_LABEL[d.kind] ?? d.kind}
                  </span>
                  <span style={{ fontSize: 12, color: "#94a3b8" }}>
                    {d.instanceCount} réplica(s) · peso {d.totalWeight}
                  </span>
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                  {d.instances.map((i) => (
                    <div
                      key={i.id}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                        fontSize: 13,
                      }}
                    >
                      <span
                        style={{
                          flex: 1,
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                        }}
                      >
                        {i.name}
                      </span>
                      <span
                        style={{
                          fontSize: 12,
                          color: "#64748b",
                          width: 64,
                          textAlign: "right",
                        }}
                      >
                        {i.sharePercent}%
                      </span>
                    </div>
                  ))}
                </div>
              </Box>
            ))}
          </div>
        </div>
      )}

      <div className="uni-panel" style={panelStyle}>
        <div style={{ marginBottom: 12 }}>
          <PanelTitle
            icon="pi pi-clone"
            title="Instâncias e réplicas"
            subtitle="Serviços Compose reais do projeto (Docker, ao vivo) + réplicas planejadas cadastradas"
          />
        </div>
        {composeUnavailable && (
          <div style={{ marginBottom: 12 }}>
            <NoticeBanner
              message="compose-agent indisponível — a lista pode estar incompleta. Verifique o serviço compose-agent no compose."
              background="#fef3c7"
              color="#92400e"
            />
          </div>
        )}
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid #e2e8f0" }}>
                <th style={thStyle}>Instância</th>
                <th style={thStyle}>Origem</th>
                <th style={thStyle}>Papel</th>
                <th style={thStyle}>Endereço</th>
                <th style={{ ...thStyle, textAlign: "center" }}>Réplicas</th>
                <th style={{ ...thStyle, textAlign: "center" }}>Peso</th>
                <th style={thStyle}>Status</th>
                <th style={thStyle}>Última verificação</th>
                <th style={{ ...thStyle, textAlign: "right" }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {(composeServices ?? []).map((s) => {
                const kind = kindDoServico(s.service);
                const status =
                  s.total === 0
                    ? chip("Sem container", "secondary")
                    : s.running === s.total
                      ? chip("Online", "success")
                      : s.running > 0
                        ? chip("Parcial", "warning")
                        : chip("Offline", "danger");
                return (
                  <tr key={`svc:${s.service}`} style={rowStyle}>
                    <td style={tdPad}>
                      <div style={{ fontWeight: 600, fontFamily: "ui-monospace, monospace", fontSize: 12 }}>
                        {s.service}
                      </div>
                      <div style={{ fontSize: 12, color: "#94a3b8" }}>
                        {s.containers.map((c) => c.name).join(", ")}
                      </div>
                    </td>
                    <td style={tdPad}>{chip("Docker", "info")}</td>
                    <td style={tdPad}>{KIND_LABEL[kind] ?? kind}</td>
                    <td style={tdMono}>—</td>
                    <td style={{ ...tdPad, textAlign: "center" }}>
                      {s.running}/{s.total}
                    </td>
                    <td style={{ ...tdPad, textAlign: "center", fontSize: 12, color: "#94a3b8" }}>
                      —
                    </td>
                    <td style={tdPad}>{status}</td>
                    <td style={{ ...tdPad, fontSize: 12, color: "#64748b" }}>ao vivo</td>
                    <td style={{ ...tdPad, textAlign: "right", whiteSpace: "nowrap" }}>
                      <Button
                        variant="ghost"
                        icon="pi pi-sparkles"
                        title="Investigar todas as réplicas deste serviço com IA"
                        aria-label={`Investigar serviço ${s.service}`}
                        disabled={logsInsightLoading}
                        onClick={() => void runLogsInsight(undefined, s.service)}
                      />
                      <Button
                        variant="ghost"
                        icon="pi pi-sort-amount-down"
                        title="Escalar réplicas"
                        aria-label={`Escalar ${s.service}`}
                        onClick={() => openScale(s.service, s.total)}
                      />
                    </td>
                  </tr>
                );
              })}
              {(overview?.instances ?? []).map((i) => (
                <tr
                  key={`inst:${i.id}`}
                  style={{ ...rowStyle, opacity: i.isActive ? 1 : 0.5 }}
                >
                  <td style={tdPad}>
                    <div style={{ fontWeight: 600 }}>{i.name}</div>
                    {i.notes && <div style={{ fontSize: 12, color: "#94a3b8" }}>{i.notes}</div>}
                  </td>
                  <td style={tdPad}>{chip("Cadastro", "secondary")}</td>
                  <td style={tdPad}>{KIND_LABEL[i.kind] ?? i.kind}</td>
                  <td style={tdMono}>
                    {i.host ? `${i.host}${i.port ? `:${i.port}` : ""}` : "—"}
                  </td>
                  <td style={{ ...tdPad, textAlign: "center", fontSize: 12, color: "#94a3b8" }}>
                    —
                  </td>
                  <td style={{ ...tdPad, textAlign: "center" }}>{i.weight}</td>
                  <td style={tdPad}>{statusInstanciaTag(i.lastStatus)}</td>
                  <td style={{ ...tdPad, fontSize: 12, color: "#64748b" }}>
                    {i.lastCheckedAt ? formatDateTime(new Date(i.lastCheckedAt)) : "—"}
                  </td>
                  <td style={{ ...tdPad, textAlign: "right", whiteSpace: "nowrap" }}>
                    <Button
                      variant="ghost"
                      icon="pi pi-heart"
                      title="Verificar agora"
                      aria-label={`Verificar ${i.name}`}
                      disabled={!i.healthUrl}
                      onClick={() => void probeOne(i.id)}
                    />
                    <Button
                      variant="ghost"
                      icon="pi pi-pencil"
                      title="Editar"
                      aria-label={`Editar ${i.name}`}
                      onClick={() => openEdit(i)}
                    />
                    <Button
                      variant="danger"
                      icon="pi pi-trash"
                      title="Remover réplica"
                      aria-label={`Remover ${i.name}`}
                      onClick={() => setConfirmRemoveInstance(i)}
                    />
                  </td>
                </tr>
              ))}
              {(composeServices ?? []).length === 0 && (overview?.instances ?? []).length === 0 && (
                <tr>
                  <td
                    colSpan={9}
                    style={{
                      padding: 24,
                      textAlign: "center",
                      color: "#94a3b8",
                      fontSize: 13,
                    }}
                  >
                    Nenhuma instância encontrada. Os serviços Compose do projeto aparecem aqui
                    automaticamente (origem Docker); cadastre nós planejados para acompanhar a
                    distribuição de peso.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* ── Confirmações (substituem window.confirm) ── */}
      <ConfirmDialog
        visible={confirmRemoveInstance !== null}
        onHide={() => setConfirmRemoveInstance(null)}
        onConfirm={() => {
          if (confirmRemoveInstance) void remove(confirmRemoveInstance);
        }}
        header="Remover réplica"
        message={`Remover a réplica "${confirmRemoveInstance?.name ?? ""}" do registro do cluster?`}
        confirmLabel="Remover"
      />
      <ConfirmDialog
        visible={confirmAction !== null}
        onHide={() => setConfirmAction(null)}
        onConfirm={() => {
          if (confirmAction) void dockerAction(confirmAction.c, confirmAction.action);
        }}
        header={confirmAction?.label ?? "Confirmar"}
        message={`${confirmAction?.label ?? ""} o container "${confirmAction?.c.name ?? ""}"?`}
        confirmLabel={confirmAction?.label ?? "Confirmar"}
        severity={confirmAction?.action === "stop" ? "warning" : "info"}
      />

      <Dialog
        visible={details !== null}
        onHide={() => setDetails(null)}
        header={`Métricas — ${details?.name ?? ""}`}
        width={672}
      >
        {details && (
          <div style={gridStyle}>
            {[
              ["Estado", estadoDockerTag(details.state)],
              ["Saúde", saudeDockerTag(details.healthStatus)],
              [
                "CPU",
                details.cpuPercent != null ? `${details.cpuPercent}%` : "—",
              ],
              [
                "Memória",
                details.memoryPercent != null
                  ? `${formatBytes(details.memoryUsageBytes)} de ${formatBytes(details.memoryLimitBytes)} (${details.memoryPercent}%)`
                  : formatBytes(details.memoryUsageBytes),
              ],
              ["Limite de memória", formatBytes(details.memoryLimitBytes)],
              ["Rede recebida", formatBytes(details.networkRxBytes)],
              ["Rede enviada", formatBytes(details.networkTxBytes)],
              ["Disco lido", formatBytes(details.blockReadBytes)],
              ["Disco gravado", formatBytes(details.blockWriteBytes)],
              ["Processos (PIDs)", details.pidsCurrent ?? "—"],
              ["Reinícios", details.restartCount ?? "—"],
              ["Uptime", uptimeDesde(details.startedAt)],
              [
                "Iniciado em",
                details.startedAt ? formatDateTime(new Date(details.startedAt)) : "—",
              ],
              ["Imagem", details.image],
              ["Status", details.status],
            ].map(([label, value]) => (
              <Box key={label as string}>
                <div style={{ fontSize: 12, color: "#94a3b8" }}>{label}</div>
                <div style={{ fontWeight: 600, wordBreak: "break-word" }}>{value}</div>
              </Box>
            ))}
          </div>
        )}
      </Dialog>

      <Dialog
        visible={logsDialog.open}
        onHide={() => setLogsDialog((s) => ({ ...s, open: false }))}
        header={`Logs — ${logsDialog.name}`}
        width={800}
        footer={
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              icon="pi pi-search"
              variant="info"
              disabled={logsDialog.analyzing}
              onClick={() => void analyzeContainerLogs()}
            >
              {logsDialog.analyzing ? "Analisando…" : "Analisar com IA"}
            </Button>
          </div>
        }
      >
        {logsDialog.loading ? (
          <LoadingBlock />
        ) : logsDialog.error ? (
          <ErrorBanner message={logsDialog.error} />
        ) : (
          <pre
            style={{
              fontSize: 12,
              background: "#0f172a",
              color: "#86efac",
              borderRadius: 6,
              padding: 12,
              overflow: "auto",
              maxHeight: "60vh",
              whiteSpace: "pre-wrap",
              wordBreak: "break-all",
              margin: 0,
            }}
          >
            {logsDialog.data?.logs || "(sem saída)"}
          </pre>
        )}
      </Dialog>

      <Dialog
        visible={dialogOpen}
        onHide={() => setDialogOpen(false)}
        header={editing ? "Editar réplica" : "Nova réplica"}
        width={560}
        footer={
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
            <Button variant="ghost" onClick={() => setDialogOpen(false)}>
              Cancelar
            </Button>
            <Button
              icon="pi pi-save"
              disabled={saving || !form.name.trim()}
              onClick={() => void save()}
            >
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <div style={gridStyle}>
          <FormField label="Papel">
            <SelectField
              value={form.kind}
              onChange={(v) => setForm({ ...form, kind: v })}
              options={KIND_OPTIONS}
              aria-label="Papel"
            />
          </FormField>
          <FormField label="Nome">
            <input
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              placeholder="ex.: API réplica 2"
              style={inputStyle}
            />
          </FormField>
          <FormField label="Host">
            <input
              value={form.host}
              onChange={(e) => setForm({ ...form, host: e.target.value })}
              placeholder="ex.: api-2 ou infracontrol-api"
              style={inputStyle}
            />
          </FormField>
          <FormField label="Porta">
            <input
              type="number"
              value={form.port ?? ""}
              onChange={(e) => setForm({ ...form, port: e.target.value === "" ? null : Number(e.target.value) })}
              min={1}
              max={65535}
              style={inputStyle}
            />
          </FormField>
          <div style={{ gridColumn: "1 / -1" }}>
            <FormField label="URL de saúde (verificação)">
              <input
                value={form.healthUrl}
                onChange={(e) => setForm({ ...form, healthUrl: e.target.value })}
                placeholder="ex.: http://api-2:8080/health/live"
                style={inputStyle}
              />
            </FormField>
          </div>
          <FormField label="Peso na distribuição">
            <input
              type="number"
              value={form.weight}
              onChange={(e) => setForm({ ...form, weight: Number(e.target.value) || 1 })}
              min={0}
              max={100}
              style={inputStyle}
            />
          </FormField>
          <div style={{ display: "flex", alignItems: "center", gap: 10, paddingTop: 20 }}>
            <SwitchField
              checked={form.isActive}
              onChange={(checked) => setForm({ ...form, isActive: checked })}
              aria-label="Ativa (recebe tráfego)"
            />
            <span style={{ fontSize: 13, fontWeight: 500 }}>Ativa (recebe tráfego)</span>
          </div>
          <div style={{ gridColumn: "1 / -1" }}>
            <FormField label="Observações">
              <input
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                style={inputStyle}
              />
            </FormField>
          </div>
        </div>
      </Dialog>

      <Dialog
        visible={scaleDialog.open}
        onHide={() => setScaleDialog((s) => ({ ...s, open: false }))}
        header={`Escalar serviço — ${scaleDialog.service}`}
        width={440}
        footer={
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
            <Button
              variant="ghost"
              onClick={() => setScaleDialog((s) => ({ ...s, open: false }))}
            >
              Cancelar
            </Button>
            <Button
              icon="pi pi-sort-amount-down"
              disabled={scaleDialog.saving}
              onClick={() => void confirmScale()}
            >
              {scaleDialog.saving ? "Escalando…" : "Escalar"}
            </Button>
          </div>
        }
      >
        <p style={{ fontSize: 13, color: "#64748b", margin: "0 0 12px" }}>
          Executa <code>docker compose up -d --no-deps --scale {scaleDialog.service}=N</code> no
          host. 0 remove todas as réplicas do serviço.
        </p>
        <FormField label="Número de réplicas (0–16)">
          <input
            type="number"
            value={scaleDialog.replicas}
            onChange={(e) =>
              setScaleDialog((s) => ({ ...s, replicas: Number(e.target.value) || 0 }))
            }
            min={0}
            max={16}
            style={inputStyle}
          />
        </FormField>
      </Dialog>

      <Dialog
        visible={removeDialog.open}
        onHide={() => setRemoveDialog((s) => ({ ...s, open: false }))}
        header={`Remover container — ${removeDialog.name}`}
        width={480}
        footer={
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
            <Button
              variant="ghost"
              onClick={() => setRemoveDialog((s) => ({ ...s, open: false }))}
            >
              Cancelar
            </Button>
            <Button
              variant="danger"
              icon="pi pi-trash"
              disabled={
                removeDialog.removing ||
                removeDialog.confirm !== removeDialog.name ||
                removeDialog.reason.trim().length < 5
              }
              onClick={() => void confirmRemove()}
            >
              {removeDialog.removing ? "Removendo…" : "Remover definitivamente"}
            </Button>
          </div>
        }
      >
        <div style={{ marginBottom: 12 }}>
          <NoticeBanner
            message="Ação irreversível: o container e seu estado local serão apagados. Só é possível remover container parado. Fica registrado na auditoria."
            background="#fef3c7"
            color="#92400e"
          />
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <FormField label={`Digite o nome exato para confirmar: ${removeDialog.name}`}>
            <input
              value={removeDialog.confirm}
              onChange={(e) => setRemoveDialog((s) => ({ ...s, confirm: e.target.value }))}
              placeholder={removeDialog.name}
              style={inputStyle}
            />
          </FormField>
          <FormField label="Motivo (vai para a auditoria)">
            <textarea
              value={removeDialog.reason}
              onChange={(e) => setRemoveDialog((s) => ({ ...s, reason: e.target.value }))}
              rows={3}
              style={{ ...inputStyle, minHeight: 72, resize: "vertical" }}
              placeholder="ex.: container órfão de teste, substituído por nova versão"
            />
          </FormField>
        </div>
      </Dialog>

      <Dialog
        visible={limitsDialog.open}
        onHide={() => setLimitsDialog((s) => ({ ...s, open: false }))}
        header={`Limites de recursos — ${limitsDialog.container?.name ?? ""}`}
        width={440}
        footer={
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
            <Button
              variant="ghost"
              onClick={() => setLimitsDialog((s) => ({ ...s, open: false }))}
            >
              Cancelar
            </Button>
            <Button
              icon="pi pi-check"
              disabled={
                limitsDialog.saving ||
                (limitsDialog.memoryMb == null && limitsDialog.cpuCores == null)
              }
              onClick={() => void confirmLimits()}
            >
              {limitsDialog.saving ? "Aplicando…" : "Aplicar limites"}
            </Button>
          </div>
        }
      >
        <p style={{ fontSize: 13, color: "#64748b", margin: "0 0 12px" }}>
          Limites aplicados em runtime via <code>docker update</code> — valem até o próximo
          recreate do container. Deixe em branco para manter o limite atual.
        </p>
        <div style={gridStyle}>
          <FormField label="Memória (MB)">
            <input
              type="number"
              value={limitsDialog.memoryMb ?? ""}
              onChange={(e) =>
                setLimitsDialog((s) => ({
                  ...s,
                  memoryMb: e.target.value === "" ? null : Number(e.target.value),
                }))
              }
              min={16}
              max={16777216}
              placeholder={
                limitsDialog.container?.memoryLimitBytes != null
                  ? `${Math.round(limitsDialog.container.memoryLimitBytes / 1024 / 1024)} (atual)`
                  : "sem limite"
              }
              style={inputStyle}
            />
          </FormField>
          <FormField label="CPU (núcleos)">
            <input
              type="number"
              step={0.1}
              value={limitsDialog.cpuCores ?? ""}
              onChange={(e) =>
                setLimitsDialog((s) => ({
                  ...s,
                  cpuCores: e.target.value === "" ? null : Number(e.target.value),
                }))
              }
              min={0.1}
              max={64}
              placeholder={
                limitsDialog.container?.cpuLimitCores != null
                  ? `${limitsDialog.container.cpuLimitCores} (atual)`
                  : "sem limite"
              }
              style={inputStyle}
            />
          </FormField>
        </div>
      </Dialog>
    </div>
  );
}
