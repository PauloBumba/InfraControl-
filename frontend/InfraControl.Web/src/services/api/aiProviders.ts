import { api } from "./client";

/**
 * FAI-T011b (ADR-057) — CRUD Administer do cadastro de providers de IA.
 * A chave NUNCA volta plaintext: só `apiKeySet` + sufixo mascarado
 * `••••XXXX`; no form, campo vazio = preserva a gravada (write-only).
 */
function required<T>(promise: Promise<T | null>, fallback: T): Promise<T> {
  return promise.then((data) => data ?? fallback);
}

export interface AiProviderDto {
  id: string;
  name: string;
  baseUrl: string;
  active: boolean;
  apiKeySet: boolean;
  apiKeyMasked: string | null;
  models: string[];
}

export interface AiProviderCapabilityRouteDto {
  capability: string;
  providerId: string | null;
  providerName: string | null;
  providerActive: boolean;
  fallbackProviderId: string | null;
  fallbackProviderName: string | null;
  fallbackProviderActive: boolean;
  defaultModel: string;
  fallbackModel: string | null;
}

export interface AiProviderList {
  providers: AiProviderDto[];
  capabilities: AiProviderCapabilityRouteDto[];
  availableAdapterNames: string[];
}

export interface AiProviderConnectionTestResult {
  ok: boolean;
  error: string | null;
}

export interface AiProviderModelsResult {
  models: string[];
  error: string | null;
}

export interface CreateAiProviderPayload {
  name: string;
  baseUrl: string;
  /** `null`/vazio = provider sem chave (ex.: ollama local). */
  apiKey: string | null;
  models: string[] | null;
}

export interface UpdateAiProviderPayload {
  baseUrl: string;
  /** `null`/vazio = mantém a chave gravada no servidor. */
  apiKey: string | null;
  models: string[] | null;
}

export interface UpsertCapabilityPayload {
  providerId: string;
  defaultModel: string;
  fallbackProviderId: string | null;
  fallbackModel: string | null;
}

const EMPTY_LIST: AiProviderList = {
  providers: [],
  capabilities: [],
  availableAdapterNames: [],
};

export function getAiProviders(): Promise<AiProviderList> {
  return required(api.get<AiProviderList>("/admin/ai-providers"), EMPTY_LIST);
}

export function createAiProvider(
  payload: CreateAiProviderPayload
): Promise<AiProviderDto> {
  return required(
    api.post<AiProviderDto>("/admin/ai-providers", payload),
    { id: "", name: "", baseUrl: "", active: false, apiKeySet: false, apiKeyMasked: null, models: [] }
  );
}

export function updateAiProvider(
  id: string,
  payload: UpdateAiProviderPayload
): Promise<AiProviderDto> {
  return required(
    api.put<AiProviderDto>(`/admin/ai-providers/${id}`, payload),
    { id, name: "", baseUrl: "", active: false, apiKeySet: false, apiKeyMasked: null, models: [] }
  );
}

export function setAiProviderActive(
  id: string,
  active: boolean
): Promise<AiProviderDto> {
  return required(
    api.put<AiProviderDto>(`/admin/ai-providers/${id}/active`, { active }),
    { id, name: "", baseUrl: "", active, apiKeySet: false, apiKeyMasked: null, models: [] }
  );
}

export function upsertAiProviderCapability(
  capability: string,
  payload: UpsertCapabilityPayload
): Promise<AiProviderCapabilityRouteDto> {
  return required(
    api.put<AiProviderCapabilityRouteDto>(
      `/admin/ai-providers/capabilities/${encodeURIComponent(capability)}`,
      payload
    ),
    {
      capability,
      providerId: payload.providerId,
      providerName: null,
      providerActive: false,
      fallbackProviderId: payload.fallbackProviderId,
      fallbackProviderName: null,
      fallbackProviderActive: false,
      defaultModel: payload.defaultModel,
      fallbackModel: payload.fallbackModel,
    }
  );
}

export function deleteAiProviderCapability(
  capability: string
): Promise<void> {
  return api.delete<void>(
    `/admin/ai-providers/capabilities/${encodeURIComponent(capability)}`
  ).then(() => undefined);
}

export function testAiProviderConnection(
  id: string
): Promise<AiProviderConnectionTestResult> {
  return required(
    api.post<AiProviderConnectionTestResult>(
      `/admin/ai-providers/${id}/test-connection`
    ),
    { ok: false, error: "Resposta vazia do servidor." }
  );
}

export function listAiProviderModels(
  id: string
): Promise<AiProviderModelsResult> {
  return required(
    api.get<AiProviderModelsResult>(`/admin/ai-providers/${id}/models`),
    { models: [], error: "Resposta vazia do servidor." }
  );
}
