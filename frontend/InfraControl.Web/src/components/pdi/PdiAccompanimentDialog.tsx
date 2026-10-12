import { useCallback, useEffect, useState, type FormEvent } from "react";
import {
  createPdiCheckIn,
  createPdiFeedback,
  createPdiImpediment,
  createPdiProgress,
  deletePdiCheckIn,
  deletePdiFeedback,
  deletePdiImpediment,
  deletePdiProgress,
  downloadPdiEvidence,
  formatFileSize,
  getPdiAccompaniment,
  PDI_EVIDENCE_STATUS_LABEL,
  PDI_EVIDENCE_STATUS_STYLE,
  PDI_EVIDENCE_TYPE_LABEL,
  PDI_IMPEDIMENT_STATUS_LABEL,
  PDI_IMPEDIMENT_STATUS_STYLE,
  parsePdiEvidenceStatus,
  parsePdiImpedimentStatus,
  reviewPdiEvidence,
  updatePdiCheckIn,
  updatePdiImpediment,
  uploadPdiEvidence,
  type Pdi,
  type PdiAccompaniment,
  type PdiEvidenceReviewDecision,
  type PdiEvidenceType,
  type PdiImpedimentStatus,
  type PdiPhaseNode,
} from "@/services/api/pdi";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog, Dialog } from "@/components/ui/Dialog";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { FormError, FormField } from "@/components/ui/FormField";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { SelectField } from "@/components/ui/SelectField";
import { StatusChip } from "@/components/ui/StatusChip";
import { errorMessage } from "@/hooks/queries";

/**
 * Acompanhamento da fase (docs/30 §9-§11, §12, §13, Fase 3/4 —
 * ADR-033/ADR-034): cinco seções — check-ins bilaterais,
 * progresso/comentários, impedimentos, evidências (upload do colaborador,
 * revisão do gestor) e feedbacks — lidas no GET único do plano e filtradas
 * pela fase aberta.
 * O backend decide permissões (403 pt-BR); aqui a UI só oferece o que o
 * papel atual costuma fazer: relato do dono, resposta/revisão do gestor,
 * exclusão de feedback restrita ao Admin. Datas "yyyy-MM-dd" formatadas à
 * mão (anti-fuso, mesmo critério do PdiStructureDialog).
 */

type TabKey =
  | "checkins"
  | "progress"
  | "impediments"
  | "evidences"
  | "feedback";

interface AccForm {
  kind: "checkin" | "progress" | "impediment" | "feedback" | "evidence" | "review";
  id: string | null;
  side: "create" | "relato" | "resposta";
  date: string;
  whatDone: string;
  whatLearned: string;
  difficulty: string;
  needSupport: string;
  managerFeedback: string;
  nextAction: string;
  percent: string;
  comment: string;
  type: string;
  description: string;
  impact: string;
  responsibleId: string;
  actionTaken: string;
  status: PdiImpedimentStatus;
  recipientId: string;
  content: string;
  file: File | null;
  decision: PdiEvidenceReviewDecision;
}

function emptyForm(kind: AccForm["kind"], plan: Pdi | null): AccForm {
  return {
    kind,
    id: null,
    side: "create",
    date: new Date().toISOString().slice(0, 10),
    whatDone: "",
    whatLearned: "",
    difficulty: "",
    needSupport: "",
    managerFeedback: "",
    nextAction: "",
    percent: "",
    comment: "",
    type: "",
    description: "",
    impact: "",
    responsibleId: "",
    actionTaken: "",
    status: "Open",
    recipientId: plan?.userId ?? "",
    content: "",
    file: null,
    decision: "Validated",
  };
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

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #e5e7eb",
  borderRadius: 6,
  fontFamily: "inherit",
  width: "100%",
};

const textAreaStyle: React.CSSProperties = {
  ...inputStyle,
  resize: "vertical",
  fontFamily: "inherit",
};

const boxStyle: React.CSSProperties = {
  background: "#f8fafc",
  border: "1px solid #eef2f7",
  borderRadius: 8,
  padding: 10,
  display: "flex",
  flexDirection: "column",
  gap: 6,
};

const metaStyle: React.CSSProperties = { fontSize: 12, color: "#64748b" };

const bodyStyle: React.CSSProperties = { fontSize: 13, color: "#334155" };

interface PdiAccompanimentDialogProps {
  visible: boolean;
  onHide: () => void;
  plan: Pdi | null;
  phase: PdiPhaseNode | null;
  isAdmin: boolean;
  currentUserId: string | null;
}

export function PdiAccompanimentDialog({
  visible,
  onHide,
  plan,
  phase,
  isAdmin,
  currentUserId,
}: PdiAccompanimentDialogProps) {
  const [data, setData] = useState<PdiAccompaniment | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [tab, setTab] = useState<TabKey>("checkins");
  const [form, setForm] = useState<AccForm | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const [confirm, setConfirm] = useState<{
    message: string;
    run: () => Promise<void>;
  } | null>(null);

  const planUserId = plan?.userId ?? null;

  const isOwner = plan !== null && plan.userId === currentUserId;
  const isManager =
    plan !== null && plan.managerId !== null && plan.managerId === currentUserId;
  const canReport = isAdmin || isOwner;
  const canAssist = isAdmin || isOwner || isManager;
  const canRespond = isAdmin || isManager;
  const canReview = isAdmin || isManager;
  const canView = isAdmin || isOwner || isManager;

  const reload = useCallback(async () => {
    if (!planUserId) return;
    try {
      setData(await getPdiAccompaniment(planUserId));
    } catch (caught) {
      setError(errorMessage(caught));
    }
  }, [planUserId]);

  useEffect(() => {
    if (!visible || !planUserId) return;
    setLoading(true);
    setError(null);
    setData(null);
    void (async () => {
      try {
        setData(await getPdiAccompaniment(planUserId));
      } catch (caught) {
        setError(errorMessage(caught));
      } finally {
        setLoading(false);
      }
    })();
  }, [visible, planUserId]);

  const phaseId = phase?.id ?? null;
  const progresses =
    data?.progresses.filter((item) => item.phaseId === phaseId) ?? [];
  const checkIns = data?.checkIns.filter((item) => item.phaseId === phaseId) ?? [];
  const impediments =
    data?.impediments.filter((item) => item.phaseId === phaseId) ?? [];
  const feedbacks =
    data?.feedbacks.filter((item) => item.phaseId === phaseId) ?? [];
  const evidences =
    data?.evidences.filter((item) => item.phaseId === phaseId) ?? [];

  const responsibleOptions = plan
    ? [
        { value: plan.userId, label: plan.ownerName },
        ...(plan.managerId
          ? [
              {
                value: plan.managerId,
                label: plan.managerName ?? plan.managerId,
              },
            ]
          : []),
      ]
    : [];

  const runAction = async (action: () => Promise<void>) => {
    try {
      await action();
      await reload();
    } catch (caught) {
      setError(errorMessage(caught));
    }
  };

  const runDelete = (label: string, action: () => Promise<void>) => {
    setConfirm({
      message: `Excluir ${label}? Essa ação não pode ser desfeita.`,
      run: () => runAction(action),
    });
  };

  // ---- Abertura dos formulários -----------------------------------------

  const openProgressForm = () => {
    setFormError(null);
    setForm(emptyForm("progress", plan));
  };

  const openCheckInCreate = () => {
    setFormError(null);
    setForm(emptyForm("checkin", plan));
  };

  const openCheckInRelato = (id: string) => {
    const item = checkIns.find((checkIn) => checkIn.id === id);
    if (!item) return;
    setFormError(null);
    setForm({
      ...emptyForm("checkin", plan),
      id,
      side: "relato",
      date: item.checkInDate,
      whatDone: item.whatDone,
      whatLearned: item.whatLearned ?? "",
      difficulty: item.difficulty ?? "",
      needSupport: item.needSupport ?? "",
    });
  };

  const openCheckInResposta = (id: string) => {
    const item = checkIns.find((checkIn) => checkIn.id === id);
    if (!item) return;
    setFormError(null);
    setForm({
      ...emptyForm("checkin", plan),
      id,
      side: "resposta",
      date: item.checkInDate,
      managerFeedback: item.managerFeedback ?? "",
      nextAction: item.nextAction ?? "",
    });
  };

  const openImpedimentForm = (id: string | null) => {
    setFormError(null);
    const item = id ? impediments.find((entry) => entry.id === id) : null;
    setForm({
      ...emptyForm("impediment", plan),
      id,
      side: id ? "relato" : "create",
      date: item?.occurredAt ?? new Date().toISOString().slice(0, 10),
      type: item?.type ?? "",
      description: item?.description ?? "",
      impact: item?.impact ?? "",
      responsibleId: item?.responsibleId ?? "",
      actionTaken: item?.actionTaken ?? "",
      status: item ? parsePdiImpedimentStatus(item.status) : "Open",
    });
  };

  const openFeedbackForm = () => {
    setFormError(null);
    setForm(emptyForm("feedback", plan));
  };

  const openEvidenceForm = () => {
    setFormError(null);
    setForm(emptyForm("evidence", plan));
  };

  const openReviewForm = (id: string) => {
    setFormError(null);
    setForm({ ...emptyForm("review", plan), id });
  };

  // ---- Envio -------------------------------------------------------------

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!form || !phase || !plan) return;

    if (form.kind === "progress") {
      const comment = form.comment.trim();
      if (!comment) {
        setFormError("O comentário do progresso é obrigatório.");
        return;
      }
      if (form.percent) {
        const percent = Number(form.percent);
        if (!Number.isFinite(percent) || percent < 0 || percent > 100) {
          setFormError("O percentual deve estar entre 0 e 100.");
          return;
        }
      }
    }

    if (form.kind === "checkin" && form.side !== "resposta") {
      if (!form.date) {
        setFormError("A data do check-in é obrigatória.");
        return;
      }
      if (!form.whatDone.trim()) {
        setFormError("O relato do que foi realizado é obrigatório.");
        return;
      }
    }

    if (form.kind === "impediment") {
      if (!form.date) {
        setFormError("A data do impedimento é obrigatória.");
        return;
      }
      if (!form.type.trim()) {
        setFormError("O tipo do impedimento é obrigatório.");
        return;
      }
      if (!form.description.trim()) {
        setFormError("A descrição do impedimento é obrigatória.");
        return;
      }
    }

    if (form.kind === "feedback") {
      if (!form.recipientId) {
        setFormError("O destinatário do feedback é obrigatório.");
        return;
      }
      if (!form.date) {
        setFormError("A data do feedback é obrigatória.");
        return;
      }
      if (!form.type.trim()) {
        setFormError("O tipo do feedback é obrigatório.");
        return;
      }
      if (!form.content.trim()) {
        setFormError("O conteúdo do feedback é obrigatório.");
        return;
      }
    }

    if (form.kind === "evidence") {
      if (!form.file) {
        setFormError("O arquivo da evidência é obrigatório.");
        return;
      }
      if (!form.type) {
        setFormError("O tipo da evidência é obrigatório.");
        return;
      }
      if (form.file.size > 10 * 1024 * 1024) {
        setFormError("O arquivo excede o tamanho máximo de 10 MB.");
        return;
      }
    }

    if (form.kind === "review") {
      if (form.decision === "Rejected" && !form.description.trim()) {
        setFormError("O motivo da rejeição é obrigatório.");
        return;
      }
    }

    setSaving(true);
    setFormError(null);
    try {
      switch (form.kind) {
        case "progress": {
          await createPdiProgress({
            phaseId: phase.id,
            percent: form.percent ? Number(form.percent) : null,
            comment: form.comment.trim(),
          });
          break;
        }
        case "checkin": {
          if (form.id && form.side === "resposta") {
            // "" limpa o campo; null preservaria o valor anterior (contrato PUT).
            await updatePdiCheckIn(form.id, {
              managerFeedback: form.managerFeedback.trim(),
              nextAction: form.nextAction.trim(),
            });
          } else if (form.id) {
            await updatePdiCheckIn(form.id, {
              whatDone: form.whatDone.trim(),
              whatLearned: form.whatLearned.trim(),
              difficulty: form.difficulty.trim(),
              needSupport: form.needSupport.trim(),
            });
          } else {
            await createPdiCheckIn({
              phaseId: phase.id,
              checkInDate: form.date,
              whatDone: form.whatDone.trim(),
              whatLearned: form.whatLearned.trim() || null,
              difficulty: form.difficulty.trim() || null,
              needSupport: form.needSupport.trim() || null,
            });
          }
          break;
        }
        case "impediment": {
          const input = {
            type: form.type.trim(),
            description: form.description.trim(),
            occurredAt: form.date,
            impact: form.impact.trim() || null,
            responsibleId: form.responsibleId || null,
            actionTaken: form.actionTaken.trim() || null,
          };
          if (form.id) {
            await updatePdiImpediment(form.id, { ...input, status: form.status });
          } else {
            await createPdiImpediment({ phaseId: phase.id, ...input });
          }
          break;
        }
        case "feedback": {
          await createPdiFeedback({
            phaseId: phase.id,
            recipientId: form.recipientId,
            type: form.type.trim(),
            content: form.content.trim(),
            feedbackDate: form.date,
          });
          break;
        }
        case "evidence": {
          if (!form.file) break;
          await uploadPdiEvidence({
            phaseId: phase.id,
            type: form.type as PdiEvidenceType,
            description: form.description.trim() || null,
            file: form.file,
          });
          break;
        }
        case "review": {
          if (!form.id) break;
          await reviewPdiEvidence(form.id, {
            decision: form.decision,
            rejectionReason:
              form.decision === "Rejected" ? form.description.trim() : null,
          });
          break;
        }
      }
      setForm(null);
      await reload();
    } catch (caught) {
      setFormError(errorMessage(caught));
    } finally {
      setSaving(false);
    }
  };

  const formTitle = !form
    ? ""
    : form.kind === "progress"
      ? "Novo progresso"
      : form.kind === "feedback"
        ? "Novo feedback"
        : form.kind === "evidence"
          ? "Nova evidência"
          : form.kind === "review"
            ? "Revisar evidência"
            : form.kind === "impediment"
              ? form.id
                ? "Editar impedimento"
                : "Novo impedimento"
              : form.id && form.side === "resposta"
                ? "Responder check-in"
                : form.id
                  ? "Editar relato do check-in"
                  : "Novo check-in";

  // ---- Renderização ------------------------------------------------------

  const tabs: { key: TabKey; label: string; count: number }[] = [
    { key: "checkins", label: "Check-ins", count: checkIns.length },
    { key: "progress", label: "Progresso", count: progresses.length },
    { key: "impediments", label: "Impedimentos", count: impediments.length },
    { key: "evidences", label: "Evidências", count: evidences.length },
    { key: "feedback", label: "Feedback", count: feedbacks.length },
  ];

  const renderCheckIns = () =>
    checkIns.length === 0 ? (
      <NoticeBanner
        message="Nenhum check-in nesta fase — registre um relato periódico (docs/30 §10)."
        background="#f8fafc"
        color="#475569"
      />
    ) : (
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {checkIns.map((item) => (
          <div key={item.id} style={boxStyle}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                flexWrap: "wrap",
              }}
            >
              <StatusChip
                label={formatDateOnly(item.checkInDate)}
                background="#e0e7ff"
                color="#4338ca"
              />
              <span style={{ fontSize: 13, fontWeight: 600 }}>
                {item.createdByName ?? "Colaborador"}
              </span>
              <span style={{ flex: 1 }} />
              {canReport && (
                <Button
                  variant="ghost"
                  onClick={() => openCheckInRelato(item.id)}
                >
                  Editar relato
                </Button>
              )}
              {canRespond && (
                <Button
                  variant="ghost"
                  onClick={() => openCheckInResposta(item.id)}
                >
                  Responder
                </Button>
              )}
              {canReport && (
                <Button
                  variant="ghost"
                  onClick={() =>
                    void runDelete("este check-in", () =>
                      deletePdiCheckIn(item.id),
                    )
                  }
                >
                  Excluir
                </Button>
              )}
            </div>
            <div style={bodyStyle}>
              <strong>O que realizou:</strong> {item.whatDone}
            </div>
            {item.whatLearned && (
              <div style={bodyStyle}>
                <strong>O que aprendeu:</strong> {item.whatLearned}
              </div>
            )}
            {item.difficulty && (
              <div style={bodyStyle}>
                <strong>Dificuldade:</strong> {item.difficulty}
              </div>
            )}
            {item.needSupport && (
              <div style={bodyStyle}>
                <strong>Precisa de suporte:</strong> {item.needSupport}
              </div>
            )}
            {(item.managerFeedback || item.nextAction) && (
              <div
                style={{
                  ...bodyStyle,
                  background: "#eef2ff",
                  borderRadius: 6,
                  padding: "6px 8px",
                }}
              >
                {item.managerFeedback && (
                  <div>
                    <strong>Resposta do gestor:</strong> {item.managerFeedback}
                  </div>
                )}
                {item.nextAction && (
                  <div>
                    <strong>Próxima ação:</strong> {item.nextAction}
                  </div>
                )}
                <div style={metaStyle}>
                  por {item.respondedByName ?? "gestor"} ·{" "}
                  {formatTimestamp(item.respondedAt)}
                </div>
              </div>
            )}
          </div>
        ))}
      </div>
    );

  const renderProgress = () =>
    progresses.length === 0 ? (
      <NoticeBanner
        message="Nenhum progresso registrado — relate avanços e comentários da fase (docs/30 §9)."
        background="#f8fafc"
        color="#475569"
      />
    ) : (
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {progresses.map((item) => (
          <div key={item.id} style={boxStyle}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                flexWrap: "wrap",
              }}
            >
              <StatusChip
                label={formatTimestamp(item.createdAt)}
                background="#f1f5f9"
                color="#475569"
              />
              {item.percent !== null && (
                <StatusChip
                  label={`${item.percent}%`}
                  background="#dbeafe"
                  color="#1d4ed8"
                />
              )}
              <span style={{ fontSize: 13, fontWeight: 600 }}>
                {item.authorName ?? "Autor"}
              </span>
              <span style={{ flex: 1 }} />
              {canReport && (
                <Button
                  variant="ghost"
                  onClick={() =>
                    void runDelete("este progresso", () =>
                      deletePdiProgress(item.id),
                    )
                  }
                >
                  Excluir
                </Button>
              )}
            </div>
            <div style={bodyStyle}>{item.comment}</div>
          </div>
        ))}
      </div>
    );

  const renderImpediments = () =>
    impediments.length === 0 ? (
      <NoticeBanner
        message="Nenhum impedimento — registre dificuldades como entidade própria (docs/30 §11)."
        background="#f8fafc"
        color="#475569"
      />
    ) : (
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {impediments.map((item) => {
          const status = parsePdiImpedimentStatus(item.status);
          return (
            <div key={item.id} style={boxStyle}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  flexWrap: "wrap",
                }}
              >
                <StatusChip
                  label={item.type}
                  background="#ffedd5"
                  color="#c2410c"
                />
                <StatusChip
                  label={PDI_IMPEDIMENT_STATUS_LABEL[status]}
                  background={PDI_IMPEDIMENT_STATUS_STYLE[status].background}
                  color={PDI_IMPEDIMENT_STATUS_STYLE[status].color}
                />
                <span style={{ flex: 1 }} />
                {canAssist && (
                  <Button
                    variant="ghost"
                    onClick={() => openImpedimentForm(item.id)}
                  >
                    Editar
                  </Button>
                )}
                {canReport && (
                  <Button
                    variant="ghost"
                    onClick={() =>
                      void runDelete(`o impedimento "${item.type}"`, () =>
                        deletePdiImpediment(item.id),
                      )
                    }
                  >
                    Excluir
                  </Button>
                )}
              </div>
              <div style={bodyStyle}>{item.description}</div>
              <div style={metaStyle}>
                Ocorrido em {formatDateOnly(item.occurredAt)}
                {item.responsibleName && <> · Resp.: {item.responsibleName}</>}
                {item.resolvedAt && (
                  <> · Resolvido em {formatDateOnly(item.resolvedAt)}</>
                )}
              </div>
              {item.impact && (
                <div style={metaStyle}>Impacto: {item.impact}</div>
              )}
              {item.actionTaken && (
                <div style={bodyStyle}>
                  <strong>Ação tomada:</strong> {item.actionTaken}
                </div>
              )}
            </div>
          );
        })}
      </div>
    );

  const renderFeedbacks = () =>
    feedbacks.length === 0 ? (
      <NoticeBanner
        message="Nenhum feedback — deixe um registro histórico para a fase (docs/30 §13)."
        background="#f8fafc"
        color="#475569"
      />
    ) : (
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {feedbacks.map((item) => (
          <div key={item.id} style={boxStyle}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                flexWrap: "wrap",
              }}
            >
              <StatusChip
                label={formatDateOnly(item.feedbackDate)}
                background="#fae8ff"
                color="#a21caf"
              />
              <span style={{ fontSize: 13, fontWeight: 600 }}>{item.type}</span>
              <span style={metaStyle}>
                {item.authorName ?? "Autor"} →{" "}
                {item.recipientName ?? "destinatário"}
              </span>
              <span style={{ flex: 1 }} />
              {isAdmin && (
                <Button
                  variant="ghost"
                  onClick={() =>
                    void runDelete(`o feedback "${item.type}"`, () =>
                      deletePdiFeedback(item.id),
                    )
                  }
                >
                  Excluir
                </Button>
              )}
            </div>
            <div style={bodyStyle}>{item.content}</div>
          </div>
        ))}
      </div>
    );

  const renderEvidences = () =>
    evidences.length === 0 ? (
      <NoticeBanner
        message="Nenhuma evidência — anexe certificados, documentos ou resultados da fase (docs/30 §12)."
        background="#f8fafc"
        color="#475569"
      />
    ) : (
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {evidences.map((item) => {
          const status = parsePdiEvidenceStatus(item.status);
          return (
            <div key={item.id} style={boxStyle}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  flexWrap: "wrap",
                }}
              >
                <StatusChip
                  label={PDI_EVIDENCE_TYPE_LABEL[item.type] ?? item.type}
                  background="#e0f2fe"
                  color="#0369a1"
                />
                <StatusChip
                  label={PDI_EVIDENCE_STATUS_LABEL[status]}
                  background={PDI_EVIDENCE_STATUS_STYLE[status].background}
                  color={PDI_EVIDENCE_STATUS_STYLE[status].color}
                />
                <span style={{ fontSize: 13, fontWeight: 600 }}>
                  {item.fileName}
                </span>
                <span style={metaStyle}>{formatFileSize(item.fileSize)}</span>
                <span style={{ flex: 1 }} />
                <Button
                  variant="ghost"
                  onClick={() =>
                    void downloadPdiEvidence(item.id, item.fileName)
                  }
                >
                  Baixar
                </Button>
                {canReview && status === "Pending" && (
                  <Button
                    variant="ghost"
                    onClick={() => openReviewForm(item.id)}
                  >
                    Revisar
                  </Button>
                )}
              </div>
              {item.description && (
                <div style={bodyStyle}>{item.description}</div>
              )}
              <div style={metaStyle}>
                Enviada por {item.createdByName ?? "Colaborador"} em{" "}
                {formatTimestamp(item.createdAt)}
              </div>
              {status !== "Pending" && (
                <div style={metaStyle}>
                  {PDI_EVIDENCE_STATUS_LABEL[status]} por{" "}
                  {item.reviewedByName ?? "gestor"} em{" "}
                  {formatTimestamp(item.reviewedAt)}
                </div>
              )}
              {item.rejectionReason && (
                <div style={bodyStyle}>
                  <strong>Motivo da rejeição:</strong> {item.rejectionReason}
                </div>
              )}
            </div>
          );
        })}
      </div>
    );

  const createAction: Record<TabKey, (() => void) | null> = {
    checkins: canReport ? openCheckInCreate : null,
    progress: canReport ? openProgressForm : null,
    impediments: canReport ? () => openImpedimentForm(null) : null,
    evidences: canReport ? openEvidenceForm : null,
    feedback: canView ? openFeedbackForm : null,
  };

  const createLabel: Record<TabKey, string> = {
    checkins: "+ Check-in",
    progress: "+ Progresso",
    impediments: "+ Impedimento",
    evidences: "+ Evidência",
    feedback: "+ Feedback",
  };

  return (
    <>
      <Dialog
        visible={visible}
        onHide={onHide}
        header={
          phase ? `Acompanhamento · ${phase.title}` : "Acompanhamento da fase"
        }
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
                {entry.label} ({entry.count})
              </Button>
            ))}
            <span style={{ flex: 1 }} />
            {createAction[tab] && (
              <Button variant="ghost" onClick={() => createAction[tab]?.()}>
                {createLabel[tab]}
              </Button>
            )}
          </div>

          {error && <ErrorBanner message={error} onRetry={() => void reload()} />}

          {loading ? (
            <LoadingBlock />
          ) : tab === "checkins" ? (
            renderCheckIns()
          ) : tab === "progress" ? (
            renderProgress()
          ) : tab === "impediments" ? (
            renderImpediments()
          ) : tab === "evidences" ? (
            renderEvidences()
          ) : (
            renderFeedbacks()
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
            onSubmit={(event) => void submit(event)}
            style={{ display: "flex", flexDirection: "column", gap: 12 }}
          >
            {form.kind === "progress" && (
              <>
                <FormField label="Percentual da fase (%) — opcional">
                  <input
                    type="number"
                    min={0}
                    max={100}
                    value={form.percent}
                    onChange={(event) =>
                      setForm({ ...form, percent: event.target.value })
                    }
                    style={inputStyle}
                  />
                </FormField>
                <FormField label="Comentário / relato">
                  <textarea
                    value={form.comment}
                    onChange={(event) =>
                      setForm({ ...form, comment: event.target.value })
                    }
                    rows={4}
                    maxLength={2000}
                    autoFocus
                    style={textAreaStyle}
                  />
                </FormField>
              </>
            )}

            {form.kind === "checkin" && form.side !== "resposta" && (
              <>
                <FormField label="Data do check-in">
                  <input
                    type="date"
                    value={form.date}
                    onChange={(event) =>
                      setForm({ ...form, date: event.target.value })
                    }
                    style={inputStyle}
                  />
                </FormField>
                <FormField label="O que realizou?">
                  <textarea
                    value={form.whatDone}
                    onChange={(event) =>
                      setForm({ ...form, whatDone: event.target.value })
                    }
                    rows={3}
                    maxLength={2000}
                    autoFocus
                    style={textAreaStyle}
                  />
                </FormField>
                <FormField label="O que aprendeu? (opcional)">
                  <textarea
                    value={form.whatLearned}
                    onChange={(event) =>
                      setForm({ ...form, whatLearned: event.target.value })
                    }
                    rows={2}
                    maxLength={2000}
                    style={textAreaStyle}
                  />
                </FormField>
                <FormField label="Qual dificuldade encontrou? (opcional)">
                  <textarea
                    value={form.difficulty}
                    onChange={(event) =>
                      setForm({ ...form, difficulty: event.target.value })
                    }
                    rows={2}
                    maxLength={2000}
                    style={textAreaStyle}
                  />
                </FormField>
                <FormField label="Precisa de suporte? (opcional)">
                  <textarea
                    value={form.needSupport}
                    onChange={(event) =>
                      setForm({ ...form, needSupport: event.target.value })
                    }
                    rows={2}
                    maxLength={2000}
                    style={textAreaStyle}
                  />
                </FormField>
              </>
            )}

            {form.kind === "checkin" && form.side === "resposta" && (
              <>
                <FormField label="Feedback do gestor (§10)">
                  <textarea
                    value={form.managerFeedback}
                    onChange={(event) =>
                      setForm({ ...form, managerFeedback: event.target.value })
                    }
                    rows={3}
                    maxLength={2000}
                    autoFocus
                    style={textAreaStyle}
                  />
                </FormField>
                <FormField label="Próxima ação (opcional)">
                  <textarea
                    value={form.nextAction}
                    onChange={(event) =>
                      setForm({ ...form, nextAction: event.target.value })
                    }
                    rows={2}
                    maxLength={2000}
                    style={textAreaStyle}
                  />
                </FormField>
                <div style={{ ...metaStyle, fontSize: 12 }}>
                  Deixar os dois campos vazios limpa a resposta do check-in.
                </div>
              </>
            )}

            {form.kind === "impediment" && (
              <>
                <FormField label="Tipo (ex.: recurso indisponível)">
                  <input
                    type="text"
                    value={form.type}
                    onChange={(event) =>
                      setForm({ ...form, type: event.target.value })
                    }
                    maxLength={100}
                    autoFocus
                    style={inputStyle}
                  />
                </FormField>
                <FormField label="Descrição do problema">
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
                <div style={{ display: "flex", gap: 12 }}>
                  <FormField label="Data de ocorrência" width={180}>
                    <input
                      type="date"
                      value={form.date}
                      onChange={(event) =>
                        setForm({ ...form, date: event.target.value })
                      }
                      style={inputStyle}
                    />
                  </FormField>
                  {form.id && (
                    <FormField label="Status" width={180}>
                      <SelectField
                        value={form.status}
                        onChange={(value) =>
                          setForm({
                            ...form,
                            status: parsePdiImpedimentStatus(value),
                          })
                        }
                        options={[
                          { value: "Open", label: "Aberto" },
                          { value: "Resolved", label: "Resolvido" },
                        ]}
                        aria-label="Status do impedimento"
                      />
                    </FormField>
                  )}
                </div>
                <FormField label="Impacto (ex.: atraso na fase) (opcional)">
                  <input
                    type="text"
                    value={form.impact}
                    onChange={(event) =>
                      setForm({ ...form, impact: event.target.value })
                    }
                    maxLength={500}
                    style={inputStyle}
                  />
                </FormField>
                <FormField label="Responsável (opcional)">
                  <SelectField
                    value={form.responsibleId}
                    onChange={(value) =>
                      setForm({ ...form, responsibleId: value })
                    }
                    options={responsibleOptions}
                    placeholder="Sem responsável"
                    aria-label="Responsável pelo impedimento"
                  />
                </FormField>
                <FormField label="Ação tomada (opcional)">
                  <textarea
                    value={form.actionTaken}
                    onChange={(event) =>
                      setForm({ ...form, actionTaken: event.target.value })
                    }
                    rows={2}
                    maxLength={2000}
                    style={textAreaStyle}
                  />
                </FormField>
              </>
            )}

            {form.kind === "evidence" && (
              <>
                <FormField label="Arquivo (até 10 MB)">
                  <input
                    type="file"
                    onChange={(event) =>
                      setForm({
                        ...form,
                        file: event.target.files?.[0] ?? null,
                      })
                    }
                    style={{ ...inputStyle, padding: "5px 0" }}
                  />
                </FormField>
                {form.file && (
                  <div style={metaStyle}>
                    Selecionado: {form.file.name} ·{" "}
                    {formatFileSize(form.file.size)}
                  </div>
                )}
                <FormField label="Tipo da evidência">
                  <SelectField
                    value={form.type}
                    onChange={(value) => setForm({ ...form, type: value })}
                    options={Object.entries(PDI_EVIDENCE_TYPE_LABEL).map(
                      ([value, label]) => ({ value, label }),
                    )}
                    aria-label="Tipo da evidência"
                  />
                </FormField>
                <FormField label="Descrição (opcional)">
                  <textarea
                    value={form.description}
                    onChange={(event) =>
                      setForm({ ...form, description: event.target.value })
                    }
                    rows={2}
                    maxLength={500}
                    style={textAreaStyle}
                  />
                </FormField>
              </>
            )}

            {form.kind === "review" && (
              <>
                <FormField label="Decisão do gestor (§12)">
                  <SelectField
                    value={form.decision}
                    onChange={(value) =>
                      setForm({
                        ...form,
                        decision:
                          value === "Rejected"
                            ? "Rejected"
                            : "Validated",
                      })
                    }
                    options={[
                      { value: "Validated", label: "Validar" },
                      { value: "Rejected", label: "Rejeitar" },
                    ]}
                    aria-label="Decisão da revisão"
                  />
                </FormField>
                {form.decision === "Rejected" && (
                  <FormField label="Motivo da rejeição">
                    <textarea
                      value={form.description}
                      onChange={(event) =>
                        setForm({
                          ...form,
                          description: event.target.value,
                        })
                      }
                      rows={3}
                      maxLength={500}
                      autoFocus
                      style={textAreaStyle}
                    />
                  </FormField>
                )}
              </>
            )}

            {form.kind === "feedback" && (
              <>
                <FormField label="Destinatário">
                  <SelectField
                    value={form.recipientId}
                    onChange={(value) => setForm({ ...form, recipientId: value })}
                    options={responsibleOptions}
                    aria-label="Destinatário do feedback"
                  />
                </FormField>
                <FormField label="Tipo (ex.: reconhecimento)">
                  <input
                    type="text"
                    value={form.type}
                    onChange={(event) =>
                      setForm({ ...form, type: event.target.value })
                    }
                    maxLength={100}
                    autoFocus
                    style={inputStyle}
                  />
                </FormField>
                <FormField label="Data">
                  <input
                    type="date"
                    value={form.date}
                    onChange={(event) =>
                      setForm({ ...form, date: event.target.value })
                    }
                    style={inputStyle}
                  />
                </FormField>
                <FormField label="Conteúdo">
                  <textarea
                    value={form.content}
                    onChange={(event) =>
                      setForm({ ...form, content: event.target.value })
                    }
                    rows={4}
                    maxLength={2000}
                    style={textAreaStyle}
                  />
                </FormField>
              </>
            )}

            {formError && <FormError message={formError} />}

            <div
              style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}
            >
              <Button variant="ghost" type="button" onClick={() => setForm(null)}>
                Cancelar
              </Button>
              <Button
                variant="primary"
                type="submit"
                disabled={saving}
              >
                {saving ? "Salvando…" : "Salvar"}
              </Button>
            </div>
          </form>
        )}
      </Dialog>

      <ConfirmDialog
        visible={confirm !== null}
        onHide={() => setConfirm(null)}
        onConfirm={() => void confirm?.run()}
        header="Confirmar exclusão"
        message={confirm?.message ?? ""}
        confirmLabel="Excluir"
      />
    </>
  );
}
