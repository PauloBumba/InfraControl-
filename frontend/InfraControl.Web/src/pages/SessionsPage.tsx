import { useCallback, useEffect, useState } from "react";
import { Column } from "primereact/column";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/Dialog";
import { StatusChip } from "@/components/ui/StatusChip";
import { InlineMessages, useInlineMessages } from "@/components/ui/InlineMessages";
import { useNavigate } from "react-router-dom";
import { useAppDispatch } from "@/app/store/hooks";
import { signedOut } from "@/app/store/slices/authSlice";
import {
  listSessions,
  revokeOtherSessions,
  revokeSession,
  type SessionItem,
} from "@/services/api/sessions";
import { formatDateTime } from "@/utils/format";
import { errorMessage } from "@/hooks/queries";

/**
 * Quadro agrupado por `isCurrent` — o mesmo recorte da coluna "Sessão"
 * ("Este dispositivo" × "Outro dispositivo"), que é a única dimensão real
 * desta lista.
 */
function sessionKindKey(session: SessionItem): string {
  return session.isCurrent ? "current" : "other";
}

const SESSION_KIND_COLUMNS = [
  { key: "current", label: "Este dispositivo" },
  { key: "other", label: "Outro dispositivo" },
];

/**
 * F1-T016d (ADR-020): tela de sessões — quem mexe é o próprio usuário, sem
 * exigir role. Revogar a sessão atual derruba o cookie no backend; aqui o
 * estado local acompanha na hora (`signedOut`) em vez de esperar o 401 do
 * próximo request. Confirmações usam `ConfirmDialog` (não `window.confirm`).
 */
export function SessionsPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();

  const [sessions, setSessions] = useState<SessionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { messagesRef, showMessage } = useInlineMessages();
  const [busyId, setBusyId] = useState<string | null>(null);
  const [busyOthers, setBusyOthers] = useState(false);
  const [pendingRevoke, setPendingRevoke] = useState<SessionItem | null>(null);
  const [confirmOthers, setConfirmOthers] = useState(false);

  // Adoção do padrão de visões (backlog Operations UX 2026-10-11): era só
  // tabela; o quadro agrupa por "este dispositivo" × "outro". Tabela = padrão.
  const [view, setView] = useEntityViewMode("sessions", "table");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setSessions(await listSessions());
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const executeRevoke = async (session: SessionItem) => {
    setBusyId(session.id);
    setError(null);
    try {
      await revokeSession(session.id);

      if (session.isCurrent) {
        dispatch(signedOut());
        navigate("/login", { replace: true });
        return;
      }

      setSessions((previous) => previous.filter((item) => item.id !== session.id));
      showMessage({ severity: "success", summary: "Sessão revogada." });
    } catch (caught) {
      showMessage({ severity: "error", summary: "Não foi possível revogar a sessão.", detail: errorMessage(caught) });
    } finally {
      setBusyId(null);
    }
  };

  const executeRevokeOthers = async () => {
    setBusyOthers(true);
    setError(null);
    try {
      await revokeOtherSessions();
      setSessions((previous) => previous.filter((item) => item.isCurrent));
      showMessage({ severity: "success", summary: "As outras sessões foram revogadas." });
    } catch (caught) {
      showMessage({ severity: "error", summary: "Não foi possível revogar as outras sessões.", detail: errorMessage(caught) });
    } finally {
      setBusyOthers(false);
    }
  };

  const othersCount = sessions.filter((session) => !session.isCurrent).length;

  /** Card de sessão — usado nas visões grid E quadro (mesmos dados da tabela). */
  function renderSessionCard(session: SessionItem) {
    return (
      <div
        style={{
          background: "white",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
          padding: 14,
          display: "flex",
          flexDirection: "column",
          gap: 6,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
          <StatusChip
            label={session.isCurrent ? "Este dispositivo" : "Outro dispositivo"}
            background={session.isCurrent ? "#dbeafe" : "#f1f5f9"}
            color={session.isCurrent ? "#2563eb" : "#64748b"}
            fontSize={10}
            fontWeight={600}
          />
          <Button
            variant="ghost"
            icon={busyId === session.id ? "pi pi-spin pi-spinner" : "pi pi-trash"}
            onClick={() => setPendingRevoke(session)}
            disabled={busyId !== null}
            style={{ color: "#dc2626", borderColor: "#fecaca", padding: "4px 8px", fontSize: 12 }}
            title={session.isCurrent ? "Revogar e sair" : "Revogar esta sessão"}
          >
            Revogar
          </Button>
        </div>
        <div style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8" }}>
          {session.id.slice(0, 8)}
        </div>
        <div style={{ fontSize: 11, color: "#64748b", display: "flex", flexDirection: "column", gap: 2 }}>
          <span>Criada em: {formatDateTime(new Date(session.createdAt))}</span>
          <span>Última atividade: {formatDateTime(new Date(session.lastSeenAt))}</span>
          <span>Expira em: {formatDateTime(new Date(session.expiresAt))}</span>
          {session.absoluteExpiresAt !== session.expiresAt && (
            <span style={{ color: "#94a3b8" }}>
              teto absoluto: {formatDateTime(new Date(session.absoluteExpiresAt))}
            </span>
          )}
        </div>
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Minhas sessões"
        subtitle={`${sessions.length} ${sessions.length === 1 ? "sessão ativa" : "sessões ativas"} · revogue o que não reconhecer`}
        actions={
          <div className="flex gap-2 flex-wrap">
            <ViewSwitcher mode={view} onChange={setView} />
            <Button
              variant="ghost"
              icon={loading ? "pi pi-spin pi-spinner" : "pi pi-refresh"}
              onClick={() => void load()}
              disabled={loading}
            >
              Atualizar
            </Button>
            <Button
              variant="danger"
              icon={busyOthers ? "pi pi-spin pi-spinner" : "pi pi-sign-out"}
              onClick={() => setConfirmOthers(true)}
              disabled={loading || busyOthers || othersCount === 0}
            >
              Sair de todos os outros
            </Button>
          </div>
        }
      />

      <InlineMessages ref={messagesRef} />

      {error && <ErrorBanner message={error} color="#dc2626" />}

      <ConfirmDialog
        visible={!!pendingRevoke}
        onHide={() => setPendingRevoke(null)}
        onConfirm={() => {
          if (pendingRevoke) void executeRevoke(pendingRevoke);
        }}
        header="Revogar sessão"
        message={
          pendingRevoke?.isCurrent
            ? "Revogar a sessão atual? Você será desconectado agora."
            : "Revogar esta sessão?"
        }
        confirmLabel="Revogar"
        severity="danger"
      />

      <ConfirmDialog
        visible={confirmOthers}
        onHide={() => setConfirmOthers(false)}
        onConfirm={() => void executeRevokeOthers()}
        header="Sair dos outros dispositivos"
        message="Sair de todos os outros dispositivos? A sessão atual continua ativa."
        confirmLabel="Sair dos outros"
        severity="danger"
      />

      <EntityView<SessionItem>
        mode={view}
        items={sessions}
        getKey={(session) => session.id}
        emptyMessage="Nenhuma sessão ativa."
        kanban={{
          groupOf: sessionKindKey,
          columns: SESSION_KIND_COLUMNS,
        }}
        renderCard={renderSessionCard}
        table={
      <DataTablePanel value={sessions} dataKey="id" loading={loading} emptyMessage="Nenhuma sessão ativa.">
          <Column header="Sessão" body={(session: SessionItem) => <><StatusChip label={session.isCurrent ? "Este dispositivo" : "Outro dispositivo"} background={session.isCurrent ? "#dbeafe" : "#f1f5f9"} color={session.isCurrent ? "#2563eb" : "#64748b"} fontWeight={600} /><div style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8", marginTop: 4 }}>{session.id.slice(0, 8)}</div></>} />
          <Column header="Criada em" body={(session: SessionItem) => formatDateTime(new Date(session.createdAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
          <Column header="Última atividade" body={(session: SessionItem) => formatDateTime(new Date(session.lastSeenAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
          <Column header="Expira em" body={(session: SessionItem) => <>{formatDateTime(new Date(session.expiresAt))}{session.absoluteExpiresAt !== session.expiresAt && <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>teto absoluto: {formatDateTime(new Date(session.absoluteExpiresAt))}</div>}</>} style={{ fontSize: 12, color: "#64748b" }} />
          <Column header="Ação" body={(session: SessionItem) => <Button variant="ghost" icon={busyId === session.id ? "pi pi-spin pi-spinner" : "pi pi-trash"} onClick={() => setPendingRevoke(session)} disabled={busyId !== null} style={{ color: "#dc2626", borderColor: "#fecaca" }} title={session.isCurrent ? "Revogar e sair" : "Revogar esta sessão"}>Revogar</Button>} style={{ width: 120 }} />
      </DataTablePanel>
        }
      />
    </div>
  );
}
