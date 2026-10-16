import type { UniFiConnectedClient } from "@/services/api/unifi";

/**
 * F30-T004 S1 (docs/52 §S1) — agregados dos clientes AO VIVO do UniFi
 * Controller (`GET /integrations/unifi/clients`, F26 Fase 4) para o painel
 * da WiFiPage. Derivados do payload que JÁ existe (ficha 45 áreas C/E:
 * clientes por SSID e RSSI médio) — nenhuma chamada de rede nova.
 *
 * REGRA 8 (ADR-028): nada aqui é persistido; é consulta sob demanda.
 * Semântica da casa: ausência não vira 0 inventado — cliente sem-fio sem
 * `essid` agrupa sob `null` (a tela renderiza "—") e `averageSignal` fica
 * `null` quando ninguém informou sinal. Cliente COM FIO não entra no
 * agrupamento por SSID nem no sinal (não passa por rádio).
 */
export interface SsidClientCount {
  ssid: string | null;
  count: number;
}

export interface BandClientCount {
  /** "2.4" | "5" (GHz) — null = sem-fio sem rádio conhecido (renderizado —). */
  band: "2.4" | "5" | null;
  count: number;
}

export interface LiveClientSummary {
  total: number;
  wired: number;
  wireless: number;
  /** Ordenado por contagem desc, depois por nome (sem-ssid por último). */
  bySsid: SsidClientCount[];
  /** Ordenado por contagem desc, depois por banda (sem-banda por último). */
  byBand: BandClientCount[];
  /** Média do `signal` (dBm, negativo) dos sem-fio que informaram; null = ninguém. */
  averageSignal: number | null;
}

/** Sentinela de ordenação: U+FFFF fica depois de qualquer nome real. */
const LAST_SORT_KEY = "\uFFFF";

/**
 * F30-T004 S1b — banda por cliente: o controller entrega o rádio BRUTO
 * ("ng"/"na"/…) e o mapeamento para banda (2.4/5 GHz) é derivado AQUI,
 * na apresentação — o backend só repassa o valor bruto (REGRA: nada
 * inventado). Valores conhecidos dos firmwares UniFi; qualquer outro
 * ("zz", vazio, ausente) vira `null` e a tela renderiza "—" — nunca
 * chute de banda.
 */
const BAND_BY_RADIO: Record<string, "2.4" | "5"> = {
  b: "2.4",
  g: "2.4",
  n: "2.4",
  ng: "2.4",
  a: "5",
  na: "5",
  ac: "5",
};

/** Rádio bruto → banda; null quando desconhecido/ausente. */
export function bandOf(
  radio: string | null | undefined
): "2.4" | "5" | null {
  if (!radio) return null;
  return BAND_BY_RADIO[radio.trim().toLowerCase()] ?? null;
}

export function summarizeLiveClients(
  clients: UniFiConnectedClient[]
): LiveClientSummary {
  let wired = 0;
  let wireless = 0;
  let signalSum = 0;
  let signalCount = 0;
  const bySsid = new Map<string | null, number>();
  const byBand = new Map<"2.4" | "5" | null, number>();

  for (const client of clients) {
    if (client.isWired) {
      wired++;
      continue;
    }

    wireless++;
    const ssid = client.essid && client.essid.trim() !== "" ? client.essid : null;
    bySsid.set(ssid, (bySsid.get(ssid) ?? 0) + 1);

    const band = bandOf(client.radio);
    byBand.set(band, (byBand.get(band) ?? 0) + 1);

    if (client.signal != null && Number.isFinite(client.signal)) {
      signalSum += client.signal;
      signalCount++;
    }
  }

  return {
    total: clients.length,
    wired,
    wireless,
    bySsid: [...bySsid.entries()]
      .map(([ssid, count]) => ({ ssid, count }))
      .sort(
        (a, b) =>
          b.count - a.count ||
          (a.ssid ?? LAST_SORT_KEY).localeCompare(b.ssid ?? LAST_SORT_KEY)
      ),
    byBand: [...byBand.entries()]
      .map(([band, count]) => ({ band, count }))
      .sort(
        (a, b) =>
          b.count - a.count ||
          (a.band ?? LAST_SORT_KEY).localeCompare(b.band ?? LAST_SORT_KEY)
      ),
    averageSignal:
      signalCount > 0 ? Math.round(signalSum / signalCount) : null,
  };
}

/** dBm → "-61 dBm"; null/inválido → "—" (nunca 0 inventado). */
export function formatSignal(dbm: number | null): string {
  return dbm == null || !Number.isFinite(dbm) ? "—" : `${Math.round(dbm)} dBm`;
}
