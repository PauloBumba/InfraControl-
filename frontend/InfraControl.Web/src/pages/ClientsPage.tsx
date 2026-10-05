import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import { type DataTablePageEvent } from "primereact/datatable";
import { listClients, type ClientItem } from "@/services/api/clients";
import { formatDateTime } from "@/utils/format";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { PageHeader } from "@/components/ui/PageHeader";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { FilterBar } from "@/components/ui/FilterBar";

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

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function ClientsPage() {
  const [pageData, setPageData] = useState<{ items: ClientItem[]; totalCount: number } | null>(null);
  const [page, setPage] = useState(1);
  const [term, setTerm] = useState("");
  const [termInput, setTermInput] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await listClients(term, page, PAGE_SIZE);
      setPageData({ items: data.items, totalCount: data.totalCount });
    } catch (caught) {
      setPageData(null);
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [term, page]);

  useEffect(() => {
    void load();
  }, [load]);

  function applyTerm(event: FormEvent) {
    event.preventDefault();
    setTerm(termInput.trim());
    setPage(1);
  }

  function clearTerm() {
    setTermInput("");
    setTerm("");
    setPage(1);
  }

  const items = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;
  const hasTerm = term !== "";

  function handlePageChange(event: DataTablePageEvent) {
    setPage(Math.floor(event.first / PAGE_SIZE) + 1);
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Clientes"
        subtitle={
          loading
            ? "Carregando…"
            : `${totalCount} ${totalCount === 1 ? "cliente" : "clientes"}${hasTerm ? ` · busca "${term}"` : ""}`
        }
        actions={
          <FilterBar><form onSubmit={applyTerm} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <SearchInput
              value={termInput}
              onChange={setTermInput}
              placeholder="IP, MAC ou hostname"
              title="Busca por ip, mac ou hostname — aplicada no servidor"
              variant="muted"
              onEnter={() => { setTerm(termInput.trim()); setPage(1); }}
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
                onClick={clearTerm}
                style={{ padding: "6px 12px", borderColor: "#e2e8f0", fontSize: 13, color: "#64748b" }}
              >
                Limpar
              </Button>
            )}
          </form></FilterBar>
        }
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <DataTablePanel value={items} loading={loading} dataKey={(row: ClientItem) => `${row.mac ?? ""}|${row.ip ?? ""}`} paginator lazy rows={PAGE_SIZE} first={(page - 1) * PAGE_SIZE} totalRecords={totalCount} onPage={handlePageChange} alwaysShowPaginator={totalCount > PAGE_SIZE} paginatorTemplate="CurrentPageReport FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink" currentPageReportTemplate="Página {currentPage} de {totalPages} ({totalRecords} clientes)" emptyMessage={error ? "—" : hasTerm ? `Nenhum cliente corresponde à busca "${term}"` : "Nenhum cliente descoberto ainda — os clientes aparecem após o primeiro discovery"}>
          <Column header="Cliente" body={(client: ClientItem) => <div className="uni-client-table-name"><div className="uni-client-table-icon"><i className="pi pi-desktop" /></div><div><div style={{ fontWeight: 500, fontFamily: "monospace", fontSize: 12 }}>{client.ip ?? client.mac}</div>{client.ip && client.mac && <div style={{ fontSize: 11, color: "#94a3b8", fontFamily: "monospace" }}>{client.mac}</div>}</div></div>} />
          <Column header="Hostname" body={(client: ClientItem) => client.hostname ?? "—"} style={{ fontSize: 12, color: "#64748b" }} />
          <Column header="Tipo" body={(client: ClientItem) => { const style = kindStyle[client.kind] ?? { bg: "#f1f5f9", color: "#64748b" }; return <StatusChip label={kindLabel[client.kind] ?? client.kind} background={style.bg} color={style.color} />; }} style={{ width: 100 }} />
          <Column header="VLAN" body={(client: ClientItem) => client.vlan ?? "—"} style={{ fontFamily: "monospace", fontSize: 12, width: 90 }} />
          <Column header="Interface" body={(client: ClientItem) => client.localIf ?? "—"} style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }} />
          <Column header="Último visto" body={(client: ClientItem) => formatDateTime(new Date(client.lastSeenAt))} style={{ fontSize: 12, color: "#64748b", width: 155 }} />
      </DataTablePanel>
    </div>
  );
}
