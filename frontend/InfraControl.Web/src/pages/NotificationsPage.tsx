import { useCallback, useEffect, useMemo, useState } from "react";
import { Column } from "primereact/column";
import { useCan } from "@/app/router/access";
import { useAppSelector } from "@/app/store/hooks";
import { formatDateTime } from "@/utils/format";
import { Pagination } from "@/components/ui/Pagination";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { SwitchField } from "@/components/ui/SwitchField";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { InlineMessages, useInlineMessages } from "@/components/ui/InlineMessages";
import { Timeline } from "primereact/timeline";
import {
  CHANNELS,
  CHANNEL_ICON,
  CHANNEL_LABEL,
  listFeed,
  listPreferences,
  setPreference,
  type NotificationFeedItem,
} from "@/services/api/notifications";
import { onNotificationPush } from "@/services/signalr/notifications";
import { errorMessage } from "@/hooks/queries";

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

export function NotificationsPage() {
  const { messagesRef, showMessage } = useInlineMessages();
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

  // Adoção do padrão de visões (backlog Operations UX 2026-10-11). O feed
  // não é tabela — é um Timeline, então o 1º modo vira "Linha do tempo"
  // (o Timeline vive no slot `table` do EntityView). Só a aba Feed ganha
  // seletor: Preferências é tabela fixa de config, não índice.
  const [feedView, setFeedView] = useEntityViewMode("notifications-feed", "table");

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
      setFeedError(errorMessage(caught));
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
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    void loadFeed();
  }, [loadFeed]);

  // Entrega 3 (docs/29 backlog): push SignalR → insere o evento no topo
  // do feed (página 1, dedup por notificationId) e soma o total. O feed
  // persistido continua fonte de histórico; fora da página 1 o próximo
  // load já traz a nova notificação na posição certa.
  useEffect(() => {
    return onNotificationPush((event) => {
      setFeedTotal((current) => current + 1);
      if (feedPage !== 1 || feedLoading) return;
      setFeedItems((current) => {
        if (current.some((item) => item.id === event.notificationId)) return current;
        return [
          {
            id: event.notificationId,
            eventType: event.eventType,
            title: event.title,
            message: event.message,
            createdAt: event.createdAt,
          },
          ...current,
        ];
      });
    });
  }, [feedPage, feedLoading]);

  useEffect(() => {
    void load();
  }, [load]);

  async function toggle(channel: number) {
    if (!userId) return;
    // ADR-059 §3 (M4): o valor EFETIVO é o exibido (`?? true` quando o GET
    // falhou) — antes `!undefined = true` reativava o canal quando o
    // usuário clicava para DESLIGAR com o mapa vazio.
    const current = enabledByChannel[channel] ?? true;
    const next = !current;
    setBusyChannel(channel);
    setError(null);
    try {
      await setPreference(userId, channel, next);
      setEnabledByChannel((current) => ({ ...current, [channel]: next }));
      showMessage({
        severity: "success",
        summary: `${CHANNEL_LABEL[channel]} ${next ? "ativado" : "desativado"}.`,
      });
    } catch (caught) {
      showMessage({ severity: "error", summary: "Não foi possível salvar a preferência.", detail: errorMessage(caught) });
    } finally {
      setBusyChannel(null);
    }
  }

  const activeCount = CHANNELS.filter((channel) => enabledByChannel[channel]).length;
  const feedTotalPages = Math.max(1, Math.ceil(feedTotal / PAGE_SIZE));
  const preferenceRows = CHANNELS.map((channel) => ({ channel }));

  // `eventType` é string livre, não enum (decisão registrada da T001), então
  // as colunas do quadro saem dos dados carregados — por frequência, depois
  // alfabética, para o ordenamento ser estável.
  const feedEventTypeColumns = useMemo(() => {
    const counts = new Map<string, number>();
    feedItems.forEach((item) =>
      counts.set(item.eventType, (counts.get(item.eventType) ?? 0) + 1)
    );
    return [...counts.entries()]
      .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
      .map(([key]) => ({ key, label: key }));
  }, [feedItems]);

  /** Card de notificação — usado nas visões grid E quadro. */
  function renderFeedCard(item: NotificationFeedItem) {
    return (
      <div
        style={{
          background: "white",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
          padding: 12,
          display: "flex",
          flexDirection: "column",
          gap: 4,
        }}
      >
        <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 8 }}>
          <span style={{ fontSize: 13, fontWeight: 600 }}>{item.title}</span>
          <StatusChip
            label={item.eventType}
            background="#eff6ff"
            color="#1d4ed8"
            fontSize={10}
            fontWeight={600}
          />
        </div>
        <div style={{ fontSize: 12, color: "#64748b" }}>{item.message}</div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>
          {formatDateTime(new Date(item.createdAt))}
        </div>
      </div>
    );
  }

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
      <PageHeader
        title="Notificações"
        subtitle={subtitle}
        actions={
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
        }
      />

      <InlineMessages ref={messagesRef} />

      {tab === "feed" && (
        <>
          {feedError && <ErrorBanner message={feedError} onRetry={() => void loadFeed()} />}
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Feed</h2>
              <ViewSwitcher
                mode={feedView}
                onChange={setFeedView}
                labels={{ table: "Linha do tempo" }}
              />
            </div>
            <EntityView<NotificationFeedItem>
              mode={feedView}
              items={feedItems}
              getKey={(item) => item.id}
              emptyMessage={feedError ? "—" : "Nenhuma notificação"}
              kanban={{ groupOf: (item) => item.eventType, columns: feedEventTypeColumns }}
              renderCard={renderFeedCard}
              table={
            <div className="uni-timeline-wrap">
              <Timeline value={feedItems} align="left" className="uni-feed-timeline" marker={() => <span className="uni-feed-marker"><i className="pi pi-bell" /></span>} content={(item: NotificationFeedItem) => <div className="uni-feed-content"><div className="uni-alert-title">{item.title}</div><div className="uni-alert-detail">{item.message}</div><span title={item.eventType}>{item.eventType}</span></div>} opposite={(item: NotificationFeedItem) => <span className="uni-feed-time">{formatDateTime(new Date(item.createdAt))}</span>} />
              {!feedLoading && feedItems.length === 0 && (
                <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  {feedError ? "—" : "Nenhuma notificação"}
                </div>
              )}
              {feedLoading && feedItems.length === 0 && <LoadingBlock />}
            </div>
              }
            />
          </div>

          {feedTotal > PAGE_SIZE && (
            <Pagination
              page={feedPage}
              totalPages={feedTotalPages}
              disabled={feedLoading}
              onPage={setFeedPage}
            />
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
            <DataTablePanel panel={false} value={preferenceRows} dataKey="channel" loading={loading} emptyMessage={error ? "—" : "Nenhuma preferência registrada (todos os canais no padrão)"}>
              <Column header="Canal" body={(row: { channel: number }) => <div style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: 500, fontSize: 13 }}><i className={CHANNEL_ICON[row.channel]} style={{ color: "#3b82f6", fontSize: 14 }} />{CHANNEL_LABEL[row.channel]}</div>} />
              <Column header="Ativo" body={(row: { channel: number }) => { const channel = row.channel; const enabled = enabledByChannel[channel] ?? true; const busy = busyChannel === channel; if (loading && enabledByChannel[channel] === undefined) return <span style={{ fontSize: 11, color: "#94a3b8" }}>…</span>; return canOperate ? <SwitchField checked={enabled} disabled={busy} aria-label={`${CHANNEL_LABEL[channel]}: ${enabled ? "ativo" : "inativo"}`} title={busy ? "Salvando…" : enabled ? "Clique para desligar" : "Clique para ligar"} onChange={() => { if (!busy) void toggle(channel); }} /> : <span style={{ fontSize: 11, padding: "2px 8px", borderRadius: 4, background: enabled ? "#f0fdf4" : "#f1f5f9", color: enabled ? "#15803d" : "#64748b" }}>{enabled ? "Ativo" : "Desativado"}</span>; }} style={{ width: 120 }} />
            </DataTablePanel>
          </div>
        </>
      )}
    </div>
  );
}
