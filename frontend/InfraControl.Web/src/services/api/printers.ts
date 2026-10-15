import { api } from "./client";
import { listDevices, type DeviceItem, type Paged } from "./inventory";

/**
 * F30-T002d — Impressora ao vivo (docs/43): lista de devices type=Printer +
 * execução dos checks de impressora (SNMP Printer MIB) com feedback
 * imediato (`POST /monitoring/run-check/{id}?check=`, MON-006 — Operate).
 * O histórico/resultados vêm de `getDeviceResults` (monitoring.ts).
 */
export const PRINTER_CHECKS = [
  "SnmpToner",
  "SnmpDrum",
  "SnmpColorToner",
  "SnmpPrinterStatus",
  "SnmpPrinterCounters",
] as const;

export type PrinterCheck = (typeof PRINTER_CHECKS)[number];

export interface CheckRunResult {
  deviceId: string;
  name: string;
  ip: string | null;
  check: string;
  executed: boolean;
  succeeded: boolean;
  durationMs: number | null;
  error: string | null;
  values: Record<string, string> | null;
}

/** Impressoras cadastradas (DeviceType.Printer) — inventário é o System of Record. */
export function listPrinters(): Promise<Paged<DeviceItem>> {
  return listDevices({ type: "Printer" });
}

/** Executa um check de impressora agora e devolve o resultado (MON-006). */
export function runPrinterCheck(
  deviceId: string,
  check: PrinterCheck,
): Promise<CheckRunResult | null> {
  return api.post<CheckRunResult | null>(
    `/monitoring/run-check/${deviceId}?check=${encodeURIComponent(check)}`,
  );
}

/** Executa a bateria completa de checks de impressora (sequencial — SNMP ao vivo). */
export async function runAllPrinterChecks(
  deviceId: string,
): Promise<CheckRunResult[]> {
  const results: CheckRunResult[] = [];
  for (const check of PRINTER_CHECKS) {
    const result = await runPrinterCheck(deviceId, check);
    if (result) results.push(result);
  }
  return results;
}
