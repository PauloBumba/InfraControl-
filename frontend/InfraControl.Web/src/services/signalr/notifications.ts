import type { HubConnection } from "@microsoft/signalr";
import { createHubConnection } from "./connection";

/**
 * Consumo do hub `GET /hubs/notifications` (F9-T004, docs/07 canal
 * SignalR — entrega 3 do backlog de UX 2026-10-03).
 *
 * O backend envia o evento `notification` para o grupo `user:{userId}`
 * quando a delivery do canal SignalR sai da fila (NotificationSignalRWorker);
 * o payload espelha o feed persistido (NotificationFeedItem) mais
 * `deliveryId`. Este módulo é um singleton com um único listener interno e
 * um barramento leve para os consumidores (AppLayout = contador do sino,
 * NotificationsPage = inserção no feed) — um hub por aba, sem reconectar
 * duplicado.
 */
export interface NotificationPushEvent {
  deliveryId: string;
  notificationId: string;
  title: string;
  message: string;
  eventType: string;
  createdAt: string;
}

type Listener = (event: NotificationPushEvent) => void;

let connection: HubConnection | null = null;
let starting = false;
let retryTimer: ReturnType<typeof setTimeout> | null = null;
let listeners: Listener[] = [];

/** Assina o barramento; retorna a função de unsubscribe. */
export function onNotificationPush(listener: Listener): () => void {
  listeners.push(listener);
  return () => {
    listeners = listeners.filter((entry) => entry !== listener);
  };
}

/**
 * Sobe a conexão (idempotente). O negotiate é POST autenticado com o
 * antiforgery (ver `connection.ts`); se ainda não há sessão/token a
 * primeira tentativa falha — tenta de novo em 10s enquanto o layout
 * logado estiver montado. Reconexões pós-sucesso são do
 * `withAutomaticReconnect` do builder.
 */
export async function startNotificationsConnection(): Promise<void> {
  if (connection || starting) return;
  starting = true;

  const hub = createHubConnection("/hubs/notifications");
  hub.on("notification", (payload: NotificationPushEvent) => {
    listeners.forEach((listener) => listener(payload));
  });

  try {
    await hub.start();
    connection = hub;
  } catch {
    // sessão/token ainda não prontos (ou API fora) — nova tentativa depois;
    // o stop() do unmount limpa o timer e descarta este hub.
    retryTimer = setTimeout(() => {
      retryTimer = null;
      starting = false;
      void startNotificationsConnection();
    }, 10_000);
    return;
  }

  starting = false;
}

/** Derruba a conexão (unmount do layout logado / logout). */
export async function stopNotificationsConnection(): Promise<void> {
  const current = connection;
  connection = null;
  starting = false;
  if (retryTimer) {
    clearTimeout(retryTimer);
    retryTimer = null;
  }
  listeners = [];
  if (current) {
    try {
      await current.stop();
    } catch {
      // conexão já morrendo — nada a fazer
    }
  }
}
