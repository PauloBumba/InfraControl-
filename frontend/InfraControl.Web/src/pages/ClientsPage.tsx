import { useCallback, useEffect, useState, type FormEvent } from "react";
import { listClients, type ClientItem } from "@/services/api/clients";
import { formatDateTime } from "@/utils/format";

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
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const hasTerm = term !== "";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Clientes</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {loading
              ? "Carregando…"
              : `${totalCount} ${totalCount === 1 ? "cliente" : "clientes"}${hasTerm ? ` · busca "${term}"` : ""}`}
          </p>
        </div>

        <form onSubmit={applyTerm} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#f1f5f9", borderRadius: 6, padding: "6px 12px" }}>
            <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
            <input
              type="text"
              placeholder="IP, MAC ou hostname"
              title="Busca por ip, mac ou hostname — aplicada no servidor"
              value={termInput}
              onChange={(event) => setTermInput(event.target.value)}
              style={{ border: "none", background: "none", outline: "none", fontSize: 13, width: 210 }}
            />
          </div>
          <button
            type="submit"
            style={{ padding: "6px 12px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            Filtrar
          </button>
          {hasTerm && (
            <button
              type="button"
              onClick={clearTerm}
              style={{ padding: "6px 12px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
            >
              Limpar
            </button>
          )}
        </form>
      </div>

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
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th>Cliente</th>
              <th>Hostname</th>
              <th>Tipo</th>
              <th>VLAN</th>
              <th>Interface</th>
              <th>Último visto</th>
            </tr>
          </thead>
          <tbody>
            {items.map((client) => {
              const style = kindStyle[client.kind] ?? { bg: "#f1f5f9", color: "#64748b" };
              return (
                <tr key={`${client.mac ?? ""}|${client.ip ?? ""}`}>
                  <td>
                    <div className="uni-client-table-name">
                      <div className="uni-client-table-icon">
                        <i className="pi pi-desktop" />
                      </div>
                      <div>
                        <div style={{ fontWeight: 500, fontFamily: "monospace", fontSize: 12 }}>
                          {client.ip ?? client.mac}
                        </div>
                        {client.ip && client.mac && (
                          <div style={{ fontSize: 11, color: "#94a3b8", fontFamily: "monospace" }}>{client.mac}</div>
                        )}
                      </div>
                    </div>
                  </td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{client.hostname ?? "—"}</td>
                  <td>
                    <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 6px", borderRadius: 4, background: style.bg, color: style.color }}>
                      {kindLabel[client.kind] ?? client.kind}
                    </span>
                  </td>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{client.vlan ?? "—"}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>{client.localIf ?? "—"}</td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(client.lastSeenAt))}</td>
                </tr>
              );
            })}
            {!loading && !error && totalCount === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  {hasTerm
                    ? `Nenhum cliente corresponde à busca "${term}"`
                    : "Nenhum cliente descoberto ainda — os clientes aparecem após o primeiro discovery"}
                </td>
              </tr>
            )}
            {loading && (
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
