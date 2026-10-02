import { api } from "./client";

/**
 * F17-T010 — contrato real de `/api/v1/notifications` (F9-T002/T003).
 *
 * O que existe: preferências de entrega por usuário/canal
 * (GET/PUT /notifications/preferences) e o feed paginado do usuário
 * (GET /notifications — F18-T001: destinatário direto OU por role,
 * mais recentes primeiro, SEM estado de leitura).
 *
 * Semântica (docs/07): **default habilitado — preferência desligada =
 * sem delivery** (opt-out); linha ausente = canal ativo.
 *
 * Serialização: `channel` é enum .NET e o MVC não registra
 * JsonStringEnumConverter → NUMÉRICO no JSON (1..6), inclusive no body do
 * PUT (o GET/PUT de `userId` é Guid).
 */
export const CHANNEL_LABEL: Record<number, string> = {
  1: "SignalR",
  2: "Email",
  3: "Telegram",
  4: "WhatsApp",
  5: "Webhook",
  6: "Slack",
};

/** Ordem do enum `NotificationChannel` (SignalR=1 … Slack=6). */
export const CHANNELS = [1, 2, 3, 4, 5, 6] as const;

export const CHANNEL_ICON: Record<number, string> = {
  1: "pi pi-bolt",
  2: "pi pi-envelope",
  3: "pi pi-send",
  4: "pi pi-comment",
  5: "pi pi-link",
  6: "pi pi-hashtag",
};

export interface NotificationPreference {
  userId: string;
  channel: number;
  enabled: boolean;
}

/** Preferências gravadas do usuário (coleção sem paginação; Read). */
export async function listPreferences(userId: string): Promise<NotificationPreference[]> {
  const data = await api.get<NotificationPreference[]>(
    `/notifications/preferences?userId=${userId}`,
  );
  return data ?? [];
}

/**
 * Upsert (PUT, Operate + antiforgery): cria ou atualiza a linha do canal.
 * 404 pt-BR quando o usuário não existe; validação de canal no handler.
 */
export function setPreference(
  userId: string,
  channel: number,
  enabled: boolean,
): Promise<NotificationPreference | null> {
  return api.put<NotificationPreference>("/notifications/preferences", {
    userId,
    channel,
    enabled,
  });
}

/** Item do feed (F18-T001 — `NotificationFeedItemDto`, sem flag de lida). */
export interface NotificationFeedItem {
  id: string;
  eventType: string;
  title: string;
  message: string;
  createdAt: string;
}

/** `PagedResult<NotificationFeedItemDto>` — o campo `data` do envelope. */
export interface PagedFeed {
  items: NotificationFeedItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/**
 * Feed do usuário autenticado (`GET /notifications`, Read): destinatário
 * direto OU de role, mais recentes primeiro; clamp 1..100 no handler.
 */
export async function listFeed(page = 1, pageSize = 20): Promise<PagedFeed> {
  const data = await api.get<PagedFeed>(
    `/notifications?page=${page}&pageSize=${pageSize}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize };
}
