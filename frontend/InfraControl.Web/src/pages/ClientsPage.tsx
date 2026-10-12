import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import { type DataTablePageEvent } from "primereact/datatable";
import { listClients, type ClientItem } from "@/services/api/clients";
import { formatDateTime } from "@/utils/format";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { FilterBar } from "@/components/ui/FilterBar";
import { errorMessage } from "@/hooks/queries";
import { useServerSearch } from "@/hooks/useServerSearch";

/**
 * F18-T008 — ClientsPage sobre `GET /api/v1/clients`: o backend agrega
 * ARP/DHCP de **todas** as execuções de discovery e deduplica por MAC, então
 * a página não consulta mais "último run" (F17-T015) e o header mostra a
 * contagem real de clientes, não de achados de uma execução.
 *
 * O que mudou em relação à versão por execução: coluna "Registrado em" virou
 * "Último visto" (`lastSeenAt` agregado), as linhas informativas de achado
 * negativo saíram (o servidor descarta rows sem ip e sem MAC) e o estado
 * vazio distingue "ainda não há discovery" de "busca sem resultado". Busca
 * `term` casa ip/mac/hostname no servidor. Sem mutations.
 */

const PAGE_SIZE = 20;

const kindLabel: Record<string, string> = {
  ArpEntry: "ARP",
  DhcpLease: "DHCP",
  MacTableEntry: "MAC",
  Neighbor: "Vizinho",
  Host: "Host",
};

const kindStyle: Record<string, { bg: string; color: string }> = {
  ArpEntry: { bg: "#eff6ff", color: "#2563eb" },
  DhcpLease: { bg: "#f0fdf4", color: "#15803d" },
  MacTableEntry: { bg: "#f5f3ff", color: "#7c3aed" },
  Neighbor: { bg: "#fef3c7", color: "#b45309" },
  Host: { bg: "#f1f5f9", color: "#475569" },
};

/**
 * Colunas do quadro — os mesmos 5 tipos do catálogo `kindLabel`, na ordem
 * do catálogo. Renderizam todas, inclusive sem nenhum cliente (o quadro é
 * o lugar de ver o recorte inteiro).
 */
const CLIENT_KIND_COLUMNS = Object.entries(kindLabel).map(([key, label]) => ({ key, label }));

export function ClientsPage() {
  const [pageData, setPageData] = useState<{ items: ClientItem[]; totalCount: number } | null>(null);
  // F29-T001B — a máquina de estados da busca (texto digitado × termo aplicado
  // × página) sai da página: era o mesmo par de estados e os mesmos dois
  // handlers em 5 páginas.
  const {
    input: termInput,
    setInput: setTermInput,
    term,
    page,
    setPage,
    applyInput,
    clear,
    hasTerm,
  } = useServerSearch();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Adoção do padrão de visões (backlog Operations UX 2026-10-11): a
  // página era só tabela; o quadro agrupa por `kind` (origem do achado).
  // Tabela continua sendo o padrão — nada muda para quem já usava.
  const [view, setView] = useEntityViewMode("clients", "table");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await listClients(term, page, PAGE_SIZE);
      setPageData({ items: data.items, totalCount: data.totalCount });
    } catch (caught) {
      setPageData(null);
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, [term, page]);

  useEffect(() => {
    void load();
  }, [load]);

  /** Aplica o que está no campo — Enter do input ou botão "Filtrar". */
  function applyTerm(event: FormEvent) {
    event.preventDefault();
    applyInput();
  }

  const items = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;

  function handlePageChange(event: DataTablePageEvent) {
    setPage(Math.floor(event.first / PAGE_SIZE) + 1);
  }

  /** Card de cliente — usado nas visões grid E quadro (mesmos dados da tabela). */
  function renderClientCard(client: ClientItem) {
    const style = kindStyle[client.kind] ?? { bg: "#f1f5f9", color: "#64748b" };
    const primary = client.ip ?? client.mac ?? "—";
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
          <span style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }}>{primary}</span>
          <StatusChip
            label={kindLabel[client.kind] ?? client.kind}
            background={style.bg}
            color={style.color}
            fontSize={10}
            fontWeight={600}
          />
        </div>
        {client.ip && client.mac && (
          <div style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8" }}>{client.mac}</div>
        )}
        <div style={{ fontSize: 12, fontWeight: 500 }}>{client.hostname ?? "—"}</div>
        <div style={{ fontSize: 11, color: "#64748b", display: "flex", gap: 12, flexWrap: "wrap" }}>
          <span>VLAN: {client.vlan ?? "—"}</span>
          <span>Interface: {client.localIf ?? "—"}</span>
        </div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>
          Último visto: {formatDateTime(new Date(client.lastSeenAt))}
        </div>
      </div>
    );
  }

  return (
    <ListPageShell
      title="Clientes"
      singular="cliente"
      plural="clientes"
      total={totalCount}
      loading={loading}
      note={hasTerm ? `busca "${term}"` : undefined}
      error={error}
      onRetry={() => void load()}
      actions={
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
          <ViewSwitcher mode={view} onChange={setView} />
          <FilterBar>
            <form onSubmit={applyTerm} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
              <SearchInput
                value={termInput}
                onChange={setTermInput}
                placeholder="IP, MAC ou hostname"
                title="Busca por ip, mac ou hostname — aplicada no servidor"
                variant="muted"
                onEnter={applyInput}
              />
              <Button
                type="submit"
                variant="ghost"
                style={{ padding: "6px 12px", borderColor: "#e2e8f0", fontSize: 13, color: "#64748b" }}
              >
                Filtrar
              </Button>
              {hasTerm && (
                <Button
                  type="button"
                  variant="ghost"
                  onClick={clear}
                  style={{ padding: "6px 12px", borderColor: "#e2e8f0", fontSize: 13, color: "#64748b" }}
                >
                  Limpar
                </Button>
              )}
            </form>
          </FilterBar>
        </div>
      }
    >
      <EntityView<ClientItem>
        mode={view}
        items={items}
        getKey={(client) => `${client.mac ?? ""}|${client.ip ?? ""}`}
        emptyMessage={error ? "—" : hasTerm ? `Nenhum cliente corresponde à busca "${term}"` : "Nenhum cliente descoberto ainda — os clientes aparecem após o primeiro discovery"}
        kanban={{
          groupOf: (client) => client.kind,
          columns: CLIENT_KIND_COLUMNS,
        }}
        table={
      <DataTablePanel value={items} loading={loading} dataKey={(row: ClientItem) => `${row.mac ?? ""}|${row.ip ?? ""}`} paginator lazy rows={PAGE_SIZE} first={(page - 1) * PAGE_SIZE} totalRecords={totalCount} onPage={handlePageChange} alwaysShowPaginator={totalCount > PAGE_SIZE} paginatorTemplate="CurrentPageReport FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink" currentPageReportTemplate="Página {currentPage} de {totalPages} ({totalRecords} clientes)" emptyMessage={error ? "—" : hasTerm ? `Nenhum cliente corresponde à busca "${term}"` : "Nenhum cliente descoberto ainda — os clientes aparecem após o primeiro discovery"}>
          <Column header="Cliente" body={(client: ClientItem) => <div className="uni-client-table-name"><div className="uni-client-table-icon"><i className="pi pi-desktop" /></div><div><div style={{ fontWeight: 500, fontFamily: "monospace", fontSize: 12 }}>{client.ip ?? client.mac}</div>{client.ip && client.mac && <div style={{ fontSize: 11, color: "#94a3b8", fontFamily: "monospace" }}>{client.mac}</div>}</div></div>} />
          <Column header="Hostname" body={(client: ClientItem) => client.hostname ?? "—"} style={{ fontSize: 12, color: "#64748b" }} />
          <Column header="Tipo" body={(client: ClientItem) => { const style = kindStyle[client.kind] ?? { bg: "#f1f5f9", color: "#64748b" }; return <StatusChip label={kindLabel[client.kind] ?? client.kind} background={style.bg} color={style.color} />; }} style={{ width: 100 }} />
          <Column header="VLAN" body={(client: ClientItem) => client.vlan ?? "—"} style={{ fontFamily: "monospace", fontSize: 12, width: 90 }} />
          <Column header="Interface" body={(client: ClientItem) => client.localIf ?? "—"} style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }} />
          <Column header="Último visto" body={(client: ClientItem) => formatDateTime(new Date(client.lastSeenAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
      </DataTablePanel>
        }
        renderCard={renderClientCard}
      />
    </ListPageShell>
  );
}
