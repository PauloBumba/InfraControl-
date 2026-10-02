import { useCallback, useEffect, useState } from "react";
import { useCan } from "@/app/router/access";
import { useAppSelector } from "@/app/store/hooks";
import { formatDateTime } from "@/utils/format";
import {
  CHANNELS,
  CHANNEL_ICON,
  CHANNEL_LABEL,
  listFeed,
  listPreferences,
  setPreference,
  type PagedFeed,
} from "@/services/api/notifications";

/**
 * F17-T010 — NotificationsPage conectada ao backend (substitui
 * `notifications`/`notificationPreferences` de `data/dashboardData`).
 *
 * Aba **Feed restaurada** (pendência pós F18-T001): `GET /notifications`
 * devolve as notificações do usuário (destinatário direto OU por role,
 * mais recentes primeiro) — sem estado de leitura, decisão da própria
 * F18-T001 (a entidade não tem flag de lida). Aba **Preferências**:
 * `GET /notifications/preferences?userId=` (Read) + `PUT` (Operate,
 * upsert) sobre os 6 canais do enum `NotificationChannel`. `channel`
 * viaja NUMÉRICO (enum sem JsonStringEnumConverter no MVC).
 *
 * Semântica real (docs/07): **default habilitado — desligar = sem
 * delivery**; linha ausente = canal ativo, então o estado inicial de cada
 * toggle é `true` e as linhas gravadas sobrescrevem. Coluna "Eventos" do
 * protótipo saiu (não existe no `NotificationPreferenceDto`).
 */

const FEED_PAGE_SIZE = 20;

type TabId = "feed" | "preferences";

const tabs: { id: TabId; label: string }[] = [
  { id: "feed", label: "Feed" },
  { id: "preferences", label: "Preferências" },
];

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

  const [activeTab, setActiveTab] = useState<TabId>("feed");

  /** Feed (F18-T001) — estado próprio para não misturar com Preferências. */
  const [feed, setFeed] = useState<PagedFeed | null>(null);
  const [feedLoading, setFeedLoading] = useState(false);
  const [feedError, setFeedError] = useState<string | null>(null);
  const [feedPage, setFeedPage] = useState(1);

  /** Estado por canal — default `true` (docs/07), sobrescrito pelas linhas. */
  const [enabledByChannel, setEnabledByChannel] = useState<Record<number, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyChannel, setBusyChannel] = useState<number | null>(null);

  const loadFeed = useCallback(async () => {
    setFeedLoading(true);
    setFeedError(null);
    try {
      setFeed(await listFeed(feedPage, FEED_PAGE_SIZE));
    } catch (caught) {
      setFeedError(messageFor(caught));
    } finally {
      setFeedLoading(false);
    }
  }, [feedPage]);

  useEffect(() => {
    if (activeTab === "feed") void loadFeed();
  }, [activeTab, loadFeed]);

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
  const feedItems = feed?.items ?? [];
  const feedTotal = feed?.totalCount ?? 0;
  const feedTotalPages = Math.max(1, Math.ceil(feedTotal / FEED_PAGE_SIZE));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div>
        <h1 style={{ fontSize: 20, fontWeight: 700 }}>Notificações</h1>
        <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
          {activeTab === "feed"
            ? feedLoading && !feed
              ? "Carregando…"
              : `${feedTotal} ${feedTotal === 1 ? "notificação" : "notificações"} para você`
            : loading
              ? "Carregando…"
              : `${activeCount} de ${CHANNELS.length} canais ativos · padrão: habilitado`}
        </p>
      </div>

      <div style={{ display: "flex", gap: 6 }}>
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              padding: "6px 14px", borderRadius: 6, fontSize: 12, fontWeight: 500,
              border: "1px solid #e5e7eb",
              background: activeTab === tab.id ? "white" : "transparent",
              color: activeTab === tab.id ? "#1a1a1a" : "#6b7280",
              boxShadow: activeTab === tab.id ? "0 1px 2px rgba(0,0,0,0.06)" : "none",
              cursor: "pointer",
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === "preferences" && error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
          <button onClick={() => void load()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
            Tentar de novo
          </button>
        </div>
      )}

      {activeTab === "feed" && (
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Notificações recebidas</h2>
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              Destinatário direto ou do seu papel · mais recentes primeiro
            </span>
          </div>

          {feedError && (
            <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c", marginBottom: 8 }}>
              <i className="pi pi-exclamation-circle" />
              <span>{feedError}</span>
              <button onClick={() => void loadFeed()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
                Tentar de novo
              </button>
            </div>
          )}

          {feedLoading && !feed && (
            <div style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>Carregando…</div>
          )}

          {!feedLoading && !feedError && feedTotal === 0 && (
            <div style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
              Nenhuma notificação ainda — elas aparecem aqui quando eventos
              (incidentes, convites, SLA…) gerarem entrega para você.
            </div>
          )}

          {feedItems.length > 0 && (
            <table className="uni-clients-table">
              <thead>
                <tr>
                  <th>Notificação</th>
                  <th>Mensagem</th>
                  <th style={{ width: 160 }}>Recebida em</th>
                </tr>
              </thead>
              <tbody>
                {feedItems.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <div style={{ fontWeight: 500, fontSize: 13 }}>{item.title}</div>
                      <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: "#f1f5f9", color: "#475569", display: "inline-block", marginTop: 4 }}>
                        {item.eventType}
                      </span>
                    </td>
                    <td style={{ fontSize: 12, color: "#6b7280" }}>{item.message}</td>
                    <td style={{ fontSize: 12, color: "#6b7280" }}>
                      {formatDateTime(new Date(item.createdAt))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {feedTotal > FEED_PAGE_SIZE && (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 8, marginTop: 10, fontSize: 12, color: "#6b7280" }}>
              <span>Página {feedPage} de {feedTotalPages}</span>
              <button
                onClick={() => setFeedPage((value) => Math.max(1, value - 1))}
                disabled={feedPage <= 1 || feedLoading}
                style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e5e7eb", background: "white", fontSize: 12, color: "#374151", cursor: feedPage <= 1 ? "default" : "pointer", opacity: feedPage <= 1 ? 0.5 : 1 }}
              >
                Anterior
              </button>
              <button
                onClick={() => setFeedPage((value) => value + 1)}
                disabled={feedPage >= feedTotalPages || feedLoading}
                style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e5e7eb", background: "white", fontSize: 12, color: "#374151", cursor: feedPage >= feedTotalPages ? "default" : "pointer", opacity: feedPage >= feedTotalPages ? 0.5 : 1 }}
              >
                Próxima
              </button>
            </div>
          )}
        </div>
      )}

      {activeTab === "preferences" && (
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
      )}
    </div>
  );
}
