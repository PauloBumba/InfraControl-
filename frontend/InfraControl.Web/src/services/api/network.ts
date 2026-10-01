import { api } from "./client";
import type { DeviceItem } from "./inventory";

/**
 * F17-T008 — conexão da NetworkPage ao backend (contrato real de
 * `GET/POST /api/v1/network/vlans`). Espelha `VlanDto`.
 *
 * DHCP, rotas e regras de firewall NÃO têm endpoint no backend (grep nos
 * controllers: nenhuma rota correspondente) — as abas do protótipo saíram.
 */
export interface VlanItem {
  id: string;
  name: string;
  vlanNumber: number;
  description: string | null;
}

/** `PagedResult<T>` — o campo `data` do envelope. */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

const PAGE_SIZE = 100;

/** Teto por página (clamp 1..100 do backend) — usado pelo pager da tela. */
export const NETWORK_PAGE_SIZE = PAGE_SIZE;

/** Busca paginada (`term` é parâmetro real; pageSize clampado 1..100). */
export async function listVlans(page = 1): Promise<Paged<VlanItem>> {
  const data = await api.get<Paged<VlanItem>>(
    `/network/vlans?page=${page}&pageSize=${PAGE_SIZE}`,
  );
  return data ?? { items: [], totalCount: 0, page, pageSize: PAGE_SIZE };
}

/**
 * Criação de VLAN (Operate + antiforgery). Nome único CI e número 1..4094
 * único — os 409 chegam em pt-BR no `errors` do envelope.
 */
export function createVlan(input: {
  name: string;
  vlanNumber: number;
  description?: string;
}): Promise<VlanItem | null> {
  return api.post<VlanItem>("/network/vlans", {
    name: input.name,
    vlanNumber: input.vlanNumber,
    description: input.description || undefined,
  });
}

// ============ F17-T020 — SwitchesPage ============

/**
 * `GET /api/v1/network/switches` (F3-T001). O backend não tem entidade
 * Switch: é **Device com type=Switch** (docs/03), e o endpoint devolve o
 * mesmo `DeviceDto` da lista de devices — F3-T001 delega ao fluxo
 * F2-T003 com `Type: "Switch"` fixo (guarda de tipo só no Get).
 * `term`/`locationId`/`rackId` são parâmetros reais; pageSize 1..100.
 */
export async function listSwitches(
  filters: {
    term?: string;
    locationId?: string;
    rackId?: string;
    page?: number;
    pageSize?: number;
  } = {},
): Promise<Paged<DeviceItem>> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.locationId) params.set("locationId", filters.locationId);
  if (filters.rackId) params.set("rackId", filters.rackId);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? PAGE_SIZE));

  const data = await api.get<Paged<DeviceItem>>(`/network/switches?${params}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: PAGE_SIZE };
}

// ============ F17-T021 — TopologyPage ============

/**
 * `GET /api/v1/network/topology` (F3-T006). Nodes = **interfaces**
 * (1 device pode ter N nodes), edges = Connections dirigidas. `status`
 * é o último Ping do device dono do node: `"up"`/`"down"`/`null` (nunca
 * verificado) — não existe "warning" nem estado atual de switch (T007).
 * A API **não devolve coordenadas** x/y (o mock tinha posições
 * manuais): o layout do mapa é calculado no cliente.
 */
export interface TopologyNodeItem {
  id: string;
  deviceId: string;
  name: string;
  deviceName: string;
  deviceType: string;
  ip: string | null;
  status: "up" | "down" | null;
}

export interface TopologyEdgeItem {
  id: string;
  sourceInterfaceId: string;
  targetInterfaceId: string;
}

export interface TopologyGraph {
  nodes: TopologyNodeItem[];
  edges: TopologyEdgeItem[];
}

export async function getTopology(): Promise<TopologyGraph | null> {
  return api.get<TopologyGraph>("/network/topology");
}
