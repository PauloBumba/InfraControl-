import { api } from "./client";
import type { UserRole } from "@/types/roles";

/**
 * F17-T006 — conexão da UsersPage ao backend (contrato real de
 * `GET/PATCH /api/v1/admin/users`, atribuição de papéis e convites).
 *
 * Decisão de rota (auditoria de 01/10, executada no F18-T004): a página
 * usa SOMENTE `api/v1/admin/users` — o `RolesController`
 * (`api/identity/users`, fora do prefixo v1, DELETE duplicado) foi
 * REMOVIDO do backend; a operação "trocar papéis" vira diff de
 * add/remove nestas rotas.
 *
 * Enums (ADR-061 §1): o contrato de enum viaja como STRING no wire. Na
 * LISTAGEM `roles` ainda vem numérico (DTO legado, exceção documentada);
 * no PATH da rota `{role}` vai o NOME (`Admin`), case-insensitive; no
 * BODY do convite `roles` são NOMES (H1: antes era numérico e quebrava
 * contra o view model string).
 */
export interface AdminUser {
  userId: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: number[];
  updatedAt: string;
}

export const ROLE_VALUE: Record<UserRole, number> = {
  Viewer: 1,
  Operator: 2,
  Admin: 3,
};

export const ROLE_BY_VALUE: Record<number, UserRole> = {
  1: "Viewer",
  2: "Operator",
  3: "Admin",
};

/** Lista completa (sem paginação no contrato). Policy Operate na classe. */
export async function listUsers(): Promise<AdminUser[]> {
  const data = await api.get<AdminUser[]>("/admin/users");
  return data ?? [];
}

/** Ativa/desativa (Administer) — 204, ou 404 no envelope. */
export function setUserStatus(userId: string, active: boolean): Promise<null> {
  return api.patch<null>(`/admin/users/${userId}/status`, { active });
}

/** Atribui papel (Administer) — sem body; o nome vai na rota. */
export function assignRole(userId: string, role: UserRole): Promise<null> {
  return api.post<null>(`/admin/users/${userId}/roles/${role}`);
}

/**
 * Remove papel (Administer) — 404 para usuário desconhecido e 409 com a
 * mensagem pt-BR quando seria o último administrador (F0-T023).
 */
export function removeRole(userId: string, role: UserRole): Promise<null> {
  return api.delete<null>(`/admin/users/${userId}/roles/${role}`);
}

/**
 * Convite (Administer) → 201 `{invitationId, token}`. O token NÃO é
 * exibido nem logado aqui — o link de aceite continua sendo pendência de
 * página; por ora o backend o manda para o log do e-mail.
 */
export function createInvitation(input: {
  email: string;
  role: UserRole;
  expiresInDays?: number;
}): Promise<{ invitationId: string; token: string } | null> {
  return api.post<{ invitationId: string; token: string }>("/admin/invitations", {
    email: input.email,
    roles: [input.role],
    expiresInDays: input.expiresInDays ?? 7,
  });
}

/** Atualiza usuário (Administer) — 204, ou 404 no envelope. */
export function updateUser(userId: string, displayName: string, email: string): Promise<null> {
  return api.put<null>(`/admin/users/${userId}`, { displayName, email });
}

/** Exclui usuário (Administer) — 204, ou 404/409 no envelope. */
export function deleteUser(userId: string): Promise<null> {
  return api.delete<null>(`/admin/users/${userId}`);
}
