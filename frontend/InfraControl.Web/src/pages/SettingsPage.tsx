import { useState } from "react";

const settingsTabs = [
  { id: "general", label: "Geral", icon: "pi pi-cog" },
  { id: "network", label: "Rede", icon: "pi pi-share-alt" },
  { id: "wifi", label: "WiFi", icon: "pi pi-wifi" },
  { id: "security", label: "Segurança", icon: "pi pi-shield" },
  { id: "notifications", label: "Notificações", icon: "pi pi-bell" },
  { id: "system", label: "Sistema", icon: "pi pi-cog" },
];

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState("general");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <h1 style={{ fontSize: 20, fontWeight: 700 }}>Configurações</h1>

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
                  <select defaultValue="America/Sao_Paulo">
                    <option value="America/Sao_Paulo">América/São Paulo (GMT-3)</option>
                    <option value="America/New_York">América/New York (GMT-5)</option>
                  </select>
                </div>
              </div>
              <div className="uni-settings-section">
                <h3>Preferências</h3>
                <div className="uni-toggle-row">
                  <div className="uni-toggle-info">
                    <strong>Modo Escuro</strong>
                    <span>Usar tema escuro na interface</span>
                  </div>
                  <div className="uni-toggle-switch on" />
                </div>
                <div className="uni-toggle-row">
                  <div className="uni-toggle-info">
                    <strong>Atualização Automática</strong>
                    <span>Atualizar dispositivos automaticamente</span>
                  </div>
                  <div className="uni-toggle-switch on" />
                </div>
                <div className="uni-toggle-row">
                  <div className="uni-toggle-info">
                    <strong>Telemetria</strong>
                    <span>Enviar dados de uso para melhorias</span>
                  </div>
                  <div className="uni-toggle-switch" />
                </div>
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
              <div className="uni-settings-field">
                <label>DNS Secundário</label>
                <input type="text" defaultValue="8.8.4.4" />
              </div>
            </div>
          )}

          {activeTab === "wifi" && (
            <div className="uni-settings-section">
              <h3>Redes WiFi</h3>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>Corporativo</strong>
                  <span>SSID: Corp-5G · Banda: 5 GHz · Canal: 36</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>Convidados</strong>
                  <span>SSID: Guest-WiFi · Banda: 2.4 GHz + 5 GHz</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>IoT</strong>
                  <span>SSID: IoT-Devices · Banda: 2.4 GHz</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
            </div>
          )}

          {activeTab === "security" && (
            <div className="uni-settings-section">
              <h3>Segurança</h3>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>IDS/IPS</strong>
                  <span>Intrusion Detection/Prevention System</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>Firewall</strong>
                  <span>Habilitar firewall no gateway</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>Deep Packet Inspection</strong>
                  <span>Inspeção profunda de pacotes</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
            </div>
          )}

          {activeTab === "notifications" && (
            <div className="uni-settings-section">
              <h3>Notificações</h3>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>Email</strong>
                  <span>Receber alertas por email</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>Push Mobile</strong>
                  <span>Receber notificações push no app</span>
                </div>
                <div className="uni-toggle-switch on" />
              </div>
              <div className="uni-toggle-row">
                <div className="uni-toggle-info">
                  <strong>Slack</strong>
                  <span>Enviar alertas para canal Slack</span>
                </div>
                <div className="uni-toggle-switch" />
              </div>
            </div>
          )}

          {activeTab === "system" && (
            <div className="uni-settings-section">
              <h3>Sistema</h3>
              <div className="uni-settings-field">
                <label>Versão do Firmware</label>
                <input type="text" defaultValue="4.1.13" disabled />
              </div>
              <div className="uni-settings-field">
                <label>Último Backup</label>
                <input type="text" defaultValue="14/09/2026 03:00" disabled />
              </div>
              <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
                <button style={{ padding: "8px 16px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 13, fontWeight: 500 }}>
                  <i className="pi pi-refresh" style={{ marginRight: 6 }} /> Verificar Atualizações
                </button>
                <button style={{ padding: "8px 16px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}>
                  <i className="pi pi-download" style={{ marginRight: 6 }} /> Backup Agora
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
