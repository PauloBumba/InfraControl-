import type { ApiResponse } from "@/types/api";
import type { AuthUser } from "@/app/store/slices/authSlice";

/**
 * Cliente de autenticação (F14-T008/F1-T014).
 *
 * Regras de segurança aplicadas aqui:
 *  - a sessão é o cookie HttpOnly do backend (`credentials: "include"`); o
 *    JavaScript nunca enxerga access token nem refresh token;
 *  - o `refreshToken` que volta no corpo do login é DESCARTADO de propósito —
 *    o dashboard não usa refresh token (docs/05);
 *  - o id_token do Google é enviado direto ao backend e nunca é salvo em
 *    estado, storage ou log;
 *  - nada de resposta de login/forgot-password distingue "e-mail não existe"
 *    de "e-mail inativo" (anti-enumeração, igual ao backend).
 */

export class AuthApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "AuthApiError";
    this.status = status;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isForbidden(): boolean {
    return this.status === 403;
  }
}

const GENERIC_ERROR = "Não foi possível falar com o servidor. Tente novamente.";
const INVALID_CREDENTIALS = "E-mail ou senha inválidos.";

interface CurrentUserPayload {
  userId: string;
  email: string;
  displayName: string | null;
  roles: string[];
  permissions: string[];
}

async function request<T>(path: string, init?: RequestInit): Promise<T | null> {
  let response: Response;

  try {
    response = await fetch(path, {
      ...init,
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
        ...(init?.headers ?? {}),
      },
    });
  } catch {
    // Falha de transporte: sem status, sem vazar URL/credencial na mensagem.
    throw new AuthApiError(GENERIC_ERROR, 0);
  }

  if (response.status === 204) return null;

  const payload = (await response.json().catch(() => null)) as ApiResponse<T> | null;

  if (!response.ok || !payload?.isSuccess) {
    const message = payload?.errors?.[0] ?? (response.status === 401 ? INVALID_CREDENTIALS : GENERIC_ERROR);
    throw new AuthApiError(message, response.status);
  }

  return payload.data;
}

/** Traduz o payload de /auth/me para o formato usado na UI. */
function toAuthUser(payload: CurrentUserPayload): AuthUser {
  return {
    id: payload.userId,
    email: payload.email,
    displayName: payload.displayName || payload.email,
    roles: payload.roles ?? [],
    permissions: payload.permissions ?? [],
  };
}

/**
 * Lê a sessão no backend. `null` = anônimo (401). Qualquer outra falha
 * (rede/5xx) vira AuthApiError — o chamador NÃO deve tratar isso como logout.
 */
export async function fetchCurrentUser(): Promise<AuthUser | null> {
  try {
    const data = await request<CurrentUserPayload>("/api/v1/auth/me");
    return data ? toAuthUser(data) : null;
  } catch (error) {
    if (error instanceof AuthApiError && error.isUnauthorized) return null;
    throw error;
  }
}

/**
 * Após o login o app pergunta QUEM é o usuário: é o /auth/me que traz roles e
 * permissões, então o estado da tela nunca diverge do backend.
 */
async function establishSession(path: string, body: unknown): Promise<AuthUser> {
  await request(path, { method: "POST", body: JSON.stringify(body) });
  const user = await fetchCurrentUser();

  if (!user) {
    throw new AuthApiError("Sessão não estabelecida. Tente novamente.", 401);
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
    await request("/api/v1/auth/logout", { method: "POST" });
  } catch {
    // Logout local sempre acontece; cookie órfão é expirado pelo backend.
  }
}

/** Resposta genérica: não revela se o e-mail existe (anti-enumeração). */
export async function requestPasswordReset(email: string): Promise<string> {
  const data = await request<{ message: string }>("/api/v1/auth/forgot-password", {
    method: "POST",
    body: JSON.stringify({ email }),
  });

  return data?.message ?? "Se o e-mail estiver cadastrado, enviaremos as instruções de redefinição.";
}
