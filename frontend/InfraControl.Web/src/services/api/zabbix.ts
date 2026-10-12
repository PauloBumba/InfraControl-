import { api } from "./client";

/** O envelope do client devolve `null` quando `data` vem ausente — aqui o
 * fallback é explícito (mesmo padrão de clients.ts/unifi.ts). */
function required<T>(promise: Promise<T | null>, fallback: T): Promise<T> {
  return promise.then((data) => data ?? fallback);
}

/** F27 Fase 2.2 — configuração Zabbix (senha NUNCA vem do backend). */
export interface ZabbixSettings {
  apiUrl: string;
  username: string;
  passwordSet: boolean;
  source: string;
  detectedVersion: string | null;
  lastCommunicationAtUtc: string | null;
  lastInventorySyncAtUtc: string | null;
  lastTestedAtUtc: string | null;
  lastTestOk: boolean;
  lastTestError: string | null;
  updatedAtUtc: string | null;
}

export interface ZabbixSettingsPayload {
  apiUrl: string;
  username: string;
  /** `null`/vazio = mantém a senha já gravada no servidor. */
  password: string | null;
}

export interface ZabbixConnectionTestResult {
  ok: boolean;
  version: string | null;
  error: string | null;
}

/** F27 Fase 3.1 — resultado do sync de hosts. */
export interface ZabbixSyncResult {
  created: number;
  updated: number;
  missing: number;
  errors: number;
  version: string | null;
}

/** F27 Fase 3.3 — linha da página Hosts monitorados. */
export interface ZabbixHostView {
  hostId: string;
  host: string;
  name: string;
  ip: string | null;
  port: number | null;
  enabled: boolean;
  discovered: boolean;
  missing: boolean;
  missingSinceUtc: string | null;
  groups: string[];
  templates: string[];
  hasAsset: boolean;
  lastSyncedAtUtc: string;
}

export interface ZabbixHostsQuery {
  term?: string;
  enabled?: boolean;
  missing?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ZabbixHostsPage {
  items: ZabbixHostView[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export function getZabbixConfig(): Promise<ZabbixSettings> {
  return required(api.get<ZabbixSettings>("/integrations/zabbix/config"), EMPTY_SETTINGS);
}

export function saveZabbixConfig(
  payload: ZabbixSettingsPayload
): Promise<ZabbixSettings> {
  return required(api.put<ZabbixSettings>("/integrations/zabbix/config", payload), EMPTY_SETTINGS);
}

export function testZabbixConnection(): Promise<ZabbixConnectionTestResult> {
  return required(api.post<ZabbixConnectionTestResult>("/integrations/zabbix/test-connection"), {
    ok: false,
    version: null,
    error: "Resposta vazia do servidor.",
  });
}

export function syncZabbixInventory(): Promise<ZabbixSyncResult> {
  return required(api.post<ZabbixSyncResult>("/integrations/zabbix/sync"), {
    created: 0,
    updated: 0,
    missing: 0,
    errors: 0,
    version: null,
  });
}

export function getZabbixHosts(query: ZabbixHostsQuery = {}): Promise<ZabbixHostsPage> {
  const params = new URLSearchParams();
  if (query.term) params.set("term", query.term);
  if (query.enabled !== undefined) params.set("enabled", String(query.enabled));
  if (query.missing !== undefined) params.set("missing", String(query.missing));
  params.set("page", String(query.page ?? 1));
  params.set("pageSize", String(query.pageSize ?? 20));
  return required(api.get<ZabbixHostsPage>(`/integrations/zabbix/hosts?${params.toString()}`), {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  });
}

/** F27 Fase 10.1 — problema ativo (problem.get ao vivo). */
export interface ZabbixProblemView {
  eventId: string;
  name: string;
  severity: number;
  severityLabel: string;
  clockUtc: string;
  recovered: boolean;
  hostNames: string[];
}

export interface ZabbixProblemsResult {
  items: ZabbixProblemView[];
  total: number;
}

/** F27 Fase 10.2 — ponto do histórico de um item (history.get). */
export interface ZabbixHistoryPoint {
  clockUtc: string;
  value: string;
}

/** F27 Fase 10.3 — resumo do dashboard de infraestrutura. */
export interface ZabbixSummary {
  totalHosts: number;
  enabledHosts: number;
  missingHosts: number;
  hostsWithAsset: number;
  hostsWithoutAsset: number;
  problemsTotal: number;
  problemsBySeverity: number[];
  hostsWithProblems: number;
  problemsAvailable: boolean;
  problemsError: string | null;
  isConfigured: boolean;
  detectedVersion: string | null;
  lastInventorySyncAtUtc: string | null;
}

export function getZabbixProblems(query: {
  minSeverity?: number;
  includeRecovered?: boolean;
} = {}): Promise<ZabbixProblemsResult> {
  const params = new URLSearchParams();
  if (query.minSeverity !== undefined) params.set("minSeverity", String(query.minSeverity));
  if (query.includeRecovered) params.set("includeRecovered", "true");
  const qs = params.toString();
  return required(
    api.get<ZabbixProblemsResult>(`/integrations/zabbix/problems${qs ? `?${qs}` : ""}`),
    { items: [], total: 0 }
  );
}

export function getZabbixHistory(query: {
  itemId: string;
  fromUtc: string;
  toUtc: string;
  limit?: number;
}): Promise<ZabbixHistoryPoint[]> {
  const params = new URLSearchParams({
    itemId: query.itemId,
    fromUtc: query.fromUtc,
    toUtc: query.toUtc,
  });
  params.set("limit", String(query.limit ?? 500));
  return required(
    api.get<ZabbixHistoryPoint[]>(`/integrations/zabbix/history?${params.toString()}`),
    []
  );
}

export function getZabbixSummary(): Promise<ZabbixSummary> {
  return required(api.get<ZabbixSummary>("/integrations/zabbix/summary"), EMPTY_SUMMARY);
}

const EMPTY_SUMMARY: ZabbixSummary = {
  totalHosts: 0,
  enabledHosts: 0,
  missingHosts: 0,
  hostsWithAsset: 0,
  hostsWithoutAsset: 0,
  problemsTotal: 0,
  problemsBySeverity: [0, 0, 0, 0, 0, 0],
  hostsWithProblems: 0,
  problemsAvailable: false,
  problemsError: null,
  isConfigured: false,
  detectedVersion: null,
  lastInventorySyncAtUtc: null,
};

const EMPTY_SETTINGS: ZabbixSettings = {
  apiUrl: "",
  username: "",
  passwordSet: false,
  source: "environment",
  detectedVersion: null,
  lastCommunicationAtUtc: null,
  lastInventorySyncAtUtc: null,
  lastTestedAtUtc: null,
  lastTestOk: false,
  lastTestError: null,
  updatedAtUtc: null,
};
