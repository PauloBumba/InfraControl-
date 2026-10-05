import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField, FormError } from "@/components/ui/FormField";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { MeterGroup } from "primereact/metergroup";
import { Can } from "@/components/auth/Can";
import { formatDateTime } from "@/utils/format";
import {
  createService,
  listServiceBudgets,
  listSlaPolicies,
  listServices,
  listSlos,
  type ServiceItem,
  type SlaPolicyItem,
  type SloBudgetItem,
  type SloItem,
} from "@/services/api/sla";

/**
 * F17-T005 — SlaPage conectada ao backend (substitui `services`/
 * `sloBudgets` de `data/dashboardData`). O protótipo inventava colunas sem
 * contrato: `status` (operacional/degradado), `uptime`, `slaTarget`/`sloTarget`
 * em %, `openIncidents`, `owner` e `burnRate` não existem nos DTOs. O que é
 * real: `ServiceDto` (nome/descrição), `SlaPolicyDto` (compromisso de
 * resposta/resolução em minutos — a coluna SLA), `SloDto` (alvo % + janela
 * de dias — a coluna SLO) e `SloBudgetDto` (orçamento consumido por período —
 * os cards de Error Budget, sem burn rate).
 *
 * Orçamento: F18-T003 trouxe o endpoint agregado — a seção faz **1
 * pedido por visão** (`/sla/slos/budgets`) e agrupa por `sloId` no
 * cliente (antes era 1 pedido por SLO, N+1 aceito no F17-T005).
 */

const PAGE_SIZE = 100;

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function SlaPage() {
  const [services, setServices] = useState<ServiceItem[]>([]);
  const [slos, setSlos] = useState<SloItem[]>([]);
  const [policies, setPolicies] = useState<SlaPolicyItem[]>([]);
  const [budgetsBySlo, setBudgetsBySlo] = useState<Map<string, SloBudgetItem[]>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [servicePage, sloPage, policyPage, budgetPage] = await Promise.all([
        listServices({ pageSize: PAGE_SIZE }),
        listSlos({ pageSize: PAGE_SIZE }),
        listSlaPolicies({ pageSize: PAGE_SIZE }),
        listServiceBudgets({ pageSize: PAGE_SIZE }),
      ]);
      setServices(servicePage.items);
      setSlos(sloPage.items);
      setPolicies(policyPage.items);

      // F18-T003: 1 pedido agregado por visão (antes: 1 por SLO — N+1 do
      // F17-T005); agrupa por sloId como o render já espera.
      const budgets = new Map<string, SloBudgetItem[]>();
      for (const budget of budgetPage.items) {
        const list = budgets.get(budget.sloId) ?? [];
        list.push(budget);
        budgets.set(budget.sloId, list);
      }
      setBudgetsBySlo(budgets);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) {
      setFormError("Informe o nome do serviço.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      await createService({ name: trimmed, description: description.trim() || undefined });
      setCreating(false);
      setName("");
      setDescription("");
      setNotice("Serviço criado.");
      await load();
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  }

  const serviceName = (id: string): string =>
    services.find((service) => service.id === id)?.name ?? "Serviço";

  const budgetCards = slos.flatMap((slo) =>
    (budgetsBySlo.get(slo.id) ?? []).map((budget) => ({ slo, budget })),
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="SLA/SLO & Services"
        subtitle={loading
          ? "Carregando…"
          : `${services.length} ${services.length === 1 ? "serviço" : "serviços"} · ${slos.length} ${slos.length === 1 ? "SLO" : "SLOs"}`}
        actions={
          <Can level="operate">
            <Button
              variant="primary"
              onClick={() => { setCreating((value) => !value); setFormError(null); }}
              style={{ padding: "6px 14px", fontSize: 13 }}
            >
              <i className="pi pi-plus" style={{ fontSize: 12 }} /> Novo Serviço
            </Button>
          </Can>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo serviço</div>
          <FormField label="Nome" error={formError}>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Nome *"
              autoFocus
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <FormField label="Descrição (opcional)">
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Descrição (opcional)"
              rows={2}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
            />
          </FormField>
          <FormError message={formError ?? ""} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setCreating(false); setFormError(null); }}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={saving}
              style={{ padding: "6px 14px" }}
            >
              {saving ? "Salvando…" : "Criar serviço"}
            </Button>
          </div>
        </form>
      )}

      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Serviços</h2></div>
        <DataTablePanel panel={false} value={services} dataKey="id" loading={loading} emptyMessage={error ? "—" : "Nenhum serviço cadastrado"}>
          <Column header="Serviço" body={(service: ServiceItem) => <><div style={{ fontWeight: 500, fontSize: 13 }}>{service.name}</div>{service.description && <div style={{ fontSize: 11, color: "#94a3b8" }}>{service.description}</div>}</>} />
          <Column header="SLA (política)" body={(service: ServiceItem) => { const values = policies.filter((policy) => policy.serviceId === service.id); return values.length ? <>{values.map((policy) => <div key={policy.id} style={{ marginBottom: 2 }}>{policy.name}: resposta {policy.responseMinutes}min · resolução {policy.resolutionMinutes}min</div>)}</> : "—"; }} style={{ fontSize: 12, color: "#64748b", width: 280 }} />
          <Column header="SLOs" body={(service: ServiceItem) => { const values = slos.filter((slo) => slo.serviceId === service.id); return values.length ? <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>{values.map((slo) => <span key={slo.id} title={`${slo.targetPercent}% em ${slo.windowDays} dias`} style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#eff6ff", color: "#3b82f6" }}>{slo.name} · {slo.targetPercent}%/{slo.windowDays}d</span>)}</div> : <span style={{ fontSize: 12, color: "#64748b" }}>—</span>; }} style={{ width: 280 }} />
        </DataTablePanel>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Error Budget (SLO)</h2></div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: 16, padding: 16 }}>
          {budgetCards.map(({ slo, budget }) => {
            const consumed = Number(budget.consumedPercent);
            const barColor = consumed >= 100 ? "#dc2626" : consumed >= 50 ? "#d97706" : "#22c55e";
            return (
              <div key={budget.id} style={{ padding: 12, borderRadius: 8, border: "1px solid #e5e7eb" }}>
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8, gap: 8 }}>
                  <span style={{ fontSize: 13, fontWeight: 500 }}>
                    {slo.name}
                    <div style={{ fontSize: 11, color: "#94a3b8", fontWeight: 400 }}>{serviceName(slo.serviceId)}</div>
                  </span>
                  <span style={{ fontSize: 11, fontWeight: 600, color: barColor }}>
                    {consumed >= 100 ? "Esgotado" : consumed >= 50 ? "Atenção" : "Normal"}
                  </span>
                </div>
                <MeterGroup className="uni-meter-group" values={[{ label: "Consumido", value: Math.min(100, consumed), color: barColor }, { label: "Disponível", value: Math.max(0, 100 - consumed), color: "#e2e8f0" }]} max={100} />
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#64748b" }}>
                  <span>{consumed}% consumido</span>
                  <span>
                    {formatDateTime(new Date(budget.periodStart))}
                    {" → "}
                    {budget.periodEnd ? formatDateTime(new Date(budget.periodEnd)) : "em aberto"}
                  </span>
                </div>
              </div>
            );
          })}
          {!loading && budgetCards.length === 0 && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 12, color: "#94a3b8", fontSize: 13 }}>
              {error ? "—" : "Nenhum orçamento registrado"}
            </div>
          )}
          {loading && budgetCards.length === 0 && (
            <LoadingBlock style={{ gridColumn: "1 / -1", padding: 12 }} />
          )}
        </div>
      </div>
    </div>
  );
}
