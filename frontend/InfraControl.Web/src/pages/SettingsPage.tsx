import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { PageHeader } from "@/components/ui/PageHeader";
import { SelectField } from "@/components/ui/SelectField";
import { SwitchField } from "@/components/ui/SwitchField";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import {
  getAccountSettings,
  updateAccountSettings,
} from "@/services/api/settings";

/**
 * Fase 06d — Settings religada ao backend. Escopo honesto: só preferências
 * PESSOAS do usuário (timezone, tema, auto-refresh, telemetria) persistem em
 * `GET/PUT /api/v1/account/settings`. Configurações de controller (UniFi/rede)
 * e políticas de segurança não são preferências de usuário — as abas foram
 * removidas. Notificações já têm página/endpoint próprios em /notifications.
 */

const TIMEZONE_OPTIONS = [
  { value: "America/Sao_Paulo", label: "América/São Paulo (GMT-3)" },
  { value: "America/New_York", label: "América/New York (GMT-5)" },
];

type TabId = "general" | "notifications" | "system";

const settingsTabs: { id: TabId; label: string; icon: string }[] = [
  { id: "general", label: "Geral", icon: "pi pi-cog" },
  { id: "notifications", label: "Notificações", icon: "pi pi-bell" },
  { id: "system", label: "Sistema", icon: "pi pi-info-circle" },
];

function ToggleRow({
  title,
  description,
  checked,
  onChange,
}: {
  title: string;
  description: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <div className="uni-toggle-row">
      <div className="uni-toggle-info">
        <strong>{title}</strong>
        <span>{description}</span>
      </div>
      <SwitchField checked={checked} onChange={onChange} aria-label={title} />
    </div>
  );
}

export function SettingsPage() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<TabId>("general");

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [timezone, setTimezone] = useState("America/Sao_Paulo");
  const [darkMode, setDarkMode] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [telemetry, setTelemetry] = useState(false);

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getAccountSettings()
      .then((data) => {
        if (cancelled) return;
        setTimezone(data.timezone);
        setDarkMode(data.darkMode);
        setAutoRefresh(data.autoRefresh);
        setTelemetry(data.telemetry);
        setLoading(false);
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        setLoadError(
          error instanceof Error
            ? error.message
            : "Não foi possível carregar as preferências.",
        );
        setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setSaved(false);
    setSaveError(null);
    try {
      await updateAccountSettings({
        timezone,
        darkMode,
        autoRefresh,
        telemetry,
      });
      setSaved(true);
    } catch (error) {
      setSaveError(
        error instanceof Error
          ? error.message
          : "Não foi possível salvar as preferências agora.",
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader title="Configurações" />

      {loading && <LoadingBlock />}

      {loadError && (
        <ErrorBanner message={loadError} onRetry={() => window.location.reload()} />
      )}

      {!loading && !loadError && (
        <div className="uni-settings-grid">
          <div className="uni-settings-nav">
            {settingsTabs.map((tab) => (
              <button
                key={tab.id}
                className={`uni-settings-nav-item ${activeTab === tab.id ? "active" : ""}`}
                onClick={() => setActiveTab(tab.id)}
              >
                <i className={tab.icon} />
                {tab.label}
              </button>
            ))}
          </div>

          <div className="uni-settings-panel">
            {activeTab === "general" && (
              <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
                {saved && (
                  <NoticeBanner message="Preferências salvas." onDismiss={() => setSaved(false)} />
                )}
                {saveError && <ErrorBanner message={saveError} />}

                <div className="uni-settings-section">
                  <h3>Preferências</h3>
                  <div className="uni-settings-field">
                    <label>Fuso Horário</label>
                    <SelectField
                      aria-label="Fuso Horário"
                      value={timezone}
                      onChange={setTimezone}
                      options={TIMEZONE_OPTIONS}
                      style={{ width: "100%" }}
                    />
                  </div>
                  <ToggleRow
                    title="Modo Escuro"
                    description="Usar tema escuro na interface"
                    checked={darkMode}
                    onChange={setDarkMode}
                  />
                  <ToggleRow
                    title="Atualização Automática"
                    description="Atualizar dados do dashboard automaticamente"
                    checked={autoRefresh}
                    onChange={setAutoRefresh}
                  />
                  <ToggleRow
                    title="Telemetria"
                    description="Enviar dados de uso para melhorias"
                    checked={telemetry}
                    onChange={setTelemetry}
                  />
                </div>

                <div>
                  <button
                    type="submit"
                    className="p-button p-component"
                    disabled={saving}
                  >
                    <i className={`pi ${saving ? "pi-spin pi-spinner" : "pi-save"}`} />
                    <span>{saving ? "Salvando…" : "Salvar preferências"}</span>
                  </button>
                </div>
              </form>
            )}

            {activeTab === "notifications" && (
              <div className="uni-settings-section">
                <h3>Notificações</h3>
                <p style={{ fontSize: 13, color: "#6b7280", margin: "0 0 12px" }}>
                  As preferências de entrega (SignalR, e-mail, Telegram, WhatsApp,
                  webhook, Slack) são gerenciadas na página de Notificações.
                </p>
                <button
                  type="button"
                  className="p-button p-component p-button-outlined"
                  onClick={() => navigate("/notifications")}
                >
                  <i className="pi pi-bell" />
                  <span>Abrir Notificações</span>
                </button>
              </div>
            )}

            {activeTab === "system" && (
              <div className="uni-settings-section">
                <h3>Sistema</h3>
                <div className="uni-settings-field">
                  <label>Versão</label>
                  <input type="text" defaultValue="InfraControl 0.1.0" readOnly />
                </div>
                <div className="uni-settings-field">
                  <label>Ambiente</label>
                  <input type="text" defaultValue="Desenvolvimento" readOnly />
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
