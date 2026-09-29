import { api } from "./client";

/**
 * F1-T016d (ADR-020): auto-serviço de sessões — o usuário lista e revoga as
 * PRÓPRIAS sessões. Todas as escritas passam pelo client, que anexa o header
 * de antiforgery (ADR-017) e trata o 401 global como expiração de sessão.
 */
export interface SessionItem {
  id: string;
  createdAt: string;
  lastSeenAt: string;
  expiresAt: string;
  /** Teto absoluto (ADR-018): sessão nunca passa dele, nem com refresh. */
  absoluteExpiresAt: string;
  /** A sessão do cookie atual — a linha marcada "Este dispositivo". */
  isCurrent: boolean;
}

/** Sessões ativas do usuário, da mais recente para a mais antiga. */
export async function listSessions(): Promise<SessionItem[]> {
  const data = await api.get<SessionItem[]>("/auth/sessions");
  return data ?? [];
}

/**
 * Revoga UMA sessão. Se for a atual, o backend também derruba o cookie —
 * o chamador deve tratar isso como logout local na hora.
 */
export function revokeSession(sessionId: string): Promise<null> {
  return api.delete<null>(`/auth/sessions/${sessionId}`);
}

/** "Sair de todos os outros dispositivos": a sessão atual continua ativa. */
export function revokeOtherSessions(): Promise<null> {
  return api.delete<null>("/auth/sessions/others");
}
