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
