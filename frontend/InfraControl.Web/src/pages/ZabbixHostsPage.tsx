import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
import { SelectField, type SelectOption } from "@/components/ui/SelectField";
import { SearchInput } from "@/components/ui/SearchInput";
import { FilterBar } from "@/components/ui/FilterBar";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { Column } from "primereact/column";
import { Pagination } from "@/components/ui/Pagination";
import { Can } from "@/components/auth/Can";
import { mutationErrorMessage } from "@/hooks/queries";
import { formatDateTime } from "@/utils/format";
import {
  getZabbixHosts,
  syncZabbixInventory,
  type ZabbixHostView,
  type ZabbixSyncResult,
} from "@/services/api/zabbix";

const PAGE_SIZE = 20;

const stateOptions: SelectOption[] = [
  { value: "all", label: "Todos os estados" },
  { value: "enabled", label: "Ativos no Zabbix" },
  { value: "disabled", label: "Desativados no Zabbix" },
];

const missingOptions: SelectOption[] = [
  { value: "all", label: "Todos" },
  { value: "present", label: "Com host no Zabbix" },
  { value: "missing", label: "Ausentes (removidos)" },
];

/**
 * Estado do host para o quadro — o mesmo recorte da coluna "Estado" e dos
 * filtros "Estado no Zabbix"/"Presença no Zabbix". "Ausente" vem SÓ de
 * host que sumiu do Zabbix, nunca de falha de consulta (regra da Fase 3.3).
 */
function hostStateKey(row: ZabbixHostView): string {
  return row.missing ? "missing" : row.enabled ? "enabled" : "disabled";
}

const HOST_STATE_COLUMNS = [
  { key: "enabled", label: "Ativo" },
  { key: "disabled", label: "Desativado" },
  { key: "missing", label: "Ausente" },
];

function formatUtc(value: string | null | undefined): string {
  return value ? formatDateTime(new Date(value)) : "—";
}

function listCell(values: string[]): React.ReactNode {
  if (values.length === 0) return "—";
  const shown = values.slice(0, 2).join(", ");
  return (
    <span title={values.join(", ")}>
      {shown}
      {values.length > 2 && <span style={{ color: "#94a3b8" }}> +{values.length - 2}</span>}
    </span>
  );
}

/**
 * F27 Fase 3.3 — Infraestrutura → Hosts monitorados.
 * Read-model sincronizado do Zabbix (`POST /sync`, política Operate) com
 * busca por nome/IP/hostid, filtros de estado/ausência, paginação no
 * servidor e flag `HasAsset` (host sem Asset correspondente). Falha de
 * consulta nunca é confundida com host offline: "Ausente" só vem de
 * host que sumiu do Zabbix, não de erro de conexão.
 */
export function ZabbixHostsPage() {
  const queryClient = useQueryClient();

  const [termInput, setTermInput] = useState("");
  const [term, setTerm] = useState("");
  const [state, setState] = useState("all");
  const [missing, setMissing] = useState("all");
  const [page, setPage] = useState(1);
  const [syncResult, setSyncResult] = useState<ZabbixSyncResult | null>(null);
  const [syncError, setSyncError] = useState<string | null>(null);

  // Adoção do padrão de visões (backlog Operations UX 2026-10-11): era só
  // tabela; o quadro agrupa por estado do host. Tabela = padrão.
  const [view, setView] = useEntityViewMode("zabbix-hosts", "table");

  const hosts = useQuery({
    queryKey: ["zabbix-hosts", term, state, missing, page],
    queryFn: () =>
      getZabbixHosts({
        term: term.trim() || undefined,
        enabled: state === "all" ? undefined : state === "enabled",
        missing: missing === "all" ? undefined : missing === "missing",
        page,
        pageSize: PAGE_SIZE,
      }),
  });

  const sync = useMutation({
    mutationFn: syncZabbixInventory,
    onSuccess: (result) => {
      setSyncError(null);
      setSyncResult(result);
      void queryClient.invalidateQueries({ queryKey: ["zabbix-hosts"] });
      void queryClient.invalidateQueries({ queryKey: ["zabbix-config"] });
    },
    onError: (error) => {
      setSyncResult(null);
      setSyncError(mutationErrorMessage(error as Error));
    },
  });

  function applyFilters(nextTerm: string, nextState: string, nextMissing: string) {
    setTerm(nextTerm.trim());
    setState(nextState);
    setMissing(nextMissing);
    setPage(1);
  }

  /** Card de host — usado nas visões grid E quadro (mesmos dados da tabela). */
  function renderHostCard(row: ZabbixHostView) {
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
          <span style={{ fontWeight: 600, fontFamily: "monospace", fontSize: 12 }}>{row.host}</span>
          <StatusChip
            label={row.hasAsset ? "Vinculado" : "Sem Asset"}
            background={row.hasAsset ? "#dcfce7" : "#fff7ed"}
            color={row.hasAsset ? "#166534" : "#c2410c"}
            fontSize={10}
            fontWeight={600}
          />
        </div>
        <div style={{ fontSize: 12 }}>{row.name || "—"}</div>
        <div style={{ fontFamily: "monospace", fontSize: 11, color: "#64748b" }}>
          {row.ip ? `${row.ip}${row.port ? `:${row.port}` : ""}` : "—"}
        </div>
        <div className="flex align-items-center gap-1 flex-wrap">
          {row.missing ? (
            <StatusChip label="Ausente" background="#fee2e2" color="#b91c1c" fontSize={10} fontWeight={600} title="Removido do Zabbix — registro preservado no InfraControl" />
          ) : row.enabled ? (
            <StatusChip label="Ativo" background="#dcfce7" color="#166534" fontSize={10} fontWeight={600} />
          ) : (
            <StatusChip label="Desativado" background="#f1f5f9" color="#64748b" fontSize={10} fontWeight={600} />
          )}
          {row.discovered && (
            <StatusChip label="Descoberto" background="#eff6ff" color="#1d4ed8" fontSize={10} fontWeight={600} />
          )}
        </div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>Grupos: {listCell(row.groups)}</div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>Templates: {listCell(row.templates)}</div>
        <div style={{ fontSize: 11, color: "#94a3b8" }}>
          Sincronizado: {formatUtc(row.lastSyncedAtUtc)}
          {row.missing && ` · ausente desde ${formatUtc(row.missingSinceUtc)}`}
        </div>
      </div>
    );
  }

  const data = hosts.data;
  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Hosts monitorados"
        subtitle={
          hosts.isLoading
            ? "Carregando…"
            : `${totalCount} ${totalCount === 1 ? "host" : "hosts"} no read-model do Zabbix`
        }
        actions={
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
            <ViewSwitcher mode={view} onChange={setView} />
            <Can level="operate">
              <Button
                variant="primary"
                disabled={sync.isPending}
                style={{ padding: "6px 14px" }}
                onClick={() => sync.mutate()}
              >
                <i className="pi pi-refresh" style={{ fontSize: 11 }} />
                {sync.isPending ? "Sincronizando…" : "Sincronizar agora"}
              </Button>
            </Can>
          </div>
        }
      />

      {syncResult && (
        <NoticeBanner
          message={`Sincronização concluída · versão ${syncResult.version ?? "?"} · ${syncResult.created} novos, ${syncResult.updated} atualizados, ${syncResult.missing} ausentes${syncResult.errors > 0 ? `, ${syncResult.errors} erros` : ""}`}
          onDismiss={() => setSyncResult(null)}
        />
      )}

      {(hosts.error || syncError) && (
        <ErrorBanner
          message={
            syncError ??
            mutationErrorMessage(hosts.error as Error | null) ??
            "Não foi possível falar com o servidor. Tente novamente."
          }
          onRetry={() => {
            setSyncError(null);
            void hosts.refetch();
          }}
        />
      )}

      <FilterBar>
        <div className="flex align-items-center gap-2 flex-wrap">
          <SearchInput
            value={termInput}
            onChange={setTermInput}
            placeholder="Buscar por nome, IP ou hostid…"
            width={260}
            onEnter={() => applyFilters(termInput, state, missing)}
          />
          <SelectField
            value={state}
            onChange={(value) => applyFilters(termInput, value, missing)}
            options={stateOptions}
            style={{ width: 180 }}
            aria-label="Estado no Zabbix"
          />
          <SelectField
            value={missing}
            onChange={(value) => applyFilters(termInput, state, value)}
            options={missingOptions}
            style={{ width: 200 }}
            aria-label="Presença no Zabbix"
          />
          {(term !== "" || state !== "all" || missing !== "all") && (
            <Button
              variant="ghost"
              onClick={() => {
                setTermInput("");
                applyFilters("", "all", "all");
              }}
            >
              Limpar filtros
            </Button>
          )}
        </div>
      </FilterBar>

      {hosts.isLoading ? (
        <LoadingBlock />
      ) : (
        <EntityView<ZabbixHostView>
          mode={view}
          items={items}
          getKey={(row) => row.hostId}
          emptyMessage="Nenhum host encontrado — use “Sincronizar agora” para importar do Zabbix"
          kanban={{
            groupOf: hostStateKey,
            columns: HOST_STATE_COLUMNS,
          }}
          renderCard={renderHostCard}
          table={
        <DataTablePanel
          value={items}
          dataKey="hostId"
          loading={hosts.isFetching}
          emptyMessage="Nenhum host encontrado — use “Sincronizar agora” para importar do Zabbix"
        >
          <Column
            header="Host"
            body={(row: ZabbixHostView) => (
              <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                <span style={{ fontWeight: 600, fontFamily: "monospace", fontSize: 12 }}>
                  {row.host}
                </span>
                <span style={{ fontSize: 10, color: "#94a3b8" }}>id {row.hostId}</span>
              </div>
            )}
            style={{ minWidth: 180 }}
          />
          <Column header="Nome" body={(row: ZabbixHostView) => row.name || "—"} style={{ minWidth: 160 }} />
          <Column
            header="IP"
            body={(row: ZabbixHostView) =>
              row.ip ? `${row.ip}${row.port ? `:${row.port}` : ""}` : "—"
            }
            style={{ width: 140 }}
          />
          <Column
            header="Estado"
            body={(row: ZabbixHostView) => (
              <div className="flex align-items-center gap-1 flex-wrap">
                {row.missing ? (
                  <StatusChip label="Ausente" background="#fee2e2" color="#b91c1c" title="Removido do Zabbix — registro preservado no InfraControl" />
                ) : row.enabled ? (
                  <StatusChip label="Ativo" background="#dcfce7" color="#166534" />
                ) : (
                  <StatusChip label="Desativado" background="#f1f5f9" color="#64748b" />
                )}
                {row.discovered && (
                  <StatusChip label="Descoberto" background="#eff6ff" color="#1d4ed8" />
                )}
              </div>
            )}
            style={{ width: 160 }}
          />
          <Column
            header="Asset"
            body={(row: ZabbixHostView) =>
              row.hasAsset ? (
                <StatusChip label="Vinculado" background="#dcfce7" color="#166534" />
              ) : (
                <StatusChip label="Sem Asset" background="#fff7ed" color="#c2410c" title="Nenhum dispositivo do inventário corresponde a este host" />
              )
            }
            style={{ width: 120 }}
          />
          <Column header="Grupos" body={(row: ZabbixHostView) => listCell(row.groups)} style={{ minWidth: 160 }} />
          <Column header="Templates" body={(row: ZabbixHostView) => listCell(row.templates)} style={{ minWidth: 160 }} />
          <Column
            header="Última sincronização"
            body={(row: ZabbixHostView) => formatUtc(row.lastSyncedAtUtc)}
            style={{ width: 150 }}
          />
          <Column
            header="Ausente desde"
            body={(row: ZabbixHostView) => formatUtc(row.missingSinceUtc)}
            style={{ width: 140 }}
          />
        </DataTablePanel>
          }
        />
      )}

      {totalCount > 0 && (
        <Pagination page={page} totalPages={totalPages} disabled={hosts.isFetching} onPage={setPage} />
      )}
    </div>
  );
}
