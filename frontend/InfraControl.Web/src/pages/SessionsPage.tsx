import { useCallback, useEffect, useState } from "react";
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

function messageFor(error: unknown): string {
  return error instanceof Error ? error.message : "Não foi possível falar com o servidor. Tente novamente.";
}

const bannerStyle = (background: string, color: string) => ({
  display: "flex",
  alignItems: "center",
  gap: 8,
  padding: "10px 12px",
  borderRadius: 6,
  fontSize: 13,
  background,
  color,
});

/**
 * F1-T016d (ADR-020): tela de sessões — quem mexe é o próprio usuário, sem
 * exigir role. Revogar a sessão atual derruba o cookie no backend; aqui o
 * estado local acompanha na hora (`signedOut`) em vez de esperar o 401 do
 * próximo request.
 */
export function SessionsPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();

  const [sessions, setSessions] = useState<SessionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [busyOthers, setBusyOthers] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setSessions(await listSessions());
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const handleRevoke = async (session: SessionItem) => {
    const question = session.isCurrent
      ? "Revogar a sessão atual? Você será desconectado agora."
      : "Revogar esta sessão?";
    if (!window.confirm(question)) return;

    setBusyId(session.id);
    setError(null);
    setNotice(null);
    try {
      await revokeSession(session.id);

      if (session.isCurrent) {
        // O backend já assinou o cookie para fora; o estado local acompanha.
        dispatch(signedOut());
        navigate("/login", { replace: true });
        return;
      }

      setSessions((previous) => previous.filter((item) => item.id !== session.id));
      setNotice("Sessão revogada.");
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setBusyId(null);
    }
  };

  const handleRevokeOthers = async () => {
    if (!window.confirm("Sair de todos os outros dispositivos? A sessão atual continua ativa.")) return;

    setBusyOthers(true);
    setError(null);
    setNotice(null);
    try {
      await revokeOtherSessions();
      setSessions((previous) => previous.filter((item) => item.isCurrent));
      setNotice("As outras sessões foram revogadas.");
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setBusyOthers(false);
    }
  };

  const othersCount = sessions.filter((session) => !session.isCurrent).length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Minhas sessões</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {sessions.length} {sessions.length === 1 ? "sessão ativa" : "sessões ativas"} ·
            {" "}revogue o que não reconhecer
          </p>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <button
            type="button"
            onClick={() => void load()}
            disabled={loading}
            style={{
              display: "flex", alignItems: "center", gap: 6, padding: "6px 12px",
              borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13,
              color: "#64748b", background: "white", cursor: "pointer",
            }}
          >
            <i className={loading ? "pi pi-spin pi-spinner" : "pi pi-refresh"} style={{ fontSize: 12 }} /> Atualizar
          </button>
          <button
            type="button"
            onClick={() => void handleRevokeOthers()}
            disabled={loading || busyOthers || othersCount === 0}
            style={{
              display: "flex", alignItems: "center", gap: 6, padding: "6px 12px",
              borderRadius: 6, border: "none", fontSize: 13, fontWeight: 500,
              background: othersCount === 0 ? "#cbd5e1" : "#dc2626", color: "white",
              cursor: othersCount === 0 ? "not-allowed" : "pointer",
            }}
          >
            <i className={busyOthers ? "pi pi-spin pi-spinner" : "pi pi-sign-out"} style={{ fontSize: 12 }} />
            Sair de todos os outros
          </button>
        </div>
      </div>

      {error && (
        <div role="alert" style={bannerStyle("#fef2f2", "#dc2626")}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
        </div>
      )}

      {notice && (
        <div role="status" style={bannerStyle("#dcfce7", "#166534")}>
          <i className="pi pi-check-circle" />
          <span>{notice}</span>
        </div>
      )}

      <div className="uni-panel">
        {loading ? (
          <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 8, padding: 32, color: "#64748b", fontSize: 13 }}>
            <i className="pi pi-spin pi-spinner" />
            <span>Carregando sessões...</span>
          </div>
        ) : (
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Sessão</th>
                <th>Criada em</th>
                <th>Última atividade</th>
                <th>Expira em</th>
                <th style={{ width: 110 }}>Ação</th>
              </tr>
            </thead>
            <tbody>
              {sessions.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ padding: 24, textAlign: "center", fontSize: 13, color: "#64748b" }}>
                    Nenhuma sessão ativa.
                  </td>
                </tr>
              )}
              {sessions.map((session) => {
                const absoluteDiffers = session.absoluteExpiresAt !== session.expiresAt;
                return (
                  <tr key={session.id}>
                    <td>
                      <span
                        style={{
                          fontSize: 11, fontWeight: 600, padding: "2px 8px", borderRadius: 4,
                          background: session.isCurrent ? "#dbeafe" : "#f1f5f9",
                          color: session.isCurrent ? "#2563eb" : "#64748b",
                        }}
                      >
                        {session.isCurrent ? "Este dispositivo" : "Outro dispositivo"}
                      </span>
                      <div style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8", marginTop: 4 }}>
                        {session.id.slice(0, 8)}
                      </div>
                    </td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(session.createdAt))}</td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(session.lastSeenAt))}</td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>
                      {formatDateTime(new Date(session.expiresAt))}
                      {absoluteDiffers && (
                        <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>
                          teto absoluto: {formatDateTime(new Date(session.absoluteExpiresAt))}
                        </div>
                      )}
                    </td>
                    <td>
                      <button
                        type="button"
                        onClick={() => void handleRevoke(session)}
                        disabled={busyId !== null}
                        title={session.isCurrent ? "Revogar e sair" : "Revogar esta sessão"}
                        style={{
                          display: "flex", alignItems: "center", gap: 6, padding: "4px 10px",
                          borderRadius: 6, border: "1px solid #fecaca", fontSize: 12,
                          color: "#dc2626", background: "white",
                          cursor: busyId !== null ? "not-allowed" : "pointer", opacity: busyId !== null ? 0.6 : 1,
                        }}
                      >
                        <i className={busyId === session.id ? "pi pi-spin pi-spinner" : "pi pi-trash"} style={{ fontSize: 11 }} />
                        Revogar
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
