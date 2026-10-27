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
  /** F30-T006c (docs/47 §A) — metadado de cadastro do telefone (opt-in). */
  extension?: string | null;
  /** F30-T006c (docs/47 §A) — servidor SIP (PBX) do telefone (opt-in). */
  sipServer?: string | null;
  /** F34-T006 (ADR-060) — SO informado; null = sem "Acesso remoto" na ficha. */
  operatingSystem?: string | null;
  /** Foto do ativo (`/uploads/devices/...`); null = sem imagem. */
  imagePath?: string | null;
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
  /** F30-T001 S4a — enum como texto ("Speed1G"); null = não observado. */
  speed: string | null;
  /** F30-T001 S4a — "Full"/"Half"/"Auto"; null = não observado. */
  duplex: string | null;
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

/** Local por id (GET /inventory/locations/{id}). */
export async function getLocation(id: string): Promise<LocationItem | null> {
  return api.get<LocationItem>(`/inventory/locations/${id}`);
}

/** Cria local (Operate + antiforgery). Erros de validação vêm em pt-BR. */
export async function createLocation(input: {
  name: string;
  description?: string;
}): Promise<LocationItem | null> {
  return api.post<LocationItem>("/inventory/locations", {
    name: input.name,
    description: input.description || null,
  });
}

/** Atualiza local (PUT /inventory/locations/{id}). */
export async function updateLocation(
  id: string,
  input: { name: string; description?: string },
): Promise<null> {
  return api.put<null>(`/inventory/locations/${id}`, {
    name: input.name,
    description: input.description || null,
  });
}

/** Exclui local (DELETE /inventory/locations/{id}; 409 se houver racks). */
export async function deleteLocation(id: string): Promise<null> {
  return api.delete<null>(`/inventory/locations/${id}`);
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

/** Rack por id (GET /inventory/racks/{id}). */
export async function getRack(id: string): Promise<RackItem | null> {
  return api.get<RackItem>(`/inventory/racks/${id}`);
}

/** Cria rack (Operate + antiforgery). */
export async function createRack(input: {
  locationId: string;
  name: string;
  description?: string;
}): Promise<RackItem | null> {
  return api.post<RackItem>("/inventory/racks", {
    locationId: input.locationId,
    name: input.name,
    description: input.description || null,
  });
}

/** Atualiza rack (PUT /inventory/racks/{id}). */
export async function updateRack(
  id: string,
  input: { name: string; description?: string },
): Promise<null> {
  return api.put<null>(`/inventory/racks/${id}`, {
    name: input.name,
    description: input.description || null,
  });
}

/** Exclui rack (DELETE /inventory/racks/{id}; 409 se houver devices). */
export async function deleteRack(id: string): Promise<null> {
  return api.delete<null>(`/inventory/racks/${id}`);
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
  operatingSystem?: string;
  imagePath?: string | null;
}): Promise<DeviceItem | null> {
  return api.post<DeviceItem>("/inventory/devices", {
    locationId: input.locationId,
    name: input.name,
    type: input.type,
    ip: input.ip || undefined,
    model: input.model || undefined,
    vendor: input.vendor || undefined,
    description: input.description || undefined,
    operatingSystem: input.operatingSystem || undefined,
    imagePath: input.imagePath || undefined,
  });
}

/**
 * Atualiza device (PUT /inventory/devices/{id}). `locationId` não muda
 * (o UpdateDeviceViewModel não o expõe — troca de local é outro fluxo).
 */
export async function updateDevice(
  id: string,
  input: {
    name: string;
    type: string;
    ip?: string;
    model?: string;
    vendor?: string;
    description?: string;
    operatingSystem?: string;
    imagePath?: string | null;
  },
): Promise<null> {
  return api.put<null>(`/inventory/devices/${id}`, {
    name: input.name,
    type: input.type,
    ip: input.ip || null,
    model: input.model || null,
    vendor: input.vendor || null,
    description: input.description || null,
    operatingSystem: input.operatingSystem || null,
    imagePath: input.imagePath || null,
  });
}

/** Exclui device (DELETE /inventory/devices/{id}; 409 com vínculos ativos). */
export async function deleteDevice(id: string): Promise<null> {
  return api.delete<null>(`/inventory/devices/${id}`);
}

// ============ F17-T017 — detalhe do device ============

/**
 * Device por id (Read) — 404 pt-BR no envelope quando não existe
 * (`KeyNotFoundException` do handler). Mesmo `DeviceDto` da lista.
 */
export async function getDevice(id: string): Promise<DeviceItem | null> {
  return api.get<DeviceItem>(`/inventory/devices/${id}`);
}

/** F1-b: estado operacional consolidado (§8.1). */
export interface DeviceStatusItem {
  status: "Available" | "Degraded" | "Unavailable" | "Unknown";
  sources: string[];
  reasons: string[];
  checkedAtUtc: string;
}

export async function getDeviceStatus(id: string): Promise<DeviceStatusItem | null> {
  return api.get<DeviceStatusItem>(`/inventory/devices/${id}/status`);
}

/**
 * F30-T001 S3 — identificação do sistema (uptime + sysDescr) sem custo de
 * agente: valores do histórico `MonitoringResult` de SnmpHealth. `null` =
 * nunca observado (nada inventado).
 */
export interface DeviceSystemInfoItem {
  uptimeSeconds: number | null;
  sysDescr: string | null;
  capturedAtUtc: string | null;
}

export async function getDeviceSystemInfo(id: string): Promise<DeviceSystemInfoItem | null> {
  return api.get<DeviceSystemInfoItem>(`/inventory/devices/${id}/system-info`);
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

/** Item da busca global (F2-T007 — `GlobalSearchItemDto`). */
export interface GlobalSearchItem {
  type: "location" | "rack" | "device" | "interface" | "mac" | "ip";
  id: string;
  name: string;
  subtitle: string | null;
}

export interface GlobalSearchResult {
  items: GlobalSearchItem[];
  totalCount: number;
}

/**
 * Busca global das 6 fontes do inventário (`GET /inventory/search`,
 * F2-T007, Read). `term` é obrigatório (400 pt-BR se vazio — por isso a
 * página só chama com termo trimmed não vazio); `limit` vale POR fonte,
 * clamp 1..50 no handler (default 5 → até 30 itens, `totalCount` = total
 * real agregado). Ordem fixa: location → rack → device → interface → mac → ip.
 */
export async function globalSearch(
  term: string,
  limit = 5,
): Promise<GlobalSearchResult> {
  const params = new URLSearchParams({ term, limit: String(limit) });
  const data = await api.get<GlobalSearchResult>(`/inventory/search?${params}`);
  return data ?? { items: [], totalCount: 0 };
}
