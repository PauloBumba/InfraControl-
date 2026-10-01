import { useCallback, useEffect, useState } from "react";
import { useCan } from "@/app/router/access";
import { useAppSelector } from "@/app/store/hooks";
import {
  CHANNELS,
  CHANNEL_ICON,
  CHANNEL_LABEL,
  listPreferences,
  setPreference,
} from "@/services/api/notifications";

/**
 * F17-T010 — NotificationsPage conectada ao backend (substitui
 * `notifications`/`notificationPreferences` de `data/dashboardData`).
 *
 * A aba "Feed" do protótipo foi REMOVIDA: não existe listagem de
 * notificações no backend (só `GET {id}/recipients`, que exige um
 * notificationId inacessível sem uma lista — pendência se/F9 publicar a
 * listagem). Resta a aba Preferências: `GET /notifications/preferences?
 * userId=` (Read) + `PUT` (Operate, upsert) sobre os 6 canais do enum
 * `NotificationChannel`. `channel` viaja NUMÉRICO (enum sem
 * JsonStringEnumConverter no MVC).
 *
 * Semântica real (docs/07): **default habilitado — desligar = sem
 * delivery**; linha ausente = canal ativo, então o estado inicial de cada
 * toggle é `true` e as linhas gravadas sobrescrevem. Coluna "Eventos" do
 * protótipo saiu (não existe no `NotificationPreferenceDto`).
 */

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function NotificationsPage() {
  const user = useAppSelector((state) => state.auth.user);
  const userId = user?.id;
  // Escrita (PUT) é Operate no backend; sem nível o toggle vira chip fixo.
  const canOperate = useCan("operate");

  /** Estado por canal — default `true` (docs/07), sobrescrito pelas linhas. */
  const [enabledByChannel, setEnabledByChannel] = useState<Record<number, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyChannel, setBusyChannel] = useState<number | null>(null);

  const load = useCallback(async () => {
    if (!userId) return;
    setLoading(true);
    setError(null);
    try {
      const defaults: Record<number, boolean> = {};
      CHANNELS.forEach((channel) => {
        defaults[channel] = true;
      });
      const rows = await listPreferences(userId);
      rows.forEach((row) => {
        defaults[row.channel] = row.enabled;
      });
      setEnabledByChannel(defaults);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function toggle(channel: number) {
    if (!userId) return;
    const next = !enabledByChannel[channel];
    setBusyChannel(channel);
    setError(null);
    try {
      await setPreference(userId, channel, next);
      setEnabledByChannel((current) => ({ ...current, [channel]: next }));
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setBusyChannel(null);
    }
  }

  const activeCount = CHANNELS.filter((channel) => enabledByChannel[channel]).length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div>
        <h1 style={{ fontSize: 20, fontWeight: 700 }}>Notificações</h1>
        <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
          {loading
            ? "Carregando…"
            : `${activeCount} de ${CHANNELS.length} canais ativos · padrão: habilitado`}
        </p>
      </div>

      {error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
          <button onClick={() => void load()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
            Tentar de novo
          </button>
        </div>
      )}

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Preferências de entrega</h2>
          <span style={{ fontSize: 11, color: "#94a3b8" }}>
            Desligar um canal = não receber por ele
          </span>
        </div>
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th>Canal</th>
              <th style={{ width: 120 }}>Ativo</th>
            </tr>
          </thead>
          <tbody>
            {CHANNELS.map((channel) => {
              const enabled = enabledByChannel[channel] ?? true;
              const busy = busyChannel === channel;
              return (
                <tr key={channel}>
                  <td>
                    <div style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: 500, fontSize: 13 }}>
                      <i className={CHANNEL_ICON[channel]} style={{ color: "#3b82f6", fontSize: 14 }} />
                      {CHANNEL_LABEL[channel]}
                    </div>
                  </td>
                  <td>
                    {loading && enabledByChannel[channel] === undefined ? (
                      <span style={{ fontSize: 11, color: "#94a3b8" }}>…</span>
                    ) : canOperate ? (
                      <div
                        role="switch"
                        aria-checked={enabled}
                        aria-label={`${CHANNEL_LABEL[channel]}: ${enabled ? "ativo" : "inativo"}`}
                        title={busy ? "Salvando…" : enabled ? "Clique para desligar" : "Clique para ligar"}
                        onClick={() => { if (!busy) void toggle(channel); }}
                        className={`uni-toggle-switch ${enabled ? "on" : ""}`}
                        style={{ cursor: busy ? "default" : "pointer", opacity: busy ? 0.6 : 1 }}
                      />
                    ) : (
                      <span
                        style={{
                          fontSize: 11, padding: "2px 8px", borderRadius: 4,
                          background: enabled ? "#f0fdf4" : "#f1f5f9",
                          color: enabled ? "#15803d" : "#64748b",
                        }}
                      >
                        {enabled ? "Ativo" : "Desativado"}
                      </span>
                    )}
                  </td>
                </tr>
              );
            })}
            {!loading && Object.keys(enabledByChannel).length === 0 && (
              <tr>
                <td colSpan={2} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  {error ? "—" : "Nenhuma preferência registrada (todos os canais no padrão)"}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
