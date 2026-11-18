import { api } from "./client";

/**
 * F55 — Messaging Operations Center.
 * Todos os dados são reais: broker via Management API (backend-only) e
 * contagens do banco (outbox/inbox/deliveries). Campos null = indisponível.
 */

export interface RabbitMqBrokerSnapshot {
  reachable: boolean;
  unavailableReason: string | null;
  connectionState: string | null;
  exchanges: number | null;
  queues: number | null;
  consumers: number | null;
  connections: number | null;
  channels: number | null;
  messagesReady: number | null;
  messagesUnacked: number | null;
  messagesTotal: number | null;
  publishRatePerSecond: number | null;
  deliverRatePerSecond: number | null;
  ackRatePerSecond: number | null;
  publishedTotal: number | null;
  deliveredTotal: number | null;
}

export interface MessagingOverview {
  broker: RabbitMqBrokerSnapshot;
  app: {
    outboxPending: number;
    outboxExhausted: number;
    outboxProcessedTotal: number;
    avgOutboxLatencySeconds: number | null;
    inboxProcessedTotal: number;
    deliveriesPending: number;
    deliveriesProcessing: number;
    deliveriesSent: number;
    deliveriesFailed: number;
    deliveriesRetryDue: number;
    deliveriesDlq: number;
    deliveriesSuppressed: number;
    deliveriesCancelled: number;
  };
}

interface ListEnvelope<T> {
  reachable: boolean;
  unavailableReason: string | null;
  items: T[];
}

export interface RabbitMqQueueSummary {
  name: string;
  vhost: string;
  messages: number;
  messagesReady: number;
  messagesUnacked: number;
  consumers: number;
  state: string;
  durable: boolean;
}

export interface RabbitMqQueueConsumer {
  consumerTag: string;
  prefetchCount: number | null;
  active: boolean | null;
  channel: string | null;
}

export interface RabbitMqQueueDetail extends RabbitMqQueueSummary {
  publishRatePerSecond: number | null;
  deliverRatePerSecond: number | null;
  ackRatePerSecond: number | null;
  redeliverRatePerSecond: number | null;
  publishedTotal: number;
  deliveredTotal: number;
  consumerDetails: RabbitMqQueueConsumer[];
}

export interface RabbitMqExchangeSummary {
  name: string;
  vhost: string;
  type: string;
  durable: boolean;
  autoDelete: boolean;
  internal: boolean;
  publishedTotal: number | null;
  publishRatePerSecond: number | null;
}

export interface RabbitMqBindingSummary {
  source: string;
  destination: string;
  destinationType: string;
  routingKey: string;
}

export interface RabbitMqConnectionSummary {
  name: string;
  user: string;
  state: string;
  channels: number;
  connectedAt: string | null;
}

export interface MessagingWorker {
  id: string;
  kind: "container" | "consumer-group";
  name: string;
  detail: string | null;
  state: string | null;
  consumedQueue: string | null;
  consumerCount: number;
  cpuPercent: number | null;
  memoryUsageBytes: number | null;
  deliverRatePerSecond: number | null;
  deliveredTotal: number | null;
  messagesReady: number | null;
  messagesUnacked: number | null;
  peerHost: string | null;
  connectionName: string | null;
  brokerReachable: boolean;
  dockerReachable: boolean;
  pausedAtUtc: string | null;
}

export interface MessagingWorkersResult {
  brokerReachable: boolean;
  brokerUnavailableReason: string | null;
  dockerReachable: boolean;
  workers: MessagingWorker[];
}

export interface MessagingQueueControlResult {
  success: boolean;
  error: string | null;
  queueName: string;
  consumersCancelled: number;
  connectionsRecycled: number;
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface MessagingMessageItem {
  id: string;
  eventType: string;
  correlationId: string | null;
  occurredAt: string;
  processedAt: string | null;
  error: string | null;
  retryCount: number;
  maxRetries: number;
  state: string;
  payloadPreview: string | null;
}

export interface MessagingMessageDetail {
  id: string;
  eventType: string;
  correlationId: string | null;
  occurredAt: string;
  processedAt: string | null;
  processingDurationSeconds: number | null;
  error: string | null;
  retryCount: number;
  maxRetries: number;
  state: string;
  sanitizedPayload: string;
  timeline: { stage: string; atUtc: string | null; detail: string | null }[];
  inboxRecords: { eventId: string; consumer: string; processedAt: string }[];
}

export interface MessagingDeliveryItem {
  id: string;
  notificationId: string;
  userId: string;
  channel: string;
  status: string;
  attemptCount: number;
  nextAttemptAt: string | null;
  sentAt: string | null;
  lastError: string | null;
  suppressionReason: string | null;
  createdAt: string;
}

export interface MessagingFlowStage {
  id: string;
  name: string;
  service: string;
  description: string;
  status: string;
  metrics: Record<string, { value: string | null; source: string }>;
}

export interface MessagingFlowResult {
  brokerReachable: boolean;
  brokerUnavailableReason: string | null;
  stages: MessagingFlowStage[];
}

const BASE = "/messaging";

export const getMessagingOverview = () =>
  api.get<MessagingOverview>(`${BASE}/overview`);

export const getMessagingQueues = () =>
  api.get<ListEnvelope<RabbitMqQueueSummary>>(`${BASE}/queues`);

export const getMessagingQueueDetail = (name: string) =>
  api.get<RabbitMqQueueDetail>(`${BASE}/queues/${encodeURIComponent(name)}`);

export const getMessagingExchanges = () =>
  api.get<ListEnvelope<RabbitMqExchangeSummary>>(`${BASE}/exchanges`);

export const getMessagingBindings = () =>
  api.get<ListEnvelope<RabbitMqBindingSummary>>(`${BASE}/bindings`);

export const getMessagingConnections = () =>
  api.get<ListEnvelope<RabbitMqConnectionSummary>>(`${BASE}/connections`);

export const getMessagingWorkers = () =>
  api.get<MessagingWorkersResult>(`${BASE}/workers`);

export const pauseQueueConsumption = (queue: string, reason?: string) =>
  api.post<MessagingQueueControlResult>(
    `${BASE}/queues/${encodeURIComponent(queue)}/pause`, { reason });

export const resumeQueueConsumption = (queue: string) =>
  api.post<MessagingQueueControlResult>(
    `${BASE}/queues/${encodeURIComponent(queue)}/resume`);

export interface MessageSearchParams {
  messageId?: string;
  correlationId?: string;
  eventType?: string;
  state?: string;
  text?: string;
  minAttempts?: number;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

const qs = (params: Record<string, string | number | undefined>) => {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== "");
  return entries.length
    ? "?" + entries.map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`).join("&")
    : "";
};

export const searchMessagingMessages = (p: MessageSearchParams) =>
  api.get<Paged<MessagingMessageItem>>(`${BASE}/messages${qs({ ...p })}`);

export const getMessagingMessage = (id: string) =>
  api.get<MessagingMessageDetail>(`${BASE}/messages/${id}`);

export interface FailureSearchParams {
  channel?: string;
  notificationId?: string;
  minAttempts?: number;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export const searchMessagingDeliveries = (p: FailureSearchParams & { status?: string }) =>
  api.get<Paged<MessagingDeliveryItem>>(`${BASE}/deliveries${qs({ ...p })}`);

export const searchMessagingRetries = (p: FailureSearchParams) =>
  api.get<Paged<MessagingDeliveryItem>>(`${BASE}/retries${qs({ ...p })}`);

export const searchMessagingDlq = (p: FailureSearchParams) =>
  api.get<Paged<MessagingDeliveryItem>>(`${BASE}/dlq${qs({ ...p })}`);

export const replayNotificationDelivery = (id: string, reason?: string) =>
  api.post<{ success: boolean; error: string | null; deliveryId: string; status: string }>(
    `${BASE}/deliveries/${id}/replay`, { reason });

export const getMessagingFlow = () =>
  api.get<MessagingFlowResult>(`${BASE}/flow`);
