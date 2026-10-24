import { api } from "./client";

/** O envelope do client devolve `null` quando `data` vem ausente — aqui o
 * fallback é explícito (mesmo padrão de clients.ts/unifi.ts). */
function required<T>(promise: Promise<T | null>, fallback: T): Promise<T> {
  return promise.then((data) => data ?? fallback);
}

/** F41-T003 — configuração SMTP (senha NUNCA vem do backend). */
export interface EmailSettings {
  host: string;
  port: number;
  username: string;
  passwordSet: boolean;
  from: string;
  fromName: string | null;
  useSsl: boolean;
  notificationsEnabled: boolean | null;
  source: string;
  lastTestedAtUtc: string | null;
  lastTestOk: boolean;
  lastTestError: string | null;
  updatedAtUtc: string | null;
}

export interface EmailSettingsPayload {
  host: string;
  port: number;
  username: string;
  /** `null`/vazio = mantém a senha já gravada no servidor. */
  password: string | null;
  from: string;
  fromName: string | null;
  useSsl: boolean;
  notificationsEnabled: boolean | null;
}

export interface EmailTestResult {
  ok: boolean;
  error: string | null;
}

export function getEmailConfig(): Promise<EmailSettings> {
  return required(api.get<EmailSettings>("/integrations/email/config"), EMPTY_SETTINGS);
}

export function saveEmailConfig(
  payload: EmailSettingsPayload
): Promise<EmailSettings> {
  return required(api.put<EmailSettings>("/integrations/email/config", payload), EMPTY_SETTINGS);
}

export function testEmail(): Promise<EmailTestResult> {
  return required(api.post<EmailTestResult>("/integrations/email/test"), {
    ok: false,
    error: "Resposta vazia do servidor.",
  });
}

const EMPTY_SETTINGS: EmailSettings = {
  host: "",
  port: 587,
  username: "",
  passwordSet: false,
  from: "",
  fromName: null,
  useSsl: true,
  notificationsEnabled: null,
  source: "environment",
  lastTestedAtUtc: null,
  lastTestOk: false,
  lastTestError: null,
  updatedAtUtc: null,
};
