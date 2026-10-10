import { useState, type CSSProperties } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import { mutationErrorMessage } from "@/hooks/queries";
import {
  getUnifiLiveClients,
  getUnifiLivePorts,
  getUnifiNetworks,
  getUnifiTopology,
} from "@/services/api/unifi";
import { listDevices } from "@/services/api/inventory";
import { SelectField } from "@/components/ui/SelectField";
import { UniFiTopologyMap } from "@/components/ui/UniFiTopologyMap";

/**
 * F26 Fase 4 — clientes conectados e redes do controller UniFi AO VIVO.
 * Dados consultados sob demanda (stat/sta + rest/networkconf) e nunca
 * persistidos (REGRA 8/9) — "desconectado" é ausência nesta consulta.
 */
export function UniFiLivePage() {
  const [term, setTerm] = useState("");
  const [portsMac, setPortsMac] = useState("");

  const clients = useQuery({
    queryKey: ["unifi-live-clients"],
    queryFn: getUnifiLiveClients,
    refetchInterval: 30_000,
  });

  const networks = useQuery({
    queryKey: ["unifi-networks"],
    queryFn: getUnifiNetworks,
    refetchInterval: 60_000,
  });

  const inventoryDevices = useQuery({
    queryKey: ["unifi-live-inventory"],
    queryFn: async () => {
      const page = await listDevices({ pageSize: 100 });
      return page.items.filter((d) => d.mac);
    },
  });

  const switches = (inventoryDevices.data ?? []).filter((d) => d.type === "Switch");
  const macToId = new Map(
    (inventoryDevices.data ?? [])
      .filter((d) => d.mac)
      .map((d) => [d.mac as string, d.id]),
  );

  const topology = useQuery({
    queryKey: ["unifi-topology"],
    queryFn: getUnifiTopology,
    refetchInterval: 60_000,
  });

  const ports = useQuery({
    queryKey: ["unifi-live-ports", portsMac],
    queryFn: () => getUnifiLivePorts(portsMac),
    enabled: portsMac !== "",
    refetchInterval: 30_000,
  });

  const t = term.trim().toLowerCase();
  const filteredClients = (clients.data ?? []).filter((c) =>
    [c.mac, c.ip, c.hostname, c.essid, c.networkName]
      .filter(Boolean)
      .some((v) => String(v).toLowerCase().includes(t))
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="UniFi ao vivo"
        subtitle="Clientes conectados e redes — consulta direta ao controller (não é inventário)"
        actions={<SearchInput value={term} onChange={setTerm} placeholder="mac, ip, hostname, ssid" />}
      />

      {clients.isError && (
        <ErrorBanner
          message={mutationErrorMessage(clients.error) ?? "Falha ao consultar clientes."}
          onRetry={() => void clients.refetch()}
        />
      )}

      <section className="uni-settings-section">
        <h3>Clientes conectados ({filteredClients.length})</h3>
        {clients.isLoading ? (
          <LoadingBlock />
        ) : filteredClients.length === 0 ? (
          <p style={{ fontSize: 13, color: "#64748b" }}>
            Nenhum cliente conectado (ou controller indisponível).
          </p>
        ) : (
          <table style={{ width: "100%", fontSize: 13, borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ textAlign: "left", color: "#64748b" }}>
                <th style={th}>MAC</th>
                <th style={th}>IP</th>
                <th style={th}>Hostname</th>
                <th style={th}>SSID/Rede</th>
                <th style={th}>Sinal</th>
                <th style={th}>Via</th>
              </tr>
            </thead>
            <tbody>
              {filteredClients.map((c) => (
                <tr key={c.mac} style={{ borderTop: "1px solid #e5e7eb" }}>
                  <td style={td}>{c.mac}</td>
                  <td style={td}>{c.ip ?? "—"}</td>
                  <td style={td}>{c.hostname || "—"}</td>
                  <td style={td}>{c.essid ?? c.networkName ?? "—"}</td>
                  <td style={td}>{c.signal !== null ? `${c.signal} dBm` : "—"}</td>
                  <td style={td}>
                    {c.isWired ? (
                      <StatusChip label="cabeado" background="#f5f3ff" color="#6d28d9" />
                    ) : (
                      <StatusChip label={`AP ${c.apMac ?? "?"}`} background="#f0fdf4" color="#15803d" />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {networks.isError && (
        <ErrorBanner
          message={mutationErrorMessage(networks.error) ?? "Falha ao consultar redes."}
          onRetry={() => void networks.refetch()}
        />
      )}

      <section className="uni-settings-section">
        <h3 style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
          Portas de switch (ao vivo)
          <span style={{ minWidth: 260 }}>
            <SelectField
              value={portsMac}
              onChange={setPortsMac}
              options={switches.map((d) => ({
                value: d.mac ?? "",
                label: `${d.name} (${d.mac})`,
              }))}
              placeholder="Selecione um switch com MAC"
              style={{ width: "100%" }}
            />
          </span>
        </h3>
        {ports.isLoading ? (
          <LoadingBlock />
        ) : ports.isError ? (
          <ErrorBanner
            message={mutationErrorMessage(ports.error) ?? "Falha ao consultar portas."}
            onRetry={() => void ports.refetch()}
          />
        ) : portsMac === "" ? (
          <p style={{ fontSize: 13, color: "#64748b" }}>
            Escolha um switch para ver o port_table do controller (não é o cadastro de portas).
          </p>
        ) : (ports.data ?? []).length === 0 ? (
          <p style={{ fontSize: 13, color: "#64748b" }}>
            O controller não retornou portas para este device.
          </p>
        ) : (
          <table style={{ width: "100%", fontSize: 13, borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ textAlign: "left", color: "#64748b" }}>
                <th style={th}>#</th>
                <th style={th}>Nome</th>
                <th style={th}>Link</th>
                <th style={th}>Velocidade</th>
                <th style={th}>VLAN</th>
                <th style={th}>PoE</th>
                <th style={th}>Cliente</th>
              </tr>
            </thead>
            <tbody>
              {(ports.data ?? []).map((p) => (
                <tr key={p.portIndex} style={{ borderTop: "1px solid #e5e7eb" }}>
                  <td style={td}>{p.portIndex}</td>
                  <td style={td}>{p.name}</td>
                  <td style={td}>
                    {p.linkUp ? (
                      <StatusChip label="up" background="#f0fdf4" color="#15803d" />
                    ) : (
                      <StatusChip label="down" background="#fee2e2" color="#991b1b" />
                    )}
                  </td>
                  <td style={td}>
                    {p.speed ? `${p.speed}${p.fullDuplex ? " full" : ""}` : "—"}
                  </td>
                  <td style={td}>{p.portVlan ?? "—"}</td>
                  <td style={td}>
                    {p.poeEnable ? (p.poeStatus ?? "sim") : "—"}
                  </td>
                  <td style={td}>{p.clientMac ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="uni-settings-section">
        <h3>Topologia ao vivo ({(topology.data?.links ?? []).length} relações)</h3>
        <p style={{ fontSize: 12, color: "#64748b", margin: "0 0 8px" }}>
          Legenda — <b>confirmed</b>: uplink reportado pelo próprio controller;
          <b>inferred</b>: vizinhança LLDP (não autoral); <b>unknown</b>: alvo fora do
          inventário (ex.: ISP/mesh). Relações nunca são persistidas (REGRA 8).
        </p>
        {topology.isLoading ? (
          <LoadingBlock />
        ) : topology.isError ? (
          <ErrorBanner
            message={mutationErrorMessage(topology.error) ?? "Falha ao consultar topologia."}
            onRetry={() => void topology.refetch()}
          />
        ) : (topology.data?.nodes ?? []).length === 0 ? (
          <p style={{ fontSize: 13, color: "#64748b" }}>
            O controller não expôs devices nesta consulta.
          </p>
        ) : (
          <>
          <UniFiTopologyMap topology={topology.data as NonNullable<typeof topology.data>} macToId={macToId} />
          {(topology.data?.links ?? []).length === 0 && (
            <p style={{ fontSize: 13, color: "#64748b", marginTop: 8 }}>
              O controller não expôs relações de uplink/LLDP nesta consulta.
            </p>
          )}
          {(topology.data?.links ?? []).length > 0 && (
          <table style={{ width: "100%", fontSize: 13, borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ textAlign: "left", color: "#64748b" }}>
                <th style={th}>Origem</th>
                <th style={th}>Porta</th>
                <th style={th}>Destino</th>
                <th style={th}>Tipo</th>
                <th style={th}>Confiança</th>
                <th style={th}>Fonte</th>
              </tr>
            </thead>
            <tbody>
              {(topology.data?.links ?? []).map((l, i) => (
                <tr key={`${l.sourceMac}-${l.targetMac}-${i}`} style={{ borderTop: "1px solid #e5e7eb" }}>
                  <td style={td}>{l.sourceName ?? l.sourceMac}</td>
                  <td style={td}>{l.sourcePort ?? "—"}</td>
                  <td style={td}>
                    {l.targetName ?? l.targetMac}
                    {!l.targetName && (
                      <span style={{ color: "#94a3b8" }}> (externo)</span>
                    )}
                  </td>
                  <td style={td}>{l.linkType}</td>
                  <td style={td}>
                    {l.confidence === "confirmed" ? (
                      <StatusChip label="confirmed" background="#f0fdf4" color="#15803d" />
                    ) : l.confidence === "inferred" ? (
                      <StatusChip label="inferred" background="#fef9c3" color="#854d0e" />
                    ) : (
                      <StatusChip label="unknown" background="#f1f5f9" color="#475569" />
                    )}
                  </td>
                  <td style={td}>{l.source}</td>
                </tr>
              ))}
            </tbody>
          </table>
          )}
          </>
        )}
        {(topology.data?.nodes ?? []).filter((n) => n.isKnownDevice && !n.kind.includes("gateway")).length > 0 &&
          (topology.data?.links ?? []).length === 0 && (
            <p style={{ fontSize: 12, color: "#64748b", marginTop: 8 }}>
              Devices sem relação conhecida:{" "}
              {(topology.data?.nodes ?? [])
                .filter((n) => n.isKnownDevice)
                .map((n) => n.name ?? n.mac)
                .join(", ")}
            </p>
          )}
      </section>

      <section className="uni-settings-section">
        <h3>Redes e VLANs ({(networks.data ?? []).length})</h3>
        {networks.isLoading ? (
          <LoadingBlock />
        ) : (networks.data ?? []).length === 0 ? (
          <p style={{ fontSize: 13, color: "#64748b" }}>Nenhuma rede retornada.</p>
        ) : (
          <table style={{ width: "100%", fontSize: 13, borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ textAlign: "left", color: "#64748b" }}>
                <th style={th}>Nome</th>
                <th style={th}>Propósito</th>
                <th style={th}>VLAN</th>
                <th style={th}>Estado</th>
              </tr>
            </thead>
            <tbody>
              {(networks.data ?? []).map((n) => (
                <tr key={n.id} style={{ borderTop: "1px solid #e5e7eb" }}>
                  <td style={td}>{n.name}</td>
                  <td style={td}>{n.purpose || "—"}</td>
                  <td style={td}>{n.vlanId ?? "—"}</td>
                  <td style={td}>
                    {n.enabled ? (
                      <StatusChip label="habilitada" background="#f0fdf4" color="#15803d" />
                    ) : (
                      <StatusChip label="desabilitada" background="#fee2e2" color="#991b1b" />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

const th: CSSProperties = { padding: "6px 8px", fontWeight: 600, fontSize: 12 };
const td: CSSProperties = { padding: "6px 8px", color: "#374151" };
