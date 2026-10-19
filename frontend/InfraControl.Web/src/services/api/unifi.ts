import { api } from "./client";

/** O envelope do client devolve `null` quando `data` vem ausente — aqui o
 * fallback é explícito (mesmo padrão de clients.ts). */
function required<T>(promise: Promise<T | null>, fallback: T): Promise<T> {
  return promise.then((data) => data ?? fallback);
}

/** F26 Fase 2.2 — configuração UniFi (senha NUNCA vem do backend). */
export interface UniFiSettings {
  baseUrl: string;
  username: string;
  site: string;
  allowInvalidTlsCertificate: boolean;
  passwordSet: boolean;
  source: "database" | "environment" | string;
  updatedAtUtc: string | null;
}

export interface UniFiSettingsPayload {
  baseUrl: string;
  username: string;
  /** `null`/vazio = mantém a senha já gravada no servidor. */
  password: string | null;
  site: string;
  allowInvalidTlsCertificate: boolean;
}

export interface UniFiTestResult {
  ok: boolean;
  devicesCount: number;
  error: string | null;
  version?: string | null;
}

export interface UniFiSiteView {
  shortId: string;
  name: string;
  description: string;
  hidden: boolean;
  missing: boolean;
  updatedAtUtc: string;
}

export interface UniFiSyncResult {
  created: number;
  updated: number;
  references: number;
  errors: number;
}

export function getUnifiConfig(): Promise<UniFiSettings> {
  return required(api.get<UniFiSettings>("/integrations/unifi/config"), EMPTY_SETTINGS);
}

export function saveUnifiConfig(
  payload: UniFiSettingsPayload
): Promise<UniFiSettings> {
  return required(api.put<UniFiSettings>("/integrations/unifi/config", payload), EMPTY_SETTINGS);
}

export function testUnifiConnection(): Promise<UniFiTestResult> {
  return required(api.post<UniFiTestResult>("/integrations/unifi/test-connection"), { ok: false, devicesCount: 0, error: "Resposta vazia do servidor." });
}

export function syncUnifiInventory(): Promise<UniFiSyncResult> {
  return required(api.post<UniFiSyncResult>("/integrations/unifi/sync"), { created: 0, updated: 0, references: 0, errors: 0 });
}

export function getUnifiSites(): Promise<UniFiSiteView[]> {
  return required(api.get<UniFiSiteView[]>("/integrations/unifi/sites"), []);
}

/** F26 Fase 4 — ao vivo (nunca persistido no InfraControl). */
export interface UniFiConnectedClient {
  mac: string;
  ip: string | null;
  hostname: string;
  apMac: string | null;
  switchMac: string | null;
  essid: string | null;
  networkName: string | null;
  signal: number | null;
  isWired: boolean;
  lastSeenUnix: number | null;
  /** Rádio BRUTO do controller ("ng"/"na"/…). S1b: banda é derivada no utilitário. */
  radio: string | null;
}

export interface UniFiNetwork {
  id: string;
  name: string;
  purpose: string;
  vlanId: number | null;
  enabled: boolean;
  siteId: string | null;
}

export function getUnifiLiveClients(): Promise<UniFiConnectedClient[]> {
  return required(api.get<UniFiConnectedClient[]>("/integrations/unifi/clients"), []);
}

export function getUnifiNetworks(): Promise<UniFiNetwork[]> {
  return required(api.get<UniFiNetwork[]>("/integrations/unifi/networks"), []);
}

/** F26 Fase 5 — portas ao vivo de um switch (port_table). */
export interface UniFiSwitchPort {
  portIndex: number;
  name: string;
  /** Estado ADMINISTRATIVO (ADR-059 §3/H6) — fonte do botão Habilitar/Desabilitar. */
  enabled: boolean;
  /** Telemetria do cabo (true/false/null) — NUNCA dirigir escrita. */
  linkUp: boolean | null;
  speed: string | null;
  fullDuplex: boolean | null;
  portVlan: number | null;
  poeEnable: boolean | null;
  poeStatus: string | null;
  media: string | null;
  clientMac: string | null;
}

export function getUnifiLivePorts(mac: string): Promise<UniFiSwitchPort[]> {
  return required(api.get<UniFiSwitchPort[]>(`/integrations/unifi/switches/${encodeURIComponent(mac)}/ports`), []);
}

/** Fase 07e (ADR-051): resultado da gravação de configuração de porta. */
export interface SetPortConfigResult {
  success: boolean;
  message: string;
}

/**
 * Fase 07e (ADR-051): grava configuração de porta (disable/enable/VLAN)
 * via GET → modify → PUT. Porta de management → 409.
 */
export function setUnifiPortConfig(
  switchId: string,
  portIndex: number,
  body: { disabled?: boolean | null; portVlan?: number | null },
): Promise<SetPortConfigResult | null> {
  return api.post<SetPortConfigResult>(
    `/integrations/unifi/switches/${encodeURIComponent(switchId)}/ports/${portIndex}/config`,
    body,
  );
}

/** F26 Fase 6 — topologia ao vivo derivada do stat/device. */
export interface UniFiTopologyNode {
  mac: string;
  name: string | null;
  type: string;
  kind: string;
  isKnownDevice: boolean;
}

export interface UniFiTopologyLink {
  sourceMac: string;
  sourceName: string | null;
  targetMac: string;
  targetName: string | null;
  sourcePort: number | null;
  linkType: string;
  confidence: string;
  source: string;
}

export interface UniFiTopology {
  nodes: UniFiTopologyNode[];
  links: UniFiTopologyLink[];
}

export function getUnifiTopology(): Promise<UniFiTopology> {
  return required(api.get<UniFiTopology>("/integrations/unifi/topology"), { nodes: [], links: [] });
}

/** F26 Fase 7 — correlação UniFi↔inventário. */
export interface UniFiAssetLink {
  mac: string;
  deviceId: string;
  deviceName: string | null;
  source: string;
  controllerPresent: boolean;
}

export interface UniFiUnlinkedDevice {
  mac: string;
  name: string | null;
  model: string | null;
  type: string | null;
  state: string | null;
}

export interface UniFiCorrelation {
  matched: UniFiAssetLink[];
  unlinked: UniFiUnlinkedDevice[];
  conflicts: UniFiAssetLink[];
}

/** F26 Fase 8 — dashboard UniFi (persistido × ao vivo separados). */
export interface UniFiDashboard {
  sites: number;
  sitesMissing: number;
  switches: number;
  accessPoints: number;
  deviceStates: Record<string, number>;
  clients: number;
  ports: { total: number; up: number } | null;
  unlinked: number;
  lastSyncAtUtc: string | null;
  lastSyncSucceeded: boolean | null;
  recentSyncErrors: number;
  controllerReachable: boolean;
  controllerError: string | null;
  stale: boolean;
}

/** F26 Fase 9 — histórico de sincronizações. */
export interface UniFiSyncRun {
  id: string;
  startedAtUtc: string;
  finishedAtUtc: string;
  succeeded: boolean;
  created: number;
  updated: number;
  references: number;
  errors: number;
  sites: number;
  message: string | null;
}

export function listUniFiSyncRuns(limit = 30): Promise<UniFiSyncRun[]> {
  return required(api.get<UniFiSyncRun[]>(`/integrations/unifi/sync-runs?limit=${limit}`), []);
}

export function getUniFiDashboard(): Promise<UniFiDashboard> {
  return required(api.get<UniFiDashboard>("/integrations/unifi/dashboard"), {
    sites: 0, sitesMissing: 0, switches: 0, accessPoints: 0, deviceStates: {},
    clients: 0, ports: null, unlinked: 0, lastSyncAtUtc: null,
    lastSyncSucceeded: null, recentSyncErrors: 0, controllerReachable: true,
    controllerError: null, stale: false,
  });
}

export function getUniFiCorrelation(): Promise<UniFiCorrelation> {
  return required(api.get<UniFiCorrelation>("/integrations/unifi/correlation"), {
    matched: [], unlinked: [], conflicts: [],
  });
}

export async function createUniFiAssetLink(mac: string, deviceId: string): Promise<UniFiAssetLink> {
  const data = await api.post<UniFiAssetLink>("/integrations/unifi/links", { mac, deviceId });
  if (!data) throw new Error("Resposta vazia ao criar vínculo UniFi.");
  return data;
}

export function deleteUniFiAssetLink(mac: string): Promise<void> {
  return api.delete(`/integrations/unifi/links/${encodeURIComponent(mac)}`).then(() => undefined);
}

const EMPTY_SETTINGS: UniFiSettings = {
  baseUrl: "",
  username: "",
  site: "default",
  allowInvalidTlsCertificate: false,
  passwordSet: false,
  source: "environment",
  updatedAtUtc: null,
};
