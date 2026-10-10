import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { StatusChip } from "@/components/ui/StatusChip";
import { getUniFiDashboard } from "@/services/api/unifi";

/**
 * F26 Fase 8 — dashboard UniFi (rota `/unifi/dashboard`, Operate).
 * Cards trazem só dado real: persistido (sites/devices/vínculo/audit) ou
 * ao vivo (estados/clientes/portas — rotulado). Controller fora do ar e
 * dados desatualizados ficam visíveis; zero ≠ erro (8.2/8.3).
 */
export function UniFiDashboardPage() {
  const dashboard = useQuery({
    queryKey: ["unifi-dashboard"],
    queryFn: getUniFiDashboard,
    refetchInterval: 60_000,
  });

  if (dashboard.isLoading) return <LoadingBlock />;
  if (dashboard.isError)
    return (
      <ErrorBanner
        message="Falha ao carregar o dashboard UniFi."
        onRetry={() => void dashboard.refetch()}
      />
    );

  const d = dashboard.data;
  if (!d) return <LoadingBlock />;

  return (
    <div>
      <PageHeader
        title="Dashboard UniFi"
        subtitle="Indicadores reais — persistido × ao vivo rotulados por origem."
      />

      {!d.controllerReachable && (
        <ErrorBanner
          message={`Controlador UniFi indisponível: ${d.controllerError ?? "sem detalhe"} — cards ao vivo não puderam ser atualizados.`}
          onRetry={() => void dashboard.refetch()}
        />
      )}
      {d.stale && (
        <StatusChip
          label={
            d.lastSyncAtUtc
              ? `Dados possivelmente desatualizados — última sincronização em ${new Date(d.lastSyncAtUtc).toLocaleString("pt-BR")}`
              : "Nenhuma sincronização registrada ainda"
          }
          background="#fef9c3"
          color="#854d0e"
        />
      )}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))",
          gap: 12,
          marginTop: 16,
        }}
      >
        <Card label="Sites (persistido)" value={d.sites} sub={d.sitesMissing > 0 ? `${d.sitesMissing} ausente(s) no controller` : undefined} />
        <Card label="Switches (persistido)" value={d.switches} />
        <Card label="Access Points (persistido)" value={d.accessPoints} />
        <Card
          label="Clientes (ao vivo)"
          value={d.controllerReachable ? d.clients : "—"}
          sub={d.controllerReachable ? "conectados agora" : "controlador indisponível"}
        />
        <Card
          label="Portas (ao vivo)"
          value={d.ports ? `${d.ports.up}/${d.ports.total}` : "—"}
          sub={d.ports ? "com link up / total" : "controlador indisponível"}
        />
        <Card
          label="Sem Asset associado"
          value={d.unlinked}
          sub={d.unlinked > 0 ? "ver Integrações" : undefined}
        />
        <Card
          label="Última sincronização"
          value={d.lastSyncAtUtc ? new Date(d.lastSyncAtUtc).toLocaleString("pt-BR") : "—"}
          sub={
            d.lastSyncSucceeded === null
              ? "sem registro"
              : d.lastSyncSucceeded
                ? "bem-sucedida"
                : "falhou"
          }
        />
        <Card
          label="Erros de sync (7 dias)"
          value={d.recentSyncErrors}
          sub={d.recentSyncErrors > 0 ? "audit — ver Auditoria" : undefined}
        />
      </div>

      <section className="uni-settings-section" style={{ marginTop: 16 }}>
        <h3>Dispositivos por estado (controlador)</h3>
        {!d.controllerReachable ? (
          <p style={{ fontSize: 13, color: "#991b1b" }}>
            Estado desconhecido — o controlador não respondeu. Não confundir com zero.
          </p>
        ) : Object.keys(d.deviceStates).length === 0 ? (
          <p style={{ fontSize: 13, color: "#6b7280" }}>
            Nenhum device no controlador.
          </p>
        ) : (
          <ul style={{ fontSize: 13, margin: 0, paddingLeft: 18 }}>
            {Object.entries(d.deviceStates).map(([state, count]) => (
              <li key={state}>
                Estado <code>{state}</code>: <strong>{count}</strong> device(s)
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Card({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div
      style={{
        background: "#fff",
        border: "1px solid #e5e7eb",
        borderRadius: 10,
        padding: 14,
        display: "flex",
        gap: 12,
        alignItems: "center",
      }}
    >
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 10, color: "#9ca3af", textTransform: "uppercase" }}>{label}</div>
        <div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1.2, wordBreak: "break-word" }}>{value}</div>
        {sub && <div style={{ fontSize: 10, color: "#9ca3af" }}>{sub}</div>}
      </div>
    </div>
  );
}
