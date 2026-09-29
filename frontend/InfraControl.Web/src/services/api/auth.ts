import type { AuthUser } from "@/app/store/slices/authSlice";
import { ApiError, api } from "./client";

export { ApiError as AuthApiError };
export { GENERIC_ERROR, UNAUTHORIZED_ERROR as INVALID_CREDENTIALS } from "./client";

interface CurrentUserPayload {
  userId: string;
  email: string;
  displayName: string | null;
  roles: string[];
}



/** Traduz o payload de /auth/me para o formato usado na UI. */
function toAuthUser(payload: CurrentUserPayload): AuthUser {
  return {
    id: payload.userId,
    email: payload.email,
    displayName: payload.displayName || payload.email,
    roles: payload.roles ?? [],
  };
}

/**
 * Lê a sessão no backend. `null` = anônimo (401). Qualquer outra falha
 * (rede/5xx) vira AuthApiError — o chamador NÃO deve tratar isso como logout.
 */
export async function fetchCurrentUser(): Promise<AuthUser | null> {
  try {
    const data = await api.get<CurrentUserPayload>("/auth/me", { anonymousExpected: true });
    return data ? toAuthUser(data) : null;
  } catch (error) {
    if (error instanceof ApiError && error.isUnauthorized) return null;
    throw error;
  }
}

/**
 * Após o login o app pergunta QUEM é o usuário: é o /auth/me que traz roles e
 * permissões, então o estado da tela nunca diverge do backend.
 */
async function establishSession(path: string, body: unknown): Promise<AuthUser> {
  await api.post(path, body);
  const user = await fetchCurrentUser();

  if (!user) {
    throw new ApiError("Sessão não estabelecida. Tente novamente.", 401);
  }

  return user;
}

export function loginWithPassword(email: string, password: string): Promise<AuthUser> {
  return establishSession("/api/v1/auth/login", { email, password });
}

/** F1-T014: o backend valida assinatura/audience/issuer/expiração do id_token. */
export function loginWithGoogle(idToken: string): Promise<AuthUser> {
  return establishSession("/api/v1/auth/google", { idToken });
}

export async function logout(): Promise<void> {
  try {
    await api.post("/auth/logout");
  } catch {
    // Logout local sempre acontece; cookie órfão é expirado pelo backend.
  }
}

/** Resposta genérica: não revela se o e-mail existe (anti-enumeração). */
export async function requestPasswordReset(email: string): Promise<string> {
  const data = await api.post<{ message: string }>("/auth/forgot-password", { email });

  return data?.message ?? "Se o e-mail estiver cadastrado, enviaremos as instruções de redefinição.";
}
