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

/** Item do export/import JSON (F50). */
export interface RetentionExportItem {
  dataType: string;
  days: number;
}

/**
 * Exporta o estado efetivo de todas as políticas (JSON) — download via
 * cookie de sessão; o caller monta objectURL + `<a download>`.
 */
export async function exportRetentionPolicies(): Promise<Blob> {
  return api.download("/operations/retention-policies/export");
}

/**
 * Importa políticas em lote: o backend valida TODOS os itens antes de
 * aplicar (payload inválido não deixa meio estado) e devolve quantos
 * foram aplicados.
 */
export async function importRetentionPolicies(
  policies: RetentionExportItem[],
): Promise<number> {
  const data = await api.post<{ imported: number } | null>(
    "/operations/retention-policies/import",
    { policies },
  );
  return data?.imported ?? 0;
}
