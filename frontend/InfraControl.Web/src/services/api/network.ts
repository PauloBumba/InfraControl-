import { api } from "./client";
import type { DeviceItem } from "./inventory";

/**
 * F17-T008 — conexão da NetworkPage ao backend (contrato real de
 * `GET/POST /api/v1/network/vlans`). Espelha `VlanDto`.
 *
 * DHCP, rotas e regras de firewall voltaram no F18-T005 (ADR-042) —
 * ver a seção "Serviços de rede" ao final do arquivo.
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

/** VLAN por id (GET /network/vlans/{id}). */
export async function getVlan(id: string): Promise<VlanItem | null> {
  return api.get<VlanItem>(`/network/vlans/${id}`);
}

/** Atualiza VLAN (PUT /network/vlans/{id}; 409 em nome/número duplicado). */
export async function updateVlan(
  id: string,
  input: { name: string; vlanNumber: number; description?: string },
): Promise<null> {
  return api.put<null>(`/network/vlans/${id}`, {
    name: input.name,
    vlanNumber: input.vlanNumber,
    description: input.description ?? null,
  });
}

/** Exclui VLAN (DELETE /network/vlans/{id}; 409 se houver vínculos). */
export async function deleteVlan(id: string): Promise<null> {
  return api.delete<null>(`/network/vlans/${id}`);
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

// ============ F18-T005 — Serviços de rede (ADR-042) ============

/**
 * `GET /api/v1/network/services/{dhcp/leases|routes|firewall/rules}`
 * (F18-T005 · ADR-042) — SOMENTE LEITURA, snapshot ao vivo do
 * equipamento configurado (`NetworkServices:Target`): leases/ARP e
 * rotas via SNMP, ACLs via SSH por vendor (v1: Cisco `show access-lists`).
 * Sem target ou equipamento inacessível = 503 + motivo no envelope — a
 * página mostra o motivo honesto em vez de dado inventado.
 */
export interface DhcpLeaseItem {
  ip: string;
  mac: string;
  ifIndex: number;
}

export interface NetworkRouteItem {
  destination: string;
  mask: string;
  nextHop: string;
  ifIndex: number;
  metric: number;
  /** "direct"/"indirect"/"other" (RFC1213) ou número cru. */
  type: string;
  /** "local"/"ospf"/"rip"… (RFC1213) ou número cru. */
  protocol: string;
}

export interface FirewallRuleItem {
  acl: string;
  /** "Standard"/"Extended" ou vazio ("IP access list X" do IOS-XE). */
  scope: string;
  sequence: number;
  /** "permit"/"deny" (cru, como o device mostra). */
  action: string;
  /** "tcp"/"udp"/"icmp"… ou vazio (ACL padrão não tem protocolo). */
  protocol: string;
  /** Texto CRU do device (host/any/wildcard/eq…) — sem semântica inventada. */
  expression: string;
}

export async function listDhcpLeases(): Promise<DhcpLeaseItem[]> {
  const data = await api.get<DhcpLeaseItem[]>("/network/services/dhcp/leases");
  return data ?? [];
}

export async function listNetworkRoutes(): Promise<NetworkRouteItem[]> {
  const data = await api.get<NetworkRouteItem[]>("/network/services/routes");
  return data ?? [];
}

export async function listFirewallRules(): Promise<FirewallRuleItem[]> {
  const data = await api.get<FirewallRuleItem[]>("/network/services/firewall/rules");
  return data ?? [];
}
