import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { StatusChip } from "@/components/ui/StatusChip";
import { SelectField, type SelectOption } from "@/components/ui/SelectField";
import { FilterBar } from "@/components/ui/FilterBar";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { Column } from "primereact/column";
import { mutationErrorMessage } from "@/hooks/queries";
import { formatDateTime } from "@/utils/format";
import {
  getZabbixProblems,
  getZabbixSummary,
  type ZabbixProblemView,
} from "@/services/api/zabbix";

const SEVERITY_OPTIONS: SelectOption[] = [
  { value: "all", label: "Todas as severidades" },
  { value: "5", label: "Desastre (5+)" },
  { value: "4", label: "Alto (4+)" },
  { value: "3", label: "Médio (3+)" },
  { value: "2", label: "Aviso (2+)" },
  { value: "1", label: "Informativo (1+)" },
];

const SEVERITY_STYLE: Record<number, { background: string; color: string }> = {
  5: { background: "#fee2e2", color: "#b91c1c" },
  4: { background: "#ffedd5", color: "#c2410c" },
  3: { background: "#fef9c3", color: "#854d0e" },
  2: { background: "#dbeafe", color: "#1d4ed8" },
  1: { background: "#dcfce7", color: "#166534" },
  0: { background: "#f1f5f9", color: "#64748b" },
};

const SEVERITY_NAMES = [
  "Não classificado",
  "Informativo",
  "Aviso",
  "Médio",
  "Alto",
  "Desastre",
];

function formatUtc(value: string | null | undefined): string {
  return value ? formatDateTime(new Date(value)) : "—";
}

/**
 * F27 Fase 10.3/10.4 — Infraestrutura → Visão geral (Zabbix).
 * Cards do read-model (hosts/Assets/ausentes), distribuição de problemas
 * por severidade (consulta ao vivo — falha segregada em
 * `problemsAvailable`, nunca confundida com "sem problemas"), estado da
 * integração e tabela dos problemas ativos com filtro de severidade.
 */
export function ZabbixOverviewPage() {
  const [minSeverity, setMinSeverity] = useState("all");
  const [includeRecovered, setIncludeRecovered] = useState(false);

  const summary = useQuery({
    queryKey: ["zabbix-summary"],
    queryFn: getZabbixSummary,
  });

  const problems = useQuery({
    queryKey: ["zabbix-problems", minSeverity, includeRecovered],
    queryFn: () =>
      getZabbixProblems({
        minSeverity: minSeverity === "all" ? undefined : Number(minSeverity),
        includeRecovered,
      }),
  });

  if (summary.isLoading) return <LoadingBlock />;

  const s = summary.data;
  const severityChips = SEVERITY_NAMES.map((name, level) => ({
    name,
    level,
    count: s?.problemsBySeverity[level] ?? 0,
  })).filter((entry) => entry.count > 0);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Visão geral"
        subtitle="Monitoramento de infraestrutura via Zabbix"
        actions={
          <Link to="/zabbix/hosts" style={{ fontSize: 13 }}>
            Hosts monitorados →
          </Link>
        }
      />

      {summary.error && (
        <ErrorBanner
          message={
            mutationErrorMessage(summary.error as Error | null) ??
            "Não foi possível carregar o resumo. Tente novamente."
          }
          onRetry={() => void summary.refetch()}
        />
      )}

      {s && !s.isConfigured && (
        <NoticeBanner message="A integração com Zabbix ainda não foi configurada — Administração → Integrações → Zabbix." />
      )}

      {s?.problemsAvailable === false && s.isConfigured && (
        <NoticeBanner message={s.problemsError ?? "Problemas indisponíveis no momento."} />
      )}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))",
          gap: 12,
        }}
      >
        <Card label="Hosts monitorados" value={s?.totalHosts ?? "—"} />
        <Card label="Ativos" value={s?.enabledHosts ?? "—"} />
        <Card
          label="Ausentes"
          value={s?.missingHosts ?? "—"}
          sub={s && s.missingHosts > 0 ? "removidos do Zabbix" : undefined}
        />
        <Card
          label="Sem Asset"
          value={s?.hostsWithoutAsset ?? "—"}
          sub={s && s.hostsWithoutAsset > 0 ? "ver Hosts monitorados" : undefined}
        />
        <Card
          label="Com problemas ativos"
          value={s?.problemsAvailable ? s.hostsWithProblems : "—"}
          sub={
            s?.problemsAvailable
              ? `${s.problemsTotal} problema(s) no Zabbix`
              : "consulta indisponível"
          }
        />
        <Card
          label="Última sincronização"
          value={formatUtc(s?.lastInventorySyncAtUtc)}
          sub={s?.detectedVersion ? `Zabbix ${s.detectedVersion}` : undefined}
        />
      </div>

      {s?.problemsAvailable && severityChips.length > 0 && (
        <div className="flex align-items-center gap-2 flex-wrap">
          <span style={{ fontSize: 11, color: "#9ca3af", textTransform: "uppercase" }}>
            Problemas por severidade
          </span>
          {severityChips.map((entry) => (
            <StatusChip
              key={entry.level}
              label={`${entry.name}: ${entry.count}`}
              background={SEVERITY_STYLE[entry.level]?.background ?? "#f1f5f9"}
              color={SEVERITY_STYLE[entry.level]?.color ?? "#64748b"}
            />
          ))}
        </div>
      )}

      <FilterBar>
        <div className="flex align-items-center gap-2 flex-wrap">
          <SelectField
            value={minSeverity}
            onChange={setMinSeverity}
            options={SEVERITY_OPTIONS}
            style={{ width: 210 }}
            aria-label="Severidade mínima"
          />
          <label style={{ fontSize: 13, display: "flex", alignItems: "center", gap: 6 }}>
            <input
              type="checkbox"
              checked={includeRecovered}
              onChange={(e) => setIncludeRecovered(e.target.checked)}
            />
            Incluir recuperados
          </label>
        </div>
      </FilterBar>

      {problems.isLoading ? (
        <LoadingBlock />
      ) : problems.error ? (
        <ErrorBanner
          message={
            mutationErrorMessage(problems.error as Error | null) ??
            "Não foi possível consultar os problemas. Tente novamente."
          }
          onRetry={() => void problems.refetch()}
        />
      ) : (
        <DataTablePanel
          value={problems.data?.items ?? []}
          dataKey="eventId"
          loading={problems.isFetching}
          emptyMessage="Nenhum problema ativo — boa notícia!"
        >
          <Column
            header="Severidade"
            body={(row: ZabbixProblemView) => (
              <StatusChip
                label={row.severityLabel}
                background={SEVERITY_STYLE[row.severity]?.background ?? "#f1f5f9"}
                color={SEVERITY_STYLE[row.severity]?.color ?? "#64748b"}
              />
            )}
            style={{ width: 150 }}
          />
          <Column header="Problema" body={(row: ZabbixProblemView) => row.name} style={{ minWidth: 240 }} />
          <Column
            header="Hosts"
            body={(row: ZabbixProblemView) =>
              row.hostNames.length === 0 ? (
                "—"
              ) : (
                <span title={row.hostNames.join(", ")}>
                  {row.hostNames.slice(0, 2).join(", ")}
                  {row.hostNames.length > 2 && (
                    <span style={{ color: "#94a3b8" }}> +{row.hostNames.length - 2}</span>
                  )}
                </span>
              )
            }
            style={{ minWidth: 160 }}
          />
          <Column
            header="Desde"
            body={(row: ZabbixProblemView) => formatUtc(row.clockUtc)}
            style={{ width: 160 }}
          />
          <Column
            header="Estado"
            body={(row: ZabbixProblemView) =>
              row.recovered ? (
                <StatusChip label="Recuperado" background="#dcfce7" color="#166534" />
              ) : (
                <StatusChip label="Ativo" background="#fee2e2" color="#b91c1c" />
              )
            }
            style={{ width: 120 }}
          />
        </DataTablePanel>
      )}
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
      }}
    >
      <div style={{ fontSize: 10, color: "#9ca3af", textTransform: "uppercase" }}>{label}</div>
      <div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1.2, wordBreak: "break-word" }}>
        {value}
      </div>
      {sub && <div style={{ fontSize: 10, color: "#9ca3af" }}>{sub}</div>}
    </div>
  );
}
