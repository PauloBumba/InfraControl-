import { useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { SelectField } from "@/components/ui/SelectField";
import { SwitchField } from "@/components/ui/SwitchField";

/**
 * Settings ainda é protótipo (sem backend de preferências). Os toggles usam
 * estado local via `SwitchField` (InputSwitch) — visual UniFi, sem persistir.
 * Select de fuso via `SelectField`. TabView fica para Fase G opcional.
 */

const settingsTabs = [
  { id: "general", label: "Geral", icon: "pi pi-cog" },
  { id: "network", label: "Rede", icon: "pi pi-share-alt" },
  { id: "wifi", label: "WiFi", icon: "pi pi-wifi" },
  { id: "security", label: "Segurança", icon: "pi pi-shield" },
  { id: "notifications", label: "Notificações", icon: "pi pi-bell" },
  { id: "system", label: "Sistema", icon: "pi pi-cog" },
];

const TIMEZONE_OPTIONS = [
  { value: "America/Sao_Paulo", label: "América/São Paulo (GMT-3)" },
  { value: "America/New_York", label: "América/New York (GMT-5)" },
];

type ToggleKey =
  | "darkMode"
  | "autoUpdate"
  | "telemetry"
  | "igmp"
  | "stp"
  | "dhcpSnooping"
  | "wifiBandSteering"
  | "wifiFastRoaming"
  | "wifiGuestIsolation"
  | "secMfaPrompt"
  | "secSessionLock"
  | "secAuditVerbose"
  | "notifEmail"
  | "notifPush"
  | "notifWebhook";

const INITIAL_TOGGLES: Record<ToggleKey, boolean> = {
  darkMode: true,
  autoUpdate: true,
  telemetry: false,
  igmp: true,
  stp: true,
  dhcpSnooping: true,
  wifiBandSteering: true,
  wifiFastRoaming: true,
  wifiGuestIsolation: true,
  secMfaPrompt: true,
  secSessionLock: true,
  secAuditVerbose: true,
  notifEmail: true,
  notifPush: true,
  notifWebhook: false,
};

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
      <SwitchField
        checked={checked}
        onChange={onChange}
        aria-label={title}
      />
    </div>
  );
}

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState("general");
  const [timezone, setTimezone] = useState("America/Sao_Paulo");
  const [toggles, setToggles] = useState(INITIAL_TOGGLES);

  function setToggle(key: ToggleKey, value: boolean) {
    setToggles((current) => ({ ...current, [key]: value }));
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader title="Configurações" />

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
            <>
              <div className="uni-settings-section">
                <h3>Informações do Site</h3>
                <div className="uni-settings-field">
                  <label>Nome do Site</label>
                  <input type="text" defaultValue="Operações TI" />
                </div>
                <div className="uni-settings-field">
                  <label>Descrição</label>
                  <input type="text" defaultValue="Infraestrutura corporativa principal" />
                </div>
                <div className="uni-settings-field">
                  <label>Fuso Horário</label>
                  <SelectField
                    value={timezone}
                    onChange={setTimezone}
                    options={TIMEZONE_OPTIONS}
                    style={{ width: "100%" }}
                  />
                </div>
              </div>
              <div className="uni-settings-section">
                <h3>Preferências</h3>
                <ToggleRow
                  title="Modo Escuro"
                  description="Usar tema escuro na interface"
                  checked={toggles.darkMode}
                  onChange={(v) => setToggle("darkMode", v)}
                />
                <ToggleRow
                  title="Atualização Automática"
                  description="Atualizar dispositivos automaticamente"
                  checked={toggles.autoUpdate}
                  onChange={(v) => setToggle("autoUpdate", v)}
                />
                <ToggleRow
                  title="Telemetria"
                  description="Enviar dados de uso para melhorias"
                  checked={toggles.telemetry}
                  onChange={(v) => setToggle("telemetry", v)}
                />
              </div>
            </>
          )}

          {activeTab === "network" && (
            <div className="uni-settings-section">
              <h3>Configurações de Rede</h3>
              <div className="uni-settings-field">
                <label>WAN IP</label>
                <input type="text" defaultValue="192.168.1.1" />
              </div>
              <div className="uni-settings-field">
                <label>Máscara de Rede</label>
                <input type="text" defaultValue="255.255.255.0" />
              </div>
              <div className="uni-settings-field">
                <label>Gateway</label>
                <input type="text" defaultValue="192.168.1.1" />
              </div>
              <div className="uni-settings-field">
                <label>DNS Primário</label>
                <input type="text" defaultValue="8.8.8.8" />
              </div>
              <ToggleRow
                title="IGMP Snooping"
                description="Otimizar tráfego multicast"
                checked={toggles.igmp}
                onChange={(v) => setToggle("igmp", v)}
              />
              <ToggleRow
                title="Spanning Tree"
                description="Prevenir loops na rede"
                checked={toggles.stp}
                onChange={(v) => setToggle("stp", v)}
              />
              <ToggleRow
                title="DHCP Snooping"
                description="Filtrar DHCP não autorizado"
                checked={toggles.dhcpSnooping}
                onChange={(v) => setToggle("dhcpSnooping", v)}
              />
            </div>
          )}

          {activeTab === "wifi" && (
            <div className="uni-settings-section">
              <h3>WiFi</h3>
              <ToggleRow
                title="Band Steering"
                description="Direcionar clientes para 5 GHz"
                checked={toggles.wifiBandSteering}
                onChange={(v) => setToggle("wifiBandSteering", v)}
              />
              <ToggleRow
                title="Fast Roaming"
                description="802.11r entre APs"
                checked={toggles.wifiFastRoaming}
                onChange={(v) => setToggle("wifiFastRoaming", v)}
              />
              <ToggleRow
                title="Isolamento de Convidados"
                description="Isolar rede guest da LAN"
                checked={toggles.wifiGuestIsolation}
                onChange={(v) => setToggle("wifiGuestIsolation", v)}
              />
            </div>
          )}

          {activeTab === "security" && (
            <div className="uni-settings-section">
              <h3>Segurança</h3>
              <ToggleRow
                title="Prompt MFA"
                description="Exigir segundo fator no login"
                checked={toggles.secMfaPrompt}
                onChange={(v) => setToggle("secMfaPrompt", v)}
              />
              <ToggleRow
                title="Bloqueio de Sessão"
                description="Encerrar sessões ociosas"
                checked={toggles.secSessionLock}
                onChange={(v) => setToggle("secSessionLock", v)}
              />
              <ToggleRow
                title="Auditoria Verbose"
                description="Registrar mais detalhes de eventos"
                checked={toggles.secAuditVerbose}
                onChange={(v) => setToggle("secAuditVerbose", v)}
              />
            </div>
          )}

          {activeTab === "notifications" && (
            <div className="uni-settings-section">
              <h3>Notificações</h3>
              <ToggleRow
                title="E-mail"
                description="Alertas por e-mail"
                checked={toggles.notifEmail}
                onChange={(v) => setToggle("notifEmail", v)}
              />
              <ToggleRow
                title="Push"
                description="Notificações push no dashboard"
                checked={toggles.notifPush}
                onChange={(v) => setToggle("notifPush", v)}
              />
              <ToggleRow
                title="Webhook"
                description="Entrega via webhook externo"
                checked={toggles.notifWebhook}
                onChange={(v) => setToggle("notifWebhook", v)}
              />
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
    </div>
  );
}
