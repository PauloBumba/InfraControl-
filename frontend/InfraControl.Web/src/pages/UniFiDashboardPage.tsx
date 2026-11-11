import { useQuery } from "@tanstack/react-query";
import { Column } from "primereact/column";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { formatDateTime } from "@/utils/format";
import {
  getUniFiDashboard,
  getUniFiCorrelation,
  listUniFiSyncRuns,
  type UniFiSyncRun,
  type UniFiUnlinkedDevice,
} from "@/services/api/unifi";

/**
 * F26 Fase 8 — dashboard UniFi (rota `/unifi/dashboard`, Operate).
 * Cards trazem só dado real: persistido (sites/devices/vínculo/audit) ou
 * ao vivo (estados/clientes/portas — rotulado). Controller fora do ar e
 * dados desatualizados ficam visíveis; zero ≠ erro (8.2/8.3).
 *
 * Layout no padrão do ZabbixOverviewPage: cards de resumo → chips de
 * estado dos dispositivos → dispositivos sem vínculo → últimas
 * sincronizações. As duas consultas auxiliares falham de forma
 * segregada (seção some com aviso, nunca confunde erro com "vazio").
 */
export function UniFiDashboardPage() {
  const dashboard = useQuery({
    queryKey: ["unifi-dashboard"],
    queryFn: getUniFiDashboard,
    refetchInterval: 60_000,
  });

  const correlation = useQuery({
    queryKey: ["unifi-correlation"],
    queryFn: getUniFiCorrelation,
    refetchInterval: 60_000,
  });

  const syncRuns = useQuery({
    queryKey: ["unifi-sync-runs"],
    queryFn: () => listUniFiSyncRuns(10),
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

  const stateEntries = Object.entries(d.deviceStates).sort(
    (a, b) => b[1] - a[1] || a[0].localeCompare(b[0]),
  );
  const unlinked = correlation.data?.unlinked ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
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
        <NoticeBanner
          message={
            d.lastSyncAtUtc
              ? `Dados possivelmente desatualizados — última sincronização em ${new Date(d.lastSyncAtUtc).toLocaleString("pt-BR")}`
              : "Nenhuma sincronização registrada ainda"
          }
          background="#fef9c3"
          color="#854d0e"
          onDismiss={undefined}
        />
      )}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))",
          gap: 12,
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
          sub={d.unlinked > 0 ? "ver tabela abaixo" : undefined}
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

      <section className="uni-settings-section">
        <h3>Dispositivos por estado (controlador)</h3>
        {!d.controllerReachable ? (
          <p style={{ fontSize: 13, color: "#991b1b" }}>
            Estado desconhecido — o controlador não respondeu. Não confundir com zero.
          </p>
        ) : stateEntries.length === 0 ? (
          <p style={{ fontSize: 13, color: "#6b7280" }}>
            Nenhum device no controlador.
          </p>
        ) : (
          <div className="flex align-items-center gap-2 flex-wrap">
            {stateEntries.map(([state, count]) => (
              <StatusChip
                key={state}
                label={`${state}: ${count}`}
                background={stateChipStyle(state).background}
                color={stateChipStyle(state).color}
              />
            ))}
          </div>
        )}
      </section>

      <section className="uni-settings-section">
        <h3>Dispositivos sem vínculo com Asset</h3>
        {correlation.isLoading ? (
          <LoadingBlock />
        ) : correlation.isError ? (
          <ErrorBanner
            message="Não foi possível carregar a correlação UniFi — a tabela pode estar desatualizada."
            onRetry={() => void correlation.refetch()}
          />
        ) : unlinked.length === 0 ? (
          <p style={{ fontSize: 13, color: "#6b7280" }}>
            Nenhum device do controller sem vínculo — tudo correlacionado.
          </p>
        ) : (
          <DataTablePanel
            panel={false}
            value={unlinked}
            dataKey="mac"
            emptyMessage="Nenhum device sem vínculo"
          >
            <Column
              header="Nome"
              body={(device: UniFiUnlinkedDevice) => device.name ?? device.mac}
              style={{ fontWeight: 600, fontSize: 13, minWidth: 160 }}
            />
            <Column
              header="MAC"
              body={(device: UniFiUnlinkedDevice) => (
                <span style={{ fontFamily: "monospace", fontSize: 11 }}>{device.mac}</span>
              )}
              style={{ width: 160 }}
            />
            <Column header="Modelo" body={(device: UniFiUnlinkedDevice) => device.model ?? "—"} style={{ width: 140 }} />
            <Column header="Tipo" body={(device: UniFiUnlinkedDevice) => device.type ?? "—"} style={{ width: 120 }} />
            <Column
              header="Estado"
              body={(device: UniFiUnlinkedDevice) =>
                device.state ? (
                  <StatusChip
                    label={device.state}
                    background={stateChipStyle(device.state).background}
                    color={stateChipStyle(device.state).color}
                  />
                ) : (
                  "—"
                )
              }
              style={{ width: 110 }}
            />
          </DataTablePanel>
        )}
      </section>

      <section className="uni-settings-section">
        <h3>Últimas sincronizações</h3>
        {syncRuns.isLoading ? (
          <LoadingBlock />
        ) : syncRuns.isError ? (
          <ErrorBanner
            message="Não foi possível carregar o histórico de sincronizações."
            onRetry={() => void syncRuns.refetch()}
          />
        ) : (
          <DataTablePanel
            panel={false}
            value={syncRuns.data ?? []}
            dataKey="id"
            emptyMessage="Nenhuma sincronização registrada"
          >
            <Column
              header="Início"
              body={(run: UniFiSyncRun) => formatDateTime(new Date(run.startedAtUtc))}
              style={{ width: 170 }}
            />
            <Column
              header="Resultado"
              body={(run: UniFiSyncRun) => (
                <StatusChip
                  label={run.succeeded ? "Sucesso" : "Falha"}
                  background={run.succeeded ? "#dcfce7" : "#fee2e2"}
                  color={run.succeeded ? "#16a34a" : "#dc2626"}
                />
              )}
              style={{ width: 110 }}
            />
            <Column header="Criados" field="created" style={{ width: 80 }} />
            <Column header="Atualizados" field="updated" style={{ width: 100 }} />
            <Column header="Erros" field="errors" style={{ width: 80 }} />
            <Column
              header="Mensagem"
              body={(run: UniFiSyncRun) => (
                <span style={{ fontSize: 12, color: "#64748b" }}>{run.message ?? "—"}</span>
              )}
              style={{ minWidth: 200 }}
            />
          </DataTablePanel>
        )}
      </section>
    </div>
  );
}

/** Cor do chip por estado do controller — up=verde, offline/down=vermelho. */
function stateChipStyle(state: string): { background: string; color: string } {
  const s = state.toLowerCase();
  if (s === "up" || s === "online" || s === "connected")
    return { background: "#dcfce7", color: "#16a34a" };
  if (s === "offline" || s === "down" || s === "disconnected")
    return { background: "#fee2e2", color: "#dc2626" };
  if (s === "provisioning" || s === "updating")
    return { background: "#fef9c3", color: "#854d0e" };
  return { background: "#f1f5f9", color: "#64748b" };
}

function Card({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div className="uni-panel" style={{ padding: 14 }}>
      <div style={{ fontSize: 10, color: "#9ca3af", textTransform: "uppercase" }}>{label}</div>
      <div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1.2, wordBreak: "break-word" }}>{value}</div>
      {sub && <div style={{ fontSize: 10, color: "#9ca3af" }}>{sub}</div>}
    </div>
  );
}
