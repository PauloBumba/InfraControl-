/** F52 — códigos de baixa (padrão ServiceNow) e labels pt-BR. */
export const RESOLUTION_CODE_LABELS: Record<string, string> = {
  Fixed: "Corrigido",
  Workaround: "Contorno",
  Duplicate: "Duplicado",
  NoAction: "Sem ação necessária",
  Other: "Outro",
};

export const RESOLUTION_CODE_OPTIONS = Object.entries(
  RESOLUTION_CODE_LABELS,
).map(([value, label]) => ({ value, label }));

export function resolutionCodeLabel(code: string): string {
  const hit = Object.entries(RESOLUTION_CODE_LABELS).find(
    ([key]) => key.toLowerCase() === code.toLowerCase(),
  );
  return hit?.[1] ?? code;
}
