import { api } from "./client";
import type { Paged } from "./inventory";

/**
 * F18-T008 — `GET /api/v1/clients`: clientes da rede (IP/MAC/hostname/vlan)
 * agregados de **todas** as execuções de discovery (ARP/DHCP) no backend, com
 * dedupe por MAC — a lista não depende mais do "último run" (decisão do
 * F17-T015). Achados sem ip e sem MAC (ex.: "Nenhuma entrada ARP.") não
 * chegam aqui: o servidor já filtra.
 *
 * Serialização: `kind` é string PascalCase (`ArpEntry`/`DhcpLease`);
 * `ip`/`mac`/`hostname`/`vlan`/`localIf` são anuláveis (cada cliente tem ao
 * menos ip ou mac); `lastSeenAt` é o achado mais recente do grupo.
 * `term` casa ip/mac/hostname sem diferenciar maiúsculas; `pageSize` do
 * backend 1..100.
 */
export interface ClientItem {
  ip: string | null;
  mac: string | null;
  hostname: string | null;
  vlan: string | null;
  localIf: string | null;
  kind: string;
  lastSeenAt: string;
}

export function listClients(term = "", page = 1, pageSize = 20): Promise<Paged<ClientItem>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const trimmed = term.trim();
  if (trimmed) params.set("term", trimmed);
  return api
    .get<Paged<ClientItem>>(`/clients?${params.toString()}`)
    .then((data) => data ?? { items: [], totalCount: 0, page, pageSize });
}
