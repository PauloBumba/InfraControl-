import { describe, expect, it } from "vitest";
import type { UniFiConnectedClient } from "@/services/api/unifi";
import { formatSignal, summarizeLiveClients } from "./wifiClients";

/**
 * F30-T004 S1 (docs/52 §S1) — agregados dos clientes AO VIVO do controller
 * (`GET /integrations/unifi/clients`, F26 Fase 4) para a WiFiPage: total,
 * fio/sem-fio, clientes por SSID e RSSI médio. Derivado do payload que já
 * existe — nenhuma chamada nova de rede.
 *
 * Semântica da casa: ausência não vira 0 inventado — `ssid` sem valor agrupa
 * sob `null` (renderizado como "—") e `averageSignal` fica `null` quando
 * nenhum cliente sem-fio informou sinal. Cliente com fio NÃO entra nem no
 * agrupamento por SSID nem no sinal (não tem rádio).
 */
function client(overrides: Partial<UniFiConnectedClient>): UniFiConnectedClient {
  return {
    mac: "aa:bb:cc:dd:ee:01",
    ip: null,
    hostname: "host",
    apMac: null,
    switchMac: null,
    essid: null,
    networkName: null,
    signal: null,
    isWired: false,
    lastSeenUnix: null,
    radio: null,
    ...overrides,
  };
}

describe("summarizeLiveClients (F30-T004 S1)", () => {
  it("should count total, wired and wireless", () => {
    const summary = summarizeLiveClients([
      client({ isWired: true }),
      client({ mac: "aa:bb:cc:dd:ee:02", essid: "Corp", signal: -55 }),
      client({ mac: "aa:bb:cc:dd:ee:03", essid: "Corp", signal: -67 }),
    ]);

    expect(summary.total).toBe(3);
    expect(summary.wired).toBe(1);
    expect(summary.wireless).toBe(2);
  });

  it("should group wireless clients by essid ordered by count desc then name", () => {
    const summary = summarizeLiveClients([
      client({ essid: "Visitantes", signal: -70 }),
      client({ mac: "aa:bb:cc:dd:ee:02", essid: "Corp", signal: -55 }),
      client({ mac: "aa:bb:cc:dd:ee:03", essid: "Corp", signal: -67 }),
    ]);

    expect(summary.bySsid).toEqual([
      { ssid: "Corp", count: 2 },
      { ssid: "Visitantes", count: 1 },
    ]);
  });

  it("should group a wireless client without essid under null (rendered as —)", () => {
    const summary = summarizeLiveClients([
      client({ essid: null, signal: -60 }),
      client({ mac: "aa:bb:cc:dd:ee:02", essid: "", signal: -61 }),
    ]);

    expect(summary.bySsid).toEqual([{ ssid: null, count: 2 }]);
  });

  it("should average only the non-null signals of wireless clients", () => {
    const summary = summarizeLiveClients([
      client({ essid: "Corp", signal: -55 }),
      client({ mac: "aa:bb:cc:dd:ee:02", essid: "Corp", signal: -67 }),
      // sem sinal informado: fora da média (não vira 0)
      client({ mac: "aa:bb:cc:dd:ee:03", essid: "Corp", signal: null }),
      // com fio: fora da média e do agrupamento
      client({ mac: "aa:bb:cc:dd:ee:04", isWired: true, signal: -10 }),
    ]);

    expect(summary.averageSignal).toBe(-61);
  });

  it("should leave averageSignal null when no wireless client reports signal", () => {
    const summary = summarizeLiveClients([
      client({ essid: "Corp", signal: null }),
      client({ mac: "aa:bb:cc:dd:ee:02", isWired: true }),
    ]);

    expect(summary.averageSignal).toBeNull();
  });

  it("should handle an empty list with zeroed counts", () => {
    const summary = summarizeLiveClients([]);

    expect(summary).toEqual({
      total: 0,
      wired: 0,
      wireless: 0,
      bySsid: [],
      byBand: [],
      averageSignal: null,
    });
  });

  it("should group wireless clients by band mapping the raw radio (S1b)", () => {
    // "ng" = 2.4 GHz, "na" = 5 GHz — mapeamento documentado no utilitário;
    // nada é inventado no backend (que só repassa o valor bruto).
    const summary = summarizeLiveClients([
      client({ essid: "Corp", radio: "ng", signal: -55 }),
      client({ mac: "aa:bb:cc:dd:ee:02", essid: "Corp", radio: "ng", signal: -60 }),
      client({ mac: "aa:bb:cc:dd:ee:03", essid: "Corp", radio: "na", signal: -70 }),
    ]);

    expect(summary.byBand).toEqual([
      { band: "2.4", count: 2 },
      { band: "5", count: 1 },
    ]);
  });

  it("should group missing/unknown radio under null and skip wired (S1b)", () => {
    const summary = summarizeLiveClients([
      // sem rádio informado → null ("—"), nunca banda inventada
      client({ essid: "Corp", radio: null, signal: -55 }),
      // rádio fora da tabela conhecida → null honesto
      client({ mac: "aa:bb:cc:dd:ee:02", essid: "Corp", radio: "zz", signal: -60 }),
      // com fio: fora do agrupamento por banda (não passa por rádio)
      client({ mac: "aa:bb:cc:dd:ee:03", isWired: true, radio: "eth" }),
    ]);

    expect(summary.byBand).toEqual([{ band: null, count: 2 }]);
  });

  it("should trim and lowercase the raw radio before mapping (S1b)", () => {
    const summary = summarizeLiveClients([
      client({ essid: "Corp", radio: " NA ", signal: -55 }),
    ]);

    expect(summary.byBand).toEqual([{ band: "5", count: 1 }]);
  });
});

describe("formatSignal (F30-T004 S1)", () => {
  it("should format an integer dBm", () => {
    expect(formatSignal(-61)).toBe("-61 dBm");
  });

  it("should round a fractional dBm", () => {
    expect(formatSignal(-61.4)).toBe("-61 dBm");
  });

  it("should render null as —", () => {
    expect(formatSignal(null)).toBe("—");
  });
});
