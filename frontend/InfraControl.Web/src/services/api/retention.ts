import { api } from "./client";

/**
 * Políticas de retenção por tipo de dado (F12-T002): dias que o cleanup
 * mantém cada tipo. `source`: "Policy" = override do admin no banco;
 * "Config" = default de Retention:* do appsettings.
 */
export interface RetentionPolicyItem {
  dataType: string;
  days: number;
  source: string;
}

export async function getRetentionPolicies(): Promise<RetentionPolicyItem[]> {
  const data = await api.get<RetentionPolicyItem[] | null>("/operations/retention-policies");
  return data ?? [];
}

export async function upsertRetentionPolicy(
  dataType: string,
  days: number,
): Promise<RetentionPolicyItem> {
  const data = await api.put<RetentionPolicyItem | null>(
    `/operations/retention-policies/${encodeURIComponent(dataType)}`,
    { days },
  );
  if (!data) throw new Error("Resposta vazia do servidor.");
  return data;
}

export async function deleteRetentionPolicy(dataType: string): Promise<void> {
  await api.delete<null>(`/operations/retention-policies/${encodeURIComponent(dataType)}`);
}
