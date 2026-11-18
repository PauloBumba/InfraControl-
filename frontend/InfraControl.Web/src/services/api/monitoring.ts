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

/**
 * F54-T001 — série de disponibilidade dos checks internos
 * (`GET /monitoring/results/availability`): buckets com polls
 * totais/com sucesso — "% de sucesso" é derivado no cliente.
 */
export interface AvailabilityPoint {
  bucketStartUtc: string;
  total: number;
  succeeded: number;
  failed: number;
}

export interface MonitoringAvailability {
  fromUtc: string;
  toUtc: string;
  bucketMinutes: number;
  points: AvailabilityPoint[];
}

export function getMonitoringAvailability(query: {
  fromUtc?: string;
  toUtc?: string;
  bucketMinutes?: number;
} = {}): Promise<MonitoringAvailability | null> {
  const params = new URLSearchParams();
  if (query.fromUtc) params.set("fromUtc", query.fromUtc);
  if (query.toUtc) params.set("toUtc", query.toUtc);
  if (query.bucketMinutes) params.set("bucketMinutes", String(query.bucketMinutes));
  const qs = params.toString();
  return api.get<MonitoringAvailability>(
    `/monitoring/results/availability${qs ? `?${qs}` : ""}`,
  );
}

/**
 * ADR-068 — `GET /monitoring/checks?term=`: todos os checks com nome do
 * device, fonte do multi-selecionador do formulário de SLO.
 */
export interface MonitoringCheckOption {
  id: string;
  deviceId: string;
  deviceName: string;
  deviceIp: string;
  /** CheckKind em PascalCase (Ping/Http/Snmp/…). */
  kind: string;
  enabled: boolean;
}

export function listMonitoringChecks(term?: string): Promise<MonitoringCheckOption[] | null> {
  const qs = term ? `?term=${encodeURIComponent(term)}` : "";
  return api.get<MonitoringCheckOption[]>(`/monitoring/checks${qs}`);
}
