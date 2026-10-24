import { api } from "./client";

/**
 * ADR-062 — gestão do canal WhatsApp (Evolution API) dentro da plataforma.
 *
 * O backend (`/api/v1/integrations/whatsapp`, policy Administer) é a
 * autoridade: a ApiKey é write-only (cifrada AES-GCM) e nunca é devolvida —
 * só o indicador `apiKeySet`. O QR volta como `png` em base64 (o `byte[]` do
 * DTO .NET serializa como string base64 no JSON), pronto para virar data-URI.
 */

/** O envelope do client devolve `null` quando `data` vem ausente. */
function required<T>(promise: Promise<T | null>, fallback: T): Promise<T> {
  return promise.then((data) => data ?? fallback);
}

/** Configuração efetiva (banco → fallback env). ApiKey nunca sai. */
export interface WhatsAppSettings {
  baseUrl: string;
  apiKeySet: boolean;
  instanceId: string;
  groupId: string | null;
  phone: string | null;
  connectionState: string | null;
  source: string;
  lastTestedAtUtc: string | null;
  lastTestOk: boolean;
  lastTestError: string | null;
  updatedAtUtc: string | null;
  notificationsEnabled: boolean;
}

/** Payload de gravação — `apiKey` vazio/null mantém a chave já gravada. */
export interface WhatsAppSettingsPayload {
  baseUrl: string;
  instanceId: string;
  apiKey: string | null;
  groupId: string | null;
  phone: string | null;
  notificationsEnabled: boolean;
}

export interface WhatsAppTestResult {
  ok: boolean;
  error: string | null;
}

/** QR de pareamento — `png` em base64 (montar `data:image/png;base64,…`). */
export interface WhatsAppQr {
  png: string | null;
  pairingCode: string | null;
  state: string | null;
}

export interface WhatsAppConnectionState {
  state: string | null;
}

export interface WhatsAppInstanceCreation {
  instanceId: string;
  created: boolean;
  state: string | null;
}

export interface WhatsAppGroup {
  id: string;
  subject: string;
  size: number;
  owner: string | null;
  desc: string | null;
  restrict: boolean;
  announce: boolean;
}

const EMPTY_SETTINGS: WhatsAppSettings = {
  baseUrl: "",
  apiKeySet: false,
  instanceId: "",
  groupId: null,
  phone: null,
  connectionState: null,
  source: "environment",
  lastTestedAtUtc: null,
  lastTestOk: false,
  lastTestError: null,
  updatedAtUtc: null,
  notificationsEnabled: false,
};

export function getWhatsAppSettings(): Promise<WhatsAppSettings> {
  return required(api.get<WhatsAppSettings>("/integrations/whatsapp/config"), EMPTY_SETTINGS);
}

export function saveWhatsAppSettings(
  payload: WhatsAppSettingsPayload,
): Promise<WhatsAppSettings> {
  return required(api.put<WhatsAppSettings>("/integrations/whatsapp/config", payload), EMPTY_SETTINGS);
}

export function getWhatsAppConnectionState(): Promise<WhatsAppConnectionState> {
  return required(
    api.get<WhatsAppConnectionState>("/integrations/whatsapp/connection-state"),
    { state: null },
  );
}

/** Gera um novo QR (logout best-effort + connect — workaround #1408). */
export function generateWhatsAppQr(): Promise<WhatsAppQr> {
  return required(
    api.post<WhatsAppQr>("/integrations/whatsapp/connect"),
    { png: null, pairingCode: null, state: null },
  );
}

export function disconnectWhatsAppInstance(): Promise<WhatsAppConnectionState> {
  return required(
    api.post<WhatsAppConnectionState>("/integrations/whatsapp/disconnect"),
    { state: null },
  );
}

export function createWhatsAppInstance(): Promise<WhatsAppInstanceCreation> {
  return api.post<WhatsAppInstanceCreation>("/integrations/whatsapp/instance").then((data) => {
    if (!data) throw new Error("Resposta vazia ao criar a instância WhatsApp.");
    return data;
  });
}

export function listWhatsAppGroups(): Promise<WhatsAppGroup[]> {
  return required(api.get<WhatsAppGroup[]>("/integrations/whatsapp/groups"), []);
}

export function sendWhatsAppTest(): Promise<WhatsAppTestResult> {
  return required(
    api.post<WhatsAppTestResult>("/integrations/whatsapp/test"),
    { ok: false, error: "Resposta vazia do servidor." },
  );
}
