import { api } from "./client";
import { listDevices, type DeviceItem, type Paged } from "./inventory";
import type { CheckRunResult } from "./printers";

/**
 * F30-T006d — Telefones IP (docs/47): lista de devices type=Phone +
 * execução dos checks Yealink (registro SIP + qualidade de chamada) com
 * feedback imediato (`POST /monitoring/run-check/{id}?check=`, MON-006 —
 * Operate). O histórico/resultados vêm de `getDeviceResults` (monitoring.ts).
 * O DTO é o mesmo do run-check de qualquer check (reuso de printers.ts).
 */
export const PHONE_CHECKS = [
  "YealinkSip",
  "YealinkCallQuality",
] as const;

export type PhoneCheck = (typeof PHONE_CHECKS)[number];

/** Telefones cadastrados (DeviceType.Phone) — inventário é o System of Record. */
export function listPhones(): Promise<Paged<DeviceItem>> {
  return listDevices({ type: "Phone" });
}

/** Executa um check do telefone agora e devolve o resultado (MON-006). */
export function runPhoneCheck(
  deviceId: string,
  check: PhoneCheck,
): Promise<CheckRunResult | null> {
  return api.post<CheckRunResult | null>(
    `/monitoring/run-check/${deviceId}?check=${encodeURIComponent(check)}`,
  );
}

/** Executa a bateria completa de checks do telefone (sequencial — HTTP ao vivo). */
export async function runAllPhoneChecks(
  deviceId: string,
): Promise<CheckRunResult[]> {
  const results: CheckRunResult[] = [];
  for (const check of PHONE_CHECKS) {
    const result = await runPhoneCheck(deviceId, check);
    if (result) results.push(result);
  }
  return results;
}
