import { useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField, FormError } from "@/components/ui/FormField";
import { SelectField } from "@/components/ui/SelectField";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { MeterGroup } from "primereact/metergroup";
import { Can } from "@/components/auth/Can";
import { formatDateTime } from "@/utils/format";
import {
  createService,
  createSlaPolicy,
  createSlo,
  deleteService,
  deleteSlaPolicy,
  deleteSlo,
  listServiceBudgets,
  listSlaPolicies,
  listServices,
  listSlos,
  updateService,
  updateSlaPolicy,
  updateSlo,
  type ServiceItem,
  type SlaPolicyItem,
  type SloBudgetItem,
  type SloItem,
} from "@/services/api/sla";
import {
  useCreateMutation,
  useDeleteMutation,
  useListQuery,
  useUpdateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";

/**
 * F17-T005 + ADR-030 Fase 5 — SlaPage com React Query (4 listas com
 * chave por domínio: `slaServices`/`slaPolicies`/`slaSlos`/`slaBudgets`)
 * e visões tabela/grid/kanban (preferência persistida por entidade).
 * CRUD completo nas abas Serviços/Políticas/SLOs (Operate; 409 de vínculo
 * vira banner) e aba Orçamentos de leitura (1 pedido agregado por visão —
 * F18-T003, sem N+1). Kanban agrupa por serviço (coluna "Sem serviço" p/
 * política global). Escrita atrás de `Can operate` (exibição; policy
 * Operate + antiforgery continuam sendo a autoridade).
 */

const PAGE_SIZE = 100;

type FormKind = "service" | "policy" | "slo";

interface FormState {
  kind: FormKind;
  mode: "create" | "edit";
  target: ServiceItem | SlaPolicyItem | SloItem | null;
}

const emptyValues = {
  name: "",
  description: "",
  responseMinutes: "15",
  resolutionMinutes: "480",
  targetPercent: "99.9",
  windowDays: "30",
  serviceId: "",
};

export function SlaPage() {
  const [tab, setTab] = useState<"services" | "policies" | "slos" | "budgets">("services");
  const [serviceView, setServiceView] = useEntityViewMode("slaServices", "table");
  const [policyView, setPolicyView] = useEntityViewMode("slaPolicies", "table");
  const [sloView, setSloView] = useEntityViewMode("slaSlos", "table");

  const services = useListQuery<ServiceItem>(["slaServices"], () =>
    listServices({ pageSize: PAGE_SIZE }).then((page) => page.items),
  );
  const slos = useListQuery<SloItem>(["slaSlos"], () =>
    listSlos({ pageSize: PAGE_SIZE }).then((page) => page.items),
  );
  const policies = useListQuery<SlaPolicyItem>(["slaPolicies"], () =>
    listSlaPolicies({ pageSize: PAGE_SIZE }).then((page) => page.items),
  );
  const budgets = useListQuery<SloBudgetItem>(["slaBudgets"], () =>
    listServiceBudgets({ pageSize: PAGE_SIZE }).then((page) => page.items),
  );

  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [form, setForm] = useState<FormState | null>(null);
  const [values, setValues] = useState(emptyValues);
  const [formError, setFormError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ kind: FormKind; target: ServiceItem | SlaPolicyItem | SloItem } | null>(null);

  const serviceCreate = useCreateMutation({
    invalidate: ["slaServices"],
    mutationFn: (input: { name: string; description?: string }) => createService(input),
    onSuccess: () => setNotice("Serviço criado."),
  });
  const serviceUpdate = useUpdateMutation({
    invalidate: ["slaServices"],
    mutationFn: (input: { id: string; name: string; description?: string }) =>
      updateService(input.id, { name: input.name, description: input.description }),
    onSuccess: () => setNotice("Serviço atualizado."),
  });
  const serviceDelete = useDeleteMutation({
    invalidate: ["slaServices"],
    mutationFn: (id: string) => deleteService(id),
    onSuccess: () => setNotice("Serviço excluído."),
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

  const policyCreate = useCreateMutation({
    invalidate: ["slaPolicies"],
    mutationFn: (input: { name: string; responseMinutes: number; resolutionMinutes: number; serviceId?: string }) =>
      createSlaPolicy(input),
    onSuccess: () => setNotice("Política de SLA criada."),
  });
  const policyUpdate = useUpdateMutation({
    invalidate: ["slaPolicies"],
    mutationFn: (input: { id: string; name: string; responseMinutes: number; resolutionMinutes: number; serviceId?: string }) => {
      const { id, ...rest } = input;
      return updateSlaPolicy(id, rest);
    },
    onSuccess: () => setNotice("Política de SLA atualizada."),
  });
  const policyDelete = useDeleteMutation({
    invalidate: ["slaPolicies"],
    mutationFn: (id: string) => deleteSlaPolicy(id),
    onSuccess: () => setNotice("Política de SLA excluída."),
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

  const sloCreate = useCreateMutation({
    invalidate: ["slaSlos"],
    mutationFn: (input: { name: string; targetPercent: number; windowDays: number; serviceId: string }) =>
      createSlo(input),
    onSuccess: () => setNotice("SLO criado."),
  });
  const sloUpdate = useUpdateMutation({
    invalidate: ["slaSlos"],
    mutationFn: (input: { id: string; name: string; targetPercent: number; windowDays: number; serviceId: string }) => {
      const { id, ...rest } = input;
      return updateSlo(id, rest);
    },
    onSuccess: () => setNotice("SLO atualizado."),
  });
  const sloDelete = useDeleteMutation({
    invalidate: ["slaSlos"],
    mutationFn: (id: string) => deleteSlo(id),
    onSuccess: () => setNotice("SLO excluído."),
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

  const serviceName = (id: string): string =>
    services.items.find((service) => service.id === id)?.name ?? "Serviço";

  const serviceOptions = [
    { value: "", label: "Sem serviço" },
    ...services.items.map((service) => ({ value: service.id, label: service.name })),
  ];

  function openCreate(kind: FormKind) {
    setForm({ kind, mode: "create", target: null });
    setValues({
      ...emptyValues,
      serviceId: kind === "slo" ? services.items[0]?.id ?? "" : "",
    });
    setFormError(null);
  }

  function openEdit(kind: FormKind, target: ServiceItem | SlaPolicyItem | SloItem) {
    setForm({ kind, mode: "edit", target });
    if (kind === "service") {
      const service = target as ServiceItem;
      setValues({ ...emptyValues, name: service.name, description: service.description ?? "" });
    } else if (kind === "policy") {
      const policy = target as SlaPolicyItem;
      setValues({
        ...emptyValues,
        name: policy.name,
        responseMinutes: String(policy.responseMinutes),
        resolutionMinutes: String(policy.resolutionMinutes),
        serviceId: policy.serviceId ?? "",
      });
    } else {
      const slo = target as SloItem;
      setValues({
        ...emptyValues,
        name: slo.name,
        targetPercent: String(slo.targetPercent),
        windowDays: String(slo.windowDays),
        serviceId: slo.serviceId,
      });
    }
    setFormError(null);
  }

  async function submitForm(event: FormEvent) {
    event.preventDefault();
    if (!form) return;
    setFormError(null);

    const name = values.name.trim();
    if (!name) {
      setFormError("Informe o nome.");
      return;
    }

    try {
      if (form.kind === "service") {
        const input = { name, description: values.description.trim() || undefined };
        if (form.mode === "create") await serviceCreate.mutateAsync(input);
        else if (form.target) await serviceUpdate.mutateAsync({ id: form.target.id, ...input });
      } else if (form.kind === "policy") {
        const response = Number(values.responseMinutes);
        const resolution = Number(values.resolutionMinutes);
        if (!Number.isFinite(response) || response <= 0 || !Number.isFinite(resolution) || resolution <= 0) {
          setFormError("Resposta e resolução devem ser números positivos (minutos).");
          return;
        }
        const input = {
          name,
          responseMinutes: response,
          resolutionMinutes: resolution,
          serviceId: values.serviceId || undefined,
        };
        if (form.mode === "create") await policyCreate.mutateAsync(input);
        else if (form.target) await policyUpdate.mutateAsync({ id: form.target.id, ...input });
      } else {
        const target = Number(values.targetPercent);
        const window = Number(values.windowDays);
        if (!values.serviceId) {
          setFormError("Selecione o serviço do SLO.");
          return;
        }
        if (!Number.isFinite(target) || target <= 0 || target > 100) {
          setFormError("O alvo deve estar entre 0 e 100 (%).");
          return;
        }
        if (!Number.isFinite(window) || window <= 0) {
          setFormError("A janela deve ser um número positivo (dias).");
          return;
        }
        const input = { name, targetPercent: target, windowDays: window, serviceId: values.serviceId };
        if (form.mode === "create") await sloCreate.mutateAsync(input);
        else if (form.target) await sloUpdate.mutateAsync({ id: form.target.id, ...input });
      }
      setForm(null);
    } catch (caught) {
      setFormError(mutationErrorMessage(caught as Error));
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    try {
      if (deleteTarget.kind === "service") await serviceDelete.mutateAsync(deleteTarget.target.id);
      else if (deleteTarget.kind === "policy") await policyDelete.mutateAsync(deleteTarget.target.id);
      else await sloDelete.mutateAsync(deleteTarget.target.id);
      setDeleteTarget(null);
      setActionError(null);
    } catch (caught) {
      setActionError(mutationErrorMessage(caught as Error));
    }
  }

  const listError =
    mutationErrorMessage(services.error as Error | null) ??
    mutationErrorMessage(slos.error as Error | null) ??
    mutationErrorMessage(policies.error as Error | null);

  const subtitle =
    services.isPending || slos.isPending
      ? "Carregando…"
      : `${services.items.length} ${services.items.length === 1 ? "serviço" : "serviços"} · ${slos.items.length} ${slos.items.length === 1 ? "SLO" : "SLOs"} · ${policies.items.length} política(s)`;

  if (services.isPending || slos.isPending || policies.isPending || budgets.isPending) {
    return <LoadingBlock />;
  }

  const formTitle = !form
    ? ""
    : form.mode === "create"
      ? form.kind === "service" ? "Novo serviço" : form.kind === "policy" ? "Nova política de SLA" : "Novo SLO"
      : form.kind === "service" ? `Editar — ${(form.target as ServiceItem).name}`
      : form.kind === "policy" ? `Editar — ${(form.target as SlaPolicyItem).name}`
      : `Editar — ${(form.target as SloItem).name}`;

  const actions = (
    <div className="flex align-items-center gap-2 flex-wrap">
      {tab !== "budgets" && (
        <ViewSwitcher
          mode={tab === "services" ? serviceView : tab === "policies" ? policyView : sloView}
          onChange={tab === "services" ? setServiceView : tab === "policies" ? setPolicyView : setSloView}
        />
      )}
      <Can level="operate">
        <Button
          variant="primary"
          onClick={() => openCreate(tab === "services" ? "service" : tab === "policies" ? "policy" : "slo")}
          style={{ padding: "6px 14px", fontSize: 13 }}
          disabled={tab === "budgets"}
        >
          <i className="pi pi-plus" style={{ fontSize: 12 }} />{" "}
          {tab === "services" ? "Novo Serviço" : tab === "policies" ? "Nova Política" : tab === "slos" ? "Novo SLO" : "—"}
        </Button>
      </Can>
    </div>
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="SLA/SLO & Services"
        subtitle={subtitle}
        actions={tab === "budgets" ? <div /> : actions}
      />

      <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
        {([
          { id: "services", label: "Serviços" },
          { id: "policies", label: "Políticas" },
          { id: "slos", label: "SLOs" },
          { id: "budgets", label: "Error Budget" },
        ] as const).map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            style={{
              padding: "8px 14px",
              fontSize: 13,
              fontWeight: 500,
              color: tab === item.id ? "#3b82f6" : "#64748b",
              background: "none",
              border: "none",
              borderBottom: tab === item.id ? "2px solid #3b82f6" : "2px solid transparent",
              marginBottom: -1,
              cursor: "pointer",
            }}
          >
            {item.label}
          </button>
        ))}
      </div>

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {(actionError || listError) && (
        <ErrorBanner
          message={actionError ?? listError ?? "Não foi possível falar com o servidor. Tente novamente."}
          onRetry={() => {
            setActionError(null);
            void services.refetch();
            void slos.refetch();
            void policies.refetch();
          }}
        />
      )}

      {tab === "services" && (
        <EntityView<ServiceItem>
          mode={serviceView}
          items={services.items}
          getKey={(service) => service.id}
          emptyMessage={listError ? "—" : "Nenhum serviço cadastrado"}
          kanban={{ groupOf: () => "services", columns: [{ key: "services", label: "Serviços" }] }}
          table={
            <DataTablePanel panel={false} value={services.items} dataKey="id" emptyMessage={listError ? "—" : "Nenhum serviço cadastrado"}>
              <Column header="Serviço" body={(service: ServiceItem) => <><div style={{ fontWeight: 500, fontSize: 13 }}>{service.name}</div>{service.description && <div style={{ fontSize: 11, color: "#94a3b8" }}>{service.description}</div>}</>} />
              <Column header="SLA (política)" body={(service: ServiceItem) => { const values = policies.items.filter((policy) => policy.serviceId === service.id); return values.length ? <>{values.map((policy) => <div key={policy.id} style={{ marginBottom: 2 }}>{policy.name}: resposta {policy.responseMinutes}min · resolução {policy.resolutionMinutes}min</div>)}</> : "—"; }} style={{ fontSize: 12, color: "#64748b", width: 280 }} />
              <Column header="SLOs" body={(service: ServiceItem) => { const values = slos.items.filter((slo) => slo.serviceId === service.id); return values.length ? <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>{values.map((slo) => <span key={slo.id} title={`${slo.targetPercent}% em ${slo.windowDays} dias`} style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#eff6ff", color: "#3b82f6" }}>{slo.name} · {slo.targetPercent}%/{slo.windowDays}d</span>)}</div> : <span style={{ fontSize: 12, color: "#64748b" }}>—</span>; }} style={{ width: 280 }} />
              <Column
                header=""
                style={{ width: 150 }}
                body={(service: ServiceItem) => (
                  <Can level="operate">
                    <div className="flex gap-1">
                      <Button variant="ghost" onClick={() => openEdit("service", service)}>Editar</Button>
                      <Button variant="ghost" onClick={() => setDeleteTarget({ kind: "service", target: service })}>Excluir</Button>
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(service) => (
            <div className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 6 }}>
                <span style={{ fontWeight: 600, fontSize: 14 }}>
                  <i className="pi pi-cog" style={{ marginRight: 6, color: "#3b82f6" }} />
                  {service.name}
                </span>
              </div>
              <p style={{ fontSize: 12, color: "#64748b", marginBottom: 8 }}>{service.description ?? "—"}</p>
              <div style={{ display: "flex", gap: 4, flexWrap: "wrap", marginBottom: 8 }}>
                {policies.items.filter((policy) => policy.serviceId === service.id).map((policy) => (
                  <span key={policy.id} style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#fef3c7", color: "#d97706" }}>
                    {policy.name}: {policy.responseMinutes}min / {policy.resolutionMinutes}min
                  </span>
                ))}
                {slos.items.filter((slo) => slo.serviceId === service.id).map((slo) => (
                  <span key={slo.id} style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#eff6ff", color: "#3b82f6" }}>
                    {slo.name} · {slo.targetPercent}%/{slo.windowDays}d
                  </span>
                ))}
              </div>
              <Can level="operate">
                <div className="flex gap-1">
                  <Button variant="ghost" onClick={() => openEdit("service", service)}>Editar</Button>
                  <Button variant="ghost" onClick={() => setDeleteTarget({ kind: "service", target: service })}>Excluir</Button>
                </div>
              </Can>
            </div>
          )}
        />
      )}

      {tab === "policies" && (
        <EntityView<SlaPolicyItem>
          mode={policyView}
          items={policies.items}
          getKey={(policy) => policy.id}
          emptyMessage={listError ? "—" : "Nenhuma política cadastrada"}
          kanban={{
            groupOf: (policy) => policy.serviceId ?? "none",
            columns: [...services.items.map((service) => ({ key: service.id, label: service.name })), { key: "none", label: "Sem serviço" }],
            overflowColumnKey: "none",
          }}
          table={
            <DataTablePanel panel={false} value={policies.items} dataKey="id" emptyMessage={listError ? "—" : "Nenhuma política cadastrada"}>
              <Column header="Política" body={(policy: SlaPolicyItem) => policy.name} />
              <Column header="Serviço" body={(policy: SlaPolicyItem) => policy.serviceId ? serviceName(policy.serviceId) : "Global"} style={{ width: 180 }} />
              <Column header="Resposta" body={(policy: SlaPolicyItem) => `${policy.responseMinutes} min`} style={{ width: 110 }} />
              <Column header="Resolução" body={(policy: SlaPolicyItem) => `${policy.resolutionMinutes} min`} style={{ width: 110 }} />
              <Column
                header=""
                style={{ width: 150 }}
                body={(policy: SlaPolicyItem) => (
                  <Can level="operate">
                    <div className="flex gap-1">
                      <Button variant="ghost" onClick={() => openEdit("policy", policy)}>Editar</Button>
                      <Button variant="ghost" onClick={() => setDeleteTarget({ kind: "policy", target: policy })}>Excluir</Button>
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(policy) => (
            <div className="uni-panel" style={{ padding: 16 }}>
              <div style={{ fontWeight: 600, fontSize: 14, marginBottom: 4 }}>{policy.name}</div>
              <div style={{ fontSize: 12, color: "#64748b", marginBottom: 8 }}>
                {policy.serviceId ? serviceName(policy.serviceId) : "Global"}
              </div>
              <div style={{ display: "flex", gap: 12, fontSize: 12, marginBottom: 8 }}>
                <span title="Tempo de resposta"><i className="pi pi-clock" style={{ marginRight: 4 }} />{policy.responseMinutes} min</span>
                <span title="Tempo de resolução"><i className="pi pi-check-circle" style={{ marginRight: 4 }} />{policy.resolutionMinutes} min</span>
              </div>
              <Can level="operate">
                <div className="flex gap-1">
                  <Button variant="ghost" onClick={() => openEdit("policy", policy)}>Editar</Button>
                  <Button variant="ghost" onClick={() => setDeleteTarget({ kind: "policy", target: policy })}>Excluir</Button>
                </div>
              </Can>
            </div>
          )}
        />
      )}

      {tab === "slos" && (
        <EntityView<SloItem>
          mode={sloView}
          items={slos.items}
          getKey={(slo) => slo.id}
          emptyMessage={listError ? "—" : "Nenhum SLO cadastrado"}
          kanban={{
            groupOf: (slo) => slo.serviceId,
            columns: services.items.map((service) => ({ key: service.id, label: service.name })),
          }}
          table={
            <DataTablePanel panel={false} value={slos.items} dataKey="id" emptyMessage={listError ? "—" : "Nenhum SLO cadastrado"}>
              <Column header="SLO" body={(slo: SloItem) => slo.name} />
              <Column header="Serviço" body={(slo: SloItem) => serviceName(slo.serviceId)} style={{ width: 180 }} />
              <Column header="Alvo" body={(slo: SloItem) => `${slo.targetPercent}%`} style={{ width: 90 }} />
              <Column header="Janela" body={(slo: SloItem) => `${slo.windowDays} dias`} style={{ width: 100 }} />
              <Column
                header=""
                style={{ width: 150 }}
                body={(slo: SloItem) => (
                  <Can level="operate">
                    <div className="flex gap-1">
                      <Button variant="ghost" onClick={() => openEdit("slo", slo)}>Editar</Button>
                      <Button variant="ghost" onClick={() => setDeleteTarget({ kind: "slo", target: slo })}>Excluir</Button>
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(slo) => (
            <div className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 4 }}>
                <span style={{ fontWeight: 600, fontSize: 14 }}>{slo.name}</span>
                <span style={{ fontSize: 12, fontWeight: 700, color: "#3b82f6" }}>{slo.targetPercent}%</span>
              </div>
              <div style={{ fontSize: 12, color: "#64748b", marginBottom: 8 }}>
                {serviceName(slo.serviceId)} · janela de {slo.windowDays} dias
              </div>
              <Can level="operate">
                <div className="flex gap-1">
                  <Button variant="ghost" onClick={() => openEdit("slo", slo)}>Editar</Button>
                  <Button variant="ghost" onClick={() => setDeleteTarget({ kind: "slo", target: slo })}>Excluir</Button>
                </div>
              </Can>
            </div>
          )}
        />
      )}

      {tab === "budgets" && (
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>Error Budget (SLO)</h2></div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: 16, padding: 16 }}>
            {budgets.items.flatMap((budget) => {
              const slo = slos.items.find((entry) => entry.id === budget.sloId);
              if (!slo) return [];
              const consumed = Number(budget.consumedPercent);
              const barColor = consumed >= 100 ? "#dc2626" : consumed >= 50 ? "#d97706" : "#22c55e";
              return [
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
                </div>,
              ];
            })}
            {budgets.items.length === 0 && (
              <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 12, color: "#94a3b8", fontSize: 13 }}>
                {listError ? "—" : "Nenhum orçamento registrado"}
              </div>
            )}
          </div>
        </div>
      )}

      <Dialog
        visible={form !== null}
        onHide={() => setForm(null)}
        header={formTitle}
      >
        {form && (
          <form onSubmit={submitForm} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            <FormField label="Nome" error={null}>
              <input
                autoFocus
                value={values.name}
                onChange={(event) => setValues((current) => ({ ...current, name: event.target.value }))}
                placeholder="Nome *"
                style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, width: "100%" }}
              />
            </FormField>

            {form.kind === "service" && (
              <FormField label="Descrição (opcional)" error={null}>
                <textarea
                  value={values.description}
                  onChange={(event) => setValues((current) => ({ ...current, description: event.target.value }))}
                  placeholder="Descrição (opcional)"
                  rows={2}
                  style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, resize: "vertical", fontFamily: "inherit", width: "100%" }}
                />
              </FormField>
            )}

            {form.kind === "policy" && (
              <>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                  <FormField label="Resposta (min)" error={null}>
                    <input
                      type="number"
                      min={1}
                      value={values.responseMinutes}
                      onChange={(event) => setValues((current) => ({ ...current, responseMinutes: event.target.value }))}
                      style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, width: "100%" }}
                    />
                  </FormField>
                  <FormField label="Resolução (min)" error={null}>
                    <input
                      type="number"
                      min={1}
                      value={values.resolutionMinutes}
                      onChange={(event) => setValues((current) => ({ ...current, resolutionMinutes: event.target.value }))}
                      style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, width: "100%" }}
                    />
                  </FormField>
                </div>
                <FormField label="Serviço" error={null}>
                  <SelectField
                    value={values.serviceId}
                    onChange={(value) => setValues((current) => ({ ...current, serviceId: value }))}
                    options={serviceOptions}
                    style={{ width: "100%" }}
                  />
                </FormField>
              </>
            )}

            {form.kind === "slo" && (
              <>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                  <FormField label="Alvo (%)" error={null}>
                    <input
                      type="number"
                      min={0.1}
                      max={100}
                      step="any"
                      value={values.targetPercent}
                      onChange={(event) => setValues((current) => ({ ...current, targetPercent: event.target.value }))}
                      style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, width: "100%" }}
                    />
                  </FormField>
                  <FormField label="Janela (dias)" error={null}>
                    <input
                      type="number"
                      min={1}
                      value={values.windowDays}
                      onChange={(event) => setValues((current) => ({ ...current, windowDays: event.target.value }))}
                      style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, width: "100%" }}
                    />
                  </FormField>
                </div>
                <FormField label="Serviço" error={null}>
                  <SelectField
                    value={values.serviceId}
                    onChange={(value) => setValues((current) => ({ ...current, serviceId: value }))}
                    options={services.items.map((service) => ({ value: service.id, label: service.name }))}
                    style={{ width: "100%" }}
                  />
                </FormField>
              </>
            )}

            {formError && <FormError message={formError} />}
            <div className="flex justify-content-end gap-2" style={{ marginTop: 4 }}>
              <Button variant="ghost" onClick={() => setForm(null)}>
                Cancelar
              </Button>
              <Button variant="primary" type="submit">
                {form.mode === "create" ? "Criar" : "Salvar"}
              </Button>
            </div>
          </form>
        )}
      </Dialog>

      <ConfirmDialog
        visible={deleteTarget !== null}
        onHide={() => setDeleteTarget(null)}
        onConfirm={() => void confirmDelete()}
        header="Excluir"
        message={
          deleteTarget
            ? `Excluir "${deleteTarget.target.name}"? Vínculos (SLOs/políticas) bloqueiam a exclusão com 409.`
            : ""
        }
        confirmLabel="Excluir"
      />
    </div>
  );
}
