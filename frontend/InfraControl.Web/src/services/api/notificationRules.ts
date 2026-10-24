import { api } from "./client";

/** F42-T003 — regras de notificação por tipo de evento (Administer). */
export interface NotificationEventRule {
  eventType: string;
  category: string;
  description: string;
  enabled: boolean;
  channels: string[];
}

export interface SaveNotificationEventRulePayload {
  eventType: string;
  enabled: boolean;
  channels: string[] | null;
}

function required<T>(promise: Promise<T | null>, fallback: T): Promise<T> {
  return promise.then((data) => data ?? fallback);
}

export function listNotificationRules(): Promise<NotificationEventRule[]> {
  return required(
    api.get<NotificationEventRule[]>("/notifications/rules"),
    []
  );
}

export function saveNotificationRules(
  rules: SaveNotificationEventRulePayload[]
): Promise<NotificationEventRule[]> {
  return required(
    api.put<NotificationEventRule[]>("/notifications/rules", { rules }),
    []
  );
}
