import { useCallback, useEffect, useState } from "react";
import { Button } from "primereact/button";
import { Card } from "primereact/card";
import { InputNumber } from "primereact/inputnumber";
import { Message } from "primereact/message";
import { ProgressSpinner } from "primereact/progressspinner";
import { Tag } from "primereact/tag";
import {
  deleteRetentionPolicy,
  getRetentionPolicies,
  upsertRetentionPolicy,
  type RetentionPolicyItem,
} from "@/services/api/retention";

/** Rótulos pt-BR dos tipos de dado do cleanup (RetentionDataType). */
const DATA_TYPE_LABELS: Record<string, string> = {
  OutboxMessages: "Mensagens do Outbox processadas",
  ProcessedMessages: "Mensagens processadas (Inbox)",
  AuditEntries: "Registros de auditoria",
  MonitoringResults: "Resultados de monitoramento",
  Notifications: "Notificações",
  RemoteSessions: "Sessões de acesso remoto",
  DiscoveryRuns: "Execuções de descoberta",
  MonitoringEvents: "Eventos de monitoramento",
  Invitations: "Convites",
  UniFiSyncRuns: "Sincronizações UniFi",
};

function dataTypeLabel(dataType: string): string {
  return DATA_TYPE_LABELS[dataType] ?? dataType;
}

/**
 * Retenção de dados por tipo (F12-T002): o admin define quantos dias o
 * cleanup mantém cada tipo de dado. "Padrão" = default de Retention:*
 * do appsettings; "Política" = override gravado no banco.
 */
export function RetentionPage() {
  const [policies, setPolicies] = useState<RetentionPolicyItem[] | null>(null);
  const [drafts, setDrafts] = useState<Record<string, number | null>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await getRetentionPolicies();
      setPolicies(list);
      setDrafts(Object.fromEntries(list.map((p) => [p.dataType, p.days])));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar políticas.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async (dataType: string) => {
    const days = drafts[dataType];
    if (days == null || days < 1) {
      setError("Informe um número de dias maior que zero.");
      return;
    }
    setSaving(dataType);
    setError(null);
    setSaved(null);
    try {
      await upsertRetentionPolicy(dataType, days);
      setSaved(dataType);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao salvar política.");
    } finally {
      setSaving(null);
    }
  };

  const reset = async (dataType: string) => {
    setSaving(dataType);
    setError(null);
    setSaved(null);
    try {
      await deleteRetentionPolicy(dataType);
      setSaved(dataType);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao restaurar padrão.");
    } finally {
      setSaving(null);
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center py-16">
        <ProgressSpinner />
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 space-y-4">
      <div className="flex items-start justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-semibold flex items-center gap-2">
            <i className="pi pi-database text-primary" />
            Retenção de dados
          </h1>
          <p className="text-sm text-gray-500">
            Quantos dias o cleanup mantém cada tipo de dado antes de apagar.
            "Padrão" segue a configuração do servidor; "Política" é um override
            definido aqui e auditado.
          </p>
        </div>
        <Button
          icon="pi pi-refresh"
          outlined
          loading={loading}
          onClick={() => void load()}
          title="Atualizar"
        />
      </div>

      {error && <Message severity="error" text={error} />}
      {saved && <Message severity="success" text="Política atualizada." />}

      <Card>
        <div className="overflow-x-auto">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b text-left">
                <th className="py-2 pr-3 font-medium">
                  <i className="pi pi-table mr-1" /> Tipo de dado
                </th>
                <th className="py-2 pr-3 font-medium">Origem</th>
                <th className="py-2 pr-3 font-medium">Dias</th>
                <th className="py-2 font-medium text-right">Ações</th>
              </tr>
            </thead>
            <tbody>
              {(policies ?? []).map((p) => (
                <tr key={p.dataType} className="border-b last:border-0">
                  <td className="py-2 pr-3">{dataTypeLabel(p.dataType)}</td>
                  <td className="py-2 pr-3">
                    {p.source === "Policy" ? (
                      <Tag severity="info" value="Política" />
                    ) : (
                      <Tag severity="secondary" value="Padrão" />
                    )}
                  </td>
                  <td className="py-2 pr-3">
                    <InputNumber
                      value={drafts[p.dataType] ?? null}
                      onValueChange={(e) =>
                        setDrafts((d) => ({ ...d, [p.dataType]: e.value ?? null }))
                      }
                      min={1}
                      max={36500}
                      className="w-32"
                    />
                  </td>
                  <td className="py-2 text-right whitespace-nowrap">
                    <Button
                      icon="pi pi-save"
                      className="p-button-text p-button-sm"
                      title="Salvar política"
                      loading={saving === p.dataType}
                      disabled={drafts[p.dataType] == null}
                      onClick={() => void save(p.dataType)}
                    />
                    {p.source === "Policy" && (
                      <Button
                        icon="pi pi-undo"
                        className="p-button-text p-button-sm"
                        title="Restaurar padrão do servidor"
                        loading={saving === p.dataType}
                        onClick={() => void reset(p.dataType)}
                      />
                    )}
                  </td>
                </tr>
              ))}
              {(policies ?? []).length === 0 && (
                <tr>
                  <td colSpan={4} className="py-6 text-center text-gray-400">
                    Nenhuma política de retenção encontrada.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}
