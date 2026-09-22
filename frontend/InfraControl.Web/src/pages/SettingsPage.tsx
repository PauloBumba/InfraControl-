import { useState } from "react";
import { InputText } from "primereact/inputtext";
import { Dropdown } from "primereact/dropdown";
import { Button } from "primereact/button";
import { InputSwitch } from "primereact/inputswitch";
import { TabView, TabPanel } from "primereact/tabview";

export function SettingsPage() {
  const [siteName, setSiteName] = useState("InfraControl - Operações TI");
  const [timezone, setTimezone] = useState("America/Luanda");
  const [language, setLanguage] = useState("pt-BR");
  const [darkMode, setDarkMode] = useState(true);
  const [autoBackup, setAutoBackup] = useState(true);
  const [emailNotifications, setEmailNotifications] = useState(true);
  const [snmpEnabled, setSnmpEnabled] = useState(false);

  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">CONFIGURAÇÕES</p>
          <h1>Settings</h1>
          <p className="heading-subtitle">Platform configuration</p>
        </div>
        <Button label="Save Changes" icon="pi pi-save" />
      </section>

      <div className="panel">
        <TabView>
          <TabPanel header="General">
            <div className="settings-form">
              <div className="settings-section">
                <h3>Site Information</h3>
                <div className="settings-grid">
                  <div className="settings-field">
                    <label>Site Name</label>
                    <InputText value={siteName} onChange={(e) => setSiteName(e.target.value)} />
                  </div>
                  <div className="settings-field">
                    <label>Timezone</label>
                    <Dropdown value={timezone} onChange={(e) => setTimezone(e.target.value)} options={[
                      { label: "Africa/Luanda (WAT)", value: "America/Luanda" },
                      { label: "UTC", value: "UTC" },
                      { label: "America/New_York (EST)", value: "America/New_York" },
                      { label: "Europe/London (GMT)", value: "Europe/London" },
                    ]} />
                  </div>
                  <div className="settings-field">
                    <label>Language</label>
                    <Dropdown value={language} onChange={(e) => setLanguage(e.target.value)} options={[
                      { label: "Português (BR)", value: "pt-BR" },
                      { label: "English", value: "en-US" },
                    ]} />
                  </div>
                </div>
              </div>

              <div className="settings-section">
                <h3>Appearance</h3>
                <div className="settings-toggle">
                  <div className="settings-toggle-info">
                    <strong>Dark Mode</strong>
                    <span>Use dark theme for the interface</span>
                  </div>
                  <InputSwitch checked={darkMode} onChange={(e) => setDarkMode(e.value ?? true)} />
                </div>
              </div>
            </div>
          </TabPanel>

          <TabPanel header="Network">
            <div className="settings-form">
              <div className="settings-section">
                <h3>DNS Settings</h3>
                <div className="settings-grid">
                  <div className="settings-field">
                    <label>Primary DNS</label>
                    <InputText defaultValue="8.8.8.8" />
                  </div>
                  <div className="settings-field">
                    <label>Secondary DNS</label>
                    <InputText defaultValue="8.8.4.4" />
                  </div>
                </div>
              </div>

              <div className="settings-section">
                <h3>DHCP</h3>
                <div className="settings-grid">
                  <div className="settings-field">
                    <label>DHCP Start</label>
                    <InputText defaultValue="192.168.1.100" />
                  </div>
                  <div className="settings-field">
                    <label>DHCP End</label>
                    <InputText defaultValue="192.168.1.254" />
                  </div>
                  <div className="settings-field">
                    <label>Lease Time</label>
                    <InputText defaultValue="86400" />
                  </div>
                </div>
              </div>
            </div>
          </TabPanel>

          <TabPanel header="Notifications">
            <div className="settings-form">
              <div className="settings-section">
                <h3>Alert Settings</h3>
                <div className="settings-toggle">
                  <div className="settings-toggle-info">
                    <strong>Email Notifications</strong>
                    <span>Receive alerts via email</span>
                  </div>
                  <InputSwitch checked={emailNotifications} onChange={(e) => setEmailNotifications(e.value ?? true)} />
                </div>
                <div className="settings-toggle">
                  <div className="settings-toggle-info">
                    <strong>SNMP Traps</strong>
                    <span>Enable SNMP trap receiver</span>
                  </div>
                  <InputSwitch checked={snmpEnabled} onChange={(e) => setSnmpEnabled(e.value ?? true)} />
                </div>
              </div>
            </div>
          </TabPanel>

          <TabPanel header="System">
            <div className="settings-form">
              <div className="settings-section">
                <h3>Backup</h3>
                <div className="settings-toggle">
                  <div className="settings-toggle-info">
                    <strong>Automatic Backup</strong>
                    <span>Daily backup at 03:00 AM</span>
                  </div>
                  <InputSwitch checked={autoBackup} onChange={(e) => setAutoBackup(e.value ?? true)} />
                </div>
                <div style={{ marginTop: "var(--space-3)", display: "flex", gap: "var(--space-2)" }}>
                  <Button label="Backup Now" icon="pi pi-download" severity="secondary" />
                  <Button label="Restore Backup" icon="pi pi-upload" severity="secondary" />
                </div>
              </div>

              <div className="settings-section">
                <h3>Danger Zone</h3>
                <Button label="Factory Reset" icon="pi pi-trash" severity="danger" />
              </div>
            </div>
          </TabPanel>
        </TabView>
      </div>
    </div>
  );
}
