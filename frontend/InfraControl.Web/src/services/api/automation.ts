import { api } from "./client";

/**
 * Automation Agent LEVEL 1 (ADR-055 · Fase F) —
 * `POST /automation/recommendations` (Operate). Recomenda para o humano
 * decidir: sem escrita, sem aprovação, sem execução (blueprint §39).
 * Sem sinais ativos o backend devolve `items` vazio sem chamar a IA;
 * sem provider configurado → 503 amigável (§44/§57).
 */
export interface AutomationRecommendation {
  title: string;
  rationale: string;
  priority: "alta" | "média" | "baixa";
}

export interface AutomationRecommendations {
  items: AutomationRecommendation[];
  provider: string;
  model: string;
  usedFallback: boolean;
}

export function generateAutomationRecommendations(): Promise<AutomationRecommendations> {
  return api.post<AutomationRecommendations>(
    "/automation/recommendations",
  ) as Promise<AutomationRecommendations>;
}
