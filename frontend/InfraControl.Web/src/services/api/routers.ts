import { api } from "./client";
import { listDevices, type DeviceItem, type Paged } from "./inventory";
import type { CheckRunResult } from "./printers";

/**
 * F30-T007d — Routers & Firewalls (docs/48): lista de devices type=Router
 * + type=Firewall (merge no front — o filtro do backend é 1 tipo) +
 * execução dos checks SNMP universais (rotas IP-FORWARD-MIB + sessões
 * TCP-MIB) com feedback imediato (`POST /monitoring/run-check/{id}?check=`,
 * MON-006 — Operate). O histórico/resultados vêm de `getDeviceResults`
 * (monitoring.ts). O DTO é o mesmo do run-check (reuso de printers.ts).
 */
export const ROUTER_CHECKS = [
  "SnmpRouterRoutes",
  "SnmpRouterSessions",
] as const;

export type RouterCheck = (typeof ROUTER_CHECKS)[number];

/** Routers + firewalls cadastrados — inventário é o System of Record. */
export async function listRouters(): Promise<Paged<DeviceItem>> {
  const [routers, firewalls] = await Promise.all([
    listDevices({ type: "Router" }),
    listDevices({ type: "Firewall" }),
  ]);
  const items = [...routers.items, ...firewalls.items];
  return {
    items,
    totalCount: routers.totalCount + firewalls.totalCount,
    page: 1,
    pageSize: items.length,
  };
}

/** Executa um check do router/firewall agora e devolve o resultado (MON-006). */
export function runRouterCheck(
  deviceId: string,
  check: RouterCheck,
): Promise<CheckRunResult | null> {
  return api.post<CheckRunResult | null>(
    `/monitoring/run-check/${deviceId}?check=${encodeURIComponent(check)}`,
  );
}

/** Executa a bateria de checks SNMP (sequencial — walk ao vivo). */
export async function runAllRouterChecks(
  deviceId: string,
): Promise<CheckRunResult[]> {
  const results: CheckRunResult[] = [];
  for (const check of ROUTER_CHECKS) {
    const result = await runRouterCheck(deviceId, check);
    if (result) results.push(result);
  }
  return results;
}
