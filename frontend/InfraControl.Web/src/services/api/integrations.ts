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

/**
 * F18-T006 — WiFi (docs/29 §WiFi): `GET /integrations/unifi/wifi` lê ao
 * vivo no Controller as redes SSID (`/rest/ssidconf`) e a telemetria dos
 * APs (`/stat/device`), servidos com cache Redis de 60s. Nada é
 * persistido (regra 8 da integração).
 */
export interface UnifiSsidItem {
  name: string;
  /** String crua do UniFi (wpapsk/wpa2psk/wpa3/none…) — sem rótulo inventado. */
  security: string;
  enabled: boolean;
  /** 0 = sem VLAN. */
  vlan: number;
  /** "2.4 GHz", "5 GHz", "2.4 GHz + 5 GHz" ou "" = não informado. */
  bands: string;
  isGuest: boolean;
}

export interface UnifiRadioItem {
  /** Banda crua do Controller ("2g"/"5g"/"6g"). */
  band: string;
  channel: number;
  /** Largura em MHz (20/40/80/160). */
  width: number;
  /** Potência de transmissão em dBm. */
  txPower: number;
  /** 0 = não informado pelo Controller. */
  utilization: number;
}

export interface UnifiAccessPointItem {
  mac: string;
  name: string;
  model: string;
  ip: string;
  connected: boolean;
  uptimeSeconds: number;
  cpuPercent: number;
  memPercent: number;
  rxBytes: number;
  txBytes: number;
  /** Clientes conectados (soma das entradas de vap_table). */
  clients: number;
  radios: UnifiRadioItem[];
}

export interface UnifiWifi {
  ssids: UnifiSsidItem[];
  accessPoints: UnifiAccessPointItem[];
  /** Momento da sondagem (ISO UTC). */
  capturedAtUtc: string;
}

export async function getUnifiWifi(): Promise<UnifiWifi> {
  const data = await api.get<UnifiWifi>("/integrations/unifi/wifi");
  if (!data) {
    // 204/vazio não é contrato deste GET (envelope sempre devolve o DTO).
    throw new Error("Resposta vazia do servidor ao buscar o WiFi do UniFi.");
  }
  return data;
}
