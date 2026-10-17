import { api } from "./client";
import { listDevices, type DeviceItem, type Paged } from "./inventory";
import type { CheckRunResult } from "./printers";

/**
 * F30-T005d — Câmeras ao vivo (docs/46): lista de devices type=Camera +
 * execução dos checks ONVIF (armazenamento + estado de gravação) com
 * feedback imediato (`POST /monitoring/run-check/{id}?check=`, MON-006 —
 * Operate). O histórico/resultados vêm de `getDeviceResults` (monitoring.ts).
 * O DTO é o mesmo do run-check de qualquer check (reuso de printers.ts).
 */
export const CAMERA_CHECKS = [
  "OnvifStorage",
  "OnvifRecording",
] as const;

export type CameraCheck = (typeof CAMERA_CHECKS)[number];

/** Câmeras cadastradas (DeviceType.Camera) — inventário é o System of Record. */
export function listCameras(): Promise<Paged<DeviceItem>> {
  return listDevices({ type: "Camera" });
}

/** Executa um check da câmera agora e devolve o resultado (MON-006). */
export function runCameraCheck(
  deviceId: string,
  check: CameraCheck,
): Promise<CheckRunResult | null> {
  return api.post<CheckRunResult | null>(
    `/monitoring/run-check/${deviceId}?check=${encodeURIComponent(check)}`,
  );
}

/** Executa a bateria completa de checks da câmera (sequencial — SOAP ao vivo). */
export async function runAllCameraChecks(
  deviceId: string,
): Promise<CheckRunResult[]> {
  const results: CheckRunResult[] = [];
  for (const check of CAMERA_CHECKS) {
    const result = await runCameraCheck(deviceId, check);
    if (result) results.push(result);
  }
  return results;
}
