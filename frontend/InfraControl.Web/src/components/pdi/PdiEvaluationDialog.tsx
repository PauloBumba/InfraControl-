import { useCallback, useEffect, useState, type FormEvent } from "react";
import {
  finalizePdiResult,
  getPdiEvaluation,
  PDI_RESULT_OUTCOME_OPTIONS,
  PDI_RESULT_OUTCOME_LABEL,
  PDI_STATUS_LABEL,
  PDI_STATUS_STYLE,
  savePdiAssessment,
  type Pdi,
  type PdiEvaluation,
  type PdiResultOutcome,
} from "@/services/api/pdi";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { FormField } from "@/components/ui/FormField";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { SelectField } from "@/components/ui/SelectField";
import { StatusChip } from "@/components/ui/StatusChip";
import { errorMessage } from "@/hooks/queries";

/**
 * Avaliação final do PDI (docs/30 §15.3, §16, Fase 6 — ADR-036): a
 * comparação do plano (inicial/durante) ao lado das duas avaliações e
 * do resultado final. Autoavaliação = dono; avaliação do gestor e
 * resultado = gestor/Admin (o backend decide — 403 pt-BR). Depois de
 * finalizado, tudo vira somente leitura (§16).
 */

interface PdiEvaluationDialogProps {
  visible: boolean;
  onHide: () => void;
  plan: Pdi | null;
  isAdmin: boolean;
  currentUserId?: string | null;
}

function formatDateOnly(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
}

function formatTimestamp(value: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString("pt-BR", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      });
}

export function PdiEvaluationDialog({
  visible,
  onHide,
  plan,
  isAdmin,
  currentUserId = null,
}: PdiEvaluationDialogProps) {
  const planUserId = plan?.userId ?? null;
  const isOwner = !!currentUserId && !!planUserId && currentUserId === planUserId;
  const isManager =
    !!currentUserId && !!plan?.managerId && currentUserId === plan.managerId;
  const canEvaluateManager = isAdmin || isManager;

  const [evaluation, setEvaluation] = useState<PdiEvaluation | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selfContent, setSelfContent] = useState("");
  const [managerContent, setManagerContent] = useState("");
  const [outcome, setOutcome] = useState<PdiResultOutcome>("Atingido");
  const [notes, setNotes] = useState("");

  const load = useCallback(async () => {
    if (!visible || !planUserId) return;
    setLoading(true);
    setError(null);
    setNotice(null);
    try {
      const data = await getPdiEvaluation(planUserId);
      setEvaluation(data);
      setSelfContent(data.selfAssessment?.content ?? "");
      setManagerContent(data.managerAssessment?.content ?? "");
      if (data.result) {
        setOutcome(data.result.outcome);
        setNotes(data.result.notes ?? "");
      }
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setLoading(false);
    }
  }, [visible, planUserId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (!visible || !planUserId) return null;

  const status = evaluation?.status ?? plan?.status ?? "Draft";
  const frozen =
    !!evaluation?.result || status === "Completed" || status === "Cancelled";
  const selfReady =
    !frozen && (status === "InProgress" || status === "UnderEvaluation");
  const managerReady =
    !frozen &&
    status === "UnderEvaluation" &&
    !!evaluation?.selfAssessment;
  const resultReady =
    !frozen && status === "UnderEvaluation" && !!evaluation?.managerAssessment;

  async function saveSelf(event: FormEvent) {
    event.preventDefault();
    if (!planUserId) return;
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await savePdiAssessment(planUserId, selfContent);
      setNotice(
        "Autoavaliação salva — o PDI segue em avaliação até o gestor responder.",
      );
      await load();
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setSaving(false);
    }
  }

  async function saveManager(event: FormEvent) {
    event.preventDefault();
    if (!planUserId) return;
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await savePdiAssessment(planUserId, managerContent);
      setNotice("Avaliação do gestor salva — o resultado final está liberado.");
      await load();
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setSaving(false);
    }
  }

  async function finalize(event: FormEvent) {
    event.preventDefault();
    if (!planUserId) return;
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await finalizePdiResult(planUserId, outcome, notes || null);
      setNotice("Resultado final registrado.");
      await load();
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setSaving(false);
    }
  }

  const detailRow = (label: string, value: string) => (
    <div className="flex flex-col gap-1">
      <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        {label}
      </span>
      <span className="text-sm text-slate-800">{value}</span>
    </div>
  );

  return (
    <Dialog
      visible={visible}
      onHide={onHide}
      header="Avaliação do PDI"
      style={{ width: "46rem", maxWidth: "95vw" }}
    >
      {loading ? (
        <LoadingBlock />
      ) : (
        <div className="flex flex-col gap-4">
          {error && <ErrorBanner message={error} onRetry={() => void load()} />}
          {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

          <section className="flex flex-col gap-3 rounded-lg border border-slate-200 bg-slate-50 p-4">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-slate-700">
                Plano (inicial · durante)
              </h3>
              <StatusChip
                label={PDI_STATUS_LABEL[status] ?? status}
                background={PDI_STATUS_STYLE[status]?.background ?? "#f1f5f9"}
                color={PDI_STATUS_STYLE[status]?.color ?? "#64748b"}
              />
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {detailRow("Estratégia", evaluation?.strategy ?? plan?.strategy ?? "—")}
              {detailRow("Prazo", formatDateOnly(evaluation?.targetDate ?? plan?.targetDate ?? null))}
              {detailRow("Gestor", evaluation?.managerName ?? "Sem gestor")}
              {detailRow(
                "Período",
                evaluation?.periodStart || evaluation?.periodEnd
                  ? `${formatDateOnly(evaluation.periodStart)} → ${formatDateOnly(evaluation.periodEnd)}`
                  : "—",
              )}
            </div>
            <p className="text-xs text-slate-500">
              A comparação final (§16) usa este plano como linha de partida e o
              resultado abaixo como desfecho.
            </p>
          </section>

          <form
            onSubmit={saveSelf}
            className="flex flex-col gap-3 rounded-lg border border-slate-200 p-4"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-slate-700">
                Autoavaliação do colaborador
              </h3>
              {evaluation?.selfAssessment && (
                <span className="text-xs text-slate-500">
                  {evaluation.selfAssessment.authorName ?? "—"} ·{" "}
                  {formatTimestamp(evaluation.selfAssessment.updatedAt)}
                </span>
              )}
            </div>
            <FormField label="O que precisava desenvolver e o que foi desenvolvido?">
              <textarea
                className="w-full rounded-md border border-slate-300 p-2 text-sm"
                rows={4}
                value={selfContent}
                onChange={(event) => setSelfContent(event.target.value)}
                disabled={!isOwner || !selfReady || saving}
                maxLength={4000}
              />
            </FormField>
            <div className="flex items-center justify-between gap-3">
              <span className="text-xs text-slate-500">
                {isOwner
                  ? "Salvar leva o PDI para Em avaliação (§15.3)."
                  : "Somente o colaborador escreve a própria autoavaliação."}
              </span>
              <Button
                type="submit"
                disabled={!isOwner || !selfReady || saving || !selfContent.trim()}
              >
                Salvar autoavaliação
              </Button>
            </div>
          </form>

          <form
            onSubmit={saveManager}
            className="flex flex-col gap-3 rounded-lg border border-slate-200 p-4"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-slate-700">
                Avaliação do gestor
              </h3>
              {evaluation?.managerAssessment && (
                <span className="text-xs text-slate-500">
                  {evaluation.managerAssessment.authorName ?? "—"} ·{" "}
                  {formatTimestamp(evaluation.managerAssessment.updatedAt)}
                </span>
              )}
            </div>
            <FormField label="Como foi a evolução no período?">
              <textarea
                className="w-full rounded-md border border-slate-300 p-2 text-sm"
                rows={4}
                value={managerContent}
                onChange={(event) => setManagerContent(event.target.value)}
                disabled={!canEvaluateManager || !managerReady || saving}
                maxLength={4000}
              />
            </FormField>
            <div className="flex items-center justify-between gap-3">
              <span className="text-xs text-slate-500">
                {!evaluation?.selfAssessment
                  ? "Disponível depois da autoavaliação do colaborador."
                  : status !== "UnderEvaluation"
                    ? "Disponível com o PDI em avaliação."
                    : ""}
              </span>
              <Button
                type="submit"
                disabled={
                  !canEvaluateManager ||
                  !managerReady ||
                  saving ||
                  !managerContent.trim()
                }
              >
                Salvar avaliação do gestor
              </Button>
            </div>
          </form>

          <form
            onSubmit={finalize}
            className="flex flex-col gap-3 rounded-lg border border-slate-200 p-4"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-slate-700">
                Resultado final (§16)
              </h3>
              {evaluation?.result && (
                <span className="text-xs text-slate-500">
                  {evaluation.result.finalizedName ?? "—"} ·{" "}
                  {formatDateOnly(evaluation.result.finalizedAt)}
                </span>
              )}
            </div>

            {evaluation?.result ? (
              <>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-semibold text-slate-700">
                    {PDI_RESULT_OUTCOME_LABEL[evaluation.result.outcome] ??
                      evaluation.result.outcome}
                  </span>
                </div>
                {evaluation.result.notes && (
                  <p className="whitespace-pre-wrap text-sm text-slate-700">
                    {evaluation.result.notes}
                  </p>
                )}
              </>
            ) : (
              <>
                <FormField label="Resultado">
                  <SelectField
                    value={outcome}
                    options={PDI_RESULT_OUTCOME_OPTIONS.map((option) => ({
                      value: option.value,
                      label: option.label,
                    }))}
                    onChange={(value) =>
                      setOutcome(value as PdiResultOutcome)
                    }
                    disabled={!canEvaluateManager || !resultReady || saving}
                  />
                </FormField>
                <FormField label="Nota (opcional)">
                  <textarea
                    className="w-full rounded-md border border-slate-300 p-2 text-sm"
                    rows={3}
                    value={notes}
                    onChange={(event) => setNotes(event.target.value)}
                    disabled={!canEvaluateManager || !resultReady || saving}
                    maxLength={4000}
                  />
                </FormField>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-xs text-slate-500">
                    Conclui o PDI — ou o devolve ao ciclo quando o resultado é
                    "Replanejado". Não pode ser alterado depois.
                  </span>
                  <Button
                    type="submit"
                    variant="primary"
                    disabled={!canEvaluateManager || !resultReady || saving}
                  >
                    Finalizar resultado
                  </Button>
                </div>
              </>
            )}
          </form>

          {frozen && (
            <NoticeBanner message="Avaliação finalizada — este PDI não aceita mais alterações de avaliação." />
          )}
        </div>
      )}
    </Dialog>
  );
}
