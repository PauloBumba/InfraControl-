import type { AuthUser } from "@/app/store/slices/authSlice";
import { ApiError, api } from "./client";

export { ApiError as AuthApiError };
export { GENERIC_ERROR, UNAUTHORIZED_ERROR as INVALID_CREDENTIALS } from "./client";

/**
 * F1-T016d (ADR-019): o 401 com estas mensagens é o CONTRATO que leva a UI
 * ao campo do 2º fator — o backend propositalmente guia a UI pela mensagem
 * pt-BR (o motivo enum não viaja no envelope). Mudar a string no controller
 * quebra a tela; se um dia existir um campo estruturado, troque aqui.
 */
export const MFA_REQUIRED_MESSAGE = "Código MFA obrigatório.";
export const MFA_INVALID_MESSAGE = "Código MFA inválido.";

/** `true` quando a senha passou e falta o código (TOTP ou recovery code). */
export function isMfaRequired(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401 && error.message === MFA_REQUIRED_MESSAGE;
}

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
 *
 * `anonymousExpected` = a recusa do login (401) NÃO derruba o estado global:
 * quem trata é a tela — sem isso, o interceptor de logout agiria sobre um
 * 401 legítimo da tela de login (mesma razão do GET /auth/me do boot).
 */
async function establishSession(path: string, body: unknown, anonymousExpected = false): Promise<AuthUser> {
  await api.post(path, body, anonymousExpected ? { anonymousExpected: true } : undefined);
  const user = await fetchCurrentUser();

  if (!user) {
    throw new ApiError("Sessão não estabelecida. Tente novamente.", 401);
  }

  return user;
}

/**
 * F1-T016d (ADR-019): `mfaCode` só existe no 2º passo — o 401 "Código MFA
 * obrigatório." é o que troca a tela; o retry reenvia email+senha+código.
 * Recovery code entra no MESMO campo (é single-use no backend).
 */
export function loginWithPassword(email: string, password: string, mfaCode?: string): Promise<AuthUser> {
  const body = mfaCode ? { email, password, mfaCode } : { email, password };
  return establishSession("/auth/login", body, true);
}

/** F1-T014: o backend valida assinatura/audience/issuer/expiração do id_token. */
export function loginWithGoogle(idToken: string): Promise<AuthUser> {
  return establishSession("/auth/google", { idToken }, true);
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

/**
 * Página pública /reset-password: o link e-maiado chega com
 * `?email=...&token=...` e esta chamada fecha o fluxo (204 no sucesso). É
 * anônima de propósito — o token do link É a prova; `anonymousExpected`
 * evita que o interceptor global de 401 confunda um 400 do envelope com
 * sessão morta. O motivo pt-BR (token expirado, política de senha) vem no
 * `errors` do envelope e a tela mostra.
 */
export async function resetPassword(email: string, token: string, newPassword: string): Promise<void> {
  await api.post("/auth/reset-password", { email, token, newPassword }, { anonymousExpected: true });
}

/**
 * F26-T001 — troca de senha autenticada (`POST /auth/change-password`,
 * userId vem do cookie de sessão no servidor). O backend REVOGA todas as
 * sessões do usuário (inclusive a atual) e audita: a UI deve avisar e
 * levar o usuário ao /login no sucesso. O motivo pt-BR (senha atual
 * errada, política) vem no `errors` do envelope.
 */
export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await api.post("/auth/change-password", { currentPassword, newPassword });
}

/**
 * Página pública de aceite de convite (pendência da F1-T017 fechada): o link
 * e-maiado é `/invitations/{id}?token=...` e a página roda o fluxo do
 * backend — `accept` provisiona a conta INATIVA e revela quem foi convidado,
 * `set-password` é o único passo que ativa, `reject` recusa. Os três são
 * AllowAnonymous; `anonymousExpected` impede que um erro da operação seja
 * confundido com sessão morta pelo interceptor global de 401.
 */
export interface InvitationAcceptance {
  invitationId: string;
  email: string;
  roles: string[];
}

export async function acceptInvitation(invitationId: string, token: string): Promise<InvitationAcceptance> {
  const data = await api.post<InvitationAcceptance>(
    `/auth/invitations/${encodeURIComponent(invitationId)}/accept`,
    { token },
    { anonymousExpected: true },
  );

  if (!data) {
    throw new ApiError("Resposta inesperada do servidor. Tente novamente em instantes.", 500);
  }

  return data;
}

export async function setInvitationPassword(invitationId: string, token: string, password: string): Promise<void> {
  await api.post(
    `/auth/invitations/${encodeURIComponent(invitationId)}/set-password`,
    { token, password },
    { anonymousExpected: true },
  );
}

export async function rejectInvitation(invitationId: string, token: string): Promise<void> {
  await api.post(
    `/auth/invitations/${encodeURIComponent(invitationId)}/reject`,
    { token },
    { anonymousExpected: true },
  );
}
