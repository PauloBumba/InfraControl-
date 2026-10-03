import { useCallback, useEffect, useState, type FormEvent } from "react";
import { DataTable, type DataTablePageEvent } from "primereact/datatable";
import { Column } from "primereact/column";
import { formatDateTime } from "@/utils/format";
import {
  AUDIT_OUTCOME_FAILED,
  AUDIT_OUTCOME_SUCCEEDED,
  listAuditEntries,
  type AuditEntry,
  type PagedAudit,
} from "@/services/api/audit";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { StatusChip } from "@/components/ui/StatusChip";
import { CopyableId } from "@/components/ui/CopyableId";

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
 *
 * Legibilidade (2026-10-02, pedido: "sem id cru, nome para facilitar;
 * fácil de ler/achar"): Ator exibe `displayName` + e-mail resolvidos via
 * `listUsers()` — o contrato traz em `actorId` o `CurrentUser.UserName`
 * (= e-mail do Identity), então o mapa indexa por userId e por e-mail; o
 * valor original fica no `title` da célula e é o fallback quando não há
 * correspondência (usuário removido). Ação em PascalCase humanizada com o
 * valor cru no `title` (o filtro é igualdade exata). Correlation truncada
 * com clique para copiar (pi-copy → pi-check), pronta para colar no
 * rastreador da LogsPage.
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

/**
 * "LoginWithMfa" → "Login With Mfa". O valor cru continua no `title` da
 * célula (filtro de ação é igualdade exata — o operador copia de lá).
 */
function humanizeAction(action: string): string {
  return action
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
}

export function AuditPage() {
  const [pageData, setPageData] = useState<PagedAudit | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Mapa de resolução do ATOR: o backend grava em `actorId` o
  // `CurrentUser.UserName` (= e-mail do Identity), NÃO o GUID — o mapa é
  // indexado por userId e por e-mail (minúsculas) para achar nos dois
  // sentidos. Falha silenciosa: sem mapa, a célula exibe o valor cru (um
  // e-mail, que já é legível).
  const [usersById, setUsersById] = useState<Map<string, AdminUser>>(new Map());

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

  useEffect(() => {
    let cancelled = false;
    void listUsers()
      .then((users) => {
        if (cancelled) return;
        const map = new Map<string, AdminUser>();
        for (const user of users) {
          map.set(user.userId, user);
          map.set(user.email.toLowerCase(), user);
        }
        setUsersById(map);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

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
  const hasFilters =
    action !== "" || outcome !== "" || from !== "" || to !== "";

  // Paginator do DataTable é controlado: o evento `first` vira a página
  // server-side (mesma semântica do bloco manual "Anterior/Próxima").
  function handlePageChange(event: DataTablePageEvent) {
    setPage(Math.floor(event.first / PAGE_SIZE) + 1);
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Auditoria"
        subtitle={
          loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "evento" : "eventos"}`
        }
        actions={
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
        }
      />

      {formError && <ErrorBanner message={formError} />}

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div className="uni-panel">
        <DataTable
          value={entries}
          loading={loading}
          size="small"
          paginator
          rows={PAGE_SIZE}
          first={(page - 1) * PAGE_SIZE}
          totalRecords={totalCount}
          onPage={handlePageChange}
          lazy
          alwaysShowPaginator={totalCount > PAGE_SIZE}
          paginatorTemplate="CurrentPageReport FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink"
          currentPageReportTemplate="Página {currentPage} de {totalPages} ({totalRecords} eventos)"
          emptyMessage={error ? "—" : "Nenhum evento encontrado"}
          tableStyle={{ width: "100%" }}
        >
          <Column
            header="Data/Hora"
            body={(row: AuditEntry) => formatDateTime(new Date(row.occurredAt))}
            style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b", width: 150 }}
          />
          <Column
            header="Ação"
            body={(row: AuditEntry) => (
              <span
                style={{ fontSize: 12, fontWeight: 500, cursor: "help" }}
                title={`${row.action} — copie este valor para o filtro "Ação exata"`}
              >
                {humanizeAction(row.action)}
              </span>
            )}
            style={{ width: 200 }}
          />
          <Column
            field="resource"
            header="Recurso"
            style={{ fontSize: 12, color: "#64748b", width: 180 }}
          />
          <Column
            header="Resultado"
            body={(row: AuditEntry) => {
              const chip = outcomeChip(row.outcome);
              return (
                <StatusChip
                  label={chip.label}
                  background={chip.background}
                  color={chip.color}
                />
              );
            }}
            style={{ width: 90 }}
          />
          <Column
            header="Ator"
            body={(row: AuditEntry) => {
              if (!row.actorId)
                return <span style={{ color: "#94a3b8" }}>sistema</span>;
              const user =
                usersById.get(row.actorId) ??
                usersById.get(row.actorId.toLowerCase());
              return (
                <span title={row.actorId}>
                  {user ? (
                    <>
                      <div style={{ fontWeight: 500, display: "flex", alignItems: "center", gap: 6 }}>
                        <i className="pi pi-user" style={{ fontSize: 11, color: "#94a3b8" }} />
                        {user.displayName}
                      </div>
                      <div style={{ fontSize: 11, color: "#94a3b8" }}>{user.email}</div>
                    </>
                  ) : (
                    // Sem correspondência (usuário removido): o valor gravado
                    // é o UserName/e-mail — exibe cru, legível.
                    <span style={{ display: "flex", alignItems: "center", gap: 6 }}>
                      <i className="pi pi-user" style={{ fontSize: 11, color: "#94a3b8" }} />
                      {row.actorId}
                    </span>
                  )}
                  {row.error && (
                    <div style={{ fontSize: 11, color: "#dc2626", marginTop: 2 }}>{row.error}</div>
                  )}
                </span>
              );
            }}
          />
          <Column
            header="Correlation"
            body={(row: AuditEntry) => <CopyableId value={row.correlationId} />}
            style={{ width: 130 }}
          />
        </DataTable>
      </div>
    </div>
  );
}
