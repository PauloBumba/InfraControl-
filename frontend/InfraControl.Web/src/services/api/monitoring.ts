import { api } from "./client";

/**
 * F17-T017 — histórico de monitoramento por device
 * (`GET /api/v1/monitoring/results/{id}?limit=`, F5-T002).
 *
 * `check` é o nome do `CheckKind` em PascalCase (Ping/Http/Snmp/…);
 * `values` são os atributos do provider (ex.: ping com `roundtrip_ms`);
 * `checkedAt` vem do `CreateDateTime` do registro. O `check` ativo do
 * perfil já vem no `DeviceDto.monitoringChecks` (T007) — não há segundo
 * pedido para a lista de checks. `limit` 1..500 (400 pt-BR fora da faixa);
 * device desconhecido → 404 pt-BR.
 */
export interface MonitoringResultItem {
  id: string;
  check: string;
  succeeded: boolean;
  durationMs: number;
  error: string | null;
  values: Record<string, string> | null;
  checkedAt: string;
}

export interface DeviceResults {
  deviceId: string;
  deviceName: string;
  results: MonitoringResultItem[];
}

/** Últimos polls do device, mais recentes primeiro. */
export function getDeviceResults(
  deviceId: string,
  limit = 20,
): Promise<DeviceResults | null> {
  return api.get<DeviceResults>(`/monitoring/results/${deviceId}?limit=${limit}`);
}
