import { api } from "./client";
import { listDevices, type DeviceItem, type Paged } from "./inventory";
import type { CheckRunResult } from "./printers";

/**
 * F30-T003d — Servidor ao vivo (docs/44): lista de devices type=Server +
 * execução dos checks de saúde (HOST-RESOURCES-MIB: CPU/memória/discos) com
 * feedback imediato (`POST /monitoring/run-check/{id}?check=`, MON-006 —
 * Operate). O histórico/resultados vêm de `getDeviceResults` (monitoring.ts).
 * O DTO é o mesmo do run-check de qualquer check (reuso de printers.ts).
 */
export const SERVER_CHECKS = [
  "SnmpServerCpu",
  "SnmpServerMemory",
  "SnmpServerDisk",
] as const;

export type ServerCheck = (typeof SERVER_CHECKS)[number];

/** Servidores cadastrados (DeviceType.Server) — inventário é o System of Record. */
export function listServers(): Promise<Paged<DeviceItem>> {
  return listDevices({ type: "Server" });
}

/** Executa um check de servidor agora e devolve o resultado (MON-006). */
export function runServerCheck(
  deviceId: string,
  check: ServerCheck,
): Promise<CheckRunResult | null> {
  return api.post<CheckRunResult | null>(
    `/monitoring/run-check/${deviceId}?check=${encodeURIComponent(check)}`,
  );
}

/** Executa a bateria completa de checks do servidor (sequencial — SNMP ao vivo). */
export async function runAllServerChecks(
  deviceId: string,
): Promise<CheckRunResult[]> {
  const results: CheckRunResult[] = [];
  for (const check of SERVER_CHECKS) {
    const result = await runServerCheck(deviceId, check);
    if (result) results.push(result);
  }
  return results;
}
