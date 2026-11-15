import { useCallback, useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
import { ConfirmDialog, Dialog } from "@/components/ui/Dialog";
import {
  getMessagingBindings,
  getMessagingConnections,
  getMessagingExchanges,
  getMessagingFlow,
  getMessagingMessage,
  getMessagingOverview,
  getMessagingQueueDetail,
  getMessagingQueues,
  getMessagingWorkers,
  pauseQueueConsumption,
  replayNotificationDelivery,
  resumeQueueConsumption,
  searchMessagingDlq,
  searchMessagingMessages,
  searchMessagingRetries,
  type MessagingDeliveryItem,
  type MessagingFlowResult,
  type MessagingMessageDetail,
  type MessagingMessageItem,
  type MessagingOverview,
  type MessagingWorkersResult,
  type Paged,
  type RabbitMqBindingSummary,
  type RabbitMqConnectionSummary,
  type RabbitMqExchangeSummary,
  type RabbitMqQueueDetail,
  type RabbitMqQueueSummary,
} from "@/services/api/messaging";
import { listAuditEntries, type AuditEntry } from "@/services/api/audit";
import { useAppSelector } from "@/app/store/hooks";
import { formatDateTime } from "@/utils/format";

const fmt = (value: string | null | undefined) =>
  value ? formatDateTime(new Date(value)) : "—";

/**
 * F55 — Messaging Operations Center.
 * Uma rota `/messaging` com abas; cada aba busca seu endpoint real na API.
 * Broker fora → banner "indisponível" com o motivo, nunca lista vazia fake.
 */

const TABS = [
  "Overview", "Queues", "Workers", "Messages", "Retry", "DLQ",
  "Flow", "Exchanges", "Connections", "Audit",
] as const;
type Tab = (typeof TABS)[number];

const card: React.CSSProperties = {
  background: "#fff", border: "1px solid #e2e8f0", borderRadius: 10,
  padding: "14px 16px", marginBottom: 12,
};
const th: React.CSSProperties = {
  textAlign: "left", fontSize: 11, color: "#64748b", fontWeight: 600,
  padding: "6px 8px", borderBottom: "1px solid #e2e8f0", whiteSpace: "nowrap",
};
const td: React.CSSProperties = {
  fontSize: 12, padding: "6px 8px", borderBottom: "1px solid #f1f5f9",
  verticalAlign: "top",
};

function Unavailable({ reason }: { reason?: string | null }) {
  return (
    <div style={{ ...card, borderColor: "#f59e0b", background: "#fffbeb" }}>
      <strong>Broker indisponível</strong>
      {reason ? <div style={{ fontSize: 12, color: "#92400e", marginTop: 4 }}>{reason}</div> : null}
    </div>
  );
}

function StateChip({ state }: { state: string | null | undefined }) {
  const s = (state ?? "unknown").toLowerCase();
  const map: Record<string, [string, string]> = {
    running: ["#dcfce7", "#166534"], ok: ["#dcfce7", "#166534"],
    sent: ["#dcfce7", "#166534"], processed: ["#dcfce7", "#166534"],
    paused: ["#fef3c7", "#92400e"], degraded: ["#fef3c7", "#92400e"],
    failed: ["#fee2e2", "#991b1b"], exhausted: ["#fee2e2", "#991b1b"],
    unavailable: ["#fee2e2", "#991b1b"], pending: ["#e0e7ff", "#3730a3"],
    processing: ["#e0e7ff", "#3730a3"],
  };
  const [bg, fg] = map[s] ?? ["#f1f5f9", "#475569"];
  return <StatusChip label={state ?? "—"} background={bg} color={fg} />;
}

function Kpi({ label, value, hint }: { label: string; value: React.ReactNode; hint?: string }) {
  return (
    <div style={{ ...card, marginBottom: 0, minWidth: 140, flex: "1 1 140px" }}>
      <div style={{ fontSize: 11, color: "#64748b" }}>{label}</div>
      <div style={{ fontSize: 20, fontWeight: 700, marginTop: 2 }}>{value}</div>
      {hint && <div style={{ fontSize: 10, color: "#94a3b8" }}>{hint}</div>}
    </div>
  );
}

function Pager({ page, total, pageSize, onPage }: {
  page: number; total: number; pageSize: number; onPage: (p: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  return (
    <div className="flex align-items-center gap-2" style={{ marginTop: 8 }}>
      <Button variant="ghost" disabled={page <= 1} onClick={() => onPage(page - 1)}>← Anterior</Button>
      <span style={{ fontSize: 12, color: "#64748b" }}>{page} / {pages} · {total} itens</span>
      <Button variant="ghost" disabled={page >= pages} onClick={() => onPage(page + 1)}>Próxima →</Button>
    </div>
  );
}

// ---------------------------------------------------------------- Overview

function OverviewTab() {
  const [data, setData] = useState<MessagingOverview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try { setData(await getMessagingOverview()); }
    catch (e) { setError(e instanceof Error ? e.message : "Falha ao carregar."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); const t = setInterval(() => void load(), 15000); return () => clearInterval(t); }, [load]);

  if (loading && !data) return <LoadingBlock />;
  if (error) return <ErrorBanner message={error} />;
  if (!data) return null;

  const b = data.broker; const a = data.app;
  const brokerBad = !b.reachable;
  return (
    <div>
      {brokerBad && <Unavailable reason={b.unavailableReason} />}
      <div className="flex flex-wrap gap-2" style={{ marginBottom: 12 }}>
        <Kpi label="Conexão RabbitMQ" value={b.reachable ? "Online" : "Offline"}
          hint={b.reachable ? b.connectionState ?? undefined : b.unavailableReason ?? undefined} />
        <Kpi label="Exchanges" value={b.exchanges ?? "—"} hint="fonte: broker" />
        <Kpi label="Queues" value={b.queues ?? "—"} hint="fonte: broker" />
        <Kpi label="Consumers" value={b.consumers ?? "—"} hint="fonte: broker" />
        <Kpi label="Ready" value={b.messagesReady ?? "—"} hint="mensagens na fila" />
        <Kpi label="Unacked" value={b.messagesUnacked ?? "—"} hint="em processamento" />
      </div>
      <div className="flex flex-wrap gap-2" style={{ marginBottom: 12 }}>
        <Kpi label="Outbox pendentes" value={a.outboxPending} hint="fonte: banco" />
        <Kpi label="Outbox esgotados" value={a.outboxExhausted} hint="retry esgotado" />
        <Kpi label="Outbox processados" value={a.outboxProcessedTotal} />
        <Kpi label="Latência outbox" value={a.avgOutboxLatencySeconds != null ? `${a.avgOutboxLatencySeconds.toFixed(2)}s` : "—"} />
        <Kpi label="Inbox processados" value={a.inboxProcessedTotal} />
      </div>
      <div className="flex flex-wrap gap-2">
        <Kpi label="Deliveries pendentes" value={a.deliveriesPending} />
        <Kpi label="Processando" value={a.deliveriesProcessing} />
        <Kpi label="Enviadas" value={a.deliveriesSent} />
        <Kpi label="Falhas" value={a.deliveriesFailed} />
        <Kpi label="Retry devido" value={a.deliveriesRetryDue} />
        <Kpi label="DLQ" value={a.deliveriesDlq} />
        <Kpi label="Suprimidas" value={a.deliveriesSuppressed} />
        <Kpi label="Publish/s" value={b.publishRatePerSecond?.toFixed(1) ?? "—"} hint="broker" />
        <Kpi label="Deliver/s" value={b.deliverRatePerSecond?.toFixed(1) ?? "—"} hint="broker" />
      </div>
    </div>
  );
}

// ------------------------------------------------------------------ Queues

function QueuesTab({ onPause, onResume, canAdminister }: {
  onPause: (q: string) => void;
  onResume: (q: string) => void;
  canAdminister: boolean;
}) {
  const [data, setData] = useState<{ reachable: boolean; unavailableReason: string | null; items: RabbitMqQueueSummary[] } | null>(null);
  const [detail, setDetail] = useState<RabbitMqQueueDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try { setData(await getMessagingQueues()); }
    catch (e) { setError(e instanceof Error ? e.message : "Falha."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); const t = setInterval(() => void load(), 15000); return () => clearInterval(t); }, [load]);

  const openDetail = async (name: string) => {
    try { setDetail(await getMessagingQueueDetail(name)); }
    catch { setDetail(null); }
  };

  if (loading && !data) return <LoadingBlock />;
  if (error) return <ErrorBanner message={error} />;
  if (!data) return null;
  if (!data.reachable) return <Unavailable reason={data.unavailableReason} />;

  return (
    <div style={{ display: "flex", gap: 12, alignItems: "flex-start" }}>
      <div style={{ flex: 1, overflowX: "auto" }}>
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead><tr>
            {["Fila", "Ready", "Unacked", "Total", "Consumers", "Estado"].map(h => <th key={h} style={th}>{h}</th>)}
            <th style={th}></th>
          </tr></thead>
          <tbody>
            {data.items.map(q => (
              <tr key={q.name}>
                <td style={td}><strong>{q.name}</strong><div style={{ fontSize: 10, color: "#94a3b8" }}>{q.vhost}</div></td>
                <td style={td}>{q.messagesReady}</td>
                <td style={td}>{q.messagesUnacked}</td>
                <td style={td}>{q.messages}</td>
                <td style={td}>{q.consumers}</td>
                <td style={td}><StateChip state={q.state} /></td>
                <td style={td}>
                  <div className="flex gap-1">
                    <Button variant="ghost" onClick={() => void openDetail(q.name)}>Detalhe</Button>
                    {canAdminister && (q.consumers > 0
                      ? <Button variant="warning" onClick={() => onPause(q.name)}>Pausar</Button>
                      : <Button variant="info" onClick={() => onResume(q.name)}>Retomar</Button>)}
                  </div>
                </td>
              </tr>
            ))}
            {data.items.length === 0 && <tr><td style={td} colSpan={7}>Nenhuma fila no broker.</td></tr>}
          </tbody>
        </table>
      </div>
      {detail && (
        <div style={{ ...card, width: 320, flexShrink: 0 }}>
          <div className="flex justify-content-between align-items-center">
            <strong>{detail.name}</strong>
            <Button variant="ghost" onClick={() => setDetail(null)}>✕</Button>
          </div>
          <dl style={{ fontSize: 12, marginTop: 8 }}>
            <dt style={{ color: "#64748b" }}>Ready / Unacked / Total</dt>
            <dd>{detail.messagesReady} / {detail.messagesUnacked} / {detail.messages}</dd>
            <dt style={{ color: "#64748b" }}>Publish/s · Deliver/s · Ack/s</dt>
            <dd>{detail.publishRatePerSecond?.toFixed(2) ?? "—"} · {detail.deliverRatePerSecond?.toFixed(2) ?? "—"} · {detail.ackRatePerSecond?.toFixed(2) ?? "—"}</dd>
            <dt style={{ color: "#64748b" }}>Publicadas / Entregues (total)</dt>
            <dd>{detail.publishedTotal} / {detail.deliveredTotal}</dd>
            <dt style={{ color: "#64748b" }}>Consumers</dt>
            <dd>{detail.consumerDetails.length === 0 ? "nenhum" : detail.consumerDetails.map(c => (
              <div key={c.consumerTag}>{c.consumerTag} · prefetch {c.prefetchCount ?? "—"} · {c.active ? "ativo" : "inativo"}</div>
            ))}</dd>
          </dl>
        </div>
      )}
    </div>
  );
}

// ----------------------------------------------------------------- Workers

function WorkersTab({ refreshKey }: { refreshKey: number }) {
  const [data, setData] = useState<MessagingWorkersResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try { setData(await getMessagingWorkers()); }
    catch (e) { setError(e instanceof Error ? e.message : "Falha."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load, refreshKey]);

  if (loading && !data) return <LoadingBlock />;
  if (error) return <ErrorBanner message={error} />;
  if (!data) return null;
  if (!data.brokerReachable && !data.dockerReachable)
    return <Unavailable reason={data.brokerUnavailableReason} />;

  return (
    <div>
      {!data.brokerReachable && <Unavailable reason={data.brokerUnavailableReason} />}
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr>
          {["Instância", "Tipo", "Fila", "Consumers", "Estado", "CPU", "Memória", "Peer"].map(h => <th key={h} style={th}>{h}</th>)}
        </tr></thead>
        <tbody>
          {data.workers.map(w => (
            <tr key={w.id}>
              <td style={td}><strong>{w.name}</strong>{w.detail && <div style={{ fontSize: 10, color: "#94a3b8" }}>{w.detail}</div>}</td>
              <td style={td}>{w.kind}</td>
              <td style={td}>{w.consumedQueue ?? "—"}</td>
              <td style={td}>{w.consumerCount || "—"}</td>
              <td style={td}><StateChip state={w.state} /></td>
              <td style={td}>{w.cpuPercent != null ? `${w.cpuPercent.toFixed(1)}%` : "—"}</td>
              <td style={td}>{w.memoryUsageBytes != null ? `${(w.memoryUsageBytes / 1048576).toFixed(0)} MB` : "—"}</td>
              <td style={td}>{w.peerHost ?? "—"}</td>
            </tr>
          ))}
          {data.workers.length === 0 && <tr><td style={td} colSpan={8}>Nenhum worker/consumer encontrado.</td></tr>}
        </tbody>
      </table>
    </div>
  );
}

// ---------------------------------------------------------------- Messages

function MessagesTab() {
  const [filters, setFilters] = useState({ text: "", correlationId: "", eventType: "", state: "" });
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Paged<MessagingMessageItem> | null>(null);
  const [detail, setDetail] = useState<MessagingMessageDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      setData(await searchMessagingMessages({
        text: filters.text || undefined,
        correlationId: filters.correlationId || undefined,
        eventType: filters.eventType || undefined,
        state: filters.state || undefined,
        page, pageSize: 20,
      }));
    } catch (e) { setError(e instanceof Error ? e.message : "Falha."); }
    finally { setLoading(false); }
  }, [filters, page]);
  useEffect(() => { void load(); }, [load]);

  const open = async (id: string) => {
    try { setDetail(await getMessagingMessage(id)); } catch { setDetail(null); }
  };

  return (
    <div>
      <div className="flex flex-wrap gap-2" style={{ marginBottom: 10 }}>
        <input placeholder="Texto / payload" value={filters.text}
          onChange={e => { setFilters(f => ({ ...f, text: e.target.value })); setPage(1); }}
          style={{ padding: "6px 8px", border: "1px solid #cbd5e1", borderRadius: 6, fontSize: 12 }} />
        <input placeholder="CorrelationId" value={filters.correlationId}
          onChange={e => { setFilters(f => ({ ...f, correlationId: e.target.value })); setPage(1); }}
          style={{ padding: "6px 8px", border: "1px solid #cbd5e1", borderRadius: 6, fontSize: 12 }} />
        <input placeholder="EventType" value={filters.eventType}
          onChange={e => { setFilters(f => ({ ...f, eventType: e.target.value })); setPage(1); }}
          style={{ padding: "6px 8px", border: "1px solid #cbd5e1", borderRadius: 6, fontSize: 12 }} />
        <select value={filters.state}
          onChange={e => { setFilters(f => ({ ...f, state: e.target.value })); setPage(1); }}
          style={{ padding: "6px 8px", border: "1px solid #cbd5e1", borderRadius: 6, fontSize: 12 }}>
          <option value="">Todos estados</option>
          <option value="pending">Pendente</option>
          <option value="processed">Processada</option>
          <option value="failed">Falha</option>
          <option value="exhausted">Esgotada</option>
        </select>
      </div>
      {error && <ErrorBanner message={error} />}
      {loading && !data ? <LoadingBlock /> : data && (
        <>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead><tr>
              {["Evento", "CorrelationId", "Ocorrida", "Estado", "Retries", "Erro"].map(h => <th key={h} style={th}>{h}</th>)}
              <th style={th}></th>
            </tr></thead>
            <tbody>
              {data.items.map(m => (
                <tr key={m.id}>
                  <td style={td}><strong>{m.eventType}</strong><div style={{ fontSize: 10, color: "#94a3b8", maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{m.payloadPreview}</div></td>
                  <td style={td}>{m.correlationId ?? "—"}</td>
                  <td style={td}>{fmt(m.occurredAt)}</td>
                  <td style={td}><StateChip state={m.state} /></td>
                  <td style={td}>{m.retryCount}/{m.maxRetries}</td>
                  <td style={td}>{m.error ? <span style={{ color: "#991b1b", fontSize: 11 }}>{m.error.slice(0, 80)}</span> : "—"}</td>
                  <td style={td}><Button variant="ghost" onClick={() => void open(m.id)}>Ver</Button></td>
                </tr>
              ))}
              {data.items.length === 0 && <tr><td style={td} colSpan={7}>Nenhuma mensagem encontrada.</td></tr>}
            </tbody>
          </table>
          <Pager page={data.page} total={data.totalCount} pageSize={data.pageSize} onPage={setPage} />
        </>
      )}
      {detail && (
        <Dialog visible onHide={() => setDetail(null)} header={`Mensagem ${detail.eventType}`} width={720}>
          <dl style={{ fontSize: 12, display: "grid", gridTemplateColumns: "140px 1fr", gap: "4px 12px" }}>
            <dt style={{ color: "#64748b" }}>ID</dt><dd style={{ wordBreak: "break-all" }}>{detail.id}</dd>
            <dt style={{ color: "#64748b" }}>CorrelationId</dt><dd>{detail.correlationId ?? "—"}</dd>
            <dt style={{ color: "#64748b" }}>Ocorrida</dt><dd>{fmt(detail.occurredAt)}</dd>
            <dt style={{ color: "#64748b" }}>Processada</dt><dd>{detail.processedAt ? fmt(detail.processedAt) : "—"}</dd>
            <dt style={{ color: "#64748b" }}>Duração</dt><dd>{detail.processingDurationSeconds != null ? `${detail.processingDurationSeconds.toFixed(2)}s` : "—"}</dd>
            <dt style={{ color: "#64748b" }}>Estado</dt><dd><StateChip state={detail.state} /></dd>
            <dt style={{ color: "#64748b" }}>Tentativas</dt><dd>{detail.retryCount}/{detail.maxRetries}</dd>
            {detail.error && <><dt style={{ color: "#991b1b" }}>Erro</dt><dd style={{ color: "#991b1b" }}>{detail.error}</dd></>}
          </dl>
          <h4 style={{ margin: "12px 0 6px", fontSize: 13 }}>Timeline</h4>
          <ul style={{ fontSize: 12, margin: 0, paddingLeft: 18 }}>
            {detail.timeline.map((t, i) => (
              <li key={i}>{t.atUtc ? fmt(t.atUtc) : "—"} — {t.stage}{t.detail ? ` (${t.detail})` : ""}</li>
            ))}
          </ul>
          <h4 style={{ margin: "12px 0 6px", fontSize: 13 }}>Payload (sanitizado)</h4>
          <pre style={{ fontSize: 11, background: "#f8fafc", padding: 10, borderRadius: 6, overflow: "auto", maxHeight: 260 }}>{detail.sanitizedPayload}</pre>
        </Dialog>
      )}
    </div>
  );
}

// ------------------------------------------------------- Retry / DLQ shared

function FailureTable({ kind, canAdminister }: { kind: "retries" | "dlq"; canAdminister: boolean }) {
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Paged<MessagingDeliveryItem> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      setData(kind === "retries"
        ? await searchMessagingRetries({ page, pageSize: 20 })
        : await searchMessagingDlq({ page, pageSize: 20 }));
    } catch (e) { setError(e instanceof Error ? e.message : "Falha."); }
    finally { setLoading(false); }
  }, [kind, page]);
  useEffect(() => { void load(); }, [load]);

  const replay = async (id: string) => {
    setNotice(null);
    try {
      const r = await replayNotificationDelivery(id);
      if (!r) { setNotice("Resposta vazia do servidor."); return; }
      setNotice(r.success ? "Delivery reenfileirada (Pending)." : `Falha: ${r.error}`);
      await load();
    } catch (e) { setNotice(e instanceof Error ? e.message : "Falha no replay."); }
  };

  if (loading && !data) return <LoadingBlock />;
  if (error) return <ErrorBanner message={error} />;
  if (!data) return null;

  return (
    <div>
      {notice && <div style={{ ...card, borderColor: "#0ea5e9", background: "#f0f9ff", fontSize: 12 }}>{notice}</div>}
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr>
          {["Canal", "Status", "Tentativas", kind === "retries" ? "Próxima tentativa" : "Último erro", "Criada"].map(h => <th key={h} style={th}>{h}</th>)}
          <th style={th}></th>
        </tr></thead>
        <tbody>
          {data.items.map(d => (
            <tr key={d.id}>
              <td style={td}>{d.channel}</td>
              <td style={td}><StateChip state={d.status} /></td>
              <td style={td}>{d.attemptCount}</td>
              <td style={td}>
                {kind === "retries"
                  ? (d.nextAttemptAt ? fmt(d.nextAttemptAt) : "—")
                  : (d.lastError ? <span style={{ color: "#991b1b", fontSize: 11 }}>{d.lastError.slice(0, 100)}</span> : "—")}
              </td>
              <td style={td}>{fmt(d.createdAt)}</td>
              <td style={td}>{canAdminister && <Button variant="warning" onClick={() => setConfirmId(d.id)}>Reprocessar</Button>}</td>
            </tr>
          ))}
          {data.items.length === 0 && <tr><td style={td} colSpan={6}>{kind === "retries" ? "Nenhum retry agendado." : "DLQ vazia."}</td></tr>}
        </tbody>
      </table>
      <Pager page={data.page} total={data.totalCount} pageSize={data.pageSize} onPage={setPage} />
      <ConfirmDialog
        visible={confirmId !== null}
        onHide={() => setConfirmId(null)}
        onConfirm={() => { if (confirmId) void replay(confirmId); }}
        header="Reprocessar delivery"
        severity="warning"
        confirmLabel="Reprocessar"
        message="Reenfileirar esta delivery para o canal? A mesma notificação será reenviada (efeito externo possível). O histórico de tentativas é preservado."
      />
    </div>
  );
}

// -------------------------------------------------------------------- Flow

function FlowTab() {
  const [data, setData] = useState<MessagingFlowResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [openStage, setOpenStage] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try { setData(await getMessagingFlow()); }
    catch (e) { setError(e instanceof Error ? e.message : "Falha."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); const t = setInterval(() => void load(), 15000); return () => clearInterval(t); }, [load]);

  if (loading && !data) return <LoadingBlock />;
  if (error) return <ErrorBanner message={error} />;
  if (!data) return null;

  return (
    <div>
      {!data.brokerReachable && <Unavailable reason={data.brokerUnavailableReason} />}
      <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
        {data.stages.map((s, i) => (
          <div key={s.id}>
            <div
              onClick={() => setOpenStage(openStage === s.id ? null : s.id)}
              style={{ ...card, cursor: "pointer", display: "flex", alignItems: "center", gap: 12, borderLeft: `4px solid ${s.status === "ok" ? "#22c55e" : s.status === "degraded" ? "#f59e0b" : "#ef4444"}` }}
            >
              <span style={{ fontSize: 18, color: "#94a3b8" }}>{i < data.stages.length - 1 ? "↓" : "•"}</span>
              <div style={{ flex: 1 }}>
                <strong>{s.name}</strong> <span style={{ fontSize: 11, color: "#64748b" }}>{s.service}</span>
                <div style={{ fontSize: 11, color: "#64748b" }}>{s.description}</div>
              </div>
              <StateChip state={s.status} />
              <span style={{ fontSize: 11, color: "#94a3b8" }}>{openStage === s.id ? "▲" : "▼"}</span>
            </div>
            {openStage === s.id && (
              <div style={{ ...card, marginTop: -6, borderTopLeftRadius: 0, borderTopRightRadius: 0, paddingTop: 8 }}>
                {Object.entries(s.metrics).map(([k, m]) => (
                  <div key={k} className="flex justify-content-between" style={{ fontSize: 12, padding: "3px 0", borderBottom: "1px solid #f1f5f9" }}>
                    <span style={{ color: "#475569" }}>{k}</span>
                    <span><strong>{m.value ?? "indisponível"}</strong> <span style={{ fontSize: 10, color: "#94a3b8" }}>({m.source})</span></span>
                  </div>
                ))}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

// ------------------------------------------------- Exchanges/Conn/Audit

function SimpleListTab<T>({
  load, columns, unreachable,
}: {
  load: () => Promise<{ reachable: boolean; unavailableReason: string | null; items: T[] } | null>;
  columns: { key: string; label: string; render: (item: T) => React.ReactNode }[];
  unreachable: (reason: string | null) => React.ReactNode;
}) {
  const [data, setData] = useState<{ reachable: boolean; unavailableReason: string | null; items: T[] } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true); setError(null);
    load().then(setData).catch(e => setError(e instanceof Error ? e.message : "Falha."))
      .finally(() => setLoading(false));
    const t = setInterval(() => {
      load().then(setData).catch(() => undefined);
    }, 20000);
    return () => clearInterval(t);
  }, [load]);

  if (loading && !data) return <LoadingBlock />;
  if (error) return <ErrorBanner message={error} />;
  if (!data) return null;
  if (!data.reachable) return <>{unreachable(data.unavailableReason)}</>;

  return (
    <div style={{ overflowX: "auto" }}>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr>{columns.map(c => <th key={c.key} style={th}>{c.label}</th>)}</tr></thead>
        <tbody>
          {data.items.map((item, i) => (
            <tr key={i}>
              {columns.map(c => <td key={c.key} style={td}>{c.render(item)}</td>)}
            </tr>
          ))}
          {data.items.length === 0 && <tr><td style={td} colSpan={columns.length}>Nenhum item.</td></tr>}
        </tbody>
      </table>
      <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 6 }}>{data.items.length} itens · atualiza a cada 20s</div>
    </div>
  );
}

function ExchangesTab() {
  return (
    <div>
      <h3 style={{ fontSize: 14, margin: "0 0 8px" }}>Exchanges</h3>
      <SimpleListTab<RabbitMqExchangeSummary>
        load={useCallback(() => getMessagingExchanges(), [])}
        unreachable={r => <Unavailable reason={r} />}
        columns={[
          { key: "name", label: "Exchange", render: e => <strong>{e.name}</strong> },
          { key: "type", label: "Tipo", render: e => e.type },
          { key: "durable", label: "Durable", render: e => e.durable ? "sim" : "não" },
          { key: "autoDelete", label: "Auto-delete", render: e => e.autoDelete ? "sim" : "não" },
          { key: "internal", label: "Interno", render: e => e.internal ? "sim" : "não" },
          { key: "pub", label: "Publish/s", render: e => e.publishRatePerSecond?.toFixed(2) ?? "—" },
          { key: "total", label: "Publicadas", render: e => e.publishedTotal ?? "—" },
        ]}
      />
      <h3 style={{ fontSize: 14, margin: "20px 0 8px" }}>Bindings</h3>
      <SimpleListTab<RabbitMqBindingSummary>
        load={useCallback(() => getMessagingBindings(), [])}
        unreachable={r => <Unavailable reason={r} />}
        columns={[
          { key: "src", label: "Origem", render: b => b.source || <em>(default)</em> },
          { key: "dst", label: "Destino", render: b => b.destination },
          { key: "type", label: "Tipo", render: b => b.destinationType },
          { key: "rk", label: "Routing key", render: b => b.routingKey || "—" },
        ]}
      />
    </div>
  );
}

function ConnectionsTab() {
  const load = useCallback(() => getMessagingConnections(), []);
  return (
    <SimpleListTab<RabbitMqConnectionSummary>
      load={load}
      unreachable={r => <Unavailable reason={r} />}
      columns={[
        { key: "name", label: "Conexão", render: c => <strong>{c.name}</strong> },
        { key: "user", label: "Usuário", render: c => c.user },
        { key: "state", label: "Estado", render: c => <StateChip state={c.state} /> },
        { key: "ch", label: "Canais", render: c => c.channels },
        { key: "at", label: "Conectada em", render: c => c.connectedAt ? fmt(c.connectedAt) : "—" },
      ]}
    />
  );
}

function AuditTab() {
  const [entries, setEntries] = useState<AuditEntry[]>([]);
  const [resource, setResource] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const p = await listAuditEntries({ page: 1, pageSize: 50 });
      setEntries(p.items);
    } catch (e) { setError(e instanceof Error ? e.message : "Falha."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const filtered = useMemo(() => resource
    ? entries.filter(e => e.resource.toLowerCase().includes(resource.toLowerCase())
        || e.action.toLowerCase().includes(resource.toLowerCase()))
    : entries, [entries, resource]);

  if (loading) return <LoadingBlock />;
  if (error) return <ErrorBanner message={error} />;

  return (
    <div>
      <input placeholder="Filtrar por recurso/ação (ex.: MessagingQueue, NotificationDelivery)"
        value={resource} onChange={e => setResource(e.target.value)}
        style={{ padding: "6px 8px", border: "1px solid #cbd5e1", borderRadius: 6, fontSize: 12, marginBottom: 10, minWidth: 320 }} />
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr>
          {["Quando", "Ação", "Recurso", "Resultado", "Ator", "Erro"].map(h => <th key={h} style={th}>{h}</th>)}
        </tr></thead>
        <tbody>
          {filtered.map(e => (
            <tr key={e.id}>
              <td style={td}>{fmt(e.occurredAt)}</td>
              <td style={td}>{e.action}</td>
              <td style={td}>{e.resource}</td>
              <td style={td}><StateChip state={e.outcome === "Succeeded" ? "ok" : "failed"} /></td>
              <td style={td}>{e.actorName ?? e.actorId ?? "—"}</td>
              <td style={td}>{e.error ? <span style={{ color: "#991b1b", fontSize: 11 }}>{e.error.slice(0, 80)}</span> : "—"}</td>
            </tr>
          ))}
          {filtered.length === 0 && <tr><td style={td} colSpan={6}>Nenhum registro.</td></tr>}
        </tbody>
      </table>
      <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 6 }}>Últimas 50 entradas · pause/resume/replay de mensageria aparecem aqui (resources MessagingQueue e NotificationDelivery)</div>
    </div>
  );
}

// -------------------------------------------------------------------- Page

export function MessagingPage() {
  const [tab, setTab] = useState<Tab>("Overview");
  const [workersRefresh, setWorkersRefresh] = useState(0);
  const [confirmAction, setConfirmAction] = useState<{ queue: string; action: "pause" | "resume" } | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);
  const roles = useAppSelector((state) => state.auth.user?.roles ?? []);
  const canAdminister = roles.includes("Admin");

  const runQueueAction = async () => {
    if (!confirmAction) return;
    const { queue, action } = confirmAction;
    setActionNotice(null);
    try {
      const r = action === "pause"
        ? await pauseQueueConsumption(queue)
        : await resumeQueueConsumption(queue);
      if (!r) { setActionNotice("Resposta vazia do servidor."); return; }
      setActionNotice(r.success
        ? (action === "pause"
          ? `Consumo de "${queue}" pausado (${r.consumersCancelled} consumers).`
          : `Consumo de "${queue}" retomado (${r.connectionsRecycled} conexões recicladas).`)
        : `Falha: ${r.error}`);
      setWorkersRefresh(k => k + 1);
    } catch (e) {
      setActionNotice(e instanceof Error ? e.message : "Falha na operação.");
    }
  };

  return (
    <div>
      <PageHeader
        title="Messaging Operations Center"
        subtitle="RabbitMQ, workers, mensagens, retries e DLQ — dados reais do broker e do banco."
        actions={<Button variant="ghost" icon="pi pi-refresh" onClick={() => setWorkersRefresh(k => k + 1)}>Atualizar</Button>}
      />
      {actionNotice && <div style={{ ...card, borderColor: "#0ea5e9", background: "#f0f9ff", fontSize: 12 }}>{actionNotice}</div>}
      <div className="flex gap-1" style={{ borderBottom: "1px solid #e2e8f0", margin: "12px 0", flexWrap: "wrap" }}>
        {TABS.map(t => (
          <button key={t}
            onClick={() => setTab(t)}
            style={{
              padding: "8px 14px", border: "none", background: "transparent",
              fontSize: 12, fontWeight: tab === t ? 700 : 500, cursor: "pointer",
              color: tab === t ? "#2563eb" : "#64748b",
              borderBottom: tab === t ? "2px solid #2563eb" : "2px solid transparent",
            }}>
            {t}
          </button>
        ))}
      </div>
      {tab === "Overview" && <OverviewTab />}
      {tab === "Queues" && <QueuesTab canAdminister={canAdminister} onPause={q => setConfirmAction({ queue: q, action: "pause" })} onResume={q => setConfirmAction({ queue: q, action: "resume" })} />}
      {tab === "Workers" && <WorkersTab refreshKey={workersRefresh} />}
      {tab === "Messages" && <MessagesTab />}
      {tab === "Retry" && <FailureTable kind="retries" canAdminister={canAdminister} />}
      {tab === "DLQ" && <FailureTable kind="dlq" canAdminister={canAdminister} />}
      {tab === "Flow" && <FlowTab />}
      {tab === "Exchanges" && <ExchangesTab />}
      {tab === "Connections" && <ConnectionsTab />}
      {tab === "Audit" && <AuditTab />}
      <ConfirmDialog
        visible={confirmAction !== null}
        onHide={() => setConfirmAction(null)}
        onConfirm={() => void runQueueAction()}
        header={confirmAction?.action === "pause" ? "Pausar consumo" : "Retomar consumo"}
        severity={confirmAction?.action === "pause" ? "warning" : "info"}
        confirmLabel={confirmAction?.action === "pause" ? "Pausar" : "Retomar"}
        message={confirmAction?.action === "pause"
          ? `Pausar o consumo da fila "${confirmAction.queue}"? Os consumers serão cancelados no broker (NÃO para o container).`
          : `Retomar o consumo da fila "${confirmAction?.queue}"? As conexões serão recicladas e o app re-subscreve os consumers.`}
      />
    </div>
  );
}

