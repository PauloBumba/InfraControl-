import { useCallback, useEffect, useState, type FormEvent } from "react";
import { formatDateTime } from "@/utils/format";
import {
  AUDIT_OUTCOME_FAILED,
  AUDIT_OUTCOME_SUCCEEDED,
  listAuditEntries,
  type PagedAudit,
} from "@/services/api/audit";

/**
 * F17-T002 — AuditPage conectada ao backend (substitui o mock de
 * `data/dashboardData`, policy Administer no controller). O que mudou em
 * relação ao protótipo: `outcome` real é **Succeeded/Failed** (o mock
 * esperava "Success"/"Failure" e exibiria tudo como "Falha"); `action` é
 * igualdade EXATA no reader — por isso é um campo aplicado no submit, não
 * busca por substring; o mock de "Pesquisar" varria action/resource/ator
 * (parâmetro inexistente na API) e o "Exportar" não tem endpoint — os dois
 * saíram. Período: `from`/`to` existem na API desde o reader e esta task
 * liga o seletor (`datetime-local` no fuso local → instante UTC no serviço;
 * "Até" inclusivo até o fim do minuto).
 */

const PAGE_SIZE = 20;

const outcomeOptions = [
  { value: "", label: "Todos os resultados" },
  { value: AUDIT_OUTCOME_SUCCEEDED, label: "Sucesso" },
  { value: AUDIT_OUTCOME_FAILED, label: "Falha" },
];

function outcomeChip(outcome: string): { label: string; background: string; color: string } {
  if (outcome === AUDIT_OUTCOME_SUCCEEDED)
    return { label: "Sucesso", background: "#dcfce7", color: "#16a34a" };
  if (outcome === AUDIT_OUTCOME_FAILED)
    return { label: "Falha", background: "#fee2e2", color: "#dc2626" };
  // Valor fora do catálogo conhecido: mostra cru em vez de mentir.
  return { label: outcome, background: "#f1f5f9", color: "#475569" };
}

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function AuditPage() {
  const [pageData, setPageData] = useState<PagedAudit | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [outcome, setOutcome] = useState("");
  const [action, setAction] = useState("");
  const [actionInput, setActionInput] = useState("");
  // Filtro de período (params `from`/`to` da API) — aplicado no submit, como
  // o campo de ação; a conversão para UTC acontece no serviço.
  const [fromInput, setFromInput] = useState("");
  const [toInput, setToInput] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setPageData(
        await listAuditEntries({
          action: action || undefined,
          outcome: outcome || undefined,
          from: from || undefined,
          to: to || undefined,
          page,
          pageSize: PAGE_SIZE,
        }),
      );
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [action, outcome, from, to, page]);

  useEffect(() => {
    void load();
  }, [load]);

  function applyAction(event: FormEvent) {
    event.preventDefault();
    if (fromInput && toInput && fromInput > toInput) {
      setFormError("O período \"De\" é posterior ao \"Até\".");
      return;
    }
    setFormError(null);
    setFrom(fromInput);
    setTo(toInput);
    setAction(actionInput.trim());
    setPage(1);
  }

  function clearFilters() {
    setActionInput("");
    setAction("");
    setOutcome("");
    setFromInput("");
    setToInput("");
    setFrom("");
    setTo("");
    setFormError(null);
    setPage(1);
  }

  const entries = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const hasFilters =
    action !== "" || outcome !== "" || from !== "" || to !== "";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Auditoria</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "evento" : "eventos"}`}
          </p>
        </div>
        <form onSubmit={applyAction} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#f1f5f9", borderRadius: 6, padding: "6px 12px" }}>
            <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
            <input
              type="text"
              placeholder="Ação exata — ex.: LoginWithMfa"
              title="Filtro de igualdade: nome do request MediatR ou evento de segurança"
              value={actionInput}
              onChange={(event) => setActionInput(event.target.value)}
              style={{ border: "none", background: "none", outline: "none", fontSize: 13, width: 210 }}
            />
          </div>
          <select
            value={outcome}
            onChange={(event) => { setOutcome(event.target.value); setPage(1); }}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            {outcomeOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
          <input
            type="datetime-local"
            title="De — início do período (relógio local)"
            value={fromInput}
            onChange={(event) => setFromInput(event.target.value)}
            style={{ padding: "6px 8px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          />
          <input
            type="datetime-local"
            title="Até — fim do período, inclusivo até o fim do minuto"
            value={toInput}
            onChange={(event) => setToInput(event.target.value)}
            style={{ padding: "6px 8px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          />
          <button
            type="submit"
            style={{ padding: "6px 12px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            Filtrar
          </button>
          {hasFilters && (
            <button
              type="button"
              onClick={clearFilters}
              style={{ padding: "6px 12px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
            >
              Limpar
            </button>
          )}
        </form>
      </div>

      {formError && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{formError}</span>
        </div>
      )}

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
        <table className="uni-logs-table">
          <thead>
            <tr>
              <th style={{ width: 150 }}>Data/Hora</th>
              <th style={{ width: 200 }}>Ação</th>
              <th style={{ width: 180 }}>Recurso</th>
              <th style={{ width: 90 }}>Resultado</th>
              <th>Ator</th>
              <th style={{ width: 130 }}>Correlation</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => {
              const chip = outcomeChip(entry.outcome);
              return (
                <tr key={entry.id}>
                  <td style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>
                    {formatDateTime(new Date(entry.occurredAt))}
                  </td>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{entry.action}</td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{entry.resource}</td>
                  <td>
                    <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 8px", borderRadius: 4, background: chip.background, color: chip.color }}>
                      {chip.label}
                    </span>
                  </td>
                  <td style={{ fontSize: 12 }}>
                    {entry.actorId ?? <span style={{ color: "#94a3b8" }}>sistema</span>}
                    {entry.error && (
                      <div style={{ fontSize: 11, color: "#dc2626", marginTop: 2 }}>{entry.error}</div>
                    )}
                  </td>
                  <td style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8" }}>{entry.correlationId}</td>
                </tr>
              );
            })}
            {!loading && entries.length === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  {error ? "—" : "Nenhum evento encontrado"}
                </td>
              </tr>
            )}
            {loading && entries.length === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  Carregando…
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {totalCount > PAGE_SIZE && (
        <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 10, fontSize: 12, color: "#64748b" }}>
          <span>Página {page} de {totalPages}</span>
          <button
            onClick={() => setPage((value) => Math.max(1, value - 1))}
            disabled={page <= 1 || loading}
            style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: page <= 1 ? "default" : "pointer", opacity: page <= 1 ? 0.5 : 1 }}
          >
            Anterior
          </button>
          <button
            onClick={() => setPage((value) => Math.min(totalPages, value + 1))}
            disabled={page >= totalPages || loading}
            style={{ padding: "5px 10px", borderRadius: 6, border: "1px solid #e2e8f0", background: "white", fontSize: 12, color: "#374151", cursor: page >= totalPages ? "default" : "pointer", opacity: page >= totalPages ? 0.5 : 1 }}
          >
            Próxima
          </button>
        </div>
      )}
    </div>
  );
}
