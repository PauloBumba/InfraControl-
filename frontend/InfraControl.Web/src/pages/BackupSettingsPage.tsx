import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { SwitchField } from "@/components/ui/SwitchField";
import {
  getBackupSettings,
  updateBackupSettings,
  type BackupSettings,
} from "@/services/api/accessPolicies";

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: "100%",
  boxSizing: "border-box" as const,
};

/**
 * Configuração de backup editável pela plataforma: override do singleton
 * `backup_settings` sobre `Backup:*` do appsettings. O BackupWorker relê a
 * cada ciclo — não precisa reiniciar container.
 *
 * Layout no padrão da casa: PageHeader + uni-panel + FormField/SwitchField.
 */
export function BackupSettingsPage() {
  const [form, setForm] = useState<BackupSettings | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setForm(await getBackupSettings());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar configurações.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    if (!form) return;
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      setForm(await updateBackupSettings(form));
      setSaved(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao salvar configurações.");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <PageHeader title="Backup do banco de dados" />
        <LoadingBlock />
      </div>
    );
  }

  if (!form) {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <PageHeader title="Backup do banco de dados" />
        <ErrorBanner
          message={error ?? "Configurações indisponíveis."}
          onRetry={() => void load()}
        />
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 720 }}>
      <PageHeader
        title="Backup do banco de dados"
        subtitle="Estas configurações sobrepõem o appsettings.json do worker. O worker relê a cada ciclo — alterações valem sem restart."
      />

      {saved && (
        <NoticeBanner
          message="Configurações salvas."
          onDismiss={() => setSaved(false)}
        />
      )}
      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div className="uni-panel" style={{ padding: 16 }}>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
            gap: 12,
          }}
        >
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              gridColumn: "1 / -1",
            }}
          >
            <SwitchField
              checked={form.enabled}
              onChange={(checked) => setForm({ ...form, enabled: checked })}
              aria-label="Backup habilitado"
            />
            <span style={{ fontSize: 13, fontWeight: 500 }}>Backup habilitado</span>
          </div>

          <FormField label="Intervalo entre backups (horas)">
            <input
              type="number"
              value={form.intervalHours}
              onChange={(e) =>
                setForm({ ...form, intervalHours: Number(e.target.value) || 24 })
              }
              min={1}
              max={720}
              style={inputStyle}
            />
          </FormField>
          <FormField label="Retenção de arquivos (dias)">
            <input
              type="number"
              value={form.retentionDays}
              onChange={(e) =>
                setForm({ ...form, retentionDays: Number(e.target.value) || 7 })
              }
              min={1}
              max={3650}
              style={inputStyle}
            />
          </FormField>
          <FormField label="Diretório de destino">
            <input
              value={form.path}
              onChange={(e) => setForm({ ...form, path: e.target.value })}
              style={inputStyle}
              autoComplete="off"
            />
          </FormField>
          <FormField label="Prefixo do arquivo">
            <input
              value={form.fileNamePrefix}
              onChange={(e) =>
                setForm({ ...form, fileNamePrefix: e.target.value })
              }
              style={inputStyle}
              autoComplete="off"
            />
          </FormField>
          <FormField label="Timeout do dump (segundos)">
            <input
              type="number"
              value={form.timeoutSeconds}
              onChange={(e) =>
                setForm({ ...form, timeoutSeconds: Number(e.target.value) || 3600 })
              }
              min={60}
              max={86400}
              style={inputStyle}
            />
          </FormField>
        </div>

        <div style={{ marginTop: 16 }}>
          <Button icon="pi pi-save" disabled={saving} onClick={() => void save()}>
            {saving ? "Salvando…" : "Salvar"}
          </Button>
        </div>
      </div>

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Observações</div>
        <ul
          style={{
            fontSize: 13,
            color: "#6b7280",
            margin: 0,
            paddingLeft: 18,
            display: "grid",
            gap: 4,
            listStyle: "disc",
          }}
        >
          <li>Connection string e alvos adicionais continuam no appsettings do worker.</li>
          <li>Desabilitar aqui pausa o backup no próximo ciclo, sem parar o container.</li>
          <li>A retenção de dados da aplicação é gerenciada em Operação → Retenção.</li>
        </ul>
      </div>
    </div>
  );
}
