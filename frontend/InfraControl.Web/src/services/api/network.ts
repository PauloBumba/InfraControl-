import { api } from "./client";
import type { DeviceItem } from "./inventory";
import type { UniFiSwitchPort } from "./unifi";

/** Fase 07b — interface SNMP monitorada pelo Zabbix. */
export interface ZabbixInterfaceItem {
  itemId: string;
  name: string;
  key: string;
  interfaceName: string;
  enabled: boolean;
  unsupported: boolean;
}

/** Fase 07c — resultado do sync de portas UniFi → cadastro. */
export interface SyncPortsResult {
  created: number;
  updated: number;
  totalFromController: number;
  totalAfterSync: number;
}

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
  /**
   * F29-5: nó "ghost" = vizinho LLDP/CDP descoberto mas não gerenciado
   * (sem host correspondente). Renderizado tracejado; `port` é o rótulo da
   * porta remota do vizinho.
   */
  isGhost?: boolean;
  port?: string | null;
  /**
   * F53-T002/T003: camada da hierarquia (BFS a partir de Router/Firewall
   * sobre as Connections reais; 1 = raiz). `null` = device sem caminho até
   * uma raiz (ou ghost) — fora da hierarquia, renderizado na faixa
   * "Sem hierarquia".
   */
  layer?: number | null;
}

export interface TopologyEdgeItem {
  id: string;
  sourceInterfaceId: string;
  targetInterfaceId: string;
  /** F29-5: aresta até um nó ghost (não é Connection persistida). */
  isGhost?: boolean;
  /**
   * F53-T002/T005 — metadados das pontas (já enviados pelo
   * `TopologyEdgeDto`): nome real da interface, velocidade/duplex da
   * `SwitchPort` casada e origem/confiança da Connection (wire minúsculo
   * `manual`/`lldp`/`cdp`/`unifi` · `confirmed`/`estimated`; null = sem
   * dado — ex.: aresta ghost ou porta sem observação).
   */
  sourceInterfaceName?: string | null;
  targetInterfaceName?: string | null;
  sourceSpeed?: string | null;
  targetSpeed?: string | null;
  sourceDuplex?: string | null;
  targetDuplex?: string | null;
  source?: string | null;
  confidence?: string | null;
  /**
   * F53-T002/T004 — VLANs nas pontas da Connection (SwitchPortVlan da
   * porta casada por nome): `null` = sem porta casada; lista vazia =
   * porta sem VLAN cadastrada. Fonte do filtro "VLAN" do mapa.
   */
  sourceVlans?: TopologyVlanRef[] | null;
  targetVlans?: TopologyVlanRef[] | null;
}

/** VLAN numa ponta de aresta (F53-T002 — `TopologyVlanDto` do backend). */
export interface TopologyVlanRef {
  number: number;
  name: string;
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

/**
 * Fase 07a — portas UniFi ao vivo de um switch (port_table via
 * ExternalDeviceReference). Lista vazia = sem referência UniFi.
 */
export async function getSwitchUnifiPorts(
  switchId: string,
): Promise<UniFiSwitchPort[]> {
  const data = await api.get<UniFiSwitchPort[]>(
    `/network/switches/${encodeURIComponent(switchId)}/unifi-ports`,
  );
  return data ?? [];
}

/**
 * Fase 07b — interfaces SNMP monitoradas pelo Zabbix para um switch.
 * Lista vazia = sem referência Zabbix ou Zabbix não configurado.
 */
export async function getSwitchZabbixInterfaces(
  switchId: string,
): Promise<ZabbixInterfaceItem[]> {
  const data = await api.get<ZabbixInterfaceItem[]>(
    `/network/switches/${encodeURIComponent(switchId)}/zabbix-interfaces`,
  );
  return data ?? [];
}

/** Porta/interface ao vivo via Zabbix SNMP (net.if.* LLD). */
export interface ZabbixPortStatus {
  interfaceName: string;
  ifIndex: number | null;
  linkUp: boolean | null;
  speedMbps: number | null;
  duplex: string | null;
  inBytesPerSecond: number | null;
  outBytesPerSecond: number | null;
}

/**
 * Status ao vivo das portas do switch via Zabbix (link + tráfego).
 * Lista vazia = sem referência Zabbix, host ausente ou sem itens net.if.*.
 */
export async function getSwitchZabbixPorts(
  switchId: string,
): Promise<ZabbixPortStatus[]> {
  const data = await api.get<ZabbixPortStatus[]>(
    `/network/switches/${encodeURIComponent(switchId)}/zabbix-ports`,
  );
  return data ?? [];
}

/**
 * Fase 07c — sincroniza o cadastro de portas a partir do port_table
 * UniFi. 409 se o switch não tem referência UniFi.
 */
export async function syncSwitchPortsFromUnifi(
  switchId: string,
): Promise<SyncPortsResult | null> {
  return api.post<SyncPortsResult>(
    `/network/switches/${encodeURIComponent(switchId)}/sync-unifi`,
  );
}

// ============ Network Explorer F1 — MAC/IP lookup ============

/**
 * `GET /api/v1/network/mac-lookup?term=` — visão agregada do MAC Explorer:
 * cruza evidências reais de discovery (tabela MAC/SNMP, ARP, DHCP, LLDP) e
 * enriquece com AP Wi-Fi quando o UniFi souber. `null`/`—` = fonte não
 * informou — nunca dado inventado.
 */
export interface MacLookupSighting {
  kind: string;
  kindLabel: string;
  deviceId: string | null;
  deviceName: string | null;
  deviceIp: string | null;
  port: string | null;
  portDescription: string | null;
  mac: string | null;
  ip: string | null;
  hostname: string | null;
  vlan: string | null;
  seenAt: string;
  ageDays: number;
}

export interface MacLookupResult {
  term: string;
  matchedMac: string | null;
  ip: string | null;
  hostname: string | null;
  vlan: string | null;
  wifiClient: boolean;
  wifiApName: string | null;
  wifiSsid: string | null;
  wifiSignal: number | null;
  isStale: boolean;
  lastSeenAt: string | null;
  sightings: MacLookupSighting[];
}

export async function lookupMac(term: string): Promise<MacLookupResult | null> {
  const params = new URLSearchParams({ term });
  return api.get<MacLookupResult>(`/network/mac-lookup?${params}`);
}

// ============ Network Explorer F2 — switch + VLAN explorer ============

/** VLAN associada a uma porta (SwitchPortVlan cadastrada). */
export interface SwitchExplorerVlanRef {
  id: string;
  number: number;
  name: string;
}

export interface SwitchExplorerPort {
  id: string;
  name: string;
  description: string | null;
  linkUp: boolean | null;
  speed: string | null;
  duplex: string | null;
  vlans: SwitchExplorerVlanRef[];
}

/** MAC aprendido pelo switch via SNMP, enriquecido com ARP/DHCP. */
export interface SwitchExplorerMac {
  mac: string;
  ip: string | null;
  hostname: string | null;
  vlan: string | null;
  bridgePort: string | null;
  portName: string | null;
  seenAt: string;
  ageDays: number;
}

export interface SwitchExplorer {
  deviceId: string;
  name: string;
  ip: string | null;
  mac: string | null;
  type: string;
  ports: SwitchExplorerPort[];
  learnedMacs: SwitchExplorerMac[];
}

/** `GET /api/v1/network/explorer/switches/{id}` — detalhe do switch. */
export async function getSwitchExplorer(switchId: string): Promise<SwitchExplorer | null> {
  return api.get<SwitchExplorer>(`/network/explorer/switches/${encodeURIComponent(switchId)}`);
}

export interface VlanExplorerSwitchRef {
  deviceId: string;
  deviceName: string;
  portCount: number;
}

export interface VlanExplorerItem {
  id: string | null;
  vlanNumber: number | null;
  name: string | null;
  description: string | null;
  isRegistered: boolean;
  portCount: number;
  discoveredDeviceCount: number;
  lastSeenAt: string | null;
  switches: VlanExplorerSwitchRef[];
}

export interface VlanExplorerResult {
  items: VlanExplorerItem[];
  discoveredVlans: VlanExplorerItem[];
}

/** `GET /api/v1/network/explorer/vlans` — VLANs cadastradas + descobertas. */
export async function listVlansExplorer(
  term?: string,
  page = 1,
  pageSize = 20,
): Promise<VlanExplorerResult> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (term) params.set("term", term);
  const data = await api.get<VlanExplorerResult>(`/network/explorer/vlans?${params}`);
  return data ?? { items: [], discoveredVlans: [] };
}

// ============ Network Explorer F3 — AP/AC explorer ============

export interface ApExplorerClient {
  mac: string;
  ip: string | null;
  hostname: string | null;
  essid: string | null;
  signal: number | null;
}

export interface ApExplorerItem {
  deviceId: string | null;
  name: string;
  /** "AccessPoint" | "Controller". */
  kind: string;
  isRegistered: boolean;
  ip: string | null;
  mac: string | null;
  locationName: string | null;
  managementVlan: string | null;
  uplinkSwitchName: string | null;
  uplinkPort: string | null;
  uniFiConnected: boolean | null;
  clientCount: number;
  ssids: string[];
  clients: ApExplorerClient[];
  lastDiscoveryAt: string | null;
  lastRunStatus: string | null;
}

export interface ApExplorerResult {
  items: ApExplorerItem[];
}

/** `GET /api/v1/network/explorer/aps` — APs e ACs do inventário + UniFi. */
export async function getApExplorer(): Promise<ApExplorerResult> {
  const data = await api.get<ApExplorerResult>("/network/explorer/aps");
  return data ?? { items: [] };
}

// ============ Network Explorer F4 — discovery trigger + auditoria ============

export interface DiscoveryProvider {
  key: string;
  name: string;
}

export interface DiscoveryRunItem {
  id: string;
  deviceId: string;
  status: string;
  startedAt: string;
  finishedAt: string | null;
  errorMessage: string | null;
}

export interface DiscoveryRunsPage {
  items: DiscoveryRunItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** `GET /api/v1/discovery/providers` — providers registrados. */
export async function listDiscoveryProviders(): Promise<DiscoveryProvider[] | null> {
  return api.get<DiscoveryProvider[]>("/discovery/providers");
}

/** `GET /api/v1/discovery/runs?deviceId=` — histórico de runs do device. */
export async function listDiscoveryRuns(
  deviceId: string,
  pageSize = 5,
): Promise<DiscoveryRunsPage | null> {
  return api.get<DiscoveryRunsPage>(
    `/discovery/runs?deviceId=${encodeURIComponent(deviceId)}&page=1&pageSize=${pageSize}`,
  );
}

/** `POST /api/v1/discovery/devices/{id}/sweep` — varredura completa (todos providers). */
export async function runDiscoverySweep(
  deviceId: string,
): Promise<DiscoveryRunItem[] | null> {
  return api.post<DiscoveryRunItem[]>(
    `/discovery/devices/${encodeURIComponent(deviceId)}/sweep`,
  );
}
