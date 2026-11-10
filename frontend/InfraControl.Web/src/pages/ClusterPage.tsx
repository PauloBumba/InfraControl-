import { useCallback, useEffect, useState } from "react";
import { Button } from "primereact/button";
import { Card } from "primereact/card";
import { Dialog } from "primereact/dialog";
import { Dropdown } from "primereact/dropdown";
import { InputNumber } from "primereact/inputnumber";
import { InputSwitch } from "primereact/inputswitch";
import { InputText } from "primereact/inputtext";
import { Message } from "primereact/message";
import { ProgressSpinner } from "primereact/progressspinner";
import { Tag } from "primereact/tag";
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
import { formatBytes, formatDateTime } from "@/utils/format";

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

function statusInstanciaTag(status: string | null) {
  if (status === "up") return <Tag severity="success" value="Online" />;
  if (status === "down") return <Tag severity="danger" value="Offline" />;
  return <Tag severity="secondary" value={status === "unknown" ? "Sem URL" : "Não verificado"} />;
}

function estadoDockerTag(state: string) {
  if (state === "running") return <Tag severity="success" value="Em execução" />;
  if (state === "exited" || state === "dead") return <Tag severity="danger" value={state === "exited" ? "Parado" : "Falhou"} />;
  return <Tag severity="warning" value={state} />;
}

function saudeDockerTag(health: string | null | undefined) {
  if (!health) return <span className="text-xs text-gray-400">—</span>;
  if (health === "healthy") return <Tag severity="success" value="Saudável" />;
  if (health === "unhealthy") return <Tag severity="danger" value="Insalubre" />;
  if (health === "starting") return <Tag severity="warning" value="Iniciando" />;
  return <Tag severity="secondary" value={health} />;
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
    if (!window.confirm(`Remover a réplica "${i.name}" do registro do cluster?`)) return;
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
    const label = action === "start" ? "Iniciar" : action === "stop" ? "Parar" : "Reiniciar";
    if (!window.confirm(`${label} o container "${c.name}"?`)) return;
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
      <div className="flex justify-center py-16">
        <ProgressSpinner />
      </div>
    );
  }

  const dockerRunning = docker?.containers.filter((c) => c.state === "running").length ?? 0;
  const dockerStopped = (docker?.containers.length ?? 0) - dockerRunning;

  return (
    <div className="p-4 md:p-6 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-semibold flex items-center gap-2">
            <i className="pi pi-server text-primary" />
            Administração do cluster
          </h1>
          <p className="text-sm text-gray-500">
            Containers reais do projeto, réplicas registradas, distribuição de
            requisições e análise por IA — tudo gerenciável aqui.
          </p>
        </div>
        <div className="flex gap-2 flex-wrap">
          <Button
            label="Verificar réplicas"
            icon="pi pi-refresh"
            outlined
            loading={probing}
            onClick={() => void probeAll()}
          />
          <Button
            label="Análise IA do cluster"
            icon="pi pi-sparkles"
            severity="help"
            outlined
            loading={insightLoading}
            onClick={() => void runInsights()}
          />
          <Button
            label="Investigar logs com IA"
            icon="pi pi-search"
            severity="info"
            outlined
            loading={logsInsightLoading}
            onClick={() => void runLogsInsight()}
          />
          <Button
            label="Nova réplica"
            icon="pi pi-plus"
            onClick={openCreate}
          />
          <Button
            label="Grafana"
            icon="pi pi-chart-bar"
            severity="secondary"
            outlined
            onClick={() => window.open("/grafana/", "_blank", "noopener")}
            title="Abrir dashboards de observabilidade (Prometheus/Grafana)"
          />
        </div>
      </div>

      {error && <Message severity="error" text={error} />}

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <Card>
          <div className="text-3xl font-semibold text-green-600">{overview?.upCount ?? 0}</div>
          <div className="text-sm text-gray-500">Réplicas online</div>
        </Card>
        <Card>
          <div className="text-3xl font-semibold text-red-600">{overview?.downCount ?? 0}</div>
          <div className="text-sm text-gray-500">Réplicas offline</div>
        </Card>
        <Card>
          <div className="text-3xl font-semibold text-green-600">{dockerRunning}</div>
          <div className="text-sm text-gray-500">Containers em execução</div>
        </Card>
        <Card>
          <div className="text-3xl font-semibold text-amber-600">{dockerStopped}</div>
          <div className="text-sm text-gray-500">Containers parados</div>
        </Card>
      </div>

      <Card
        title={
          <span className="flex items-center gap-2">
            <i className="pi pi-cog" /> Containers do projeto
          </span>
        }
        subTitle="Apenas containers deste projeto Compose — estado, CPU e memória reais do host"
        header={
          <div className="flex items-center justify-between">
            <span className="font-semibold flex items-center gap-2">
              <i className="pi pi-cog" /> Containers do projeto
            </span>
            <Button
              icon="pi pi-refresh"
              className="p-button-text p-button-sm"
              loading={dockerLoading}
              onClick={() => void loadDocker()}
              title="Atualizar lista"
            />
          </div>
        }
      >
        {docker === null ? (
          <ProgressSpinner />
        ) : !docker.available ? (
          <Message
            severity="warn"
            text="Docker indisponível para a API — verifique a montagem de /var/run/docker.sock no serviço api do compose."
          />
        ) : docker.containers.length === 0 ? (
          <div className="text-sm text-gray-400">Nenhum container do projeto encontrado.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm border-collapse">
              <thead>
                <tr className="border-b text-left">
                  <th className="py-2 pr-3 font-medium">Container</th>
                  <th className="py-2 pr-3 font-medium">Imagem</th>
                  <th className="py-2 pr-3 font-medium">Estado</th>
                  <th className="py-2 pr-3 font-medium">Saúde</th>
                  <th className="py-2 pr-3 font-medium text-right">CPU</th>
                  <th className="py-2 pr-3 font-medium text-right">Memória</th>
                  <th className="py-2 pr-3 font-medium text-right">Reinícios</th>
                  <th className="py-2 pr-3 font-medium">Uptime</th>
                  <th className="py-2 pr-3 font-medium">Status</th>
                  <th className="py-2 font-medium text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {docker.containers.map((c) => (
                  <tr key={c.id} className="border-b last:border-0">
                    <td className="py-2 pr-3 font-mono text-xs">{c.name}</td>
                    <td className="py-2 pr-3 font-mono text-xs max-w-56 truncate">{c.image}</td>
                    <td className="py-2 pr-3">{estadoDockerTag(c.state)}</td>
                    <td className="py-2 pr-3">{saudeDockerTag(c.healthStatus)}</td>
                    <td className="py-2 pr-3 text-right font-mono text-xs">
                      {c.cpuPercent != null ? `${c.cpuPercent}%` : "—"}
                    </td>
                    <td className="py-2 pr-3 text-right font-mono text-xs">
                      {c.memoryPercent != null
                        ? `${formatBytes(c.memoryUsageBytes)} (${c.memoryPercent}%)`
                        : formatBytes(c.memoryUsageBytes)}
                    </td>
                    <td className="py-2 pr-3 text-right font-mono text-xs">
                      {c.restartCount ?? "—"}
                    </td>
                    <td className="py-2 pr-3 text-xs whitespace-nowrap">
                      {uptimeDesde(c.startedAt)}
                    </td>
                    <td className="py-2 pr-3 text-xs text-gray-500">{c.status}</td>
                    <td className="py-2 text-right whitespace-nowrap">
                      {c.state !== "running" && (
                        <Button
                          icon="pi pi-play"
                          className="p-button-text p-button-sm"
                          title="Iniciar"
                          loading={dockerBusy === `${c.id}:start`}
                          onClick={() => void dockerAction(c, "start")}
                        />
                      )}
                      {c.state === "running" && (
                        <Button
                          icon="pi pi-stop"
                          className="p-button-text p-button-sm"
                          title="Parar"
                          loading={dockerBusy === `${c.id}:stop`}
                          onClick={() => void dockerAction(c, "stop")}
                        />
                      )}
                      <Button
                        icon="pi pi-chart-bar"
                        className="p-button-text p-button-sm"
                        title="Ver métricas detalhadas"
                        onClick={() => setDetails(c)}
                      />
                      <Button
                        icon="pi pi-file"
                        className="p-button-text p-button-sm"
                        title="Ver logs"
                        onClick={() => void openLogs(c)}
                      />
                      <Button
                        icon="pi pi-search"
                        className="p-button-text p-button-sm"
                        title="Investigar container com IA (estado, métricas e logs)"
                        loading={logsInsightLoading}
                        onClick={() => void runLogsInsight(c.name)}
                      />
                      <Button
                        icon="pi pi-refresh"
                        className="p-button-text p-button-sm"
                        title="Reiniciar"
                        loading={dockerBusy === `${c.id}:restart`}
                        onClick={() => void dockerAction(c, "restart")}
                      />
                      <Button
                        icon="pi pi-sliders-h"
                        className="p-button-text p-button-sm"
                        title="Limites de CPU/memória"
                        onClick={() => openLimits(c)}
                      />
                      {c.state !== "running" && (
                        <Button
                          icon="pi pi-trash"
                          className="p-button-text p-button-danger p-button-sm"
                          title="Remover container parado"
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
      </Card>

      {docker?.available && topology?.available && topology.topology && (
        <Card
          title={
            <span className="flex items-center gap-2">
              <i className="pi pi-sitemap" /> Topologia Docker (ao vivo)
            </span>
          }
          subTitle="Containers agrupados por rede Docker — dados reais do Engine a cada carregamento."
        >
          {topology.topology.networks.length === 0 ? (
            <div className="text-sm text-gray-400">Nenhuma rede Docker detectada.</div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {topology.topology.networks.map((net) => {
                const members = topology.topology!.containers.filter((n) =>
                  n.networks.includes(net.name),
                );
                return (
                  <div key={net.name} className="border rounded-lg p-3">
                    <div className="flex items-center justify-between mb-2">
                      <span className="font-medium font-mono text-xs">{net.name}</span>
                      <span className="text-xs text-gray-400">
                        {members.length} container(s)
                      </span>
                    </div>
                    <div className="space-y-1.5">
                      {members.map((n) => (
                        <div key={n.id} className="flex items-center gap-2 text-sm">
                          {estadoDockerTag(n.state)}
                          <span className="flex-1 truncate font-mono text-xs" title={n.name}>
                            {n.service}
                          </span>
                          {n.healthStatus && saudeDockerTag(n.healthStatus)}
                        </div>
                      ))}
                      {members.length === 0 && (
                        <div className="text-xs text-gray-400">Sem containers do projeto.</div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </Card>
      )}

      {docker?.available && traffic && (
        <Card
          title={
            <span className="flex items-center gap-2">
              <i className="pi pi-chart-line" /> Tráfego interno da plataforma
            </span>
          }
          subTitle="Requisições reais coletadas pelo middleware — por rota, com latência"
          header={
            <div className="flex items-center gap-2">
              <span className="font-semibold flex items-center gap-2">
                <i className="pi pi-chart-line" /> Tráfego interno
              </span>
              <Dropdown
                value={trafficWindow}
                options={[
                  { label: "Últimos 15 min", value: 15 },
                  { label: "Última hora", value: 60 },
                  { label: "Últimas 6 horas", value: 360 },
                  { label: "Últimas 24 horas", value: 1440 },
                ]}
                optionLabel="label"
                optionValue="value"
                className="w-40"
                onChange={(e) => setTrafficWindow(e.value as number)}
              />
            </div>
          }
        >
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
            <div className="border rounded-lg p-3">
              <div className="text-2xl font-semibold">{traffic.totalRequests}</div>
              <div className="text-xs text-gray-500">Requisições na janela</div>
            </div>
            <div className="border rounded-lg p-3">
              <div className="text-2xl font-semibold text-red-600">{traffic.totalErrors}</div>
              <div className="text-xs text-gray-500">
                Erros ({traffic.errorRatePercent}%)
              </div>
            </div>
            <div className="border rounded-lg p-3">
              <div className="text-2xl font-semibold">{formatMs(traffic.avgLatencyMs)}</div>
              <div className="text-xs text-gray-500">Latência média</div>
            </div>
            <div className="border rounded-lg p-3">
              <div className="text-2xl font-semibold">
                {formatMs(traffic.p50Ms)} / {formatMs(traffic.p95Ms)} / {formatMs(traffic.p99Ms)}
              </div>
              <div className="text-xs text-gray-500">P50 / P95 / P99</div>
            </div>
          </div>
          {(traffic.instances?.length ?? 0) > 0 && (
            <div className="mb-4">
              <div className="text-sm font-medium mb-2 flex items-center gap-2">
                <i className="pi pi-share-alt" /> Distribuição por instância/réplica
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-sm border-collapse">
                  <thead>
                    <tr className="border-b text-left">
                      <th className="py-2 pr-3 font-medium">Instância</th>
                      <th className="py-2 pr-3 font-medium text-right">Requisições</th>
                      <th className="py-2 pr-3 font-medium text-right">Erros</th>
                      <th className="py-2 pr-3 font-medium text-right">Latência média</th>
                      <th className="py-2 font-medium text-right">Participação</th>
                    </tr>
                  </thead>
                  <tbody>
                    {traffic.instances.map((inst) => {
                      const container = docker?.containers.find(
                        (c) => c.id.startsWith(inst.instance) || inst.instance.startsWith(c.id.slice(0, 12)),
                      );
                      return (
                        <tr key={inst.instance} className="border-b last:border-0">
                          <td className="py-2 pr-3 font-mono text-xs">
                            {container?.name ?? inst.instance}
                            {container && (
                              <span className="text-gray-400"> ({inst.instance})</span>
                            )}
                          </td>
                          <td className="py-2 pr-3 text-right font-mono text-xs">{inst.requests}</td>
                          <td className="py-2 pr-3 text-right font-mono text-xs">{inst.errors}</td>
                          <td className="py-2 pr-3 text-right font-mono text-xs">
                            {formatMs(inst.avgLatencyMs)}
                          </td>
                          <td className="py-2 text-right font-mono text-xs">
                            {inst.sharePercent}%
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
          {traffic.routes.length === 0 ? (
            <div className="text-sm text-gray-400">
              Nenhuma requisição registrada na janela — os dados aparecem após
              o tráfego real passar pela API.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm border-collapse">
                <thead>
                  <tr className="border-b text-left">
                    <th className="py-2 pr-3 font-medium">Rota</th>
                    <th className="py-2 pr-3 font-medium text-right">Reqs</th>
                    <th className="py-2 pr-3 font-medium text-right">Erros</th>
                    <th className="py-2 pr-3 font-medium text-right">Taxa erro</th>
                    <th className="py-2 pr-3 font-medium text-right">Média</th>
                    <th className="py-2 pr-3 font-medium text-right">P50</th>
                    <th className="py-2 pr-3 font-medium text-right">P95</th>
                    <th className="py-2 font-medium text-right">P99</th>
                  </tr>
                </thead>
                <tbody>
                  {traffic.routes.map((r) => (
                    <tr key={r.route} className="border-b last:border-0">
                      <td className="py-2 pr-3 font-mono text-xs">{r.route}</td>
                      <td className="py-2 pr-3 text-right font-mono text-xs">{r.requests}</td>
                      <td className="py-2 pr-3 text-right font-mono text-xs">{r.errors}</td>
                      <td className="py-2 pr-3 text-right font-mono text-xs">
                        {r.errorRatePercent}%
                      </td>
                      <td className="py-2 pr-3 text-right font-mono text-xs">
                        {formatMs(r.avgLatencyMs)}
                      </td>
                      <td className="py-2 pr-3 text-right font-mono text-xs">
                        {formatMs(r.p50Ms)}
                      </td>
                      <td className="py-2 pr-3 text-right font-mono text-xs">
                        {formatMs(r.p95Ms)}
                      </td>
                      <td className="py-2 text-right font-mono text-xs">
                        {formatMs(r.p99Ms)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}

      {logsInsight && (
        <Card
          title={`Investigação — ${logsInsightScope} (${logsInsight.provider}/${logsInsight.model})`}
          className="border-l-4 border-blue-400"
        >
          <div className="text-sm whitespace-pre-wrap">{logsInsight.analysis}</div>
        </Card>
      )}

      {insight && (
        <Card
          title={`Análise do cluster — ${insight.provider}/${insight.model}`}
          className="border-l-4 border-purple-400"
        >
          <div className="text-sm whitespace-pre-wrap">{insight.analysis}</div>
        </Card>
      )}

      {overview && overview.distribution.length > 0 && (
        <Card
          title={
            <span className="flex items-center gap-2">
              <i className="pi pi-share-alt" /> Distribuição de requisições por papel
            </span>
          }
        >
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            {overview.distribution.map((d) => (
              <div key={d.kind} className="border rounded-lg p-3">
                <div className="flex items-center justify-between mb-2">
                  <span className="font-medium">{KIND_LABEL[d.kind] ?? d.kind}</span>
                  <span className="text-xs text-gray-400">
                    {d.instanceCount} réplica(s) · peso {d.totalWeight}
                  </span>
                </div>
                <div className="space-y-1">
                  {d.instances.map((i) => (
                    <div key={i.id} className="flex items-center gap-2 text-sm">
                      <span className="flex-1 truncate">{i.name}</span>
                      <span className="text-xs text-gray-500 w-16 text-right">
                        {i.sharePercent}%
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

      <Card
        title={
          <span className="flex items-center gap-2">
            <i className="pi pi-clone" /> Instâncias e réplicas
          </span>
        }
        subTitle="Serviços Compose reais do projeto (Docker, ao vivo) + réplicas planejadas cadastradas"
      >
        {composeUnavailable && (
          <Message
            severity="warn"
            className="mb-3"
            text="compose-agent indisponível — a lista pode estar incompleta. Verifique o serviço compose-agent no compose."
          />
        )}
        <div className="overflow-x-auto">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b text-left">
                <th className="py-2 pr-3 font-medium">Instância</th>
                <th className="py-2 pr-3 font-medium">Origem</th>
                <th className="py-2 pr-3 font-medium">Papel</th>
                <th className="py-2 pr-3 font-medium">Endereço</th>
                <th className="py-2 pr-3 font-medium text-center">Réplicas</th>
                <th className="py-2 pr-3 font-medium text-center">Peso</th>
                <th className="py-2 pr-3 font-medium">Status</th>
                <th className="py-2 pr-3 font-medium">Última verificação</th>
                <th className="py-2 font-medium text-right">Ações</th>
              </tr>
            </thead>
            <tbody>
              {(composeServices ?? []).map((s) => {
                const kind = kindDoServico(s.service);
                const status =
                  s.total === 0
                    ? <Tag severity="secondary" value="Sem container" />
                    : s.running === s.total
                      ? <Tag severity="success" value="Online" />
                      : s.running > 0
                        ? <Tag severity="warning" value="Parcial" />
                        : <Tag severity="danger" value="Offline" />;
                return (
                  <tr key={`svc:${s.service}`} className="border-b last:border-0">
                    <td className="py-2 pr-3">
                      <div className="font-medium font-mono text-xs">{s.service}</div>
                      <div className="text-xs text-gray-400">
                        {s.containers.map((c) => c.name).join(", ")}
                      </div>
                    </td>
                    <td className="py-2 pr-3"><Tag severity="info" value="Docker" /></td>
                    <td className="py-2 pr-3">{KIND_LABEL[kind] ?? kind}</td>
                    <td className="py-2 pr-3 font-mono text-xs">—</td>
                    <td className="py-2 pr-3 text-center font-mono text-xs">
                      {s.running}/{s.total}
                    </td>
                    <td className="py-2 pr-3 text-center text-xs text-gray-400">—</td>
                    <td className="py-2 pr-3">{status}</td>
                    <td className="py-2 pr-3 text-xs text-gray-500">ao vivo</td>
                    <td className="py-2 text-right whitespace-nowrap">
                      <Button
                        icon="pi pi-sparkles"
                        className="p-button-text p-button-sm"
                        title="Investigar todas as réplicas deste serviço com IA"
                        loading={logsInsightLoading}
                        onClick={() => void runLogsInsight(undefined, s.service)}
                      />
                      <Button
                        icon="pi pi-sort-amount-down"
                        className="p-button-text p-button-sm"
                        title="Escalar réplicas"
                        onClick={() => openScale(s.service, s.total)}
                      />
                    </td>
                  </tr>
                );
              })}
              {(overview?.instances ?? []).map((i) => (
                <tr key={`inst:${i.id}`} className={`border-b last:border-0 ${i.isActive ? "" : "opacity-50"}`}>
                  <td className="py-2 pr-3">
                    <div className="font-medium">{i.name}</div>
                    {i.notes && <div className="text-xs text-gray-400">{i.notes}</div>}
                  </td>
                  <td className="py-2 pr-3"><Tag severity="secondary" value="Cadastro" /></td>
                  <td className="py-2 pr-3">{KIND_LABEL[i.kind] ?? i.kind}</td>
                  <td className="py-2 pr-3 font-mono text-xs">
                    {i.host ? `${i.host}${i.port ? `:${i.port}` : ""}` : "—"}
                  </td>
                  <td className="py-2 pr-3 text-center text-xs text-gray-400">—</td>
                  <td className="py-2 pr-3 text-center">{i.weight}</td>
                  <td className="py-2 pr-3">{statusInstanciaTag(i.lastStatus)}</td>
                  <td className="py-2 pr-3 text-xs text-gray-500">
                    {i.lastCheckedAt ? formatDateTime(new Date(i.lastCheckedAt)) : "—"}
                  </td>
                  <td className="py-2 text-right whitespace-nowrap">
                    <Button
                      icon="pi pi-heart"
                      className="p-button-text p-button-sm"
                      title="Verificar agora"
                      disabled={!i.healthUrl}
                      onClick={() => void probeOne(i.id)}
                    />
                    <Button
                      icon="pi pi-pencil"
                      className="p-button-text p-button-sm"
                      title="Editar"
                      onClick={() => openEdit(i)}
                    />
                    <Button
                      icon="pi pi-trash"
                      className="p-button-text p-button-danger p-button-sm"
                      title="Remover réplica"
                      onClick={() => void remove(i)}
                    />
                  </td>
                </tr>
              ))}
              {(composeServices ?? []).length === 0 && (overview?.instances ?? []).length === 0 && (
                <tr>
                  <td colSpan={9} className="py-6 text-center text-gray-400">
                    Nenhuma instância encontrada. Os serviços Compose do projeto
                    aparecem aqui automaticamente (origem Docker); cadastre nós
                    planejados para acompanhar a distribuição de peso.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      <Dialog
        header={`Métricas — ${details?.name ?? ""}`}
        visible={details !== null}
        onHide={() => setDetails(null)}
        className="w-full max-w-2xl"
      >
        {details && (
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3 text-sm">
            {[
              ["Estado", estadoDockerTag(details.state)],
              ["Saúde", saudeDockerTag(details.healthStatus)],
              ["CPU", details.cpuPercent != null ? `${details.cpuPercent}%` : "—"],
              ["Memória", details.memoryPercent != null
                ? `${formatBytes(details.memoryUsageBytes)} de ${formatBytes(details.memoryLimitBytes)} (${details.memoryPercent}%)`
                : formatBytes(details.memoryUsageBytes)],
              ["Limite de memória", formatBytes(details.memoryLimitBytes)],
              ["Rede recebida", formatBytes(details.networkRxBytes)],
              ["Rede enviada", formatBytes(details.networkTxBytes)],
              ["Disco lido", formatBytes(details.blockReadBytes)],
              ["Disco gravado", formatBytes(details.blockWriteBytes)],
              ["Processos (PIDs)", details.pidsCurrent ?? "—"],
              ["Reinícios", details.restartCount ?? "—"],
              ["Uptime", uptimeDesde(details.startedAt)],
              ["Iniciado em", details.startedAt ? formatDateTime(new Date(details.startedAt)) : "—"],
              ["Imagem", details.image],
              ["Status", details.status],
            ].map(([label, value]) => (
              <div key={label as string} className="border rounded p-2">
                <div className="text-xs text-gray-400">{label}</div>
                <div className="font-medium break-words">{value}</div>
              </div>
            ))}
          </div>
        )}
      </Dialog>

      <Dialog
        header={`Logs — ${logsDialog.name}`}
        visible={logsDialog.open}
        onHide={() => setLogsDialog((s) => ({ ...s, open: false }))}
        className="w-full max-w-4xl"
      >
        <div className="flex justify-end mb-2">
          <Button
            label="Analisar com IA"
            icon="pi pi-search"
            size="small"
            outlined
            loading={logsDialog.analyzing}
            onClick={() => void analyzeContainerLogs()}
          />
        </div>
        {logsDialog.loading ? (
          <div className="flex justify-center py-8"><ProgressSpinner /></div>
        ) : logsDialog.error ? (
          <Message severity="error" text={logsDialog.error} />
        ) : (
          <pre className="text-xs bg-gray-900 text-green-300 rounded p-3 overflow-auto max-h-[60vh] whitespace-pre-wrap break-all">
            {logsDialog.data?.logs || "(sem saída)"}
          </pre>
        )}
      </Dialog>

      <Dialog
        header={editing ? "Editar réplica" : "Nova réplica"}
        visible={dialogOpen}
        onHide={() => setDialogOpen(false)}
        className="w-full max-w-xl"
      >
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Papel</label>
            <Dropdown
              value={form.kind}
              options={KIND_OPTIONS}
              optionLabel="label"
              optionValue="value"
              onChange={(e) => setForm({ ...form, kind: e.value as string })}
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Nome</label>
            <InputText
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              placeholder="ex.: API réplica 2"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Host</label>
            <InputText
              value={form.host}
              onChange={(e) => setForm({ ...form, host: e.target.value })}
              placeholder="ex.: api-2 ou infracontrol-api"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Porta</label>
            <InputNumber
              value={form.port}
              onValueChange={(e) => setForm({ ...form, port: e.value ?? null })}
              min={1}
              max={65535}
              className="w-full"
            />
          </div>
          <div className="flex flex-col gap-1 md:col-span-2">
            <label className="text-sm font-medium">URL de saúde (verificação)</label>
            <InputText
              value={form.healthUrl}
              onChange={(e) => setForm({ ...form, healthUrl: e.target.value })}
              placeholder="ex.: http://api-2:8080/health/live"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Peso na distribuição</label>
            <InputNumber
              value={form.weight}
              onValueChange={(e) => setForm({ ...form, weight: e.value ?? 1 })}
              min={0}
              max={100}
              className="w-full"
            />
          </div>
          <div className="flex items-center gap-3 pt-5">
            <InputSwitch
              checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.value })}
            />
            <label className="text-sm font-medium">Ativa (recebe tráfego)</label>
          </div>
          <div className="flex flex-col gap-1 md:col-span-2">
            <label className="text-sm font-medium">Observações</label>
            <InputText
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
            />
          </div>
        </div>
        <div className="flex justify-end gap-2 mt-4">
          <Button label="Cancelar" className="p-button-text" onClick={() => setDialogOpen(false)} />
          <Button
            label="Salvar"
            icon="pi pi-save"
            loading={saving}
            disabled={!form.name.trim()}
            onClick={() => void save()}
          />
        </div>
      </Dialog>

      <Dialog
        header={`Escalar serviço — ${scaleDialog.service}`}
        visible={scaleDialog.open}
        onHide={() => setScaleDialog((s) => ({ ...s, open: false }))}
        className="w-full max-w-md"
      >
        <p className="text-sm text-gray-500 mb-3">
          Executa <code>docker compose up -d --no-deps --scale {scaleDialog.service}=N</code> no
          host. 0 remove todas as réplicas do serviço.
        </p>
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium">Número de réplicas (0–16)</label>
          <InputNumber
            value={scaleDialog.replicas}
            onValueChange={(e) => setScaleDialog((s) => ({ ...s, replicas: e.value ?? 0 }))}
            min={0}
            max={16}
            className="w-full"
          />
        </div>
        <div className="flex justify-end gap-2 mt-4">
          <Button label="Cancelar" className="p-button-text" onClick={() => setScaleDialog((s) => ({ ...s, open: false }))} />
          <Button
            label="Escalar"
            icon="pi pi-sort-amount-down"
            loading={scaleDialog.saving}
            onClick={() => void confirmScale()}
          />
        </div>
      </Dialog>

      <Dialog
        header={`Remover container — ${removeDialog.name}`}
        visible={removeDialog.open}
        onHide={() => setRemoveDialog((s) => ({ ...s, open: false }))}
        className="w-full max-w-lg"
      >
        <Message
          severity="warn"
          className="mb-3"
          text="Ação irreversível: o container e seu estado local serão apagados. Só é possível remover container parado. Fica registrado na auditoria."
        />
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">
              Digite o nome exato para confirmar: <code>{removeDialog.name}</code>
            </label>
            <InputText
              value={removeDialog.confirm}
              onChange={(e) => setRemoveDialog((s) => ({ ...s, confirm: e.target.value }))}
              placeholder={removeDialog.name}
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Motivo (vai para a auditoria)</label>
            <textarea
              value={removeDialog.reason}
              onChange={(e) => setRemoveDialog((s) => ({ ...s, reason: e.target.value }))}
              rows={3}
              className="w-full rounded border border-gray-300 p-2 text-sm"
              placeholder="ex.: container órfão de teste, substituído por nova versão"
            />
          </div>
        </div>
        <div className="flex justify-end gap-2 mt-4">
          <Button label="Cancelar" className="p-button-text" onClick={() => setRemoveDialog((s) => ({ ...s, open: false }))} />
          <Button
            label="Remover definitivamente"
            icon="pi pi-trash"
            severity="danger"
            loading={removeDialog.removing}
            disabled={
              removeDialog.confirm !== removeDialog.name || removeDialog.reason.trim().length < 5
            }
            onClick={() => void confirmRemove()}
          />
        </div>
      </Dialog>

      <Dialog
        header={`Limites de recursos — ${limitsDialog.container?.name ?? ""}`}
        visible={limitsDialog.open}
        onHide={() => setLimitsDialog((s) => ({ ...s, open: false }))}
        className="w-full max-w-md"
      >
        <p className="text-sm text-gray-500 mb-3">
          Limites aplicados em runtime via <code>docker update</code> — valem até o
          próximo recreate do container. Deixe em branco para manter o limite atual.
        </p>
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Memória (MB)</label>
            <InputNumber
              value={limitsDialog.memoryMb}
              onValueChange={(e) => setLimitsDialog((s) => ({ ...s, memoryMb: e.value ?? null }))}
              min={16}
              max={16777216}
              suffix=" MB"
              className="w-full"
              placeholder={
                limitsDialog.container?.memoryLimitBytes != null
                  ? `${Math.round(limitsDialog.container.memoryLimitBytes / 1024 / 1024)} (atual)`
                  : "sem limite"
              }
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">CPU (núcleos)</label>
            <InputNumber
              value={limitsDialog.cpuCores}
              onValueChange={(e) => setLimitsDialog((s) => ({ ...s, cpuCores: e.value ?? null }))}
              min={0.1}
              max={64}
              step={0.1}
              className="w-full"
              placeholder={
                limitsDialog.container?.cpuLimitCores != null
                  ? `${limitsDialog.container.cpuLimitCores} (atual)`
                  : "sem limite"
              }
            />
          </div>
        </div>
        <div className="flex justify-end gap-2 mt-4">
          <Button label="Cancelar" className="p-button-text" onClick={() => setLimitsDialog((s) => ({ ...s, open: false }))} />
          <Button
            label="Aplicar limites"
            icon="pi pi-check"
            loading={limitsDialog.saving}
            disabled={limitsDialog.memoryMb == null && limitsDialog.cpuCores == null}
            onClick={() => void confirmLimits()}
          />
        </div>
      </Dialog>
    </div>
  );
}
