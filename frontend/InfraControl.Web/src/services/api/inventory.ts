import { api } from "./client";

/**
 * F17-T007 — conexão da DevicesPage ao backend (contrato real de
 * `GET/POST /api/v1/inventory/devices`, `GET /inventory/locations` e
 * portas em `GET /network/switches/{id}/ports`). Espelha `DeviceDto`,
 * `LocationDto` e `SwitchPortDto`.
 *
 * `type` do device é string com o NOME do enum `DeviceType` (PascalCase:
 * Server/Switch/Router/Firewall/AccessPoint/...); `linkUp` das portas é
 * tri-estado (`bool?` — null = nunca testado).
 */
export interface DeviceItem {
  id: string;
  locationId: string;
  rackId: string | null;
  type: string;
  name: string;
  ip: string | null;
  mac: string | null;
  serial: string | null;
  model: string | null;
  vendor: string | null;
  vlanId: string | null;
  description: string | null;
  monitoringChecks: string[];
  profileIsCustom: boolean;
}

export interface LocationItem {
  id: string;
  name: string;
  description: string | null;
}

export interface SwitchPortItem {
  id: string;
  deviceId: string;
  name: string;
  description: string | null;
  linkUp: boolean | null;
}

/** `PagedResult<T>` — o campo `data` do envelope. */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Teto do clamp do backend (1..100) — inventário usa o máximo por página. */
export const INVENTORY_PAGE_SIZE = 100;

export interface DeviceFilters {
  term?: string;
  type?: string;
  locationId?: string;
  page?: number;
  pageSize?: number;
}

/** Busca paginada (`term`/`type`/`locationId` são parâmetros reais). */
export async function listDevices(
  filters: DeviceFilters = {},
): Promise<Paged<DeviceItem>> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.type) params.set("type", filters.type);
  if (filters.locationId) params.set("locationId", filters.locationId);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? INVENTORY_PAGE_SIZE));

  const data = await api.get<Paged<DeviceItem>>(`/inventory/devices?${params}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: INVENTORY_PAGE_SIZE };
}

/** Localizações (setores) — resolvem o `locationId` dos devices. */
export async function listLocations(): Promise<Paged<LocationItem>> {
  const data = await api.get<Paged<LocationItem>>(
    `/inventory/locations?page=1&pageSize=${INVENTORY_PAGE_SIZE}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: INVENTORY_PAGE_SIZE };
}

/**
 * Portas de um switch — coleção sob o device (`network/switches/{id}/ports`);
 * 1 pedido por switch (sem endpoint agregado).
 */
export async function listSwitchPorts(switchId: string): Promise<Paged<SwitchPortItem>> {
  const data = await api.get<Paged<SwitchPortItem>>(
    `/network/switches/${switchId}/ports?page=1&pageSize=${INVENTORY_PAGE_SIZE}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: INVENTORY_PAGE_SIZE };
}

/**
 * Criação de device (Operate + antiforgery). Só os campos com UI —
 * rackId/mac/serial/vlanId continuam aceitos pela API mas sem formulário
 * nesta task; validações do handler chegam em pt-BR no `errors`.
 */
export function createDevice(input: {
  locationId: string;
  name: string;
  type: string;
  ip?: string;
  model?: string;
  vendor?: string;
  description?: string;
}): Promise<DeviceItem | null> {
  return api.post<DeviceItem>("/inventory/devices", {
    locationId: input.locationId,
    name: input.name,
    type: input.type,
    ip: input.ip || undefined,
    model: input.model || undefined,
    vendor: input.vendor || undefined,
    description: input.description || undefined,
  });
}
