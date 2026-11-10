import { useCallback, useEffect, useState } from "react";
import { Button } from "primereact/button";
import { Card } from "primereact/card";
import { InputNumber } from "primereact/inputnumber";
import { InputText } from "primereact/inputtext";
import { InputSwitch } from "primereact/inputswitch";
import { Message } from "primereact/message";
import { ProgressSpinner } from "primereact/progressspinner";
import {
  getBackupSettings,
  updateBackupSettings,
  type BackupSettings,
} from "@/services/api/accessPolicies";

/**
 * Configuração de backup editável pela plataforma: override do singleton
 * `backup_settings` sobre `Backup:*` do appsettings. O BackupWorker relê a
 * cada ciclo — não precisa reiniciar container.
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
      <div className="flex justify-center py-16">
        <ProgressSpinner />
      </div>
    );
  }

  if (!form) {
    return (
      <div className="p-4 md:p-6">
        <Message severity="error" text={error ?? "Configurações indisponíveis."} />
      </div>
    );
  }

  const field = (label: string, control: React.ReactNode) => (
    <div className="flex flex-col gap-1">
      <label className="text-sm font-medium">{label}</label>
      {control}
    </div>
  );

  return (
    <div className="p-4 md:p-6 space-y-4 max-w-3xl">
      <div>
        <h1 className="text-2xl font-semibold">Backup do banco de dados</h1>
        <p className="text-sm text-gray-500">
          Estas configurações sobrepõem o <code>appsettings.json</code> do
          worker. O worker relê a cada ciclo — alterações valem sem restart.
        </p>
      </div>

      <Card>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="flex items-center gap-3 md:col-span-2">
            <InputSwitch
              checked={form.enabled}
              onChange={(e) => setForm({ ...form, enabled: e.value })}
            />
            <label className="text-sm font-medium">
              Backup habilitado
            </label>
          </div>

          {field(
                        "Intervalo entre backups (horas)",
            <InputNumber
              value={form.intervalHours}
              onValueChange={(e) =>
                setForm({ ...form, intervalHours: e.value ?? 24 })
              }
              min={1}
              max={720}
              className="w-full"
            />,
          )}
          {field(
                        "Retenção de arquivos (dias)",
            <InputNumber
              value={form.retentionDays}
              onValueChange={(e) =>
                setForm({ ...form, retentionDays: e.value ?? 7 })
              }
              min={1}
              max={3650}
              className="w-full"
            />,
          )}
          {field(
                        "Diretório de destino",
            <InputText
              value={form.path}
              onChange={(e) => setForm({ ...form, path: e.target.value })}
              className="w-full"
            />,
          )}
          {field(
                        "Prefixo do arquivo",
            <InputText
              value={form.fileNamePrefix}
              onChange={(e) =>
                setForm({ ...form, fileNamePrefix: e.target.value })
              }
              className="w-full"
            />,
          )}
          {field(
                        "Timeout do dump (segundos)",
            <InputNumber
              value={form.timeoutSeconds}
              onValueChange={(e) =>
                setForm({ ...form, timeoutSeconds: e.value ?? 3600 })
              }
              min={60}
              max={86400}
              className="w-full"
            />,
          )}
        </div>

        <div className="flex items-center gap-3 mt-6">
          {saved && <Message severity="success" text="Configurações salvas." />}
          {error && <Message severity="error" text={error} />}
          <Button
            label="Salvar"
            icon="pi pi-save"
            loading={saving}
            onClick={() => void save()}
          />
        </div>
      </Card>

      <Card title="Observações">
        <ul className="text-sm text-gray-600 space-y-1 list-disc list-inside">
          <li>Connection string e alvos adicionais continuam no appsettings do worker.</li>
          <li>Desabilitar aqui pausa o backup no próximo ciclo, sem parar o container.</li>
          <li>A retenção de dados da aplicação é gerenciada em Operação → Retenção.</li>
        </ul>
      </Card>
    </div>
  );
}
