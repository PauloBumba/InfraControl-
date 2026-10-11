import { useMemo } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { useObjectQuery } from "@/hooks/queries";
import {
  getUnifiInsights,
  type UnifiClientTrafficItem,
  type UnifiDpiAppItem,
} from "@/services/api/integrations";
import { formatDateTime } from "@/utils/format";

/**
 * F18-T007 (decisão de 2026-10-09) — InsightsPage conectada ao backend.
 *
 * Substitui os estados vazios do F17-T016 ("N/A de contrato") por dados
 * REAIS: `GET /integrations/unifi/insights` sonda o UniFi Controller ao
 * vivo (docs/29 §Insights) e devolve os dois painéis que existem de
 * verdade — DPI por aplicação (`/stat/app`) e Tráfego por Cliente
 * (`/stat/sta`). Nada é persistido (regra 8 da integração): os números
 * são contadores acumulados desde o reset do cliente, não taxa por
 * período — por isso o rodapé diz "sondagem ao vivo" com o horário.
 *
 * O painel Histórico de Speed Test continua com estado vazio honesto:
 * não há coletor nem domínio de speed test — escopo adiado na mesma
 * task (decisão humana registrada em TASKS.md/PROJECT_STATE.md).
 */

/** Bytes → texto legível (KB/MB/GB/TB, base 1024). */
function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  const index = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    units.length - 1
  );
  const value = bytes / Math.pow(1024, index);
  return `${value >= 100 ? Math.round(value) : value.toFixed(1)} ${units[index]}`;
}

function clientName(client: UnifiClientTrafficItem): string {
  return client.hostname || client.mac || "Cliente sem nome";
}

/** Categoria do app no quadro — o campo vazio vira "—" (não some). */
function dpiCategoryKey(app: UnifiDpiAppItem): string {
  return app.category || "—";
}

/** Colunas do quadro DPI — saem dos dados carregados (categoria é string livre). */
function dpiCategoryColumns(apps: readonly UnifiDpiAppItem[]) {
  const counts = new Map<string, number>();
  apps.forEach((app) => counts.set(dpiCategoryKey(app), (counts.get(dpiCategoryKey(app)) ?? 0) + 1));
  return [...counts.entries()]
    .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
    .map(([key]) => ({ key, label: key }));
}

/**
 * Faixa de tráfego por TERCIL: divide o conjunto carregado em 3 partes de
 * tamanho quase igual (menor / intermediário / maior tráfego).
 *
 * O corte sai dos próprios dados — não existe constante de bytes inventada
 * — e o recorte é estável porque esta página carrega a lista inteira, sem
 * paginação: os grupos não mudam conforme o usuário navega.
 */
function trafficTercileByMac(clients: readonly UnifiClientTrafficItem[]): Map<string, string> {
  const sorted = [...clients].sort((a, b) => a.totalBytes - b.totalBytes);
  const total = sorted.length;
  const groups = new Map<string, string>();
  sorted.forEach((client, index) => {
    groups.set(
      client.mac,
      index < total / 3 ? "low" : index < (2 * total) / 3 ? "mid" : "high"
    );
  });
  return groups;
}

const TRAFFIC_TERCILE_COLUMNS = [
  { key: "high", label: "Maior tráfego" },
  { key: "mid", label: "Tráfego intermediário" },
  { key: "low", label: "Menor tráfego" },
];

export function InsightsPage() {
  const insights = useObjectQuery(["unifi-insights"], getUnifiInsights);
  const loading = insights.isLoading;
  const error = insights.error ? insights.error.message : null;
  const data = insights.data;

  // Adoção do padrão de visões (backlog Operations UX 2026-10-11): esta
  // página não é um índice — são 2 painéis de métrica distintos. Cada
  // painel ganha seu PRÓPRIO seletor no cabeçalho, com preferência própria
  // no localStorage; um seletor global não teria sobre o que agir.
  const [dpiView, setDpiView] = useEntityViewMode("insights-dpi", "table");
  const [trafficView, setTrafficView] = useEntityViewMode("insights-traffic", "table");

  const trafficClients = useMemo(() => data?.trafficPerClient ?? [], [data]);
  const dpiApps = useMemo(() => data?.dpiPerApplication ?? [], [data]);
  const trafficTercile = useMemo(() => trafficTercileByMac(trafficClients), [trafficClients]);

  const emptyStyle = {
    textAlign: "center" as const,
    padding: 24,
    color: "#94a3b8",
    fontSize: 13,
  };

  /** Card de aplicação DPI — usado nas visões grid E quadro. */
  function renderDpiCard(app: UnifiDpiAppItem) {
    return (
      <div
        style={{
          background: "white",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
          padding: 12,
          display: "flex",
          flexDirection: "column",
          gap: 4,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
          <span style={{ fontSize: 13, fontWeight: 600 }}>{app.app}</span>
          <StatusChip
            label={dpiCategoryKey(app)}
            background="#f5f3ff"
            color="#7c3aed"
            fontSize={10}
            fontWeight={600}
          />
        </div>
        <div style={{ fontSize: 12, color: "#64748b" }}>
          Tráfego inspecionado: {formatBytes(app.totalBytes)}
        </div>
      </div>
    );
  }

  /** Card de cliente — usado nas visões grid E quadro. */
  function renderTrafficCard(client: UnifiClientTrafficItem) {
    return (
      <div
        style={{
          background: "white",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
          padding: 12,
          display: "flex",
          flexDirection: "column",
          gap: 4,
        }}
      >
        <div style={{ fontSize: 13, fontWeight: 600 }}>{clientName(client)}</div>
        <div style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8" }}>
          {client.ip || client.mac}
        </div>
        <div style={{ fontSize: 12, fontWeight: 500 }}>{formatBytes(client.totalBytes)}</div>
        <div style={{ fontSize: 10, color: "#94a3b8" }}>
          ↓ {formatBytes(client.rxBytes)} · ↑ {formatBytes(client.txBytes)}
        </div>
        <div style={{ fontSize: 11, color: "#64748b" }}>
          Última medição: {client.lastSeen ? formatDateTime(new Date(client.lastSeen)) : "—"}
        </div>
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Insights"
        subtitle={
          data
            ? `Sondagem ao vivo no UniFi Controller · ${formatDateTime(new Date(data.capturedAtUtc))}`
            : "Análises de tráfego e DPI (UniFi) ao vivo"
        }
      />

      {error && <ErrorBanner message={error} onRetry={() => void insights.refetch()} />}

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Aplicações por Tráfego (DPI)</h2>
          <ViewSwitcher mode={dpiView} onChange={setDpiView} />
        </div>
        <div className="uni-panel-body">
          {loading ? (
            <LoadingBlock />
          ) : (
            <EntityView<UnifiDpiAppItem>
              mode={dpiView}
              items={dpiApps}
              getKey={(app) => app.app}
              emptyMessage={error ? "—" : "Nenhuma aplicação registrada pela inspeção de pacotes"}
              kanban={{ groupOf: dpiCategoryKey, columns: dpiCategoryColumns(dpiApps) }}
              renderCard={renderDpiCard}
              table={
            <DataTablePanel
              panel={false}
              value={data?.dpiPerApplication ?? []}
              dataKey="app"
              emptyMessage={error ? "—" : "Nenhuma aplicação registrada pela inspeção de pacotes"}
            >
              <Column header="Aplicação" body={(app: UnifiDpiAppItem) => app.app} />
              <Column
                header="Categoria"
                body={(app: UnifiDpiAppItem) => app.category || "—"}
                style={{ width: 180 }}
              />
              <Column
                header="Tráfego inspecionado"
                body={(app: UnifiDpiAppItem) => formatBytes(app.totalBytes)}
                style={{ width: 180, textAlign: "right", fontSize: 12 }}
              />
            </DataTablePanel>
              }
            />
          )}
        </div>
      </div>

      <div className="uni-grid-2">
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Tráfego por Cliente</h2>
            <ViewSwitcher mode={trafficView} onChange={setTrafficView} />
          </div>
          <div className="uni-panel-body">
            {loading ? (
              <LoadingBlock />
            ) : (
              <EntityView<UnifiClientTrafficItem>
                mode={trafficView}
                items={trafficClients}
                getKey={(client) => client.mac}
                emptyMessage={error ? "—" : "Nenhum cliente ativo no Controller"}
                kanban={{ groupOf: (client) => trafficTercile.get(client.mac) ?? "low", columns: TRAFFIC_TERCILE_COLUMNS }}
                renderCard={renderTrafficCard}
                table={
              <DataTablePanel
                panel={false}
                value={data?.trafficPerClient ?? []}
                dataKey="mac"
                emptyMessage={error ? "—" : "Nenhum cliente ativo no Controller"}
              >
                <Column
                  header="Cliente"
                  body={(client: UnifiClientTrafficItem) => (
                    <>
                      <div style={{ fontWeight: 500, fontSize: 13 }}>{clientName(client)}</div>
                      <div style={{ fontSize: 11, color: "#94a3b8" }}>
                        {client.ip || client.mac}
                      </div>
                    </>
                  )}
                />
                <Column
                  header="Tráfego total"
                  body={(client: UnifiClientTrafficItem) => (
                    <>
                      <div style={{ fontSize: 12, fontWeight: 500 }}>
                        {formatBytes(client.totalBytes)}
                      </div>
                      <div style={{ fontSize: 10, color: "#94a3b8" }}>
                        ↓ {formatBytes(client.rxBytes)} · ↑ {formatBytes(client.txBytes)}
                      </div>
                    </>
                  )}
                  style={{ width: 150, textAlign: "right" }}
                />
                <Column
                  header="Última medição"
                  body={(client: UnifiClientTrafficItem) =>
                    client.lastSeen ? formatDateTime(new Date(client.lastSeen)) : "—"
                  }
                  style={{ width: 150, fontSize: 12, color: "#64748b" }}
                />
              </DataTablePanel>
                }
              />
            )}
          </div>
        </div>

        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Histórico de Speed Test</h2>
          </div>
          <div className="uni-panel-body">
            <div style={emptyStyle}>
              Não implementado no backend — não há coletor nem execução de
              speed test (escopo adiado — F18-T007)
            </div>
          </div>
        </div>
      </div>

      {data && !loading && (
        <div style={{ fontSize: 11, color: "#94a3b8", textAlign: "right" }}>
          Contadores acumulados desde o último reset — não representam
          consumo de um período específico.
        </div>
      )}
    </div>
  );
}
