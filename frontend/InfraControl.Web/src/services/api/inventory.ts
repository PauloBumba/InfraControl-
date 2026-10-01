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
 * F17-T018 — racks (`GET /inventory/racks`, F2-T002). `locationId` é
 * parâmetro real do endpoint. Mesmo `RackDto` da API: **sem** units/
 * usedUnits/power/temp/devices do mock de `dashboardData` (não existem
 * no contrato).
 */
export interface RackItem {
  id: string;
  locationId: string;
  name: string;
  description: string | null;
}

export async function listRacks(
  filters: { locationId?: string; page?: number; pageSize?: number } = {},
): Promise<Paged<RackItem>> {
  const params = new URLSearchParams();
  if (filters.locationId) params.set("locationId", filters.locationId);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? INVENTORY_PAGE_SIZE));

  const data = await api.get<Paged<RackItem>>(`/inventory/racks?${params}`);
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

// ============ F17-T017 — detalhe do device ============

/**
 * Device por id (Read) — 404 pt-BR no envelope quando não existe
 * (`KeyNotFoundException` do handler). Mesmo `DeviceDto` da lista.
 */
export async function getDevice(id: string): Promise<DeviceItem | null> {
  return api.get<DeviceItem>(`/inventory/devices/${id}`);
}

export interface NetworkInterfaceItem {
  id: string;
  deviceId: string;
  name: string;
  description: string | null;
}

export interface IpAddressItem {
  id: string;
  interfaceId: string;
  ip: string;
}

export interface MacAddressItem {
  id: string;
  interfaceId: string;
  mac: string;
}

/** Interfaces de rede do device (`?deviceId=`; página única de 100). */
export async function listNetworkInterfaces(
  deviceId: string,
): Promise<Paged<NetworkInterfaceItem>> {
  const data = await api.get<Paged<NetworkInterfaceItem>>(
    `/inventory/network-interfaces?deviceId=${deviceId}&page=1&pageSize=${INVENTORY_PAGE_SIZE}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: INVENTORY_PAGE_SIZE };
}

/** IPs por device (`?deviceId=`; pertencem a uma interface — join no front). */
export async function listDeviceIps(
  deviceId: string,
): Promise<Paged<IpAddressItem>> {
  const data = await api.get<Paged<IpAddressItem>>(
    `/inventory/ip-addresses?deviceId=${deviceId}&page=1&pageSize=${INVENTORY_PAGE_SIZE}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: INVENTORY_PAGE_SIZE };
}

/** MACs por device (`?deviceId=`; pertencem a uma interface). */
export async function listDeviceMacs(
  deviceId: string,
): Promise<Paged<MacAddressItem>> {
  const data = await api.get<Paged<MacAddressItem>>(
    `/inventory/mac-addresses?deviceId=${deviceId}&page=1&pageSize=${INVENTORY_PAGE_SIZE}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: INVENTORY_PAGE_SIZE };
}
