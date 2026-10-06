import { useCallback, useEffect, useState, type FormEvent } from "react";
import {
  createPdiActivity,
  createPdiCompetency,
  createPdiObjective,
  createPdiPhase,
  createPdiStrategy,
  deletePdiActivity,
  deletePdiCompetency,
  deletePdiObjective,
  deletePdiPhase,
  deletePdiStrategy,
  getPdiStructure,
  listPdiCompetencies,
  PDI_PHASE_STATUS_LABEL,
  PDI_PHASE_STATUS_OPTIONS,
  PDI_PHASE_STATUS_STYLE,
  updatePdiActivity,
  updatePdiCompetency,
  updatePdiObjective,
  updatePdiPhase,
  updatePdiStrategy,
  type Pdi,
  type PdiActivityNode,
  type PdiCompetency,
  type PdiObjectiveNode,
  type PdiPhaseNode,
  type PdiPhaseStatus,
  type PdiStrategyNode,
  type PdiStructure,
} from "@/services/api/pdi";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import { PdiAccompanimentDialog } from "@/components/pdi/PdiAccompanimentDialog";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog, Dialog } from "@/components/ui/Dialog";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { FormError, FormField } from "@/components/ui/FormField";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { SelectField } from "@/components/ui/SelectField";
import { StatusChip } from "@/components/ui/StatusChip";

/**
 * Estrutura do desenvolvimento (docs/30 §5-§8, Fase 2, ADR-032):
 * árvore objetivo → estratégia → fase → atividade + catálogo de
 * competências (escrita Admin). Escrita permitida a dono do plano ou
 * Admin (o backend responde 403/409 pt-BR — aqui só exibimos).
 * Datas "yyyy-MM-dd" formatadas à mão (anti-fuso, mesmo critério do
 * PdiPage). Após qualquer mutação a árvore é recarregada por inteiro.
 */

type NodeKind = "objective" | "strategy" | "phase" | "activity" | "competency";

interface NodeForm {
  kind: NodeKind;
  id: string | null;
  parentId: string | null;
  title: string;
  description: string;
  competencyId: string;
  currentSituation: string;
  desiredSituation: string;
  expectedResult: string;
  indicator: string;
  targetDate: string;
  responsibleId: string;
  startDate: string;
  endDate: string;
  completionCriteria: string;
  status: PdiPhaseStatus;
  percent: string;
  dueDate: string;
  completed: boolean;
  name: string;
}

function emptyForm(kind: NodeKind, parentId: string | null): NodeForm {
  return {
    kind,
    id: null,
    parentId,
    title: "",
    description: "",
    competencyId: "",
    currentSituation: "",
    desiredSituation: "",
    expectedResult: "",
    indicator: "",
    targetDate: "",
    responsibleId: "",
    startDate: "",
    endDate: "",
    completionCriteria: "",
    status: "NotStarted",
    percent: "0",
    dueDate: "",
    completed: false,
    name: "",
  };
}

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

function formatDateOnly(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
}

const dateInputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #e5e7eb",
  borderRadius: 6,
  width: "100%",
};

const textAreaStyle: React.CSSProperties = {
  resize: "vertical",
  padding: "8px 10px",
  fontSize: 13,
  border: "1px solid #e5e7eb",
  borderRadius: 6,
  fontFamily: "inherit",
};

interface PdiStructureDialogProps {
  visible: boolean;
  onHide: () => void;
  plan: Pdi | null;
  isAdmin: boolean;
  currentUserId?: string | null;
}

export function PdiStructureDialog({
  visible,
  onHide,
  plan,
  isAdmin,
  currentUserId = null,
}: PdiStructureDialogProps) {
  const planUserId = plan?.userId ?? null;

  const [structure, setStructure] = useState<PdiStructure | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [competencies, setCompetencies] = useState<PdiCompetency[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);

  const [form, setForm] = useState<NodeForm | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const [confirm, setConfirm] = useState<{
    message: string;
    run: () => Promise<void>;
  } | null>(null);

  // Acompanhamento da fase (Fase 3 — ADR-033).
  const [accompaniedPhase, setAccompaniedPhase] =
    useState<PdiPhaseNode | null>(null);

  // Catálogo de competências (Admin).
  const [catalogVisible, setCatalogVisible] = useState(false);
  const [catalogForm, setCatalogForm] = useState<{
    id: string | null;
    name: string;
    description: string;
  } | null>(null);
  const [catalogSaving, setCatalogSaving] = useState(false);
  const [catalogError, setCatalogError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (!planUserId) return;
    try {
      const [nextStructure, nextCompetencies] = await Promise.all([
        getPdiStructure(planUserId),
        listPdiCompetencies(),
      ]);
      setStructure(nextStructure);
      setCompetencies(nextCompetencies);
    } catch (caught) {
      setError(messageFor(caught));
    }
  }, [planUserId]);

  useEffect(() => {
    if (!visible || !planUserId) return;
    setLoading(true);
    setError(null);
    setStructure(null);
    void (async () => {
      try {
        const [nextStructure, nextCompetencies, nextUsers] = await Promise.all([
          getPdiStructure(planUserId),
          listPdiCompetencies(),
          isAdmin ? listUsers() : Promise.resolve([] as AdminUser[]),
        ]);
        setStructure(nextStructure);
        setCompetencies(nextCompetencies);
        setUsers(nextUsers);
      } catch (caught) {
        setError(messageFor(caught));
      } finally {
        setLoading(false);
      }
    })();
  }, [visible, planUserId, isAdmin]);

  const openObjectiveForm = (objective: PdiObjectiveNode | null) => {
    const base = emptyForm("objective", null);
    setFormError(null);
    setForm(
      objective
        ? {
            ...base,
            id: objective.id,
            title: objective.title,
            description: objective.description ?? "",
            competencyId: objective.competencyId ?? "",
            currentSituation: objective.currentSituation ?? "",
            desiredSituation: objective.desiredSituation ?? "",
            expectedResult: objective.expectedResult ?? "",
            indicator: objective.indicator ?? "",
            targetDate: objective.targetDate ?? "",
          }
        : base,
    );
  };

  const openStrategyForm = (
    objectiveId: string,
    strategy: PdiStrategyNode | null,
  ) => {
    const base = emptyForm("strategy", objectiveId);
    setFormError(null);
    setForm(
      strategy
        ? {
            ...base,
            id: strategy.id,
            title: strategy.title,
            description: strategy.description ?? "",
          }
        : base,
    );
  };

  const openPhaseForm = (strategyId: string, phase: PdiPhaseNode | null) => {
    const base = emptyForm("phase", strategyId);
    setFormError(null);
    setForm(
      phase
        ? {
            ...base,
            id: phase.id,
            title: phase.title,
            description: phase.description ?? "",
            responsibleId: phase.responsibleId ?? "",
            startDate: phase.startDate ?? "",
            endDate: phase.endDate ?? "",
            completionCriteria: phase.completionCriteria ?? "",
            status: phase.status,
            percent: String(phase.percent),
          }
        : base,
    );
  };

  const openActivityForm = (
    phaseId: string,
    activity: PdiActivityNode | null,
  ) => {
    const base = emptyForm("activity", phaseId);
    setFormError(null);
    setForm(
      activity
        ? {
            ...base,
            id: activity.id,
            title: activity.title,
            dueDate: activity.dueDate ?? "",
            completed: activity.completed,
          }
        : base,
    );
  };

  const openCompetencyForm = (competency: PdiCompetency | null) => {
    setCatalogError(null);
    setCatalogForm(
      competency
        ? {
            id: competency.id,
            name: competency.name,
            description: competency.description ?? "",
          }
        : { id: null, name: "", description: "" },
    );
  };

  const submitNode = async (event: FormEvent) => {
    event.preventDefault();
    if (!form || !plan) return;

    const title = form.kind === "competency" ? form.name.trim() : form.title.trim();
    if (!title) {
      setFormError("O título é obrigatório.");
      return;
    }
    if (form.kind === "phase") {
      if (form.startDate && form.endDate && form.endDate < form.startDate) {
        setFormError("O fim da fase não pode ser anterior ao início.");
        return;
      }
      const percent = Number(form.percent);
      if (!Number.isFinite(percent) || percent < 0 || percent > 100) {
        setFormError("O percentual deve estar entre 0 e 100.");
        return;
      }
    }

    setSaving(true);
    setFormError(null);
    try {
      switch (form.kind) {
        case "objective": {
          const input = {
            title,
            description: form.description.trim() || null,
            competencyId: form.competencyId || null,
            currentSituation: form.currentSituation.trim() || null,
            desiredSituation: form.desiredSituation.trim() || null,
            expectedResult: form.expectedResult.trim() || null,
            indicator: form.indicator.trim() || null,
            targetDate: form.targetDate || null,
          };
          if (form.id) await updatePdiObjective(form.id, input);
          else await createPdiObjective(plan.userId, input);
          break;
        }
        case "strategy": {
          const input = {
            title,
            description: form.description.trim() || null,
          };
          if (form.id) await updatePdiStrategy(form.id, input);
          else await createPdiStrategy(form.parentId ?? "", input);
          break;
        }
        case "phase": {
          const input = {
            title,
            description: form.description.trim() || null,
            responsibleId: form.responsibleId || null,
            startDate: form.startDate || null,
            endDate: form.endDate || null,
            completionCriteria: form.completionCriteria.trim() || null,
            status: form.status,
            percent: Number(form.percent),
          };
          if (form.id) await updatePdiPhase(form.id, input);
          else await createPdiPhase(form.parentId ?? "", input);
          break;
        }
        case "activity": {
          const input = {
            title,
            dueDate: form.dueDate || null,
            completed: form.completed,
          };
          if (form.id) await updatePdiActivity(form.id, input);
          else await createPdiActivity(form.parentId ?? "", input);
          break;
        }
        case "competency": {
          const input = {
            name: title,
            description: form.description.trim() || null,
          };
          if (form.id) await updatePdiCompetency(form.id, input);
          else await createPdiCompetency(input);
          break;
        }
      }
      const wasCompetency = form.kind === "competency";
      setForm(null);
      if (wasCompetency) {
        setCompetencies(await listPdiCompetencies());
        if (structure) setStructure(await getPdiStructure(plan.userId));
      } else {
        await reload();
      }
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  };

  const submitCatalog = async (event: FormEvent) => {
    event.preventDefault();
    if (!catalogForm) return;
    const name = catalogForm.name.trim();
    if (!name) {
      setCatalogError("O nome da competência é obrigatório.");
      return;
    }
    setCatalogSaving(true);
    setCatalogError(null);
    try {
      const input = { name, description: catalogForm.description.trim() || null };
      if (catalogForm.id) await updatePdiCompetency(catalogForm.id, input);
      else await createPdiCompetency(input);
      setCatalogForm(null);
      setCompetencies(await listPdiCompetencies());
      if (plan) setStructure(await getPdiStructure(plan.userId));
    } catch (caught) {
      setCatalogError(messageFor(caught));
    } finally {
      setCatalogSaving(false);
    }
  };

  const runDelete = async (
    label: string,
    action: () => Promise<void>,
  ) => {
    setConfirm({
      message: `Excluir ${label}? Essa ação não pode ser desfeita.`,
      run: async () => {
        try {
          await action();
          await reload();
        } catch (caught) {
          setError(messageFor(caught));
        }
      },
    });
  };

  const toggleActivity = async (activity: PdiActivityNode) => {
    try {
      await updatePdiActivity(activity.id, {
        title: activity.title,
        dueDate: activity.dueDate,
        completed: !activity.completed,
      });
      await reload();
    } catch (caught) {
      setError(messageFor(caught));
    }
  };

  const responsibleOptions = isAdmin
    ? users.map((candidate) => ({
        value: candidate.userId,
        label: `${candidate.displayName} · ${candidate.email}`,
      }))
    : plan
      ? [
          { value: plan.userId, label: plan.ownerName },
          ...(plan.managerId
            ? [{ value: plan.managerId, label: plan.managerName ?? plan.managerId }]
            : []),
        ]
      : [];

  const statusChip = (value: PdiPhaseStatus) => (
    <StatusChip
      label={PDI_PHASE_STATUS_LABEL[value] ?? value}
      background={PDI_PHASE_STATUS_STYLE[value]?.background ?? "#f1f5f9"}
      color={PDI_PHASE_STATUS_STYLE[value]?.color ?? "#64748b"}
    />
  );

  const renderActivity = (phase: PdiPhaseNode, activity: PdiActivityNode) => (
    <div
      key={activity.id}
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        fontSize: 13,
        padding: "4px 0",
      }}
    >
      <input
        type="checkbox"
        checked={activity.completed}
        onChange={() => void toggleActivity(activity)}
        aria-label={`Marcar "${activity.title}"`}
      />
      <span
        style={{
          textDecoration: activity.completed ? "line-through" : "none",
          color: activity.completed ? "#94a3b8" : "#334155",
          flex: 1,
        }}
      >
        {activity.title}
        {activity.dueDate && (
          <span style={{ color: "#94a3b8", fontSize: 12 }}>
            {" "}· até {formatDateOnly(activity.dueDate)}
          </span>
        )}
      </span>
      <Button
        variant="ghost"
        onClick={() => openActivityForm(phase.id, activity)}
      >
        Editar
      </Button>
      <Button
        variant="ghost"
        onClick={() =>
          void runDelete(`a atividade "${activity.title}"`, () =>
            deletePdiActivity(activity.id),
          )
        }
      >
        Excluir
      </Button>
    </div>
  );

  const renderPhase = (strategy: PdiStrategyNode, phase: PdiPhaseNode) => (
    <div
      key={phase.id}
      style={{
        background: "#f8fafc",
        border: "1px solid #eef2f7",
        borderRadius: 8,
        padding: 10,
        display: "flex",
        flexDirection: "column",
        gap: 6,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          flexWrap: "wrap",
        }}
      >
        {statusChip(phase.status)}
        <span style={{ fontWeight: 600, fontSize: 13 }}>{phase.title}</span>
        <span style={{ fontSize: 12, color: "#64748b" }}>{phase.percent}%</span>
        {(phase.startDate || phase.endDate) && (
          <span style={{ fontSize: 12, color: "#64748b" }}>
            {formatDateOnly(phase.startDate)} a {formatDateOnly(phase.endDate)}
          </span>
        )}
        {phase.responsibleName && (
          <span style={{ fontSize: 12, color: "#64748b" }}>
            Resp.: {phase.responsibleName}
          </span>
        )}
        <span style={{ flex: 1 }} />
        <Button
          variant="ghost"
          onClick={() => setAccompaniedPhase(phase)}
        >
          Acompanhar
        </Button>
        <Button
          variant="ghost"
          onClick={() => openActivityForm(phase.id, null)}
        >
          + Atividade
        </Button>
        <Button variant="ghost" onClick={() => openPhaseForm(strategy.id, phase)}>
          Editar
        </Button>
        <Button
          variant="ghost"
          onClick={() =>
            void runDelete(`a fase "${phase.title}"`, () =>
              deletePdiPhase(phase.id),
            )
          }
        >
          Excluir
        </Button>
      </div>
      {phase.description && (
        <div style={{ fontSize: 12, color: "#475569" }}>{phase.description}</div>
      )}
      {phase.completionCriteria && (
        <div style={{ fontSize: 12, color: "#64748b" }}>
          Critério de conclusão: {phase.completionCriteria}
        </div>
      )}
      <div>
        {phase.activities.length === 0 ? (
          <div style={{ fontSize: 12, color: "#94a3b8", fontStyle: "italic" }}>
            Nenhuma atividade — adicione ações concretas da fase.
          </div>
        ) : (
          phase.activities.map((activity) => renderActivity(phase, activity))
        )}
      </div>
    </div>
  );

  const renderStrategy = (
    objective: PdiObjectiveNode,
    strategy: PdiStrategyNode,
  ) => (
    <div
      key={strategy.id}
      style={{
        borderLeft: "3px solid #e5e7eb",
        paddingLeft: 12,
        display: "flex",
        flexDirection: "column",
        gap: 8,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          flexWrap: "wrap",
        }}
      >
        <span style={{ fontWeight: 600, fontSize: 13 }}>{strategy.title}</span>
        {strategy.description && (
          <span style={{ fontSize: 12, color: "#64748b", maxWidth: 380 }}>
            {strategy.description}
          </span>
        )}
        <span style={{ flex: 1 }} />
        <Button variant="ghost" onClick={() => openPhaseForm(strategy.id, null)}>
          + Fase
        </Button>
        <Button
          variant="ghost"
          onClick={() => openStrategyForm(objective.id, strategy)}
        >
          Editar
        </Button>
        <Button
          variant="ghost"
          onClick={() =>
            void runDelete(`a estratégia "${strategy.title}"`, () =>
              deletePdiStrategy(strategy.id),
            )
          }
        >
          Excluir
        </Button>
      </div>
      {strategy.phases.length === 0 ? (
        <div style={{ fontSize: 12, color: "#94a3b8", fontStyle: "italic" }}>
          Nenhuma fase — divida a estratégia em marcos com prazo.
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {strategy.phases.map((phase) => renderPhase(strategy, phase))}
        </div>
      )}
    </div>
  );

  const renderObjective = (objective: PdiObjectiveNode) => (
    <div
      key={objective.id}
      style={{
        background: "white",
        border: "1px solid #e5e7eb",
        borderRadius: 8,
        padding: 14,
        display: "flex",
        flexDirection: "column",
        gap: 10,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          flexWrap: "wrap",
        }}
      >
        <span style={{ fontWeight: 600, fontSize: 14 }}>{objective.title}</span>
        {objective.competencyName && (
          <StatusChip
            label={objective.competencyName}
            background="#e0e7ff"
            color="#4338ca"
          />
        )}
        {objective.targetDate && (
          <span style={{ fontSize: 12, color: "#64748b" }}>
            Prazo: {formatDateOnly(objective.targetDate)}
          </span>
        )}
        <span style={{ flex: 1 }} />
        <Button
          variant="ghost"
          onClick={() => openStrategyForm(objective.id, null)}
        >
          + Estratégia
        </Button>
        <Button variant="ghost" onClick={() => openObjectiveForm(objective)}>
          Editar
        </Button>
        <Button
          variant="ghost"
          onClick={() =>
            void runDelete(`o objetivo "${objective.title}"`, () =>
              deletePdiObjective(objective.id),
            )
          }
        >
          Excluir
        </Button>
      </div>

      {objective.description && (
        <div style={{ fontSize: 13, color: "#334155" }}>
          {objective.description}
        </div>
      )}

      {(objective.currentSituation ||
        objective.desiredSituation ||
        objective.expectedResult ||
        objective.indicator) && (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "4px 16px",
            fontSize: 12,
            color: "#64748b",
          }}
        >
          {objective.currentSituation && (
            <div>Atual: {objective.currentSituation}</div>
          )}
          {objective.desiredSituation && (
            <div>Desejada: {objective.desiredSituation}</div>
          )}
          {objective.expectedResult && (
            <div>Resultado: {objective.expectedResult}</div>
          )}
          {objective.indicator && <div>Indicador: {objective.indicator}</div>}
        </div>
      )}

      {objective.strategies.length === 0 ? (
        <div style={{ fontSize: 12, color: "#94a3b8", fontStyle: "italic" }}>
          Nenhuma estratégia — declare como o objetivo será desenvolvido.
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {objective.strategies.map((strategy) =>
            renderStrategy(objective, strategy),
          )}
        </div>
      )}
    </div>
  );

  const formTitle = !form
    ? ""
    : form.kind === "competency"
      ? `${form.id ? "Editar" : "Nova"} competência`
      : form.kind === "objective"
        ? `${form.id ? "Editar" : "Novo"} objetivo`
        : form.kind === "strategy"
          ? `${form.id ? "Editar" : "Nova"} estratégia`
          : form.kind === "phase"
            ? `${form.id ? "Editar" : "Nova"} fase`
            : `${form.id ? "Editar" : "Nova"} atividade`;

  return (
    <>
      <Dialog
        visible={visible}
        onHide={onHide}
        header={
          plan
            ? `Estrutura do PDI · ${plan.ownerName}`
            : "Estrutura do PDI"
        }
        width={760}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "flex", gap: 8 }}>
            <Button variant="primary" onClick={() => openObjectiveForm(null)}>
              <i className="pi pi-plus" style={{ fontSize: 11 }} />
              Novo objetivo
            </Button>
            {isAdmin && (
              <Button variant="ghost" onClick={() => setCatalogVisible(true)}>
                Competências
              </Button>
            )}
          </div>

          {error && <ErrorBanner message={error} onRetry={() => void reload()} />}

          {loading ? (
            <LoadingBlock />
          ) : !structure || structure.objectives.length === 0 ? (
            <NoticeBanner
              message='Nenhum objetivo ainda — use "Novo objetivo" para começar o planejamento (docs/30 §5).'
              background="#f8fafc"
              color="#475569"
            />
          ) : (
            <div
              style={{ display: "flex", flexDirection: "column", gap: 12 }}
            >
              {structure.objectives.map((objective) =>
                renderObjective(objective),
              )}
            </div>
          )}
        </div>
      </Dialog>

      <Dialog
        visible={form !== null}
        onHide={() => setForm(null)}
        header={formTitle}
        width={560}
      >
        {form && (
          <form
            onSubmit={(event) => void submitNode(event)}
            style={{ display: "flex", flexDirection: "column", gap: 12 }}
          >
            {(form.kind === "objective" ||
              form.kind === "strategy" ||
              form.kind === "phase" ||
              form.kind === "activity") && (
              <FormField label="Título">
                <input
                  type="text"
                  value={form.title}
                  onChange={(event) =>
                    setForm({ ...form, title: event.target.value })
                  }
                  maxLength={200}
                  autoFocus
                  style={dateInputStyle}
                />
              </FormField>
            )}

            {form.kind === "competency" && (
              <FormField label="Nome da competência">
                <input
                  type="text"
                  value={form.name}
                  onChange={(event) =>
                    setForm({ ...form, name: event.target.value })
                  }
                  maxLength={120}
                  autoFocus
                  style={dateInputStyle}
                />
              </FormField>
            )}

            {(form.kind === "objective" ||
              form.kind === "strategy" ||
              form.kind === "phase" ||
              form.kind === "competency") && (
              <FormField label="Descrição">
                <textarea
                  value={form.description}
                  onChange={(event) =>
                    setForm({ ...form, description: event.target.value })
                  }
                  rows={3}
                  maxLength={2000}
                  style={textAreaStyle}
                />
              </FormField>
            )}

            {form.kind === "objective" && (
              <>
                <FormField label="Competência trabalhada">
                  <SelectField
                    value={form.competencyId}
                    onChange={(value) =>
                      setForm({ ...form, competencyId: value })
                    }
                    options={competencies.map((competency) => ({
                      value: competency.id,
                      label: competency.name,
                    }))}
                    placeholder="Sem competência"
                    aria-label="Competência do objetivo"
                  />
                </FormField>
                <div style={{ display: "flex", gap: 12 }}>
                  <FormField label="Situação atual" width={236}>
                    <input
                      type="text"
                      value={form.currentSituation}
                      onChange={(event) =>
                        setForm({ ...form, currentSituation: event.target.value })
                      }
                      maxLength={1000}
                      style={dateInputStyle}
                    />
                  </FormField>
                  <FormField label="Situação desejada" width={236}>
                    <input
                      type="text"
                      value={form.desiredSituation}
                      onChange={(event) =>
                        setForm({ ...form, desiredSituation: event.target.value })
                      }
                      maxLength={1000}
                      style={dateInputStyle}
                    />
                  </FormField>
                </div>
                <div style={{ display: "flex", gap: 12 }}>
                  <FormField label="Resultado esperado" width={236}>
                    <input
                      type="text"
                      value={form.expectedResult}
                      onChange={(event) =>
                        setForm({ ...form, expectedResult: event.target.value })
                      }
                      maxLength={1000}
                      style={dateInputStyle}
                    />
                  </FormField>
                  <FormField label="Indicador" width={236}>
                    <input
                      type="text"
                      value={form.indicator}
                      onChange={(event) =>
                        setForm({ ...form, indicator: event.target.value })
                      }
                      maxLength={200}
                      style={dateInputStyle}
                    />
                  </FormField>
                </div>
                <FormField label="Prazo" width={180}>
                  <input
                    type="date"
                    value={form.targetDate}
                    onChange={(event) =>
                      setForm({ ...form, targetDate: event.target.value })
                    }
                    style={dateInputStyle}
                  />
                </FormField>
              </>
            )}

            {form.kind === "phase" && (
              <>
                <div style={{ display: "flex", gap: 12 }}>
                  <FormField label="Início" width={180}>
                    <input
                      type="date"
                      value={form.startDate}
                      onChange={(event) =>
                        setForm({ ...form, startDate: event.target.value })
                      }
                      style={dateInputStyle}
                    />
                  </FormField>
                  <FormField label="Fim" width={180}>
                    <input
                      type="date"
                      value={form.endDate}
                      onChange={(event) =>
                        setForm({ ...form, endDate: event.target.value })
                      }
                      style={dateInputStyle}
                    />
                  </FormField>
                </div>
                <div style={{ display: "flex", gap: 12 }}>
                  <FormField label="Status">
                    <SelectField
                      value={form.status}
                      onChange={(value) =>
                        setForm({
                          ...form,
                          status: value as PdiPhaseStatus,
                        })
                      }
                      options={[...PDI_PHASE_STATUS_OPTIONS]}
                      aria-label="Status da fase"
                    />
                  </FormField>
                  <FormField label="Percentual (%)" width={130}>
                    <input
                      type="number"
                      min={0}
                      max={100}
                      value={form.percent}
                      onChange={(event) =>
                        setForm({ ...form, percent: event.target.value })
                      }
                      style={dateInputStyle}
                    />
                  </FormField>
                </div>
                <FormField label="Responsável">
                  <SelectField
                    value={form.responsibleId}
                    onChange={(value) =>
                      setForm({ ...form, responsibleId: value })
                    }
                    options={responsibleOptions}
                    placeholder="Sem responsável"
                    aria-label="Responsável pela fase"
                  />
                </FormField>
                <FormField label="Critérios de conclusão">
                  <textarea
                    value={form.completionCriteria}
                    onChange={(event) =>
                      setForm({
                        ...form,
                        completionCriteria: event.target.value,
                      })
                    }
                    rows={2}
                    maxLength={1000}
                    style={textAreaStyle}
                  />
                </FormField>
              </>
            )}

            {form.kind === "activity" && (
              <>
                <FormField label="Prazo" width={180}>
                  <input
                    type="date"
                    value={form.dueDate}
                    onChange={(event) =>
                      setForm({ ...form, dueDate: event.target.value })
                    }
                    style={dateInputStyle}
                  />
                </FormField>
                <label
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    fontSize: 13,
                    color: "#334155",
                  }}
                >
                  <input
                    type="checkbox"
                    checked={form.completed}
                    onChange={(event) =>
                      setForm({ ...form, completed: event.target.checked })
                    }
                  />
                  Atividade concluída
                </label>
              </>
            )}

            {formError && <FormError message={formError} />}

            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
              <Button
                variant="ghost"
                type="button"
                onClick={() => setForm(null)}
              >
                Cancelar
              </Button>
              <Button variant="primary" type="submit" disabled={saving}>
                {saving ? "Salvando…" : "Salvar"}
              </Button>
            </div>
          </form>
        )}
      </Dialog>

      <Dialog
        visible={catalogVisible}
        onHide={() => {
          setCatalogVisible(false);
          setCatalogForm(null);
          setCatalogError(null);
        }}
        header="Competências do catálogo"
        width={520}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Catálogo compartilhado usado nos objetivos (docs/30 §23) — somente
            administrador gerencia.
          </div>

          {competencies.length === 0 ? (
            <NoticeBanner
              message="Nenhuma competência cadastrada."
              background="#f8fafc"
              color="#475569"
            />
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {competencies.map((competency) => (
                <div
                  key={competency.id}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    fontSize: 13,
                    border: "1px solid #eef2f7",
                    borderRadius: 6,
                    padding: "6px 10px",
                  }}
                >
                  <span style={{ fontWeight: 500 }}>{competency.name}</span>
                  {competency.description && (
                    <span style={{ color: "#94a3b8", fontSize: 12 }}>
                      {competency.description}
                    </span>
                  )}
                  <span style={{ flex: 1 }} />
                  <Button
                    variant="ghost"
                    onClick={() => openCompetencyForm(competency)}
                  >
                    Editar
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() =>
                      void runDelete(
                        `a competência "${competency.name}"`,
                        async () => {
                          await deletePdiCompetency(competency.id);
                          setCompetencies(await listPdiCompetencies());
                          if (plan)
                            setStructure(await getPdiStructure(plan.userId));
                        },
                      )
                    }
                  >
                    Excluir
                  </Button>
                </div>
              ))}
            </div>
          )}

          <form
            onSubmit={(event) => void submitCatalog(event)}
            style={{ display: "flex", flexDirection: "column", gap: 10 }}
          >
            <FormField label={catalogForm?.id ? "Editar competência" : "Nova competência"}>
              <input
                type="text"
                value={catalogForm?.name ?? ""}
                onChange={(event) =>
                  setCatalogForm({
                    id: catalogForm?.id ?? null,
                    name: event.target.value,
                    description: catalogForm?.description ?? "",
                  })
                }
                maxLength={120}
                placeholder="Ex.: Liderança"
                style={dateInputStyle}
              />
            </FormField>
            <FormField label="Descrição (opcional)">
              <input
                type="text"
                value={catalogForm?.description ?? ""}
                onChange={(event) =>
                  setCatalogForm({
                    id: catalogForm?.id ?? null,
                    name: catalogForm?.name ?? "",
                    description: event.target.value,
                  })
                }
                maxLength={500}
                style={dateInputStyle}
              />
            </FormField>

            {catalogError && <FormError message={catalogError} />}

            <div
              style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}
            >
              {catalogForm && (
                <Button
                  variant="ghost"
                  type="button"
                  onClick={() => setCatalogForm(null)}
                >
                  Limpar
                </Button>
              )}
              <Button
                variant="primary"
                type="submit"
                disabled={catalogSaving || !catalogForm?.name.trim()}
              >
                {catalogSaving ? "Salvando…" : "Salvar"}
              </Button>
            </div>
          </form>
        </div>
      </Dialog>

      <ConfirmDialog
        visible={confirm !== null}
        onHide={() => setConfirm(null)}
        onConfirm={() => void confirm?.run()}
        header="Confirmar exclusão"
        message={confirm?.message ?? ""}
        confirmLabel="Excluir"
      />

      <PdiAccompanimentDialog
        visible={accompaniedPhase !== null}
        onHide={() => setAccompaniedPhase(null)}
        plan={plan}
        phase={accompaniedPhase}
        isAdmin={isAdmin}
        currentUserId={currentUserId}
      />
    </>
  );
}
