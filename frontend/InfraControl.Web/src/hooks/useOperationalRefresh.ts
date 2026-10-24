import { useEffect } from "react";
import { onNotificationPush } from "@/services/signalr/notifications";

/**
 * F44-T002/T003 (prompt master §8): recarrega os dados das páginas da
 * central quando algo operacional muda no backend, sem F5.
 *
 * Gatilhos:
 * - push SignalR do hub de notificações com evento operacional
 *   (IncidentCreated/Updated/Resolved, AlertCreated/Resolved) — o backend
 *   empurra via NotificationEventConsumer (F44-T001);
 * - `infracontrol:refresh` (botão global do AppLayout) — antes o evento
 *   só alcançava o Dashboard.
 *
 * A conexão em si é singleton do AppLayout; este hook só assina o
 * barramento `onNotificationPush` e limpa tudo no unmount.
 */
const OPERATIONAL_EVENTS = new Set([
  "IncidentCreatedDomainEvent",
  "IncidentUpdatedDomainEvent",
  "IncidentResolvedDomainEvent",
  "AlertCreatedDomainEvent",
  "AlertResolvedDomainEvent",
]);

export function useOperationalRefresh(onChange: () => void): void {
  useEffect(() => {
    const unsubscribe = onNotificationPush((event) => {
      if (OPERATIONAL_EVENTS.has(event.eventType)) onChange();
    });
    const handleGlobalRefresh = () => onChange();
    window.addEventListener("infracontrol:refresh", handleGlobalRefresh);
    return () => {
      unsubscribe();
      window.removeEventListener("infracontrol:refresh", handleGlobalRefresh);
    };
  }, [onChange]);
}
