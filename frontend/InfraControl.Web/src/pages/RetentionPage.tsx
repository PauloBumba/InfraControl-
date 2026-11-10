import { useCallback, useEffect, useState } from "react";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
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

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: 128,
  boxSizing: "border-box" as const,
};

/**
 * Retenção de dados por tipo (F12-T002): o admin define quantos dias o
 * cleanup mantém cada tipo de dado. "Padrão" = default de Retention:*
 * do appsettings; "Política" = override gravado no banco.
 *
 * Layout no padrão da casa: ListPageShell + StatusChip + banners ui/.
 */
export function RetentionPage() {
  const [policies, setPolicies] = useState<RetentionPolicyItem[]>([]);
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

  return (
    <ListPageShell
      title="Retenção de dados"
      singular="política"
      plural="políticas"
      total={policies.length}
      loading={loading}
      note={
        saved
          ? "Política atualizada."
          : "Quantos dias o cleanup mantém cada tipo de dado antes de apagar."
      }
      error={error}
      onRetry={() => void load()}
      actions={
        <Button
          variant="ghost"
          icon="pi pi-refresh"
          title="Atualizar"
          aria-label="Atualizar"
          disabled={loading}
          onClick={() => void load()}
        />
      }
    >
      {!loading && (
        <div className="uni-panel" style={{ padding: 16 }}>
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", fontSize: 13, borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid #e5e7eb", textAlign: "left" }}>
                  <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>
                    <i className="pi pi-table" style={{ marginRight: 4 }} /> Tipo de dado
                  </th>
                  <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>Origem</th>
                  <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>Dias</th>
                  <th style={{ padding: "8px 0 8px 8px", fontWeight: 600, textAlign: "right" }}>
                    Ações
                  </th>
                </tr>
              </thead>
              <tbody>
                {policies.map((p) => (
                  <tr key={p.dataType} style={{ borderBottom: "1px solid #f3f4f6" }}>
                    <td style={{ padding: "8px 12px 8px 0" }}>
                      {dataTypeLabel(p.dataType)}
                    </td>
                    <td style={{ padding: "8px 12px 8px 0" }}>
                      {p.source === "Policy" ? (
                        <StatusChip label="Política" background="#eff6ff" color="#2563eb" />
                      ) : (
                        <StatusChip
                          label="Padrão"
                          background="#f3f4f6"
                          color="#6b7280"
                        />
                      )}
                    </td>
                    <td style={{ padding: "8px 12px 8px 0" }}>
                      <input
                        type="number"
                        value={drafts[p.dataType] ?? ""}
                        onChange={(e) =>
                          setDrafts((d) => ({
                            ...d,
                            [p.dataType]:
                              e.target.value === "" ? null : Number(e.target.value),
                          }))
                        }
                        min={1}
                        max={36500}
                        style={inputStyle}
                        aria-label={`Dias — ${dataTypeLabel(p.dataType)}`}
                      />
                    </td>
                    <td
                      style={{
                        padding: "8px 0 8px 8px",
                        textAlign: "right",
                        whiteSpace: "nowrap",
                      }}
                    >
                      <Button
                        variant="ghost"
                        icon="pi pi-save"
                        title="Salvar política"
                        aria-label={`Salvar ${dataTypeLabel(p.dataType)}`}
                        disabled={drafts[p.dataType] == null || saving === p.dataType}
                        onClick={() => void save(p.dataType)}
                      />
                      {p.source === "Policy" && (
                        <Button
                          variant="ghost"
                          icon="pi pi-undo"
                          title="Restaurar padrão do servidor"
                          aria-label={`Restaurar ${dataTypeLabel(p.dataType)}`}
                          disabled={saving === p.dataType}
                          onClick={() => void reset(p.dataType)}
                        />
                      )}
                    </td>
                  </tr>
                ))}
                {policies.length === 0 && (
                  <tr>
                    <td
                      colSpan={4}
                      style={{
                        padding: 24,
                        textAlign: "center",
                        color: "#9ca3af",
                      }}
                    >
                      Nenhuma política de retenção encontrada.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </ListPageShell>
  );
}
