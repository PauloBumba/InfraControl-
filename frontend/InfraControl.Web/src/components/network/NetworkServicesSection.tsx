import { Column } from "primereact/column";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { useObjectQuery } from "@/hooks/queries";
import {
  listDhcpLeases,
  listFirewallRules,
  listNetworkRoutes,
  type DhcpLeaseItem,
  type FirewallRuleItem,
  type NetworkRouteItem,
} from "@/services/api/network";

/**
 * F18-T005 (ADR-042) — abas DHCP/Rotas/Firewall restauradas na
 * NetworkPage (removidas no F17-T008 por falta de backend): leitura
 * SOMENTE-LEITURA do equipamento configurado (`NetworkServices:Target`)
 * — leases/ARP e rotas via SNMP, regras de firewall via comando show
 * por vendor (v1: Cisco). Snapshot ao vivo, sem persistência; com a
 * integração desligada/alcançável cada painel mostra o motivo honesto
 * (503 + motivo) em vez de dado inventado.
 */
export function NetworkServicesSection() {
  const leases = useObjectQuery(["network-services-dhcp"], listDhcpLeases);
  const routes = useObjectQuery(["network-services-routes"], listNetworkRoutes);
  const firewall = useObjectQuery(["network-services-firewall"], listFirewallRules);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div>
        <h2 style={{ fontSize: 14, fontWeight: 600 }}>Serviços de rede (leitura)</h2>
        <p style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>
          Snapshot ao vivo do equipamento configurado (NetworkServices:Target) —
          somente leitura, nada é alterado no device
        </p>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Leases DHCP / ARP</h2>
        </div>
        <div className="uni-panel-body">
          {leases.isLoading ? (
            <LoadingBlock />
          ) : leases.error ? (
            <ErrorBanner
              message={leases.error.message}
              onRetry={() => void leases.refetch()}
            />
          ) : (
            <DataTablePanel
              panel={false}
              value={leases.data ?? []}
              dataKey="ip"
              emptyMessage="Nenhuma entrada ativa no equipamento"
            >
              <Column
                header="IP"
                body={(lease: DhcpLeaseItem) => (
                  <span style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }}>
                    {lease.ip}
                  </span>
                )}
                style={{ width: 160 }}
              />
              <Column
                header="MAC"
                body={(lease: DhcpLeaseItem) => (
                  <span style={{ fontFamily: "monospace", fontSize: 12 }}>
                    {lease.mac || "—"}
                  </span>
                )}
                style={{ width: 180 }}
              />
              <Column
                header="Interface (ifIndex)"
                body={(lease: DhcpLeaseItem) => lease.ifIndex}
                style={{ width: 150, fontSize: 12 }}
              />
            </DataTablePanel>
          )}
        </div>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Rotas IP</h2>
        </div>
        <div className="uni-panel-body">
          {routes.isLoading ? (
            <LoadingBlock />
          ) : routes.error ? (
            <ErrorBanner
              message={routes.error.message}
              onRetry={() => void routes.refetch()}
            />
          ) : (
            <DataTablePanel
              panel={false}
              value={routes.data ?? []}
              dataKey="destination"
              emptyMessage="Nenhuma rota no equipamento"
            >
              <Column
                header="Destino"
                body={(route: NetworkRouteItem) => (
                  <span style={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }}>
                    {route.destination}
                  </span>
                )}
                style={{ width: 150 }}
              />
              <Column
                header="Máscara"
                body={(route: NetworkRouteItem) => (
                  <span style={{ fontFamily: "monospace", fontSize: 12 }}>
                    {route.mask || "—"}
                  </span>
                )}
                style={{ width: 140 }}
              />
              <Column
                header="Próximo salto"
                body={(route: NetworkRouteItem) => (
                  <span style={{ fontFamily: "monospace", fontSize: 12 }}>
                    {route.nextHop || "—"}
                  </span>
                )}
                style={{ width: 150 }}
              />
              <Column
                header="If"
                body={(route: NetworkRouteItem) => route.ifIndex}
                style={{ width: 60, fontSize: 12 }}
              />
              <Column
                header="Métrica"
                body={(route: NetworkRouteItem) => route.metric}
                style={{ width: 80, textAlign: "right", fontSize: 12 }}
              />
              <Column
                header="Tipo"
                body={(route: NetworkRouteItem) => route.type || "—"}
                style={{ width: 90, fontSize: 12 }}
              />
              <Column
                header="Protocolo"
                body={(route: NetworkRouteItem) => route.protocol || "—"}
                style={{ width: 100, fontSize: 12 }}
              />
            </DataTablePanel>
          )}
        </div>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Firewall (ACLs)</h2>
        </div>
        <div className="uni-panel-body">
          {firewall.isLoading ? (
            <LoadingBlock />
          ) : firewall.error ? (
            <ErrorBanner
              message={firewall.error.message}
              onRetry={() => void firewall.refetch()}
            />
          ) : (
            <DataTablePanel
              panel={false}
              value={firewall.data ?? []}
              dataKey={(rule) => `${rule.acl}-${rule.sequence}`}
              emptyMessage="Nenhuma regra no equipamento"
            >
              <Column
                header="ACL"
                body={(rule: FirewallRuleItem) => (
                  <>
                    <div style={{ fontSize: 12, fontWeight: 600 }}>{rule.acl}</div>
                    {rule.scope && (
                      <div style={{ fontSize: 10, color: "#9ca3af" }}>{rule.scope}</div>
                    )}
                  </>
                )}
                style={{ width: 170 }}
              />
              <Column
                header="Seq"
                body={(rule: FirewallRuleItem) => rule.sequence}
                style={{ width: 60, fontSize: 12 }}
              />
              <Column
                header="Ação"
                body={(rule: FirewallRuleItem) => (
                  <span
                    style={{
                      fontSize: 12,
                      fontWeight: 500,
                      color: rule.action === "permit" ? "#16a34a" : "#dc2626",
                    }}
                  >
                    {rule.action}
                  </span>
                )}
                style={{ width: 80 }}
              />
              <Column
                header="Protocolo"
                body={(rule: FirewallRuleItem) => rule.protocol || "—"}
                style={{ width: 90, fontSize: 12 }}
              />
              <Column
                header="Expressão (texto do device)"
                body={(rule: FirewallRuleItem) => (
                  <span style={{ fontFamily: "monospace", fontSize: 11 }}>
                    {rule.expression}
                  </span>
                )}
              />
            </DataTablePanel>
          )}
        </div>
      </div>
    </div>
  );
}
