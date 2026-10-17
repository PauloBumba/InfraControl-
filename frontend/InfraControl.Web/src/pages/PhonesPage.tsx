import { useEffect, useMemo, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { listPhones, runAllPhoneChecks } from "@/services/api/phones";
import { getDeviceResults, type DeviceResults, type MonitoringResultItem } from "@/services/api/monitoring";
import { listLocations, type DeviceItem, type LocationItem } from "@/services/api/inventory";
import {
  useListQuery,
  usePagedQuery,
  useObjectQuery,
  useCreateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";

/**
 * F30-T006d — Telefones IP (docs/47). Padrão CamerasPage: lista
 * (DeviceType.Phone, inventário é System of Record) + painel com os 3
 * blocos lidos do histórico de monitoramento (getDeviceResults, mais
 * recente por check):
 *
 *  - SIP: YealinkSip (chip Registrado/Não registrado + extensão/servidor/codec);
 *  - Qualidade: YealinkCallQuality (MOS/jitter/latência/perda com limiares
 *    3.0/3.5, 50ms, 150ms, 5%/10% — mesmos do AlertEvaluator);
 *  - PoE: da Qualidade (chip Energia OK/Sem energia + watts, best-effort).
 *
 * Botão "Executar checks" roda a bateria Yealink ao vivo (MON-006, Operate)
 * e refaz o histórico. Checks novos são opt-in no perfil — sem resultado
 * ainda = estado vazio com dica, não erro.
 */

interface LatestByCheck {
  sip?: MonitoringResultItem;
  quality?: MonitoringResultItem;
}

function latestByCheck(results: MonitoringResultItem[]): LatestByCheck {
  const latest: LatestByCheck = {};
  for (const result of results) {
    switch (result.check) {
      case "YealinkSip":
        latest.sip ??= result;
        break;
      case "YealinkCallQuality":
        latest.quality ??= result;
        break;
    }
  }
  return latest;
}

function valueOf(result: MonitoringResultItem | undefined, key: string): number | null {
  const raw = result?.values?.[key];
  return raw != null && Number.isFinite(Number(raw)) ? Number(raw) : null;
}

function boolOf(result: MonitoringResultItem | undefined, key: string): boolean | null {
  const raw = result?.values?.[key];
  return raw == null ? null : raw === "true";
}

function textOf(result: MonitoringResultItem | undefined, key: string): string | null {
  const raw = result?.values?.[key];
  return raw == null || raw === "" ? null : raw;
}

function checkedAtLabel(result: MonitoringResultItem | undefined): string {
  if (!result) return "sem execução";
  const at = new Date(result.checkedAt);
  return `última execução ${at.toLocaleString("pt-BR")}`;
}

function numLabel(value: number | null, unit: string, decimals = 0): string {
  if (value == null) return "—";
  return `${value.toLocaleString("pt-BR", {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })} ${unit}`;
}

/** Chip binário (registrado / PoE) — verde quando OK, vermelho quando não. */
function StatusChip({ ok, okLabel, badLabel }: { ok: boolean | null; okLabel: string; badLabel: string }) {
  if (ok == null) {
    return <span style={{ fontSize: 12, color: "#94a3b8" }}>sem dado</span>;
  }
  const tone = ok ? "#16a34a" : "#dc2626";
  return (
    <span
      style={{
        display: "inline-block",
        padding: "3px 10px",
        borderRadius: 999,
        fontSize: 12,
        fontWeight: 700,
        color: tone,
        background: ok ? "#f0fdf4" : "#fef2f2",
        border: `1px solid ${tone}`,
      }}
    >
      {ok ? okLabel : badLabel}
    </span>
  );
}

/**
 * Linha de métrica com limiar — espelha as regras do AlertEvaluator:
 * MOS 3.5/3.0 (warn/crit), jitter 50ms, latência 150ms, perda 5%/10%.
 */
function MetricRow({
  label,
  value,
  unit,
  decimals = 0,
  warnAt,
  critAt,
  invert = false,
}: {
  label: string;
  value: number | null;
  unit: string;
  decimals?: number;
  warnAt?: number;
  critAt?: number;
  invert?: boolean;
}) {
  let tone = "#16a34a";
  let note: string | null = null;
  if (value != null) {
    const isCrit = invert
      ? value >= (critAt ?? Number.POSITIVE_INFINITY)
      : value <= (critAt ?? Number.NEGATIVE_INFINITY);
    const isWarn = invert
      ? value >= (warnAt ?? Number.POSITIVE_INFINITY)
      : value <= (warnAt ?? Number.POSITIVE_INFINITY);
    if (isCrit) {
      tone = "#dc2626";
      note = critAt != null ? `crítico (${critAt})` : null;
    } else if (isWarn) {
      tone = "#b45309";
      note = warnAt != null ? `atenção (${warnAt})` : null;
    }
  }
  return (
    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
      <span>{label}</span>
      <span style={{ fontWeight: 700, color: value == null ? "#64748b" : tone }}>
        {numLabel(value, unit, decimals)}
        {note && <span style={{ fontSize: 10, fontWeight: 600, marginLeft: 6 }}>{note}</span>}
      </span>
    </div>
  );
}

export function PhonesPage() {
  const [view, setView] = useEntityViewMode("phones", "grid");

  const phones = usePagedQuery<DeviceItem>(["phones"], () => listPhones());
  const locationsQuery = useListQuery<LocationItem>(["locations"], () =>
    listLocations().then((page) => page.items),
  );

  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedId && phones.items.length > 0) {
      setSelectedId(phones.items[0]?.id ?? null);
    }
  }, [phones.items, selectedId]);

  const results = useObjectQuery<DeviceResults | null>(["phoneResults", selectedId ?? ""], () =>
    getDeviceResults(selectedId as string, 100),
    { enabled: selectedId !== null },
  );

  // Execução ao vivo da bateria Yealink (MON-006, Operate).
  const runMutation = useCreateMutation({
    invalidate: ["phoneResults"],
    mutationFn: (deviceId: string) => runAllPhoneChecks(deviceId),
    onSuccess: (data) => {
      const list = data ?? [];
      const failed = list.filter((entry) => !entry.succeeded).length;
      setRunMessage(
        failed > 0
          ? `${list.length} checks executados, ${failed} com falha (telefone inacessível/modelo não suportado)`
          : `${list.length} checks executados com sucesso`,
      );
      void results.refetch();
    },
    onError: (error) => setRunMessage(mutationErrorMessage(error)),
  });
  const [runMessage, setRunMessage] = useState<string | null>(null);

  const locations = locationsQuery.items;
  const locationName = (id: string): string =>
    locations.find((entry) => entry.id === id)?.name ?? id;

  const phonesError = mutationErrorMessage(phones.error as Error | null);
  const resultsError = mutationErrorMessage(results.error as Error | null);
  const selected = phones.items.find((entry) => entry.id === selectedId) ?? null;

  const history = useMemo(() => results.data?.results ?? [], [results.data]);
  const latest = useMemo(() => latestByCheck(history), [history]);

  const registered = boolOf(latest.sip, "sipRegistered");
  const codec = textOf(latest.sip, "codec");
  // Cadastro (§A) + dado ao vivo (§B) — o vivo prevalece quando presente.
  const extension = textOf(latest.sip, "extension") ?? selected?.extension ?? null;
  const sipServer = textOf(latest.sip, "sipServer") ?? selected?.sipServer ?? null;

  const mos = valueOf(latest.quality, "mos");
  const jitterMs = valueOf(latest.quality, "jitterMs");
  const latencyMs = valueOf(latest.quality, "latencyMs");
  const packetLossPercent = valueOf(latest.quality, "packetLossPercent");
  const poePowered = boolOf(latest.quality, "poePowered");
  const poePowerW = valueOf(latest.quality, "poePowerW");

  const hasAnyData = history.length > 0;

  if (phones.isPending || locationsQuery.isPending) {
    return <LoadingBlock />;
  }

  if (phones.items.length === 0 && phonesError) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{phonesError}</h2>
        <Button variant="primary" onClick={() => void phones.refetch()} style={{ marginTop: 8, padding: "6px 14px" }}>
          Tentar de novo
        </Button>
      </div>
    );
  }

  return (
    <ListPageShell
      title="Telefones"
      singular="telefone"
      plural="telefones"
      total={phones.totalCount}
      error={phonesError}
      onRetry={() => void phones.refetch()}
      actions={
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <ViewSwitcher mode={view} onChange={setView} />
        </div>
      }
    >
      <EntityView<DeviceItem>
        mode={view}
        items={phones.items}
        getKey={(phone) => phone.id}
        emptyMessage={phonesError ? "—" : "Nenhum telefone cadastrado"}
        kanban={{
          groupOf: (phone) => phone.locationId || "unknown",
          columns: [
            ...locations.map((location) => ({ key: location.id, label: location.name })),
            { key: "unknown", label: "Sem localização" },
          ],
          overflowColumnKey: "unknown",
        }}
        renderCard={(phone) => (
          <div
            onClick={() => setSelectedId(phone.id)}
            style={{
              padding: 14,
              borderRadius: 8,
              border: `2px solid ${phone.id === selectedId ? "#2563eb" : "#e5e7eb"}`,
              background: phone.id === selectedId ? "#eff6ff" : "#fff",
              cursor: "pointer",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
              <i className="pi pi-phone" style={{ color: "#6366f1" }} />
              <span style={{ fontWeight: 700 }}>{phone.name}</span>
            </div>
            <div style={{ fontSize: 11, color: "#64748b" }}>
              {phone.model ?? "modelo —"} · {phone.ip ?? "sem IP"}
            </div>
            <div style={{ fontSize: 11, color: "#94a3b8" }}>
              {locationName(phone.locationId)}
              {phone.extension ? ` · ramal ${phone.extension}` : ""}
            </div>
          </div>
        )}
        table={
          <DataTablePanel
            value={phones.items}
            dataKey="id"
            emptyMessage={phonesError ? "—" : "Nenhum telefone cadastrado"}
            onRowClick={(event) => setSelectedId(String(event.data.id))}
          >
            <Column header="Telefone" body={(phone: DeviceItem) => (
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <i className="pi pi-phone" style={{ color: "#6366f1" }} />
                <span style={{ fontWeight: 600 }}>{phone.name}</span>
                {phone.id === selectedId && (
                  <i className="pi pi-angle-right" style={{ color: "#2563eb", fontSize: 12 }} />
                )}
              </div>
            )} />
            <Column header="Modelo" body={(phone: DeviceItem) => phone.model ?? "—"} />
            <Column header="Fabricante" body={(phone: DeviceItem) => phone.vendor ?? "—"} />
            <Column header="IP" body={(phone: DeviceItem) => phone.ip ?? "—"} style={{ width: 130 }} />
            <Column header="Ramal" body={(phone: DeviceItem) => phone.extension ?? "—"} style={{ width: 90 }} />
            <Column header="Servidor SIP" body={(phone: DeviceItem) => phone.sipServer ?? "—"} />
            <Column header="Localização" body={(phone: DeviceItem) => locationName(phone.locationId)} />
          </DataTablePanel>
        }
      />

      {/* Painel — SIP & Qualidade (docs/47 §B–§D) */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>SIP &amp; Qualidade — {selected?.name ?? "—"}</h2>
          {selected && (
            <Button
              variant="ghost"
              disabled={runMutation.isPending}
              onClick={() => {
                setRunMessage(null);
                runMutation.mutate(selected.id);
              }}
              style={{ marginLeft: "auto" }}
            >
              {runMutation.isPending ? "Executando…" : "Executar checks"}
            </Button>
          )}
        </div>

        {runMessage && (
          <div
            style={{
              padding: "6px 16px",
              fontSize: 12,
              background: runMutation.isError ? "#fef2f2" : "#f0fdf4",
              color: runMutation.isError ? "#dc2626" : "#16a34a",
              borderBottom: "1px solid #e5e7eb",
            }}
          >
            {runMessage}
          </div>
        )}

        {resultsError && (
          <ErrorBanner
            message={resultsError}
            onRetry={() => void results.refetch()}
            style={{ padding: "10px 16px", borderRadius: 0 }}
          />
        )}

        {results.isFetching && !hasAnyData && <LoadingBlock />}

        {!results.isFetching && !resultsError && !hasAnyData && (
          <div style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
            Nenhum resultado de monitoramento ainda. Clique em{" "}
            <strong>Executar checks</strong> para consultar o telefone (SIP + qualidade).
          </div>
        )}

        {hasAnyData && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: 16, padding: 16 }}>
            {/* SIP */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Registro SIP</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.sip)}
                {latest.sip?.succeeded === false && latest.sip.error
                  ? ` · ${latest.sip.error}`
                  : ""}
              </div>
              <StatusChip ok={registered} okLabel="Registrado" badLabel="Não registrado" />
              <div style={{ display: "grid", gap: 10, marginTop: 12 }}>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Extensão</span>
                  <span style={{ fontWeight: 700 }}>{extension ?? "—"}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Servidor SIP</span>
                  <span style={{ fontWeight: 700 }}>{sipServer ?? "—"}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                  <span>Codec</span>
                  <span style={{ fontWeight: 700 }}>{codec ?? "—"}</span>
                </div>
              </div>
            </div>

            {/* Qualidade */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Qualidade de chamada</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.quality)}
                {latest.quality?.succeeded === false && latest.quality.error
                  ? ` · ${latest.quality.error}`
                  : ""}
              </div>
              <div style={{ display: "grid", gap: 10 }}>
                {/* MOS: baixo é ruim — warn 3.5, crit 3.0 */}
                <MetricRow label="MOS" value={mos} unit="" decimals={1} warnAt={3.5} critAt={3.0} />
                {/* Jitter/latência/perda: alto é ruim — invert */}
                <MetricRow label="Jitter" value={jitterMs} unit="ms" warnAt={50} invert />
                <MetricRow label="Latência" value={latencyMs} unit="ms" warnAt={150} invert />
                <MetricRow label="Perda de pacotes" value={packetLossPercent} unit="%" decimals={1} warnAt={5} critAt={10} invert />
              </div>
              <div style={{ fontSize: 10, color: "#94a3b8", marginTop: 8 }}>
                Limiares iguais aos alertas (MOS 3.5/3.0 · jitter 50ms · latência 150ms · perda 5%/10%).
              </div>
            </div>

            {/* PoE (best-effort, vem do check de qualidade) */}
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 8, padding: 14 }}>
              <h3 style={{ fontSize: 13, fontWeight: 700, margin: "0 0 4px" }}>Energia (PoE)</h3>
              <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 10 }}>
                {checkedAtLabel(latest.quality)}
              </div>
              <StatusChip ok={poePowered} okLabel="Energia OK" badLabel="Sem energia" />
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, marginTop: 12 }}>
                <span>Potência</span>
                <span style={{ fontWeight: 700 }}>{numLabel(poePowerW, "W", 1)}</span>
              </div>
              <div style={{ fontSize: 10, color: "#94a3b8", marginTop: 6 }}>
                Best-effort — nem todo modelo/fabricante expõe PoE via API.
              </div>
            </div>
          </div>
        )}
      </div>
    </ListPageShell>
  );
}
