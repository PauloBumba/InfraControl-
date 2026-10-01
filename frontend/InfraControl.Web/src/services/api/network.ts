import { api } from "./client";

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
