import { useState } from "react";
import { notifications, notificationPreferences } from "@/data/dashboardData";

const channelIcons: Record<string, string> = {
  Email: "pi pi-envelope",
  Telegram: "pi pi-send",
  WhatsApp: "pi pi-comment",
  Webhook: "pi pi-link",
  SignalR: "pi pi-bolt",
};

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  sent: { label: "Enviada", bg: "#dcfce7", color: "#16a34a" },
  queued: { label: "Na fila", bg: "#fef3c7", color: "#d97706" },
  failed: { label: "Falhou", bg: "#fee2e2", color: "#dc2626" },
};

export function NotificationsPage() {
  const [tab, setTab] = useState<"feed" | "prefs">("feed");

  const unread = notifications.filter((n) => !n.read).length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Notificações</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {notifications.length} total · {unread} não lidas
          </p>
        </div>
        <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
          {([
            { id: "feed", label: "Feed" },
            { id: "prefs", label: "Preferências" },
          ] as const).map((t) => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              style={{
                padding: "8px 14px",
                fontSize: 13,
                fontWeight: 500,
                color: tab === t.id ? "#3b82f6" : "#64748b",
                background: "none",
                border: "none",
                borderBottom: tab === t.id ? "2px solid #3b82f6" : "2px solid transparent",
                marginBottom: -1,
                cursor: "pointer",
              }}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      {tab === "feed" && (
        <div className="uni-panel">
          <div className="uni-alerts-list">
            {notifications.map((n) => {
              const st = statusConfig[n.status] ?? { label: n.status, bg: "#f1f5f9", color: "#64748b" };
              return (
                <div className="uni-alert-row" key={n.id} style={{ opacity: n.read ? 0.65 : 1 }}>
                  <div className="uni-alert-icon info">
                    <i className={channelIcons[n.channel] ?? "pi pi-bell"} />
                  </div>
                  <div className="uni-alert-body">
                    <div className="uni-alert-title">
                      {n.title}
                      {!n.read && (
                        <span style={{ display: "inline-block", width: 7, height: 7, borderRadius: "50%", background: "#3b82f6", marginLeft: 8 }} />
                      )}
                    </div>
                    <div className="uni-alert-detail">{n.body} · {n.recipient}</div>
                  </div>
                  <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4 }}>
                    <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4, background: st.bg, color: st.color }}>
                      {st.label}
                    </span>
                    <span style={{ fontSize: 11, color: "#94a3b8" }}>{n.channel} · {n.sentAt}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {tab === "prefs" && (
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Canal</th>
                <th style={{ width: 100 }}>Ativo</th>
                <th>Eventos</th>
              </tr>
            </thead>
            <tbody>
              {notificationPreferences.map((p) => (
                <tr key={p.channel}>
                  <td>
                    <div style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: 500, fontSize: 13 }}>
                      <i className={channelIcons[p.channel] ?? "pi pi-bell"} style={{ color: "#3b82f6", fontSize: 14 }} />
                      {p.channel}
                    </div>
                  </td>
                  <td>
                    <div className={`uni-toggle-switch ${p.enabled ? "on" : ""}`} />
                  </td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{p.events}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
