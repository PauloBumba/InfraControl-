import { api } from "./client";

/**
 * Fase 06d — preferências do usuário autenticado.
 * `GET/PUT /api/v1/account/settings` — o userId vem do cookie de sessão,
 * nunca do payload.
 */
export interface UserSettings {
  userId: string;
  timezone: string;
  darkMode: boolean;
  autoRefresh: boolean;
  telemetry: boolean;
}

export interface UpdateUserSettingsPayload {
  timezone: string;
  darkMode: boolean;
  autoRefresh: boolean;
  telemetry: boolean;
}

export async function getAccountSettings(): Promise<UserSettings> {
  const data = await api.get<UserSettings | null>("/account/settings");
  // 204 não acontece neste endpoint; null só se o envelope vier vazio.
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function updateAccountSettings(
  payload: UpdateUserSettingsPayload,
): Promise<UserSettings> {
  const data = await api.put<UserSettings | null>("/account/settings", payload);
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}
