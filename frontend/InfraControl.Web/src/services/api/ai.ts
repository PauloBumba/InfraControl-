import { api } from "./client";

/**
 * Copilot (ADR-052 · Fase B) — chat grounded LEVEL 0. Read: qualquer
 * role autenticada conversa; a IA enxerga o que o usuário enxerga
 * (ADR-040 §3). Sem provider configurado o backend responde 503 com
 * mensagem amigável (§44/§57) — a tela mostra o estado degradado.
 */
export interface ChatTurn {
  role: "user" | "assistant";
  content: string;
}

export interface AiChatEvidence {
  source: string;
  sourceType: string;
  citation: string;
  confidence: string;
  timestampUtc: string;
}

export interface AiChatResponse {
  answer: string;
  evidences: AiChatEvidence[];
  provider: string;
  model: string;
  usedFallback: boolean;
}

/** Envia a conversa (últimas 20 turns; a última é do usuário). */
export async function sendAiChat(
  messages: ChatTurn[],
): Promise<AiChatResponse> {
  return (await api.post<AiChatResponse>("/ai/chat", { messages })) as AiChatResponse;
}
