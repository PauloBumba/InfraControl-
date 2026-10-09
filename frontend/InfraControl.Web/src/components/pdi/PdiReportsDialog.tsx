import { useCallback, useEffect, useState } from "react";
import {
  downloadPdiDifficultiesReport,
  downloadPdiIndividualReport,
  downloadPdiSemesterReport,
  getPdiDifficultiesReport,
  getPdiIndividualReport,
  getPdiIndicators,
  getPdiSemesterReport,
  type Pdi,
  type PdiDifficultiesReport,
  type PdiIndividualReport,
  type PdiIndicators,
  type PdiReportFormat,
  type PdiSemesterReport,
} from "@/services/api/pdi";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { FormField } from "@/components/ui/FormField";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { SelectField } from "@/components/ui/SelectField";

/**
 * Relatórios e indicadores do PDI (docs/30 §21, §22 — Fase 8,
 * ADR-038). 4 abas: Indicadores e Dificuldades (Admin), Individual
 * (Admin escolhe o plano; Operador vê o próprio) e Semestral (Admin).
 * Filtros "De/Até" (AAAA-MM-DD) valem para os relatórios com período;
 * exportação PDF/Excel reutiliza os mesmos dados (blob → download).
 */

type TabKey = "indicators" | "difficulties" | "individual" | "semester";

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

function signed(value: number): string {
  return `${value > 0 ? "+" : ""}${value}`;
}

const dateInputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #e5e7eb",
  borderRadius: 6,
  width: "100%",
};

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
      <div style={{ fontWeight: 600, fontSize: 13, color: "#0f172a" }}>
        {title}
      </div>
      {children}
    </div>
  );
}

function Field({ label, value }: { label: string; value: string | number }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        gap: 12,
        fontSize: 13,
      }}
    >
      <span style={{ color: "#64748b" }}>{label}</span>
      <span style={{ fontWeight: 600, color: "#0f172a" }}>{value}</span>
    </div>
  );
}

export function PdiReportsDialog({
  visible,
  onHide,
  isAdmin,
  plans,
  ownUserId,
}: {
  visible: boolean;
  onHide: () => void;
  isAdmin: boolean;
  plans: Pdi[];
  ownUserId: string | null;
}) {
  const [tab, setTab] = useState<TabKey>("indicators");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  const [indicators, setIndicators] = useState<PdiIndicators | null>(null);
  const [difficulties, setDifficulties] =
    useState<PdiDifficultiesReport | null>(null);
  const [semester, setSemester] = useState<PdiSemesterReport | null>(null);
  const [individual, setIndividual] =
    useState<PdiIndividualReport | null>(null);
  const [individualUserId, setIndividualUserId] = useState("");

  const targetUserId = isAdmin ? individualUserId : (ownUserId ?? "");

  useEffect(() => {
    if (!visible) return;
    if (!isAdmin && tab !== "individual") setTab("individual");
    if (isAdmin && !individualUserId)
      setIndividualUserId(plans[0]?.userId ?? "");
  }, [visible, isAdmin, tab, plans, individualUserId]);

  const loadTab = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      if (tab === "indicators") {
        setIndicators(
          await getPdiIndicators(from || undefined, to || undefined),
        );
      } else if (tab === "difficulties") {
        setDifficulties(
          await getPdiDifficultiesReport(from || undefined, to || undefined),
        );
      } else if (tab === "semester") {
        setSemester(
          await getPdiSemesterReport(from || undefined, to || undefined),
        );
      } else if (targetUserId) {
        setIndividual(await getPdiIndividualReport(targetUserId));
      } else {
        setIndividual(null);
      }
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [tab, from, to, targetUserId]);

  useEffect(() => {
    if (visible && (tab !== "individual" || targetUserId))
      void loadTab();
  }, [visible, loadTab, tab, targetUserId]);

  const exportReport = async (format: PdiReportFormat) => {
    setExporting(true);
    setError(null);
    try {
      if (tab === "individual") {
        if (targetUserId)
          await downloadPdiIndividualReport(targetUserId, format);
      } else if (tab === "semester") {
        await downloadPdiSemesterReport(
          format,
          from || undefined,
          to || undefined,
        );
      } else {
        await downloadPdiDifficultiesReport(
          format,
          from || undefined,
          to || undefined,
        );
      }
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setExporting(false);
    }
  };

  const tabs: { key: TabKey; label: string }[] = [
    ...(isAdmin
      ? [
          { key: "indicators" as TabKey, label: "Indicadores" },
          { key: "difficulties" as TabKey, label: "Dificuldades" },
        ]
      : []),
    { key: "individual" as TabKey, label: "Individual" },
    ...(isAdmin
      ? [{ key: "semester" as TabKey, label: "Semestral" }]
      : []),
  ];

  const showFilters = isAdmin && tab !== "individual";
  const canExport = tab !== "indicators" && (isAdmin || tab === "individual");

  const renderIndicators = () => {
    if (!indicators) return null;
    const { execution, development, accompaniment, difficulties: blocks } =
      indicators;
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <Section title="Execução">
          <Field label="Planos no período" value={execution.totalPlans} />
          <Field
            label="Concluídos"
            value={`${execution.completedPlans} (${execution.completionRatePercent}%)`}
          />
          <Field label="Atrasados" value={execution.overduePlans} />
          <Field
            label="Fases concluídas / atrasadas"
            value={`${execution.phasesCompleted} / ${execution.phasesOverdue}`}
          />
          <Field
            label="Dias médios até concluir"
            value={execution.avgCompletionDays ?? "—"}
          />
        </Section>

        <Section title="Desenvolvimento">
          <Field
            label="Competências trabalhadas"
            value={development.competenciesWorked}
          />
          <Field label="Objetivos" value={development.objectivesTotal} />
          <Field
            label="Resultados (§16)"
            value={`Atingido ${development.resultsAtingido} · Parcial ${development.resultsParcial} · Não atingido ${development.resultsNaoAtingido} · Replanejado ${development.resultsReplanejado}`}
          />
          {development.evolutionByCompetency.length > 0 && (
            <div
              style={{
                display: "flex",
                flexWrap: "wrap",
                gap: 6,
                paddingTop: 4,
              }}
            >
              {development.evolutionByCompetency.map((item) => (
                <span
                  key={item.name}
                  style={{
                    fontSize: 11,
                    padding: "3px 8px",
                    borderRadius: 999,
                    border: "1px solid #e5e7eb",
                    background: "#f8fafc",
                    color: "#334155",
                  }}
                >
                  {item.name}: {item.objectives} objetivo
                  {item.objectives === 1 ? "" : "s"} ·{" "}
                  {item.avgPhasePercent}%
                </span>
              ))}
            </div>
          )}
        </Section>

        <Section title="Acompanhamento">
          <Field
            label="Check-ins (por plano)"
            value={`${accompaniment.checkIns} (${accompaniment.checkInsPerPlan})`}
          />
          <Field label="Feedbacks" value={accompaniment.feedbacks} />
          <Field
            label="Evidências validadas"
            value={accompaniment.evidencesValidated}
          />
        </Section>

        <Section title="Dificuldades">
          <Field label="Impedimentos" value={blocks.totalImpediments} />
          <Field
            label="Dias médios p/ resolver"
            value={blocks.avgResolutionDays ?? "—"}
          />
          {blocks.byType.map((share) => (
            <Field
              key={share.type}
              label={share.type}
              value={`${share.count} (${share.percent}%)`}
            />
          ))}
        </Section>
      </div>
    );
  };

  const renderDifficulties = () => {
    if (!difficulties) return null;
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <Section title="Consolidado">
          <Field
            label="Período"
            value={`${formatDateOnly(difficulties.from)} a ${formatDateOnly(difficulties.to)}`}
          />
          <Field label="Dificuldades" value={difficulties.total} />
          <Field label="Resolvidas" value={difficulties.resolved} />
          <Field
            label="Dias médios p/ resolver"
            value={difficulties.avgResolutionDays ?? "—"}
          />
        </Section>
        <Section title="Por tipo">
          {difficulties.byType.map((share) => (
            <Field
              key={share.type}
              label={share.type}
              value={`${share.count} (${share.percent}%)`}
            />
          ))}
        </Section>
      </div>
    );
  };

  const renderSemester = () => {
    if (!semester) return null;
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <Section title={semester.label}>
          <Field
            label="Período"
            value={`${formatDateOnly(semester.from)} a ${formatDateOnly(semester.to)}`}
          />
          <Field label="Planos no período" value={semester.plansInPeriod} />
          <Field
            label="Planos criados"
            value={semester.plansCreatedInPeriod}
          />
          <Field
            label="Concluídos"
            value={`${semester.completedInPeriod} (${semester.completionRatePercent}%)`}
          />
          <Field
            label="Dias médios até concluir"
            value={semester.avgCompletionDays ?? "—"}
          />
        </Section>

        <Section title="Tendências (vs. janela anterior)">
          <Field
            label="Planos anteriores"
            value={semester.trends.previousPlansInPeriod}
          />
          <Field
            label="Variação de planos"
            value={signed(semester.trends.plansDelta)}
          />
          <Field
            label="Variação de concluídos"
            value={signed(semester.trends.completedDelta)}
          />
          <Field
            label="Variação da taxa"
            value={`${signed(semester.trends.completionRateDelta)} p.p.`}
          />
        </Section>

        <Section title="Gestores">
          {semester.managers.map((manager) => (
            <Field
              key={manager.managerName}
              label={manager.managerName}
              value={`${manager.plans} planos · ${manager.completed} concluídos`}
            />
          ))}
        </Section>

        <Section title="Competências">
          {semester.competencies.map((competency) => (
            <Field
              key={competency.name}
              label={competency.name}
              value={`${competency.objectives} objetivos · ${competency.avgPhasePercent}%`}
            />
          ))}
        </Section>

        <Section title="Dificuldades">
          {semester.difficulties.map((share) => (
            <Field
              key={share.type}
              label={share.type}
              value={`${share.count} (${share.percent}%)`}
            />
          ))}
        </Section>
      </div>
    );
  };

  const renderIndividual = () => {
    if (!individual) return null;
    const objectives = individual.structure.objectives.length;
    const phases = individual.structure.objectives.reduce(
      (total, objective) =>
        total +
        objective.strategies.reduce(
          (sum, strategy) => sum + strategy.phases.length,
          0,
        ),
      0,
    );
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <Section title="Dados">
          <Field label="Colaborador" value={individual.ownerName} />
          <Field
            label="Gestor"
            value={individual.managerName ?? "—"}
          />
          <Field
            label="Período"
            value={`${formatDateOnly(individual.periodStart)} a ${formatDateOnly(individual.periodEnd)}`}
          />
          <Field label="Status" value={individual.evaluation.status} />
        </Section>
        <Section title="Estrutura">
          <Field label="Objetivos" value={objectives} />
          <Field label="Fases" value={phases} />
        </Section>
        <Section title="Acompanhamento">
          <Field
            label="Check-ins"
            value={individual.accompaniment.checkIns.length}
          />
          <Field
            label="Dificuldades"
            value={individual.accompaniment.impediments.length}
          />
          <Field
            label="Evidências"
            value={individual.accompaniment.evidences.length}
          />
          <Field
            label="Feedbacks"
            value={individual.accompaniment.feedbacks.length}
          />
        </Section>
        <Section title="Avaliação final">
          <Field
            label="Autoavaliação"
            value={individual.evaluation.selfAssessment ? "registrada" : "—"}
          />
          <Field
            label="Avaliação do gestor"
            value={individual.evaluation.managerAssessment ? "registrada" : "—"}
          />
          <Field
            label="Resultado"
            value={individual.evaluation.result?.outcome ?? "—"}
          />
        </Section>
      </div>
    );
  };

  return (
    <Dialog
      visible={visible}
      onHide={onHide}
      header="Relatórios do PDI"
      width={720}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {tabs.map((entry) => (
            <Button
              key={entry.key}
              variant={tab === entry.key ? "primary" : "ghost"}
              onClick={() => setTab(entry.key)}
            >
              {entry.label}
            </Button>
          ))}
        </div>

        {showFilters && (
          <div style={{ display: "flex", gap: 12, alignItems: "flex-end" }}>
            <FormField label="De" width={160}>
              <input
                type="date"
                value={from}
                onChange={(event) => setFrom(event.target.value)}
                style={dateInputStyle}
                aria-label="De"
              />
            </FormField>
            <FormField label="Até" width={160}>
              <input
                type="date"
                value={to}
                onChange={(event) => setTo(event.target.value)}
                style={dateInputStyle}
                aria-label="Até"
              />
            </FormField>
            {(from || to) && (
              <Button
                variant="ghost"
                onClick={() => {
                  setFrom("");
                  setTo("");
                }}
              >
                Limpar
              </Button>
            )}
          </div>
        )}

        {tab === "individual" && isAdmin && plans.length > 0 && (
          <FormField label="Colaborador">
            <SelectField
              value={individualUserId}
              onChange={setIndividualUserId}
              options={plans.map((plan) => ({
                value: plan.userId,
                label: `${plan.ownerName} · ${plan.ownerEmail}`,
              }))}
              placeholder="Selecione o colaborador"
              aria-label="Colaborador do relatório"
            />
          </FormField>
        )}

        <div
          style={{ display: "flex", justifyContent: "flex-end", gap: 6 }}
        >
          {canExport && (
            <>
              <Button
                variant="ghost"
                disabled={exporting}
                onClick={() => void exportReport("pdf")}
              >
                {exporting ? "Exportando…" : "PDF"}
              </Button>
              <Button
                variant="ghost"
                disabled={exporting}
                onClick={() => void exportReport("xlsx")}
              >
                {exporting ? "Exportando…" : "Excel"}
              </Button>
            </>
          )}
        </div>

        {error && <ErrorBanner message={error} onRetry={() => void loadTab()} />}

        {loading ? (
          <LoadingBlock />
        ) : tab === "indicators" ? (
          renderIndicators()
        ) : tab === "difficulties" ? (
          renderDifficulties()
        ) : tab === "semester" ? (
          renderSemester()
        ) : (
          renderIndividual()
        )}
      </div>
    </Dialog>
  );
}
