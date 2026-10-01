import { api } from "./client";
import type { Paged } from "./inventory";

/**
 * F17-T015 — contrato real de `/api/v1/discovery` (F4-T001/T002) para a
 * ClientsPage.
 *
 * Não existe domínio de "clientes conectados" (telemetria de AP/controller
 * com sinal/velocidade/uso/SO); o que existe de cliente de rede são os
 * **achados de discovery** (ARP/DHCP/MAC), consultados por execução. A
 * página usa a última execução (`runs` ordenado `StartedAt DESC`, página 1
 * com `pageSize=1`) e lista os resultados dela (`CreatedAt DESC`).
 *
 * Serialização: `kind`/`status` são strings PascalCase via `ToString()`
 * (`ArpEntry`/`DhcpLease`/`MacTableEntry`/`Neighbor`/`Host` e
 * `Running`/`Completed`/`Failed`). `values` é o dicionário de atributos do
 * provider — ARP: `ip/mac/if_index/local_if/arp_type`; DHCP:
 * `vlan/mac/ip/hostname/local_if/leased_time/binding_status/addr_type`.
 * Run desconhecido → 404 pt-BR.
 */
export type DiscoveryKind = "Neighbor" | "ArpEntry" | "MacTableEntry" | "DhcpLease" | "Host";

export interface DiscoveryRunItem {
  id: string;
  deviceId: string;
  status: "Running" | "Completed" | "Failed";
  startedAt: string;
  finishedAt: string | null;
  errorMessage: string | null;
}

export interface DiscoveryResultItem {
  id: string;
  discoveryRunId: string;
  kind: DiscoveryKind;
  summary: string;
  values: Record<string, string> | null;
  deviceId: string | null;
  createdAt: string;
}

/** Execuções (Read), ordenadas StartedAt DESC; `pageSize` do backend 1..100. */
export function listRuns(page = 1, pageSize = 20): Promise<Paged<DiscoveryRunItem>> {
  return api
    .get<Paged<DiscoveryRunItem>>(`/discovery/runs?page=${page}&pageSize=${pageSize}`)
    .then((data) => data ?? { items: [], totalCount: 0, page, pageSize });
}

/** Achados de uma execução (Read, CreatedAt DESC). */
export function listRunResults(
  runId: string,
  page = 1,
  pageSize = 20,
): Promise<Paged<DiscoveryResultItem>> {
  return api
    .get<Paged<DiscoveryResultItem>>(`/discovery/runs/${runId}/results?page=${page}&pageSize=${pageSize}`)
    .then((data) => data ?? { items: [], totalCount: 0, page, pageSize });
}
