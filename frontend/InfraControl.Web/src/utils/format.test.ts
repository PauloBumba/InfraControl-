import { describe, expect, it } from "vitest";
import { formatPortDuplex, formatPortSpeed, formatUptime } from "./format";

/**
 * F30-T001 S5a — velocidade/duplex das portas do switch na tela.
 * O backend envia o enum serializado como texto (SwitchPortDto: "Speed1G",
 * "Full") e o usuário vê o token da ficha (docs/42 §B): "1G", "Full".
 * REGRA: não observado ⇒ "—" (nunca 0, nunca string vazia); valor que o
 * backend mandou e o front não conhece ⇒ mostra CRU (nada escondido).
 */
describe("formatPortSpeed", () => {
  it("should strip the enum prefix and show the ficha token", () => {
    expect(formatPortSpeed("Speed10M")).toBe("10M");
    expect(formatPortSpeed("Speed100M")).toBe("100M");
    expect(formatPortSpeed("Speed1G")).toBe("1G");
    expect(formatPortSpeed("Speed10G")).toBe("10G");
    expect(formatPortSpeed("Speed40G")).toBe("40G");
    expect(formatPortSpeed("Speed100G")).toBe("100G");
  });

  it("should keep auto-negotiation as a word", () => {
    expect(formatPortSpeed("Auto")).toBe("Auto");
  });

  it("should show an em dash when nothing was observed yet", () => {
    expect(formatPortSpeed(null)).toBe("—");
    expect(formatPortSpeed(undefined)).toBe("—");
    expect(formatPortSpeed("")).toBe("—");
  });

  it("should not hide a value the frontend does not know", () => {
    expect(formatPortSpeed("SomethingNew")).toBe("SomethingNew");
  });
});

describe("formatPortDuplex", () => {
  it("should keep the ficha words", () => {
    expect(formatPortDuplex("Full")).toBe("Full");
    expect(formatPortDuplex("Half")).toBe("Half");
    expect(formatPortDuplex("Auto")).toBe("Auto");
  });

  it("should show an em dash when the controller never reported it", () => {
    expect(formatPortDuplex(null)).toBe("—");
    expect(formatPortDuplex(undefined)).toBe("—");
    expect(formatPortDuplex("")).toBe("—");
  });
});

/**
 * F30-T001 S5b — uptime do device vindo de GET /devices/{id}/system-info
 * (uptimeSeconds, S3). Escala: "1d 1h" ≥ 1 dia, "1h 1m" ≥ 1 hora, "1m" ≥ 1
 * minuto, "42s" no resto. REGRA: sem leitura ou dado absurdo (negativo/NaN)
 * ⇒ "—" — uptime nunca vira 0s inventado.
 */
describe("formatUptime", () => {
  it("should scale from seconds up to days", () => {
    expect(formatUptime(0)).toBe("0s");
    expect(formatUptime(42)).toBe("42s");
    expect(formatUptime(60)).toBe("1m");
    expect(formatUptime(90)).toBe("1m");
    expect(formatUptime(3600)).toBe("1h");
    expect(formatUptime(3660)).toBe("1h 1m");
    expect(formatUptime(86400)).toBe("1d");
    expect(formatUptime(90060)).toBe("1d 1h"); // 25h 1m
  });

  it("should show an em dash when there is no reading", () => {
    expect(formatUptime(null)).toBe("—");
    expect(formatUptime(undefined)).toBe("—");
  });

  it("should refuse to turn nonsense into a duration", () => {
    expect(formatUptime(-1)).toBe("—");
    expect(formatUptime(Number.NaN)).toBe("—");
  });
});
