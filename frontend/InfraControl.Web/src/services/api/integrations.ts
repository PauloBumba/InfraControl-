import { api } from "./client";

/**
 * F18-T007 — InsightsPage conectada ao backend (docs/29 §Insights).
 *
 * Contrato real: `GET /api/v1/integrations/unifi/insights`
 * (`UnifiInsightsDto`) — sondagem AO VIVO no UniFi Controller: tráfego
 * por cliente (`/stat/sta`, contadores acumulados desde o reset do
 * cliente) e DPI por aplicação (`/stat/app`, bytes inspecionados). Nada
 * é persistido no InfraControl (regra 8 da integração).
 *
 * Speed test não existe: não há coletor nem domínio — painel continua
 * com estado vazio honesto (escopo adiado na task, decisão 2026-10-09).
 */
export interface UnifiClientTrafficItem {
  mac: string;
  hostname: string;
  ip: string;
  rxBytes: number;
  txBytes: number;
  /** ISO UTC do último visto; null quando o Controller não informou. */
  lastSeen: string | null;
  /** rxBytes + txBytes (propriedade computada do DTO). */
  totalBytes: number;
}

export interface UnifiDpiAppItem {
  app: string;
  category: string;
  totalBytes: number;
}

export interface UnifiInsights {
  trafficPerClient: UnifiClientTrafficItem[];
  dpiPerApplication: UnifiDpiAppItem[];
  /** Momento da sondagem (ISO UTC). */
  capturedAtUtc: string;
}

export async function getUnifiInsights(): Promise<UnifiInsights> {
  const data = await api.get<UnifiInsights>("/integrations/unifi/insights");
  if (!data) {
    // 204/vazio não é contrato deste GET (envelope sempre devolve o DTO).
    throw new Error("Resposta vazia do servidor ao buscar os insights do UniFi.");
  }
  return data;
}
