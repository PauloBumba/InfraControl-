import { useCallback, useEffect, useState } from "react";
import {
  listRuns,
  listRunResults,
  type DiscoveryResultItem,
  type DiscoveryRunItem,
} from "@/services/api/discovery";
import { formatDateTime } from "@/utils/format";

/**
 * F17-T015 — ClientsPage conectada ao backend (substitui `clients` de
 * `data/dashboardData`).
 *
 * Não existe domínio de "clientes conectados" (telemetria de controller AP
 * com sinal/velocidade/uso/SO — nada disso tem endpoint); o que existe de
 * cliente de rede são os **achados de discovery** (ARP/DHCP/MAC — F4), e
 * só por execução. A página mostra a última execução (`GET /discovery/runs`
 * ordenado StartedAt DESC, pageSize=1) e pagina os resultados dela (`GET
 * runs/{id}/results`, CreatedAt DESC).
 *
 * Divergências do protótipo: colunas Sinal/Velocidade/Uso/SO/Conexão
 * saíram (sem contrato), "Tipo" WiFi/LAN virou o `kind` real
 * (ARP/DHCP/MAC/Vizinho/Host), "Filtrar" saiu (a query não tem filtro por
 * kind — filtro honesto só server-side, documentado como pendência) e o
 * header "N clientes conectados" virou a contagem real de achados da
 * última descoberta. Achados negativos ("Nenhuma entrada ARP." sem ip/mac)
 * aparecem como linha informativa em vez de sumir (a query não pagina
 * por `succeeded` — remover quebraria a contagem). Sem mutations.
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

const runStatusLabel: Record<string, string> = {
  Running: "em execução",
  Completed: "concluída",
  Failed: "falhou",
};

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function ClientsPage() {
  const [run, setRun] = useState<DiscoveryRunItem | null>(null);
  const [runsChecked, setRunsChecked] = useState(false);
  const [items, setItems] = useState<DiscoveryResultItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const latest = await listRuns(1, 1);
      const found = latest.items[0] ?? null;
      setRun(found);
      setRunsChecked(true);
      if (!found) {
        setItems([]);
        setTotalCount(0);
        return;
      }
      const results = await listRunResults(found.id, page, PAGE_SIZE);
      setItems(results.items);
      setTotalCount(results.totalCount);
    } catch (caught) {
      setItems([]);
      setTotalCount(0);
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div>
        <h1 style={{ fontSize: 20, fontWeight: 700 }}>Clientes</h1>
        <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
          {loading
            ? "Carregando…"
            : run
              ? `${totalCount} ${totalCount === 1 ? "achado" : "achados"} · última descoberta ${formatDateTime(new Date(run.startedAt))} (${runStatusLabel[run.status] ?? run.status})`
              : "nenhuma descoberta executada ainda"}
        </p>
        {run?.status === "Failed" && run.errorMessage && (
          <p style={{ fontSize: 12, color: "#b91c1c", marginTop: 4 }}>
            {run.errorMessage}
          </p>
        )}
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
              <th>Registrado em</th>
            </tr>
          </thead>
          <tbody>
            {items.map((finding) => {
              const values = finding.values ?? {};
              const ip = values.ip;
              const mac = values.mac;
              const isClient = Boolean(ip || mac);
              const style = kindStyle[finding.kind] ?? { bg: "#f1f5f9", color: "#64748b" };
              if (!isClient) {
                // Achado negativo ("Nenhuma entrada ARP.") — informativo,
                // sem ip/mac; a query não filtra por succeeded.
                return (
                  <tr key={finding.id}>
                    <td colSpan={6} style={{ fontSize: 12, color: "#94a3b8", fontStyle: "italic" }}>
                      <span style={{ fontStyle: "normal", marginRight: 8 }}>
                        <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: style.bg, color: style.color }}>
                          {kindLabel[finding.kind] ?? finding.kind}
                        </span>
                      </span>
                      {finding.summary}
                    </td>
                  </tr>
                );
              }
              return (
                <tr key={finding.id}>
                  <td>
                    <div className="uni-client-table-name">
                      <div className="uni-client-table-icon">
                        <i className="pi pi-desktop" />
                      </div>
                      <div>
                        {ip ? (
                          <div style={{ fontWeight: 500, fontFamily: "monospace", fontSize: 12 }}>{ip}</div>
                        ) : (
                          <div style={{ fontWeight: 500, fontFamily: "monospace", fontSize: 12 }}>{mac}</div>
                        )}
                        {ip && mac && (
                          <div style={{ fontSize: 11, color: "#94a3b8", fontFamily: "monospace" }}>{mac}</div>
                        )}
                      </div>
                    </div>
                  </td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{values.hostname ?? "—"}</td>
                  <td>
                    <span style={{ fontSize: 11, fontWeight: 500, padding: "2px 6px", borderRadius: 4, background: style.bg, color: style.color }}>
                      {kindLabel[finding.kind] ?? finding.kind}
                    </span>
                  </td>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{values.vlan ?? "—"}</td>
                  <td style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>{values.local_if ?? "—"}</td>
                  <td style={{ fontSize: 12, color: "#64748b" }}>{formatDateTime(new Date(finding.createdAt))}</td>
                </tr>
              );
            })}
            {!loading && runsChecked && run === null && !error && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  Nenhuma descoberta executada ainda — os clientes aparecem após o primeiro discovery
                </td>
              </tr>
            )}
            {!loading && run !== null && items.length === 0 && !error && (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  Nenhum achado nesta execução
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
