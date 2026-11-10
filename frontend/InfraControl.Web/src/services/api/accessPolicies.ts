import { api } from "./client";

/**
 * ADR-066 — matriz de acesso dinâmico role×módulo (admin).
 * `level`: 0=Revogar, 1=Ler, 2=Operar, 3=Administrar.
 * `isOverride=false` → célula segue o default do ADR-016.
 */
export interface AccessRoleGrant {
  role: string;
  level: number;
  isOverride: boolean;
}

export interface AccessModulePolicy {
  key: string;
  label: string;
  roles: AccessRoleGrant[];
}

export interface AccessRoleInfo {
  key: string;
  label: string;
  description: string | null;
}

export interface AccessPolicyMatrix {
  modules: AccessModulePolicy[];
  customRoles: AccessRoleInfo[];
}

/** Perfil de acesso customizado criado pelo admin. */
export interface AccessRoleItem {
  id: string;
  key: string;
  label: string;
  description: string | null;
  userCount: number;
}

export interface AccessRoleInput {
  key: string;
  label: string;
  description?: string | null;
}

export interface UserAccessRole {
  key: string;
  label: string;
}

export async function listAccessRoles(): Promise<AccessRoleItem[]> {
  const data = await api.get<AccessRoleItem[] | null>("/admin/access-roles");
  return data ?? [];
}

export async function createAccessRole(payload: AccessRoleInput): Promise<AccessRoleItem> {
  const data = await api.post<AccessRoleItem | null>("/admin/access-roles", payload);
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function updateAccessRole(id: string, payload: AccessRoleInput): Promise<AccessRoleItem> {
  const data = await api.put<AccessRoleItem | null>(`/admin/access-roles/${id}`, payload);
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function deleteAccessRole(id: string): Promise<void> {
  await api.delete<null>(`/admin/access-roles/${id}`);
}

export async function getUserAccessRoles(userId: string): Promise<UserAccessRole[]> {
  const data = await api.get<UserAccessRole[] | null>(`/admin/access-roles/users/${userId}`);
  return data ?? [];
}

export async function setUserAccessRoles(userId: string, roleKeys: string[]): Promise<UserAccessRole[]> {
  const data = await api.put<UserAccessRole[] | null>(`/admin/access-roles/users/${userId}`, { roleKeys });
  return data ?? [];
}

/** Instância/réplica do cluster registrada pelo admin. */
export interface ServiceInstanceItem {
  id: string;
  kind: string;
  name: string;
  host: string | null;
  port: number | null;
  healthUrl: string | null;
  weight: number;
  isActive: boolean;
  notes: string | null;
  lastStatus: string | null;
  lastCheckedAt: string | null;
}

export interface InstanceShare {
  id: string;
  name: string;
  weight: number;
  sharePercent: number;
}

export interface KindDistribution {
  kind: string;
  instanceCount: number;
  totalWeight: number;
  instances: InstanceShare[];
}

export interface ClusterOverview {
  instances: ServiceInstanceItem[];
  distribution: KindDistribution[];
  upCount: number;
  downCount: number;
  unknownCount: number;
}

export interface ClusterInsight {
  analysis: string;
  provider: string;
  model: string;
  usedFallback: boolean;
}

export interface ServiceInstanceInput {
  id?: string | null;
  kind: string;
  name: string;
  host?: string | null;
  port?: number | null;
  healthUrl?: string | null;
  weight: number;
  isActive: boolean;
  notes?: string | null;
}

export async function getClusterOverview(): Promise<ClusterOverview> {
  const data = await api.get<ClusterOverview | null>("/admin/cluster/overview");
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function upsertServiceInstance(payload: ServiceInstanceInput): Promise<ServiceInstanceItem> {
  const data = await api.put<ServiceInstanceItem | null>("/admin/cluster/instances", payload);
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function deleteServiceInstance(id: string): Promise<void> {
  await api.delete<null>(`/admin/cluster/instances/${id}`);
}

export async function checkServiceInstance(id: string): Promise<ServiceInstanceItem> {
  const data = await api.post<ServiceInstanceItem | null>(`/admin/cluster/instances/${id}/check`, {});
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function checkAllInstances(): Promise<ClusterOverview> {
  const data = await api.post<ClusterOverview | null>("/admin/cluster/check-all", {});
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function getClusterInsights(): Promise<ClusterInsight> {
  const data = await api.post<ClusterInsight | null>("/admin/cluster/insights", {});
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

/** Container Docker real (Engine API via socket da API). */
export interface DockerContainerItem {
  id: string;
  name: string;
  image: string;
  state: string;
  status: string;
  memoryUsageBytes?: number | null;
  cpuPercent?: number | null;
  /** Preenchidos só com ?stats=true (inspect one-shot por container). */
  startedAt?: string | null;
  restartCount?: number | null;
  healthStatus?: string | null;
  memoryLimitBytes?: number | null;
  memoryPercent?: number | null;
  networkRxBytes?: number | null;
  networkTxBytes?: number | null;
  blockReadBytes?: number | null;
  blockWriteBytes?: number | null;
  pidsCurrent?: number | null;
  /** Limite de CPU em núcleos (HostConfig.NanoCpus do inspect). */
  cpuLimitCores?: number | null;
}

export interface DockerContainersResult {
  available: boolean;
  containers: DockerContainerItem[];
}

export async function getDockerContainers(withStats = true): Promise<DockerContainersResult> {
  const data = await api.get<DockerContainersResult | null>(
    `/admin/cluster/docker/containers${withStats ? "?stats=true" : ""}`,
  );
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function dockerContainerAction(
  name: string,
  action: "start" | "stop" | "restart",
): Promise<void> {
  await api.post<null>(`/admin/cluster/docker/containers/${encodeURIComponent(name)}/action`, { action });
}

/** Remove container PARADO — dupla confirmação + motivo (auditado). */
export async function removeDockerContainer(
  name: string,
  reason: string,
  confirmName: string,
): Promise<void> {
  await api.post<null>(`/admin/cluster/docker/containers/${encodeURIComponent(name)}/remove`, {
    reason,
    confirmName,
  });
}

/** Limites de CPU/memória em runtime (docker update — até o recreate). */
export async function updateDockerResources(
  name: string,
  memoryBytes?: number | null,
  cpuCores?: number | null,
): Promise<void> {
  await api.post<null>(`/admin/cluster/docker/containers/${encodeURIComponent(name)}/resources`, {
    memoryBytes: memoryBytes ?? null,
    cpuCores: cpuCores ?? null,
  });
}

/** Serviço Compose do projeto com réplicas reais (compose-agent). */
export interface ComposeServiceContainer {
  name: string;
  state: string;
  status: string;
}

export interface ComposeServiceItem {
  service: string;
  total: number;
  running: number;
  containers: ComposeServiceContainer[];
}

export interface ComposeScaleResult {
  service: string;
  replicas: number;
  success: boolean;
  output: string;
}

export async function getComposeServices(): Promise<ComposeServiceItem[]> {
  const data = await api.get<ComposeServiceItem[] | null>("/admin/cluster/compose/services");
  return data ?? [];
}

export async function scaleComposeService(
  service: string,
  replicas: number,
): Promise<ComposeScaleResult> {
  const data = await api.post<ComposeScaleResult | null>(
    `/admin/cluster/compose/services/${encodeURIComponent(service)}/scale`,
    { replicas },
  );
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export interface DockerContainerLogs {
  name: string;
  tail: number;
  logs: string;
}

export async function getDockerContainerLogs(
  name: string,
  tail = 200,
): Promise<DockerContainerLogs> {
  const data = await api.get<DockerContainerLogs | null>(
    `/admin/cluster/docker/containers/${encodeURIComponent(name)}/logs?tail=${tail}`,
  );
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

/**
 * Investigação por IA dos logs reais. Escopo: um container, um serviço
 * Compose inteiro (todas as réplicas) ou o projeto (sem filtro).
 */
export async function getDockerLogsInsight(
  container?: string,
  service?: string,
): Promise<ClusterInsight> {
  const data = await api.post<ClusterInsight | null>("/admin/cluster/docker/logs-insight", {
    container: container ?? null,
    service: service ?? null,
  });
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

/** Topologia Docker dinâmica: containers + redes do projeto Compose. */
export interface DockerTopologyNode {
  id: string;
  name: string;
  service: string;
  image: string;
  state: string;
  networks: string[];
  ports: string[];
  healthStatus: string | null;
  restartCount: number;
}

export interface DockerNetwork {
  name: string;
  containers: string[];
}

export interface DockerTopology {
  containers: DockerTopologyNode[];
  networks: DockerNetwork[];
}

export interface DockerTopologyResult {
  available: boolean;
  topology?: DockerTopology;
}

export async function getDockerTopology(): Promise<DockerTopologyResult> {
  const data = await api.get<DockerTopologyResult | null>("/admin/cluster/docker/topology");
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

/** Tráfego interno da plataforma: volume/erros/latência por rota. */
export interface RouteTraffic {
  route: string;
  requests: number;
  errors: number;
  errorRatePercent: number;
  avgLatencyMs: number;
  p50Ms: number | null;
  p95Ms: number | null;
  p99Ms: number | null;
}

export interface MinuteTraffic {
  minuteUtc: string;
  requests: number;
  errors: number;
}

/** Distribuição de requisições por instância/réplica na janela. */
export interface InstanceTraffic {
  instance: string;
  requests: number;
  errors: number;
  avgLatencyMs: number;
  sharePercent: number;
}

export interface TrafficMetrics {
  windowMinutes: number;
  totalRequests: number;
  totalErrors: number;
  errorRatePercent: number;
  avgLatencyMs: number;
  p50Ms: number | null;
  p95Ms: number | null;
  p99Ms: number | null;
  requestsPerMinute: number;
  routes: RouteTraffic[];
  timeline: MinuteTraffic[];
  instances: InstanceTraffic[];
}

export async function getTrafficMetrics(minutes = 60): Promise<TrafficMetrics> {
  const data = await api.get<TrafficMetrics | null>(`/admin/cluster/traffic?minutes=${minutes}`);
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export interface AccessPolicyGrantInput {
  role: string;
  moduleKey: string;
  level: number | null;
}

export async function getAccessPolicyMatrix(): Promise<AccessPolicyMatrix> {
  const data = await api.get<AccessPolicyMatrix | null>("/admin/access-policies");
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function updateAccessPolicyGrants(
  grants: AccessPolicyGrantInput[],
): Promise<AccessPolicyMatrix> {
  const data = await api.put<AccessPolicyMatrix | null>("/admin/access-policies", { grants });
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

/** Configuração de backup editável pela plataforma (override sobre appsettings). */
export interface BackupSettings {
  enabled: boolean;
  intervalHours: number;
  retentionDays: number;
  path: string;
  fileNamePrefix: string;
  timeoutSeconds: number;
}

export async function getBackupSettings(): Promise<BackupSettings> {
  const data = await api.get<BackupSettings | null>("/operations/backup-settings");
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function updateBackupSettings(payload: BackupSettings): Promise<BackupSettings> {
  const data = await api.put<BackupSettings | null>("/operations/backup-settings", payload);
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}
