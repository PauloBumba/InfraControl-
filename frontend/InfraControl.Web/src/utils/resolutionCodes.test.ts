import { describe, expect, it } from "vitest";
import { RESOLUTION_CODE_LABELS, resolutionCodeLabel } from "./resolutionCodes";

describe("resolutionCodes", () => {
  it("labels em pt-BR cobrem todos os códigos", () => {
    expect(RESOLUTION_CODE_LABELS).toEqual({
      Fixed: "Corrigido",
      Workaround: "Contorno",
      Duplicate: "Duplicado",
      NoAction: "Sem ação necessária",
      Other: "Outro",
    });
  });

  it("resolutionCodeLabel devolve o label e cai para o código cru", () => {
    expect(resolutionCodeLabel("Fixed")).toBe("Corrigido");
    expect(resolutionCodeLabel("workaround")).toBe("Contorno");
    expect(resolutionCodeLabel("Unknown")).toBe("Unknown");
  });
});
