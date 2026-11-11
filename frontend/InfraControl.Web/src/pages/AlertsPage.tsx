import { useCallback, useEffect, useState } from "react";
import {
  listAlerts,
  setAlertNote,
  type AlertItem,
  type AlertStatusFilter,
} from "@/services/api/alerts";
import { formatDateTime } from "@/utils/format";
import { Can } from "@/components/auth/Can";
import { Dialog } from "@/components/ui/Dialog";
import { Button } from "@/components/ui/Button";
import { FormField, FormError } from "@/components/ui/FormField";
import { TextArea } from "@/components/ui/TextArea";
import { Pagination } from "@/components/ui/Pagination";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { StatusChip } from "@/components/ui/StatusChip";
import { SelectButton } from "primereact/selectbutton";
import { errorMessage } from "@/hooks/queries";
import { useOperationalRefresh } from "@/hooks/useOperationalRefresh";
import { useAutoRefresh } from "@/hooks/useAutoRefresh";

/**
 * F17-T011 — AlertsPage conectada ao backend (substitui `alertsData` de
 * `data/dashboardData`). Contrato real: `GET /monitoring/alerts`
 * (`PagedResult<AlertDto>`; serviço já criado em F17-T009). O botão
 * "Filtrar" morto do protótipo virou o segmented control do **parâmetro
 * real** `status` (open|resolved|suppressed — vazio = todos; valor fora
 * responde 400 pt-BR, por isso só mandamos os três do contrato).
 *
 * Divergências do protótipo: severidade só tem Warning/Critical no enum
 * (chip "Info" saiu); `title`/`detail` não existem — o título real é
 * `deviceName · check` e o detalhe é a `message`; entrou `status`
 * (Aberto/Resolvido/Suprimido) e os horários (`openedAt`/`resolvedAt`).
 *
 * F50-T003 — anotação do operador: o alerta nasce do sistema, mas o
 * operador pode registrar contexto nele (`PUT alerts/{id}/note`, Operate).
 * A linha virou componente único (`AlertRow`) — as duas abas renderizavam
 * o mesmo JSX duplicado; a nota aparece na linha e o botão "Anotar" abre
 * dialog com `TextArea` (PUT de substituição).
 */

const PAGE_SIZE = 20;

const severityConfig: Record<string, { label: string; bg: string; color: string; icon: string }> = {
  Critical: { label: "Crítico", bg: "#fef2f2", color: "#dc2626", icon: "pi pi-exclamation-circle" },
  Warning: { label: "Atenção", bg: "#fef3c7", color: "#d97706", icon: "pi pi-exclamation-triangle" },
};

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  Open: { label: "Aberto", bg: "#fef2f2", color: "#dc2626" },
  Resolved: { label: "Resolvido", bg: "#f0fdf4", color: "#15803d" },
  Suppressed: { label: "Suprimido", bg: "#f1f5f9", color: "#64748b" },
};

const filters: Array<{ key: "" | AlertStatusFilter; label: string }> = [
  { key: "", label: "Todos" },
  { key: "open", label: "Abertos" },
  { key: "resolved", label: "Resolvidos" },
  { key: "suppressed", label: "Suprimidos" },
];

export function AlertsPage() {
  const [items, setItems] = useState<AlertItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [status, setStatus] = useState<"" | AlertStatusFilter>("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // F50-T003 — dialog de anotação do operador.
  const [noteTarget, setNoteTarget] = useState<AlertItem | null>(null);
  const [noteText, setNoteText] = useState("");
  const [noteSaving, setNoteSaving] = useState(false);
  const [noteError, setNoteError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listAlerts({
        status: status || undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, [status, page]);

  useEffect(() => {
    void load();
  }, [load]);

  // F44-T002/T003: push operacional + botão global + polling (autoRefresh)
  useOperationalRefresh(load);
  useAutoRefresh(load);

  function openNote(alert: AlertItem) {
    setNoteTarget(alert);
    setNoteText(alert.operatorNote ?? "");
    setNoteError(null);
  }

  async function submitNote() {
    if (!noteTarget) return;
    setNoteSaving(true);
    setNoteError(null);
    try {
      await setAlertNote(noteTarget.id, noteText);
      setNoteTarget(null);
      await load();
    } catch (caught) {
      setNoteError(errorMessage(caught));
    } finally {
      setNoteSaving(false);
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <ListPageShell
      title="Alertas"
      singular="alerta"
      plural="alertas"
      total={totalCount}
      loading={loading}
      error={error}
      onRetry={() => void load()}
      actions={
        <SelectButton
          className="uni-select-button"
          value={status}
          options={filters}
          optionLabel="label"
          optionValue="key"
          allowEmpty={false}
          onChange={(event) => {
            setStatus(event.value);
            setPage(1);
          }}
        />
      }
      footer={
        totalCount > PAGE_SIZE ? (
          <Pagination page={page} totalPages={totalPages} disabled={loading} onPage={setPage} />
        ) : undefined
      }
    >
      <div className="uni-panel">
        <div className="uni-alerts-list">
          {items.map((alert) => (
            <AlertRow key={alert.id} alert={alert} onNote={() => openNote(alert)} />
          ))}
          {!loading && items.length === 0 && (
            <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              {error ? "—" : "Nenhum alerta neste filtro"}
            </div>
          )}
          {loading && items.length === 0 && <LoadingBlock />}
        </div>
      </div>
      <Dialog
        visible={noteTarget !== null}
        onHide={() => setNoteTarget(null)}
        header="Anotar alerta"
        width={460}
        footer={
          <>
            <Button
              type="button"
              variant="ghost"
              onClick={() => setNoteTarget(null)}
              disabled={noteSaving}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="button"
              variant="primary"
              onClick={() => void submitNote()}
              disabled={noteSaving}
              style={{ padding: "6px 14px" }}
            >
              {noteSaving ? "Salvando…" : "Salvar"}
            </Button>
          </>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {noteTarget && (
            <div style={{ fontSize: 12, color: "#64748b" }}>
              {noteTarget.deviceName ?? "Sem device"} · {noteTarget.check ?? noteTarget.source}
            </div>
          )}
          <FormField label="Anotação do operador (máx. 500)">
            <TextArea
              value={noteText}
              onChange={setNoteText}
              rows={4}
              maxLength={500}
              placeholder="Contexto que o sistema não sabe (ex.: ticket aberto, janela de manutenção)…"
              disabled={noteSaving}
            />
          </FormField>
          <FormError message={noteError ?? ""} />
        </div>
      </Dialog>
    </ListPageShell>
  );
}

function AlertRow({ alert, onNote }: { alert: AlertItem; onNote: () => void }) {
  const severity = severityConfig[alert.severity] ?? {
    label: alert.severity, bg: "#f1f5f9", color: "#64748b", icon: "pi pi-info-circle",
  };
  const statusStyle = statusConfig[alert.status] ?? {
    label: alert.status, bg: "#f1f5f9", color: "#64748b",
  };
  return (
    <div className="uni-alert-row">
      <div className={`uni-alert-icon ${alert.severity === "Critical" ? "critical" : "warning"}`}>
        <i className={severity.icon} />
      </div>
      <div className="uni-alert-body">
        <div className="uni-alert-title">{alert.deviceName ?? "Sem device"} · {alert.check ?? alert.source}</div>
        <div className="uni-alert-detail">{alert.message}</div>
        {alert.operatorNote && (
          <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>
            <i className="pi pi-sticky-note" style={{ marginRight: 4 }} />
            {alert.operatorNote}
          </div>
        )}
      </div>
      <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4 }}>
        <StatusChip label={severity.label} background={severity.bg} color={severity.color} fontWeight={600} />
        <StatusChip label={statusStyle.label} background={statusStyle.bg} color={statusStyle.color} />
        <span style={{ fontSize: 10, color: "#94a3b8" }}>
          {formatDateTime(new Date(alert.openedAt))}
          {alert.resolvedAt ? ` → ${formatDateTime(new Date(alert.resolvedAt))}` : ""}
        </span>
        <Can level="operate">
          <button
            type="button"
            onClick={onNote}
            aria-label="Anotar"
            title="Anotar"
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: 4,
              border: "1px solid #e5e7eb",
              background: "white",
              borderRadius: 6,
              padding: "2px 8px",
              fontSize: 11,
              color: "#475569",
              cursor: "pointer",
            }}
          >
            <i className="pi pi-pencil" style={{ fontSize: 10 }} />
            Anotar
          </button>
        </Can>
      </div>
    </div>
  );
}
