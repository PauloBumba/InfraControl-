import { useCallback, useEffect, useRef, useState } from "react";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
import {
  deleteRetentionPolicy,
  deleteTableRetention,
  exportRetentionPolicies,
  getRetentionPolicies,
  getRetentionTables,
  importRetentionPolicies,
  upsertRetentionPolicy,
  upsertTableRetention,
  type RetentionPolicyItem,
  type RetentionTableItem,
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
  OccurrenceTransitions: "Transições de ocorrências (histórico)",
  Backups: "Arquivos de backup",
  Grafana: "Métricas do Grafana",
  Zabbix: "Histórico do Zabbix",
  Databases: "Dados dos bancos da stack",
};

/**
 * F50: tipos cuja linha é só registro de política — o app NÃO apaga o
 * dado (Grafana/Zabbix são sistemas externos; bancos da stack são
 * gerenciados). Exibidos com o chip "Gerenciada".
 */
const MANAGED_TYPES: ReadonlySet<string> = new Set([
  "Grafana",
  "Zabbix",
  "Databases",
]);

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
 * Retenção de dados por tipo (F12-T002 + F50): o admin define quantos
 * dias cada tipo de dado é mantido antes de apagar. "Padrão" = default de
 * Retention:* do appsettings; "Política" = override gravado no banco;
 * "Backup" = BackupSettings.RetentionDays (mesmo valor do /settings/backup).
 * Grafana/Zabbix/Databases são política gerenciada (só registro — o app
 * não apaga). Export/import JSON das linhas.
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
  const [imported, setImported] = useState<number | null>(null);
  const [tables, setTables] = useState<RetentionTableItem[]>([]);
  const [tableDrafts, setTableDrafts] = useState<Record<string, number | null>>({});
  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadTables = useCallback(async () => {
    try {
      const list = await getRetentionTables();
      setTables(list);
      setTableDrafts(
        Object.fromEntries(
          list.filter((t) => t.policyDays != null).map((t) => [t.entityName, t.policyDays]),
        ),
      );
    } catch {
      // Tabelas são complemento — falha não derruba a tela principal.
      setTables([]);
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await getRetentionPolicies();
      setPolicies(list);
      setDrafts(Object.fromEntries(list.map((p) => [p.dataType, p.days])));
      await loadTables();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar políticas.");
    } finally {
      setLoading(false);
    }
  }, [loadTables]);

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

  const saveTable = async (entityName: string) => {
    const days = tableDrafts[entityName];
    if (days == null || days < 1) {
      setError("Informe um número de dias maior que zero.");
      return;
    }
    setSaving(`table:${entityName}`);
    setError(null);
    setSaved(null);
    try {
      await upsertTableRetention(entityName, days);
      setSaved(`table:${entityName}`);
      await loadTables();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao salvar política da tabela.");
    } finally {
      setSaving(null);
    }
  };

  const resetTable = async (entityName: string) => {
    setSaving(`table:${entityName}`);
    setError(null);
    setSaved(null);
    try {
      await deleteTableRetention(entityName);
      setSaved(`table:${entityName}`);
      await loadTables();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao remover política da tabela.");
    } finally {
      setSaving(null);
    }
  };

  /** Exporta o estado efetivo e dispara o download do JSON no browser. */
  const exportPolicies = async () => {
    setError(null);
    try {
      const blob = await exportRetentionPolicies();
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `retention-policies-${new Date().toISOString().slice(0, 10)}.json`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao exportar políticas.");
    }
  };

  /** Valida o JSON localmente e delega a aplicação ao backend (tudo-ou-nada). */
  const importPolicies = async (file: File) => {
    setError(null);
    setImported(null);
    try {
      const text = await file.text();
      let parsed: unknown;
      try {
        parsed = JSON.parse(text);
      } catch {
        throw new Error("Arquivo JSON inválido.");
      }

      const items = Array.isArray(parsed)
        ? parsed
        : (parsed as { policies?: unknown } | null)?.policies;
      if (!Array.isArray(items))
        throw new Error('Formato inválido: esperado { "policies": [...] }.');

      const policiesToImport = items.map((item) => {
        const record = item as { dataType?: unknown; days?: unknown } | null;
        if (typeof record?.dataType !== "string" || typeof record?.days !== "number")
          throw new Error("Item de importação inválido: informe dataType e days.");
        return { dataType: record.dataType, days: record.days };
      });

      const count = await importRetentionPolicies(policiesToImport);
      setImported(count);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao importar políticas.");
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
        imported != null
          ? `${imported} ${imported === 1 ? "política importada" : "políticas importadas"}.`
          : saved
            ? "Política atualizada."
            : "Quantos dias o cleanup mantém cada tipo de dado antes de apagar."
      }
      error={error}
      onRetry={() => void load()}
      actions={
        <>
          <Button
            variant="ghost"
            icon="pi pi-download"
            title="Exportar políticas (JSON)"
            aria-label="Exportar políticas (JSON)"
            disabled={loading}
            onClick={() => void exportPolicies()}
          />
          <Button
            variant="ghost"
            icon="pi pi-upload"
            title="Importar políticas (JSON)"
            aria-label="Importar políticas (JSON)"
            disabled={loading}
            onClick={() => fileInputRef.current?.click()}
          />
          <Button
            variant="ghost"
            icon="pi pi-refresh"
            title="Atualizar"
            aria-label="Atualizar"
            disabled={loading}
            onClick={() => void load()}
          />
          <input
            ref={fileInputRef}
            type="file"
            accept="application/json,.json"
            style={{ display: "none" }}
            aria-label="Arquivo de políticas JSON"
            onChange={(e) => {
              const file = e.target.files?.[0];
              // zera para permitir reimportar o mesmo arquivo em seguida
              e.target.value = "";
              if (file) void importPolicies(file);
            }}
          />
        </>
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
                      {MANAGED_TYPES.has(p.dataType) && (
                        <StatusChip
                          label="Gerenciada"
                          background="#f5f3ff"
                          color="#7c3aed"
                          style={{ marginLeft: 8 }}
                        />
                      )}
                    </td>
                    <td style={{ padding: "8px 12px 8px 0" }}>
                      {p.source === "Policy" ? (
                        <StatusChip label="Política" background="#eff6ff" color="#2563eb" />
                      ) : p.source === "BackupSettings" ? (
                        <StatusChip label="Backup" background="#fff7ed" color="#c2410c" />
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
                      {(p.source === "Policy" || p.source === "BackupSettings") && (
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

      {!loading && tables.length > 0 && (
        <div className="uni-panel" style={{ padding: 16, marginTop: 16 }}>
          <h3 style={{ margin: "0 0 12px", fontSize: 15, fontWeight: 600 }}>
            <i className="pi pi-database" style={{ marginRight: 6 }} />
            Retenção por tabela
          </h3>
          <p style={{ margin: "0 0 12px", fontSize: 12, color: "#6b7280" }}>
            Tabelas descobertas automaticamente do modelo. Atribua os dias e o
            cleanup poda linhas com CreateDateTime anterior ao corte — tabela
            nova já aparece aqui sem mudança de código.
          </p>
          <div style={{ overflowX: "auto", maxHeight: 420, overflowY: "auto" }}>
            <table
              style={{
                width: "100%",
                fontSize: 13,
                borderCollapse: "collapse",
              }}
            >
              <thead>
                <tr
                  style={{
                    borderBottom: "1px solid #e5e7eb",
                    textAlign: "left",
                    position: "sticky",
                    top: 0,
                    background: "#fff",
                  }}
                >
                  <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>
                    Tabela
                  </th>
                  <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>
                    Entidade
                  </th>
                  <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>
                    Dias
                  </th>
                  <th
                    style={{
                      padding: "8px 0 8px 8px",
                      fontWeight: 600,
                      textAlign: "right",
                    }}
                  >
                    Ações
                  </th>
                </tr>
              </thead>
              <tbody>
                {tables.map((t) => (
                  <tr
                    key={t.entityName}
                    style={{ borderBottom: "1px solid #f3f4f6" }}
                  >
                    <td style={{ padding: "8px 12px 8px 0" }}>{t.tableName}</td>
                    <td style={{ padding: "8px 12px 8px 0", color: "#6b7280" }}>
                      {t.entityName}
                      {t.policyDays == null && (
                        <StatusChip
                          label="Sem retenção"
                          background="#fef2f2"
                          color="#b91c1c"
                          style={{ marginLeft: 8 }}
                        />
                      )}
                    </td>
                    <td style={{ padding: "8px 12px 8px 0" }}>
                      <input
                        type="number"
                        value={tableDrafts[t.entityName] ?? ""}
                        onChange={(e) =>
                          setTableDrafts((d) => ({
                            ...d,
                            [t.entityName]:
                              e.target.value === "" ? null : Number(e.target.value),
                          }))
                        }
                        min={1}
                        max={3650}
                        placeholder="—"
                        style={inputStyle}
                        aria-label={`Dias de retenção — ${t.entityName}`}
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
                        title="Salvar dias de retenção"
                        aria-label={`Salvar retenção de ${t.entityName}`}
                        disabled={
                          tableDrafts[t.entityName] == null ||
                          saving === `table:${t.entityName}`
                        }
                        onClick={() => void saveTable(t.entityName)}
                      />
                      {t.policyDays != null && (
                        <Button
                          variant="ghost"
                          icon="pi pi-trash"
                          title="Remover retenção desta tabela"
                          aria-label={`Remover retenção de ${t.entityName}`}
                          disabled={saving === `table:${t.entityName}`}
                          onClick={() => void resetTable(t.entityName)}
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </ListPageShell>
  );
}
