import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Column } from "primereact/column";
import { lookupMac, listVlansExplorer, getApExplorer, type ApExplorerItem, type ApExplorerResult, type MacLookupResult, type MacLookupSighting, type VlanExplorerItem, type VlanExplorerResult } from "@/services/api/network";
import { formatDateTime } from "@/utils/format";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { FilterBar } from "@/components/ui/FilterBar";
import { errorMessage } from "@/hooks/queries";

/**
 * Network Explorer F1 — MAC/IP Explorer (`GET /api/v1/network/mac-lookup`).
 *
 * Responde "onde esse MAC/IP está" cruzando as evidências reais já coletadas
 * pelos providers de discovery: tabela MAC (SNMP dot1dTpFdbTable → switch +
 * porta), ARP, DHCP (hostname/VLAN) e LLDP, com enriquecimento opcional do
 * AP Wi-Fi via UniFi ao vivo.
 *
 * Regras do spec: campo vazio/`—` = fonte não informou (nunca inventado);
 * registro antigo é sinalizado, não escondido; ausência de evidência NÃO
 * significa "offline" — a página explicita isso no estado vazio.
 */

const kindStyle: Record<string, { bg: string; color: string }> = {
  MacTableEntry: { bg: "#f5f3ff", color: "#7c3aed" },
  DhcpLease: { bg: "#f0fdf4", color: "#15803d" },
  ArpEntry: { bg: "#eff6ff", color: "#2563eb" },
  Neighbor: { bg: "#fef3c7", color: "#b45309" },
};

function SummaryField({ label, value, mono }: { label: string; value: string | null; mono?: boolean }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 110 }}>
      <span style={{ fontSize: 10, color: "#94a3b8", textTransform: "uppercase", letterSpacing: 0.4 }}>{label}</span>
      <span style={{ fontSize: 13, fontWeight: 600, fontFamily: mono ? "monospace" : undefined, color: value ? "#1e293b" : "#cbd5e1" }}>
        {value ?? "—"}
      </span>
    </div>
  );
}

/** Aba APs/ACs — inventário + UniFi ao vivo (clientes Wi-Fi por AP). */
function ApExplorerView() {
  const [data, setData] = useState<ApExplorerResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await getApExplorer());
    } catch (caught) {
      setData(null);
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const items = data?.items ?? [];

  return (
    <>
      {error && (
        <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, padding: "10px 14px", color: "#991b1b", fontSize: 13, marginBottom: 16 }}>
          {error}
        </div>
      )}
      <DataTablePanel
        value={items}
        loading={loading}
        dataKey={(row: ApExplorerItem) => row.deviceId ?? `${row.kind}-${row.mac ?? row.name}`}
        emptyMessage="Nenhum AP ou controlador identificado — cadastre devices tipo AccessPoint ou conecte o UniFi"
      >
        <Column header="Nome" body={(row: ApExplorerItem) =>
          row.deviceId ? (
            <Link to={`/devices/${row.deviceId}`} style={{ fontSize: 12, fontWeight: 600, color: "#2563eb", textDecoration: "none" }}>
              {row.name}
            </Link>
          ) : (
            <span style={{ fontSize: 12, fontWeight: 600 }}>{row.name}</span>
          )
        } style={{ minWidth: 140 }} />
        <Column header="Tipo" body={(row: ApExplorerItem) => (
          <span style={{ display: "inline-flex", gap: 4 }}>
            <StatusChip
              label={row.kind === "Controller" ? "AC" : "AP"}
              background={row.kind === "Controller" ? "#eff6ff" : "#f5f3ff"}
              color={row.kind === "Controller" ? "#2563eb" : "#7c3aed"}
              fontSize={10}
            />
            {!row.isRegistered && (
              <StatusChip label="Sem cadastro" background="#f1f5f9" color="#64748b" fontSize={10} />
            )}
          </span>
        )} style={{ width: 150 }} />
        <Column header="IP" body={(row: ApExplorerItem) => (
          <span style={{ fontFamily: "monospace", fontSize: 12 }}>{row.ip ?? "—"}</span>
        )} style={{ width: 120 }} />
        <Column header="Localização" body={(row: ApExplorerItem) => row.locationName ?? "—"} style={{ fontSize: 12, color: "#64748b" }} />
        <Column header="VLAN gerência" body={(row: ApExplorerItem) => row.managementVlan ?? "—"} style={{ fontSize: 12, color: "#64748b", width: 100 }} />
        <Column header="Uplink" body={(row: ApExplorerItem) =>
          row.uplinkSwitchName
            ? `${row.uplinkSwitchName}${row.uplinkPort ? ` · ${row.uplinkPort}` : ""}`
            : "—"
        } style={{ fontSize: 12, color: "#64748b" }} />
        <Column header="Clientes Wi-Fi" body={(row: ApExplorerItem) => (
          <span title={row.ssids.join(", ")}>
            {row.clientCount > 0 ? row.clientCount : "—"}
            {row.ssids.length > 0 && (
              <span style={{ fontSize: 10, color: "#94a3b8" }}> · {row.ssids.join(", ")}</span>
            )}
          </span>
        )} style={{ width: 160 }} />
        <Column header="UniFi" body={(row: ApExplorerItem) =>
          row.uniFiConnected === null || row.uniFiConnected === undefined ? (
            "—"
          ) : (
            <StatusChip
              label={row.uniFiConnected ? "Conectado" : "Offline"}
              background={row.uniFiConnected ? "#dcfce7" : "#fee2e2"}
              color={row.uniFiConnected ? "#16a34a" : "#dc2626"}
              fontSize={10}
            />
          )
        } style={{ width: 100 }} />
        <Column header="Último discovery" body={(row: ApExplorerItem) =>
          row.lastDiscoveryAt ? formatDateTime(new Date(row.lastDiscoveryAt)) : "—"
        } style={{ fontSize: 12, color: "#64748b", width: 140 }} />
      </DataTablePanel>
    </>
  );
}

const EXPLORER_TABS = ["mac", "vlans", "aps"] as const;
type ExplorerTab = (typeof EXPLORER_TABS)[number];

const VLAN_PAGE_SIZE = 20;

/** Aba VLANs do Explorer — VLANs cadastradas enriquecidas + descobertas não cadastradas. */
function VlanExplorerView() {
  const [termInput, setTermInput] = useState("");
  const [term, setTerm] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<VlanExplorerResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await listVlansExplorer(term || undefined, page, VLAN_PAGE_SIZE));
    } catch (caught) {
      setData(null);
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, [term, page]);

  useEffect(() => {
    void load();
  }, [load]);

  function applyTerm(event: FormEvent) {
    event.preventDefault();
    setPage(1);
    setTerm(termInput.trim());
  }

  const items = data?.items ?? [];
  const discovered = data?.discoveredVlans ?? [];
  const totalCount = items.length; // o backend não devolve total — pagina pelo retorno

  return (
    <>
      <FilterBar>
        <form onSubmit={applyTerm} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <SearchInput
            value={termInput}
            onChange={setTermInput}
            placeholder="Nome ou número da VLAN"
            variant="muted"
            onEnter={() => { setPage(1); setTerm(termInput.trim()); }}
          />
          <Button type="submit" variant="ghost" style={{ padding: "6px 12px", borderColor: "#e2e8f0", fontSize: 13, color: "#64748b" }}>
            Filtrar
          </Button>
          {term && (
            <Button type="button" variant="ghost" onClick={() => { setTermInput(""); setTerm(""); setPage(1); }} style={{ padding: "6px 12px", borderColor: "#e2e8f0", fontSize: 13, color: "#64748b" }}>
              Limpar
            </Button>
          )}
        </form>
      </FilterBar>

      {error && (
        <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, padding: "10px 14px", color: "#991b1b", fontSize: 13, marginBottom: 16 }}>
          {error}
        </div>
      )}

      <DataTablePanel
        value={items}
        loading={loading}
        dataKey={(row: VlanExplorerItem) => row.id ?? `disc-${row.vlanNumber}`}
        emptyMessage={term ? `Nenhuma VLAN corresponde à busca "${term}"` : "Nenhuma VLAN cadastrada"}
      >
        <Column header="VLAN" body={(row: VlanExplorerItem) => (
          <span style={{ fontFamily: "monospace", fontWeight: 700, fontSize: 13 }}>{row.vlanNumber ?? "—"}</span>
        )} style={{ width: 80 }} />
        <Column header="Nome" body={(row: VlanExplorerItem) => row.name ?? "—"} style={{ fontWeight: 500 }} />
        <Column header="Switches" body={(row: VlanExplorerItem) =>
          row.switches.length === 0 ? "—" : row.switches.map((s) => s.deviceName).join(", ")
        } style={{ fontSize: 12, color: "#64748b" }} />
        <Column header="Portas" body={(row: VlanExplorerItem) => row.portCount} style={{ width: 80 }} />
        <Column header="Dispositivos" body={(row: VlanExplorerItem) => row.discoveredDeviceCount} style={{ width: 110 }} />
        <Column header="Último visto" body={(row: VlanExplorerItem) =>
          row.lastSeenAt ? formatDateTime(new Date(row.lastSeenAt)) : "—"
        } style={{ fontSize: 12, color: "#64748b", width: 140 }} />
        <Column header="Estado" body={(row: VlanExplorerItem) => (
          <StatusChip
            label={row.isRegistered ? "Cadastrada" : "Não cadastrada"}
            background={row.isRegistered ? "#dcfce7" : "#fef3c7"}
            color={row.isRegistered ? "#16a34a" : "#d97706"}
            fontSize={10}
          />
        )} style={{ width: 130 }} />
      </DataTablePanel>

      {discovered.length > 0 && (
        <div style={{ marginTop: 20 }}>
          <div style={{ fontSize: 13, fontWeight: 700, color: "#1e293b", marginBottom: 8 }}>
            VLANs descobertas (presentes em leases DHCP, sem cadastro)
          </div>
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            {discovered.map((vlan) => (
              <StatusChip
                key={vlan.vlanNumber}
                label={`VLAN ${vlan.vlanNumber} · ${vlan.discoveredDeviceCount} disp.`}
                background="#fef3c7"
                color="#b45309"
                fontSize={11}
              />
            ))}
          </div>
        </div>
      )}

      {totalCount >= VLAN_PAGE_SIZE && (
        <div style={{ display: "flex", gap: 8, marginTop: 12, justifyContent: "flex-end" }}>
          <Button variant="ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} style={{ padding: "6px 12px", fontSize: 12, color: "#64748b" }}>
            Anterior
          </Button>
          <Button variant="ghost" onClick={() => setPage((p) => p + 1)} style={{ padding: "6px 12px", fontSize: 12, color: "#64748b" }}>
            Próxima
          </Button>
        </div>
      )}
    </>
  );
}

export function NetworkExplorerPage() {
  const [tab, setTab] = useState<ExplorerTab>("mac");
  const [searchParams] = useSearchParams();
  const [termInput, setTermInput] = useState("");
  const [term, setTerm] = useState("");
  const [result, setResult] = useState<MacLookupResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searched, setSearched] = useState(false);

  async function search(nextTerm: string) {
    setTerm(nextTerm);
    setSearched(true);
    if (nextTerm.trim().length < 2) {
      setResult(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      setResult(await lookupMac(nextTerm.trim()));
    } catch (caught) {
      setResult(null);
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }

  // Deep-link da busca global (?term=MAC/IP/hostname) — F3.
  useEffect(() => {
    const deepTerm = searchParams.get("term")?.trim();
    if (deepTerm && deepTerm.length >= 2) {
      setTermInput(deepTerm);
      void search(deepTerm);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function applyTerm(event: FormEvent) {
    event.preventDefault();
    void search(termInput);
  }

  const sightings = result?.sightings ?? [];

  return (
    <ListPageShell
      title="Network Explorer"
      singular="registro"
      plural="registros"
      total={sightings.length}
      loading={loading}
      note={term ? `busca "${term}"` : undefined}
      error={error}
      actions={
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
          <div style={{ display: "inline-flex", border: "1px solid #e2e8f0", borderRadius: 6, overflow: "hidden" }}>
            {EXPLORER_TABS.map((key) => (
              <button
                key={key}
                type="button"
                onClick={() => setTab(key)}
                style={{
                  padding: "6px 14px",
                  fontSize: 12,
                  fontWeight: 600,
                  border: "none",
                  cursor: "pointer",
                  background: tab === key ? "#1e293b" : "white",
                  color: tab === key ? "white" : "#64748b",
                }}
              >
                {key === "mac" ? "MAC / IP" : key === "vlans" ? "VLANs" : "APs / ACs"}
              </button>
            ))}
          </div>
          {tab === "mac" && (
            <FilterBar>
              <form onSubmit={applyTerm} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                <SearchInput
                  value={termInput}
                  onChange={setTermInput}
                  placeholder="MAC, IP, hostname ou VLAN"
                  title="Busca unificada — aplicada no servidor"
                  variant="muted"
                  onEnter={() => void search(termInput)}
                />
                <Button
                  type="submit"
                  variant="ghost"
                  style={{ padding: "6px 12px", borderColor: "#e2e8f0", fontSize: 13, color: "#64748b" }}
                >
                  Pesquisar
                </Button>
                {term && (
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => {
                      setTermInput("");
                      setTerm("");
                      setResult(null);
                      setSearched(false);
                    }}
                    style={{ padding: "6px 12px", borderColor: "#e2e8f0", fontSize: 13, color: "#64748b" }}
                  >
                    Limpar
                  </Button>
                )}
              </form>
            </FilterBar>
          )}
        </div>
      }
    >
      {tab === "vlans" ? (
        <VlanExplorerView />
      ) : tab === "aps" ? (
        <ApExplorerView />
      ) : (
      <>
      {!searched && (
        <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 24, textAlign: "center", color: "#64748b", fontSize: 13 }}>
          Pesquise por MAC, IP, hostname ou VLAN para localizar o equipamento na infraestrutura.
        </div>
      )}

      {searched && result && sightings.length === 0 && !loading && (
        <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 24, textAlign: "center" }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: "#1e293b", marginBottom: 6 }}>Nenhuma evidência encontrada</div>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Nenhuma fonte de descoberta (tabela MAC, ARP, DHCP, LLDP) registrou "{term}".
            Ausência de evidência <strong>não significa</strong> que o equipamento esteja offline —
            ele pode estar em uma rede ainda não descoberta ou em um equipamento sem SNMP.
          </div>
        </div>
      )}

      {result && sightings.length > 0 && (
        <>
          {/* Resumo do que se sabe sobre o termo */}
          <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: 16, marginBottom: 16 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12, flexWrap: "wrap" }}>
              <span style={{ fontSize: 14, fontWeight: 700, color: "#1e293b" }}>Resumo</span>
              {result.isStale && (
                <StatusChip label="Registro antigo" background="#fef3c7" color="#b45309" fontSize={10} fontWeight={600} />
              )}
              {result.wifiClient && (
                <StatusChip
                  label={`Wi-Fi${result.wifiApName ? ` · ${result.wifiApName}` : ""}`}
                  background="#eff6ff"
                  color="#2563eb"
                  fontSize={10}
                  fontWeight={600}
                />
              )}
            </div>
            <div style={{ display: "flex", gap: 20, flexWrap: "wrap" }}>
              <SummaryField label="MAC" value={result.matchedMac} mono />
              <SummaryField label="IP" value={result.ip} mono />
              <SummaryField label="Hostname" value={result.hostname} />
              <SummaryField label="VLAN" value={result.vlan} mono />
              {result.wifiSsid && <SummaryField label="SSID" value={result.wifiSsid} />}
              {result.wifiSignal != null && <SummaryField label="Sinal Wi-Fi" value={`${result.wifiSignal} dBm`} />}
              <SummaryField
                label="Última identificação"
                value={result.lastSeenAt ? formatDateTime(new Date(result.lastSeenAt)) : null}
              />
            </div>
          </div>

          {/* Evidências por fonte */}
          <DataTablePanel
            value={sightings}
            dataKey={(row: MacLookupSighting) => `${row.kind}|${row.seenAt}|${row.mac ?? ""}|${row.port ?? ""}`}
            emptyMessage="—"
          >
            <Column
              header="Origem"
              body={(row: MacLookupSighting) => {
                const style = kindStyle[row.kind] ?? { bg: "#f1f5f9", color: "#64748b" };
                return <StatusChip label={row.kindLabel} background={style.bg} color={style.color} />;
              }}
              style={{ width: 140 }}
            />
            <Column
              header="Switch / equipamento"
              body={(row: MacLookupSighting) =>
                row.deviceId ? (
                  <Link to={`/devices/${row.deviceId}`} style={{ fontSize: 12, fontWeight: 500, color: "#2563eb", textDecoration: "none" }}>
                    {row.deviceName ?? row.deviceId}
                  </Link>
                ) : (
                  <span style={{ fontSize: 12, color: "#94a3b8" }}>{row.deviceName ?? "—"}</span>
                )
              }
            />
            <Column
              header="Porta"
              body={(row: MacLookupSighting) => (
                <div>
                  <div style={{ fontFamily: "monospace", fontSize: 12 }}>{row.port ?? "—"}</div>
                  {row.portDescription && (
                    <div style={{ fontSize: 10, color: "#94a3b8" }}>{row.portDescription}</div>
                  )}
                </div>
              )}
            />
            <Column header="MAC" body={(row: MacLookupSighting) => <span style={{ fontFamily: "monospace", fontSize: 12 }}>{row.mac ?? "—"}</span>} />
            <Column header="IP" body={(row: MacLookupSighting) => <span style={{ fontFamily: "monospace", fontSize: 12 }}>{row.ip ?? "—"}</span>} />
            <Column header="Hostname" body={(row: MacLookupSighting) => row.hostname ?? "—"} style={{ fontSize: 12, color: "#64748b" }} />
            <Column header="VLAN" body={(row: MacLookupSighting) => row.vlan ?? "—"} style={{ fontFamily: "monospace", fontSize: 12, width: 70 }} />
            <Column header="Visto em" body={(row: MacLookupSighting) => formatDateTime(new Date(row.seenAt))} style={{ fontSize: 12, color: "#64748b", width: 140 }} />
            <Column header="Idade" body={(row: MacLookupSighting) => (row.ageDays === 0 ? "hoje" : `${row.ageDays}d`)} style={{ fontSize: 12, color: "#94a3b8", width: 60 }} />
          </DataTablePanel>
        </>
      )}
      </>
      )}
    </ListPageShell>
  );
}
