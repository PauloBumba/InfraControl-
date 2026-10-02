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
  type NotificationFeedItem,
} from "@/services/api/notifications";

/**
 * F17-T010 + pendência da F18-T001 — NotificationsPage com as duas abas
 * do protótipo sobre contratos reais.
 *
 * Aba Feed (restaurada — a F17-T010 a tinha removido por não existir
 * listagem): `GET /notifications` paginado (F18-T001,
 * `NotificationFeedItemDto` = `{id, eventType, title, message,
 * createdAt}`), destinatário direto OU de role, mais recentes primeiro.
 * **Sem estado de leitura**: `Notification` não tem flag de lida, então
 * não há "não lidas" nem ação de marcar (decisão registrada da T001 — o
 * `read`/`status`/`recipient` do protótipo não têm contrato e saíram;
 * `eventType` aparece cru, é string livre e não enum).
 *
 * Aba Preferências (F17-T010): `GET /notifications/preferences?userId=`
 * (Read) + `PUT` (Operate, upsert) sobre os 6 canais do enum
 * `NotificationChannel`. `channel` viaja NUMÉRICO (enum sem
 * JsonStringEnumConverter no MVC). Semântica real (docs/07): **default
 * habilitado — desligar = sem delivery**; linha ausente = canal ativo,
 * então o estado inicial de cada toggle é `true` e as linhas gravadas
 * sobrescrevem. Coluna "Eventos" do protótipo saiu (não existe no
 * `NotificationPreferenceDto`).
 */

const PAGE_SIZE = 20;

type Tab = "feed" | "prefs";

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

function ErrorBanner({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
      <i className="pi pi-exclamation-circle" />
      <span>{message}</span>
      <button onClick={onRetry} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
        Tentar de novo
      </button>
    </div>
  );
}

export function NotificationsPage() {
  const user = useAppSelector((state) => state.auth.user);
  const userId = user?.id;
  // Escrita (PUT) é Operate no backend; sem nível o toggle vira chip fixo.
  const canOperate = useCan("operate");

  const [tab, setTab] = useState<Tab>("feed");

  // Aba Feed — só leitura, sem mutations (F18-T001 não tem estado de leitura).
  const [feedItems, setFeedItems] = useState<NotificationFeedItem[]>([]);
  const [feedTotal, setFeedTotal] = useState(0);
  const [feedPage, setFeedPage] = useState(1);
  const [feedLoading, setFeedLoading] = useState(true);
  const [feedError, setFeedError] = useState<string | null>(null);

  // Aba Preferências — estado por canal — default `true` (docs/07),
  // sobrescrito pelas linhas gravadas.
  const [enabledByChannel, setEnabledByChannel] = useState<Record<number, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyChannel, setBusyChannel] = useState<number | null>(null);

  const loadFeed = useCallback(async () => {
    setFeedLoading(true);
    setFeedError(null);
    try {
      const result = await listFeed(feedPage, PAGE_SIZE);
      setFeedItems(result.items);
      setFeedTotal(result.totalCount);
    } catch (caught) {
      setFeedError(messageFor(caught));
    } finally {
      setFeedLoading(false);
    }
  }, [feedPage]);

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
    void loadFeed();
  }, [loadFeed]);

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
  const feedTotalPages = Math.max(1, Math.ceil(feedTotal / PAGE_SIZE));

  const subtitle =
    tab === "feed"
      ? feedLoading
        ? "Carregando…"
        : `${feedTotal} ${feedTotal === 1 ? "notificação" : "notificações"}`
      : loading
        ? "Carregando…"
        : `${activeCount} de ${CHANNELS.length} canais ativos · padrão: habilitado`;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Notificações</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>{subtitle}</p>
        </div>
        <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
          {([
            { id: "feed", label: "Feed" },
            { id: "prefs", label: "Preferências" },
          ] as const).map((entry) => (
            <button
              key={entry.id}
              onClick={() => setTab(entry.id)}
              style={{
                padding: "8px 14px",
                fontSize: 13,
                fontWeight: 500,
                color: tab === entry.id ? "#3b82f6" : "#64748b",
                background: "none",
                border: "none",
                borderBottom: tab === entry.id ? "2px solid #3b82f6" : "2px solid transparent",
                marginBottom: -1,
                cursor: "pointer",
              }}
            >
              {entry.label}
            </button>
          ))}
        </div>
      </div>

      {tab === "feed" && (
        <>
          {feedError && <ErrorBanner message={feedError} onRetry={() => void loadFeed()} />}
          <div className="uni-panel">
            <div className="uni-alerts-list">
              {feedItems.map((item) => (
                <div className="uni-alert-row" key={item.id}>
                  <div className="uni-alert-icon info">
                    <i className="pi pi-bell" />
                  </div>
                  <div className="uni-alert-body">
                    <div className="uni-alert-title">{item.title}</div>
                    <div className="uni-alert-detail">{item.message}</div>
                  </div>
                  <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4 }}>
                    <span
                      style={{
                        fontSize: 11,
                        fontWeight: 500,
                        padding: "2px 8px",
                        borderRadius: 4,
                        background: "#f1f5f9",
                        color: "#64748b",
                        maxWidth: 220,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                      }}
                      title={item.eventType}
                    >
                      {item.eventType}
                    </span>
                    <span style={{ fontSize: 10, color: "#94a3b8" }}>
                      {formatDateTime(new Date(item.createdAt))}
                    </span>
                  </div>
                </div>
              ))}
              {!feedLoading && feedItems.length === 0 && (
                <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  {feedError ? "—" : "Nenhuma notificação"}
                </div>
              )}
              {feedLoading && feedItems.length === 0 && (
                <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  Carregando…
                </div>
              )}
            </div>
          </div>

          {feedTotal > PAGE_SIZE && (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 10, fontSize: 12, color: "#64748b" }}>
              <span>Página {feedPage} de {feedTotalPages}</span>
              <button
                onClick={() => setFeedPage((value) => Math.max(1, value - 1))}
                disabled={feedPage <= 1 || feedLoading}
                style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: feedPage <= 1 ? "default" : "pointer", opacity: feedPage <= 1 ? 0.5 : 1 }}
              >
                Anterior
              </button>
              <button
                onClick={() => setFeedPage((value) => Math.min(feedTotalPages, value + 1))}
                disabled={feedPage >= feedTotalPages || feedLoading}
                style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: feedPage >= feedTotalPages ? "default" : "pointer", opacity: feedPage >= feedTotalPages ? 0.5 : 1 }}
              >
                Próxima
              </button>
            </div>
          )}
        </>
      )}

      {tab === "prefs" && (
        <>
          {error && <ErrorBanner message={error} onRetry={() => void load()} />}
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
        </>
      )}
    </div>
  );
}
