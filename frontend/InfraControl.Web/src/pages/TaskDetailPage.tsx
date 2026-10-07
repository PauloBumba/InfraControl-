import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { StatusChip } from "@/components/ui/StatusChip";
import { Can } from "@/components/auth/Can";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField } from "@/components/ui/FormField";
import { SelectField } from "@/components/ui/SelectField";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { formatDateTime } from "@/utils/format";
import { ApiError } from "@/services/api/client";
import {
  addTaskComment,
  addTaskCriterion,
  addTaskImpediment,
  addTaskPhase,
  applyTaskSla,
  assignTaskUser,
  changeTaskState,
  createTask,
  deleteTask,
  deleteTaskComment,
  deleteTaskCriterion,
  deleteTaskEvidence,
  deleteTaskImpediment,
  deleteTaskPhase,
  downloadTaskEvidence,
  getTask,
  getTaskComments,
  getTaskCriteria,
  getTaskEvidence,
  getTaskImpediments,
  getTaskPhases,
  getTaskSla,
  getTaskSlaPauses,
  getTaskTimeline,
  linkTaskDevice,
  linkTaskIncident,
  listSubtasks,
  pauseTaskSla,
  replanTask,
  resumeTaskSla,
  setTaskIdentification,
  setTaskParent,
  unassignTaskUser,
  unlinkTaskDevice,
  unlinkTaskIncident,
  updateTask,
  updateTaskCriterion,
  updateTaskImpediment,
  updateTaskPhase,
  updateTaskPlanning,
  updateTaskResult,
  uploadTaskEvidence,
  type TaskComment,
  type TaskCriterion,
  type TaskEvidence,
  type TaskImpediment,
  type TaskItem,
  type TaskPhase,
  type TaskSla,
  type TaskSlaPause,
  type TaskTimelineEntry,
} from "@/services/api/tasks";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import { listDevices, INVENTORY_PAGE_SIZE, type DeviceItem } from "@/services/api/inventory";
import { listIncidents, type IncidentItem } from "@/services/api/incidents";
import { listSlaPolicies, type SlaPolicyItem } from "@/services/api/sla";
import {
  EVIDENCE_ACCEPT,
  formatDateOnly,
  formatFileSize,
  historyActionLabel,
  isOverdue,
  kindLabel,
  messageFor,
  priorityOptions,
  priorityVisual,
  stateColumns,
  stateVisual,
  timelineTypeVisual,
} from "@/components/tasks/taskCatalogs";

/**
 * F23-T002 — tela de detalhe `/tasks/:id`: a tela que o kanban não tinha.
 * Liga os recursos já existentes do backend (F7): dados/identificação
 * editáveis (`PUT /tasks/{id}` e `PUT /tasks/{id}/identification`),
 * transição de estado (F7-T007 — 409 do domínio vira banner), comentários
 * (F7-T005), histórico (F7-T006), participantes (F7-T002), vínculos de
 * device/incidente (F7-T003/T004) e SLA (F8-T003/T004). Leitura libera
 * para qualquer role autenticada; escrita fica atrás de `Can operate` —
 * o backend continua sendo a autoridade (403 no envelope).
 */

const shortId = (value: string): string => value.slice(0, 8);

function Card({
  title,
  actions,
  children,
}: {
  title: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section
      style={{
        background: "white",
        border: "1px solid #e5e7eb",
        borderRadius: 8,
        padding: 14,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 8,
          marginBottom: 10,
        }}
      >
        <h2 style={{ fontSize: 13, fontWeight: 600, margin: 0 }}>{title}</h2>
        {actions}
      </div>
      {children}
    </section>
  );
}

function DataRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div style={{ display: "flex", gap: 8, fontSize: 12, padding: "3px 0" }}>
      <span style={{ color: "#6b7280", minWidth: 110 }}>{label}</span>
      <span style={{ color: "#111827" }}>{value}</span>
    </div>
  );
}

export function TaskDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [task, setTask] = useState<TaskItem | null>(null);
  const [comments, setComments] = useState<TaskComment[]>([]);
  // F23-T004/P4d — timeline unificada tipada (§8) substitui o histórico
  // cru: histórico + comentários em leitura, com chip de filtro por tipo.
  const [timeline, setTimeline] = useState<TaskTimelineEntry[]>([]);
  const [timelineType, setTimelineType] = useState("");
  const [criteria, setCriteria] = useState<TaskCriterion[]>([]);
  const [sla, setSla] = useState<TaskSla | null>(null);
  const [pauses, setPauses] = useState<TaskSlaPause[]>([]);

  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Listas de apoio (best-effort: Viewer sem acesso a users → fallback id).
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [devices, setDevices] = useState<DeviceItem[]>([]);
  const [incidents, setIncidents] = useState<IncidentItem[]>([]);
  const [policies, setPolicies] = useState<SlaPolicyItem[]>([]);

  // Diálogos/formulários.
  const [dataOpen, setDataOpen] = useState(false);
  const [dataTitle, setDataTitle] = useState("");
  const [dataDescription, setDataDescription] = useState("");
  const [identOpen, setIdentOpen] = useState(false);
  const [priority, setPriority] = useState("Medium");
  const [dueDate, setDueDate] = useState("");
  const [responsibleId, setResponsibleId] = useState("");
  const [commentText, setCommentText] = useState("");
  const [pendingComment, setPendingComment] = useState<TaskComment | null>(null);
  const [confirmDeleteTask, setConfirmDeleteTask] = useState(false);
  const [slaPolicyChoice, setSlaPolicyChoice] = useState("");

  // F23-T003 — planejamento (§5), resultado (§5), replanejo (§13) e
  // critérios de aceite (§12).
  const [planOpen, setPlanOpen] = useState(false);
  const [plannedStart, setPlannedStart] = useState("");
  const [plannedEnd, setPlannedEnd] = useState("");
  const [replanOpen, setReplanOpen] = useState(false);
  const [replanStart, setReplanStart] = useState("");
  const [replanEnd, setReplanEnd] = useState("");
  const [replanDue, setReplanDue] = useState("");
  const [replanReason, setReplanReason] = useState("");
  const [resultOpen, setResultOpen] = useState(false);
  const [resultText, setResultText] = useState("");
  const [criterionText, setCriterionText] = useState("");
  const [pendingCriterion, setPendingCriterion] = useState<TaskCriterion | null>(null);

  // F23-T004/P4a — subtarefas: lista dos filhos + diálogo de criação.
  const [subtasks, setSubtasks] = useState<TaskItem[]>([]);
  const [subtaskOpen, setSubtaskOpen] = useState(false);
  const [subtaskTitle, setSubtaskTitle] = useState("");
  const [subtaskDescription, setSubtaskDescription] = useState("");
  const [pendingSubtask, setPendingSubtask] = useState<TaskItem | null>(null);

  // F23-T004/P4b — impedimentos estruturados: lista + registro/exclusão.
  const [impediments, setImpediments] = useState<TaskImpediment[]>([]);
  const [impedOpen, setImpedOpen] = useState(false);
  const [impedText, setImpedText] = useState("");
  const [pendingImpediment, setPendingImpediment] = useState<TaskImpediment | null>(null);

  // F23-T004/P4c — evidências anexadas: upload em disco local (ADR-033).
  const [evidences, setEvidences] = useState<TaskEvidence[]>([]);
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [evidenceNote, setEvidenceNote] = useState("");
  const [pendingEvidence, setPendingEvidence] = useState<TaskEvidence | null>(null);
  const evidenceInputRef = useRef<HTMLInputElement>(null);

  // F23-T004/P4d — fases/etapas dentro da task (§7): lista + cadastro
  // inline (rename fica no PUT testado, sem UI — como nos critérios).
  const [phases, setPhases] = useState<TaskPhase[]>([]);
  const [phaseText, setPhaseText] = useState("");
  const [pendingPhase, setPendingPhase] = useState<TaskPhase | null>(null);

  const load = useCallback(async () => {
    try {
      const loaded = await getTask(id);
      if (!loaded) {
        setNotFound(true);
        return;
      }
      setTask(loaded);

      const [loadedComments, loadedTimeline, loadedCriteria, loadedSubtasks,
        loadedImpediments, loadedEvidences, loadedPhases] = await Promise.all([
          getTaskComments(id),
          // 500 = teto do backend; o filtro por tipo é local (instantâneo).
          getTaskTimeline(id, undefined, 500),
          getTaskCriteria(id),
          listSubtasks(id),
          getTaskImpediments(id),
          getTaskEvidence(id),
          getTaskPhases(id),
        ]);
      setComments(loadedComments ?? []);
      setTimeline(loadedTimeline ?? []);
      setCriteria(loadedCriteria ?? []);
      setSubtasks(loadedSubtasks.items);
      setImpediments(loadedImpediments ?? []);
      setEvidences(loadedEvidences ?? []);
      setPhases(loadedPhases ?? []);

      // 404 de SLA é fluxo normal = sem SLA aplicado (F8-T003).
      let loadedSla: TaskSla | null = null;
      try {
        loadedSla = await getTaskSla(id);
      } catch (caught) {
        if (!(caught instanceof ApiError) || caught.status !== 404) throw caught;
      }
      setSla(loadedSla);
      setPauses(loadedSla ? ((await getTaskSlaPauses(id)) ?? []) : []);
      setError(null);
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 404) {
        setNotFound(true);
      } else {
        setError(messageFor(caught));
      }
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  // Vocabulário para selects de apoio — falha (403 de Viewer em users,
  // rede) silencia e a UI cai para ids curtos.
  useEffect(() => {
    void listUsers()
      .then(setUsers)
      .catch(() => setUsers([]));
    void listDevices({ pageSize: INVENTORY_PAGE_SIZE })
      .then((page) => setDevices(page.items))
      .catch(() => setDevices([]));
    void listIncidents({ pageSize: 100 })
      .then((page) => setIncidents(page.items))
      .catch(() => setIncidents([]));
    void listSlaPolicies({ pageSize: 100 })
      .then((page) => setPolicies(page.items))
      .catch(() => setPolicies([]));
  }, []);

  /** Executa uma mutação, mostra o aviso pt-BR e recarrega a tela. */
  async function run(action: () => Promise<unknown>, successMessage: string) {
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await action();
      setNotice(successMessage);
      await load();
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  }

  async function saveData() {
    const trimmed = dataTitle.trim();
    if (!trimmed) {
      setError("Informe o título da tarefa.");
      return;
    }
    await run(
      () => updateTask(id, { title: trimmed, description: dataDescription.trim() || undefined }),
      "Dados da tarefa atualizados.",
    );
    setDataOpen(false);
  }

  async function saveIdentification() {
    await run(
      () =>
        setTaskIdentification(id, {
          priority,
          dueDate: dueDate || undefined,
          responsibleId: responsibleId || undefined,
        }),
      "Identificação atualizada.",
    );
    setIdentOpen(false);
  }

  // F23-T003 — planejamento/resultado (§5) e replanejo (§13).

  async function savePlanning() {
    await run(
      () =>
        updateTaskPlanning(id, {
          plannedStart: plannedStart || undefined,
          plannedEnd: plannedEnd || undefined,
        }),
      "Planejamento atualizado.",
    );
    setPlanOpen(false);
  }

  async function saveReplan() {
    const reason = replanReason.trim();
    if (!reason) {
      setError("Informe o motivo do replanejamento.");
      return;
    }
    await run(
      () =>
        replanTask(id, {
          plannedStart: replanStart || undefined,
          plannedEnd: replanEnd || undefined,
          dueDate: replanDue || undefined,
          reason,
        }),
      "Tarefa replanejada.",
    );
    setReplanOpen(false);
  }

  async function saveResult() {
    await run(
      () => updateTaskResult(id, resultText.trim() || undefined),
      "Resultado salvo.",
    );
    setResultOpen(false);
  }

  // F23-T003 — critérios de aceite (§12): adicionar, marcar/desmarcar e
  // excluir (edição da descrição fica sem UI, como nos comentários).

  async function addCriterion(event: FormEvent) {
    event.preventDefault();
    const description = criterionText.trim();
    if (!description) {
      setError("Descreva o critério antes de adicionar.");
      return;
    }
    await run(() => addTaskCriterion(id, description), "Critério adicionado.");
    setCriterionText("");
  }

  async function toggleCriterion(criterion: TaskCriterion) {
    await run(
      () => updateTaskCriterion(criterion.id, { done: !criterion.isDone }),
      criterion.isDone
        ? "Critério desmarcado."
        : "Critério marcado como atendido.",
    );
  }

  async function removeCriterion() {
    if (!pendingCriterion) return;
    const criterion = pendingCriterion;
    setPendingCriterion(null);
    await run(() => deleteTaskCriterion(criterion.id), "Critério excluído.");
  }

  // F23-T004/P4a — cria a subtarefa com o mesmo tipo do pai (profundidade
  // ≤ 2: 400 pt-BR se o "pai" aqui já for subtarefa).
  async function addSubtask(event?: FormEvent) {
    event?.preventDefault();
    if (!task) return;
    const title = subtaskTitle.trim();
    if (!title) {
      setError("Dê um título à subtarefa antes de adicionar.");
      return;
    }
    await run(
      () =>
        createTask({
          title,
          kind: task.kind,
          description: subtaskDescription.trim() || undefined,
          parentId: id,
        }),
      "Subtarefa criada.",
    );
    setSubtaskTitle("");
    setSubtaskDescription("");
    setSubtaskOpen(false);
  }

  /** Desvincula a subtarefa do pai (vira task raiz) após confirmação. */
  async function unlinkSubtask() {
    if (!pendingSubtask) return;
    const child = pendingSubtask;
    setPendingSubtask(null);
    await run(
      () => setTaskParent(child.id),
      "Subtarefa desvinculada — a task agora é raiz.",
    );
  }

  // F23-T004/P4b — impedimentos: registrar, resolver/reabrir e excluir.
  // Não mexe no estado do workflow (Blocked segue independente — ADR).

  async function addImpediment(event?: FormEvent) {
    event?.preventDefault();
    const description = impedText.trim();
    if (!description) {
      setError("Descreva o impedimento antes de registrar.");
      return;
    }
    await run(
      () => addTaskImpediment(id, description),
      "Impedimento registrado.",
    );
    setImpedText("");
    setImpedOpen(false);
  }

  async function toggleImpediment(impediment: TaskImpediment) {
    const isResolved = impediment.resolvedAt != null;
    await run(
      () => updateTaskImpediment(impediment.id, { resolved: !isResolved }),
      isResolved ? "Impedimento reaberto." : "Impedimento resolvido.",
    );
  }

  async function removeImpediment() {
    if (!pendingImpediment) return;
    const impediment = pendingImpediment;
    setPendingImpediment(null);
    await run(() => deleteTaskImpediment(impediment.id), "Impedimento excluído.");
  }

  // F23-T004/P4c — evidências: anexar (multipart ≤10 MB, allowlist no
  // domínio) e baixar/excluir (o binário mora em disco local: ADR-033).

  async function uploadEvidence() {
    if (!evidenceFile) {
      setError("Escolha um arquivo para anexar.");
      return;
    }
    const file = evidenceFile;
    const note = evidenceNote.trim();

    await run(
      () => uploadTaskEvidence(id, file, note || undefined),
      `Evidência "${file.name}" anexada.`,
    );
    setEvidenceFile(null);
    setEvidenceNote("");
    if (evidenceInputRef.current) evidenceInputRef.current.value = "";
  }

  async function removeEvidence() {
    if (!pendingEvidence) return;
    const evidence = pendingEvidence;
    setPendingEvidence(null);
    await run(
      () => deleteTaskEvidence(evidence.id),
      "Evidência excluída (registro e arquivo).",
    );
  }

  // F23-T004/P4d — fases (§7): adicionar, concluir/reabrir, ponteiro de
  // fase atual (máx. 1 por task) e excluir.

  async function addPhase(event?: FormEvent) {
    event?.preventDefault();
    const name = phaseText.trim();
    if (!name) {
      setError("Dê um nome à fase antes de adicionar.");
      return;
    }
    await run(() => addTaskPhase(id, name), "Fase adicionada.");
    setPhaseText("");
  }

  async function togglePhase(phase: TaskPhase) {
    const completed = phase.completedAt == null;
    await run(
      () => updateTaskPhase(phase.id, { completed }),
      completed ? "Fase concluída." : "Fase reaberta.",
    );
  }

  async function makeCurrentPhase(phase: TaskPhase) {
    await run(
      () => updateTaskPhase(phase.id, { current: true }),
      `Fase "${phase.name}" agora é a atual.`,
    );
  }

  async function removePhase() {
    if (!pendingPhase) return;
    const phase = pendingPhase;
    setPendingPhase(null);
    await run(() => deleteTaskPhase(phase.id), "Fase removida.");
  }

  async function submitComment(event: FormEvent) {
    event.preventDefault();
    const content = commentText.trim();
    if (!content) {
      setError("Escreva um comentário antes de enviar.");
      return;
    }
    await run(() => addTaskComment(id, content), "Comentário adicionado.");
    setCommentText("");
  }

  async function move(target: string) {
    if (!task || target === task.state) return;
    await run(() => changeTaskState(task.id, target), `Estado alterado para ${stateVisual(target).label}.`);
  }

  function openDataDialog() {
    if (!task) return;
    setDataTitle(task.title);
    setDataDescription(task.description ?? "");
    setError(null);
    setDataOpen(true);
  }

  function openIdentDialog() {
    if (!task) return;
    setPriority(task.priority || "Medium");
    setDueDate(task.dueDate ?? "");
    setResponsibleId(task.responsibleId ?? "");
    setError(null);
    setIdentOpen(true);
  }

  if (loading) {
    return <LoadingBlock />;
  }

  if (notFound || !task) {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        <Link to="/tasks" style={{ fontSize: 13, color: "#3b82f6" }}>
          ← Voltar para tarefas
        </Link>
        <ErrorBanner message="Tarefa não encontrada." />
      </div>
    );
  }

  const stateInfo = stateVisual(task.state);
  const priorityInfo = priorityVisual(task.priority);
  const overdue = task.dueDate != null && isOverdue(task.dueDate);
  const activePause = pauses.find((pause) => pause.endAt == null);

  const userName = (userId: string): string =>
    users.find((user) => user.userId === userId)?.displayName || shortId(userId);
  const deviceName = (deviceId: string): string =>
    devices.find((device) => device.id === deviceId)?.name || shortId(deviceId);
  const incidentTitle = (incidentId: string): string =>
    incidents.find((incident) => incident.id === incidentId)?.title || shortId(incidentId);

  const assignableUsers = users.filter(
    (user) => !task.assigneeIds.includes(user.userId),
  );
  const linkableDevices = devices.filter(
    (device) => !task.deviceIds.includes(device.id),
  );
  const linkableIncidents = incidents.filter(
    (incident) => !task.incidentIds.includes(incident.id),
  );

  const responsibleOptions = [
    { value: "", label: "Sem responsável" },
    ...users.map((user) => ({ value: user.userId, label: user.displayName || user.email })),
  ];
  if (task.responsibleId && !users.some((user) => user.userId === task.responsibleId)) {
    responsibleOptions.push({
      value: task.responsibleId,
      label: `Usuário ${shortId(task.responsibleId)}`,
    });
  }

  const policyName = (policyId: string): string =>
    policies.find((policy) => policy.id === policyId)?.name || shortId(policyId);

  // F23-T004/P4d — filtro de tipo da linha do tempo: chips só para os
  // tipos presentes; filtragem local sobre a resposta já carregada.
  const presentTypes = [...new Set(timeline.map((entry) => entry.type))];
  const visibleTimeline = timeline.filter(
    (entry) => !timelineType || entry.type === timelineType,
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
      <Link to="/tasks" style={{ fontSize: 13, color: "#3b82f6" }}>
        ← Voltar para tarefas
      </Link>

      <PageHeader
        title={task.title}
        subtitle={
          <span>
            {kindLabel(task.kind)} · aberta em {formatDateTime(new Date(task.openedAt))}
            {/* F23-T004/P4a — breadcrumb de pai nas subtarefas. */}
            {task.parentTaskId && (
              <>
                {" · "}
                <Link
                  to={`/tasks/${task.parentTaskId}`}
                  style={{ color: "#3b82f6" }}
                  title="Abrir a task pai"
                >
                  Subtarefa de {task.parentTitle ?? `task ${shortId(task.parentTaskId)}`}
                </Link>
              </>
            )}
          </span>
        }
        actions={
          <Can level="operate">
            <Button
              variant="danger"
              onClick={() => setConfirmDeleteTask(true)}
              style={{ padding: "6px 14px" }}
            >
              <i className="pi pi-trash" style={{ fontSize: 11 }} /> Excluir
            </Button>
          </Can>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}
      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "minmax(0, 2fr) minmax(0, 1fr)",
          gap: 14,
          alignItems: "start",
        }}
      >
        {/* ── Coluna principal ─────────────────────────────────────── */}
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <Card
            title="Descrição"
            actions={
              <Can level="operate">
                <Button variant="ghost" onClick={openDataDialog} style={{ padding: "4px 10px", fontSize: 12 }}>
                  <i className="pi pi-pencil" style={{ fontSize: 10 }} /> Editar
                </Button>
              </Can>
            }
          >
            {task.description ? (
              <p style={{ fontSize: 13, color: "#374151", margin: 0, whiteSpace: "pre-wrap" }}>
                {task.description}
              </p>
            ) : (
              <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>Sem descrição.</p>
            )}
          </Card>

          {/* F23-T003 — resultado da execução (§5). */}
          <Card
            title="Resultado"
            actions={
              <Can level="operate">
                <Button
                  variant="ghost"
                  onClick={() => {
                    setResultText(task.result ?? "");
                    setResultOpen(true);
                  }}
                  style={{ padding: "4px 10px", fontSize: 12 }}
                >
                  <i className="pi pi-pencil" style={{ fontSize: 10 }} /> Editar
                </Button>
              </Can>
            }
          >
            {task.result ? (
              <p style={{ fontSize: 13, color: "#374151", margin: 0, whiteSpace: "pre-wrap" }}>
                {task.result}
              </p>
            ) : (
              <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>
                Sem resultado registrado.
              </p>
            )}
          </Card>

          {/* F23-T004/P4a — subtarefas e progresso derivado (§ subtarefas). */}
          <Card
            title={`Subtarefas (${subtasks.length})`}
            actions={
              <Can level="operate">
                <Button
                  variant="ghost"
                  onClick={() => setSubtaskOpen(true)}
                  style={{ padding: "4px 10px", fontSize: 12 }}
                >
                  <i className="pi pi-plus" style={{ fontSize: 10 }} /> Nova
                </Button>
              </Can>
            }
          >
            {/* Barra de progresso do pai: concluídas ÷ não canceladas. */}
            {task.progress != null && (
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  marginBottom: 10,
                  fontSize: 12,
                }}
              >
                <div
                  style={{
                    flex: 1,
                    height: 6,
                    background: "#e5e7eb",
                    borderRadius: 3,
                    overflow: "hidden",
                  }}
                >
                  <div
                    style={{
                      width: `${task.progress}%`,
                      height: "100%",
                      background: "#0ea5e9",
                    }}
                  />
                </div>
                <span style={{ color: "#64748b", whiteSpace: "nowrap" }}>
                  {task.progress}%
                </span>
              </div>
            )}
            {subtasks.length === 0 ? (
              <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>
                Sem subtarefas — o progresso aparece quando existirem.
              </p>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {subtasks.map((child) => {
                  const childState = stateVisual(child.state);
                  return (
                    <div
                      key={child.id}
                      style={{ display: "flex", alignItems: "center", gap: 8 }}
                    >
                      <StatusChip
                        label={childState.label}
                        background={childState.background}
                        color={childState.color}
                        fontWeight={600}
                        fontSize={10}
                        style={{ whiteSpace: "nowrap", padding: "2px 6px" }}
                      />
                      <Link
                        to={`/tasks/${child.id}`}
                        style={{ flex: 1, fontSize: 13, color: "inherit" }}
                        title="Abrir detalhe da subtarefa"
                      >
                        {child.title}
                      </Link>
                      <Can level="operate">
                        <Button
                          variant="ghost"
                          onClick={() => setPendingSubtask(child)}
                          title="Desvincular do pai"
                          style={{ padding: "2px 8px", fontSize: 11 }}
                        >
                          <i className="pi pi-times" style={{ fontSize: 10 }} />
                        </Button>
                      </Can>
                    </div>
                  );
                })}
              </div>
            )}
          </Card>

          {/* F23-T004/P4d — fases/etapas dentro da task (§7): sequência
              nomeada por task, máx. 1 fase atual; progresso x/y derivado. */}
          <Card
            title={`Fases (${phases.filter((phase) => phase.completedAt != null).length}/${phases.length})`}
          >
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {phases.length === 0 && (
                <p style={{ fontSize: 12, color: "#9ca3af", margin: 0 }}>
                  Sem fases — adicione etapas para acompanhar a execução.
                </p>
              )}
              {phases.map((phase) => (
                <div
                  key={phase.id}
                  style={{ display: "flex", alignItems: "center", gap: 8 }}
                >
                  <input
                    type="checkbox"
                    checked={phase.completedAt != null}
                    disabled={saving}
                    onChange={() => void togglePhase(phase)}
                    title={
                      phase.completedAt == null
                        ? "Concluir fase"
                        : "Reabrir fase"
                    }
                    style={{ marginTop: 2, accentColor: "#3b82f6" }}
                  />
                  <span
                    style={{
                      flex: 1,
                      fontSize: 13,
                      color: phase.completedAt != null ? "#9ca3af" : "#374151",
                      textDecoration:
                        phase.completedAt != null ? "line-through" : "none",
                    }}
                  >
                    {phase.position + 1}. {phase.name}
                  </span>
                  {phase.isCurrent ? (
                    <StatusChip
                      label="Atual"
                      background="#faf5ff"
                      color="#9333ea"
                      fontWeight={600}
                      fontSize={10}
                      style={{
                        whiteSpace: "nowrap",
                        padding: "2px 6px",
                        alignSelf: "flex-start",
                      }}
                    />
                  ) : (
                    <Can level="operate">
                      <Button
                        variant="ghost"
                        onClick={() => void makeCurrentPhase(phase)}
                        title="Tornar esta a fase atual"
                        style={{ padding: "2px 8px", fontSize: 11 }}
                      >
                        <i className="pi pi-map-marker" style={{ fontSize: 10 }} />
                      </Button>
                    </Can>
                  )}
                  <Can level="operate">
                    <Button
                      variant="ghost"
                      onClick={() => setPendingPhase(phase)}
                      title="Excluir fase"
                      style={{ padding: "2px 8px", fontSize: 11 }}
                    >
                      <i className="pi pi-trash" style={{ fontSize: 10 }} />
                    </Button>
                  </Can>
                </div>
              ))}

              <Can level="operate">
                <form
                  onSubmit={(event) => void addPhase(event)}
                  style={{ display: "flex", gap: 8, marginTop: 2 }}
                >
                  <input
                    value={phaseText}
                    onChange={(event) => setPhaseText(event.target.value)}
                    placeholder="Nova etapa da execução…"
                    maxLength={120}
                    style={{ flex: 1, fontSize: 13, padding: "6px 8px" }}
                  />
                  <Button
                    type="submit"
                    disabled={saving}
                    style={{ padding: "6px 12px", fontSize: 12 }}
                  >
                    Adicionar
                  </Button>
                </form>
              </Can>
            </div>
          </Card>

          {/* F23-T004/P4b — impedimentos estruturados (§10): abertos/
              resolvidos; não altera o estado do workflow. */}
          <Card
            title={`Impedimentos (${impediments.filter((impediment) => impediment.resolvedAt == null).length}/${impediments.length})`}
            actions={
              <Can level="operate">
                <Button
                  variant="ghost"
                  onClick={() => setImpedOpen(true)}
                  style={{ padding: "4px 10px", fontSize: 12 }}
                >
                  <i className="pi pi-plus" style={{ fontSize: 10 }} /> Registrar
                </Button>
              </Can>
            }
          >
            {impediments.length === 0 ? (
              <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>
                Sem impedimentos registrados.
              </p>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {impediments.map((impediment) => {
                  const resolved = impediment.resolvedAt != null;
                  return (
                    <div
                      key={impediment.id}
                      style={{ display: "flex", alignItems: "center", gap: 8 }}
                    >
                      <StatusChip
                        label={resolved ? "Resolvido" : "Aberto"}
                        background={resolved ? "#ecfdf5" : "#fef2f2"}
                        color={resolved ? "#059669" : "#dc2626"}
                        fontWeight={600}
                        fontSize={10}
                        style={{ whiteSpace: "nowrap", padding: "2px 6px" }}
                      />
                      <span style={{ flex: 1, fontSize: 13, color: "#374151" }}>
                        {impediment.description}
                      </span>
                      <Can level="operate">
                        <Button
                          variant="ghost"
                          onClick={() => void toggleImpediment(impediment)}
                          title={resolved ? "Reabrir impedimento" : "Marcar como resolvido"}
                          style={{ padding: "2px 8px", fontSize: 11 }}
                        >
                          <i
                            className={`pi ${resolved ? "pi-undo" : "pi-check"}`}
                            style={{ fontSize: 10 }}
                          />
                        </Button>
                        <Button
                          variant="ghost"
                          onClick={() => setPendingImpediment(impediment)}
                          title="Excluir impedimento"
                          style={{ padding: "2px 8px", fontSize: 11 }}
                        >
                          <i className="pi pi-trash" style={{ fontSize: 10 }} />
                        </Button>
                      </Can>
                    </div>
                  );
                })}
              </div>
            )}
          </Card>

          {/* F23-T003 — critérios de aceite (§12): checklist com progresso. */}
          <Card title={`Critérios (${criteria.filter((c) => c.isDone).length}/${criteria.length})`}>
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {criteria.length === 0 && (
                <p style={{ fontSize: 12, color: "#9ca3af", margin: 0 }}>
                  Nenhum critério de aceite definido.
                </p>
              )}
              {criteria.map((criterion) => (
                <div
                  key={criterion.id}
                  style={{
                    display: "flex",
                    alignItems: "flex-start",
                    gap: 8,
                    fontSize: 13,
                  }}
                >
                  <input
                    type="checkbox"
                    checked={criterion.isDone}
                    disabled={saving}
                    onChange={() => void toggleCriterion(criterion)}
                    style={{ marginTop: 2, accentColor: "#3b82f6" }}
                  />
                  <span
                    style={{
                      flex: 1,
                      color: criterion.isDone ? "#9ca3af" : "#374151",
                      textDecoration: criterion.isDone ? "line-through" : "none",
                    }}
                  >
                    {criterion.description}
                  </span>
                  <Can level="operate">
                    <Button
                      variant="ghost"
                      onClick={() => setPendingCriterion(criterion)}
                      style={{ padding: "2px 8px", fontSize: 11 }}
                      title="Excluir critério"
                    >
                      <i className="pi pi-trash" style={{ fontSize: 11 }} />
                    </Button>
                  </Can>
                </div>
              ))}

              <Can level="operate">
                <form
                  onSubmit={(event) => void addCriterion(event)}
                  style={{ display: "flex", gap: 8, marginTop: 2 }}
                >
                  <input
                    value={criterionText}
                    onChange={(event) => setCriterionText(event.target.value)}
                    placeholder="Novo critério verificável…"
                    maxLength={300}
                    style={{ flex: 1, fontSize: 13, padding: "6px 8px" }}
                  />
                  <Button type="submit" disabled={saving} style={{ padding: "6px 12px", fontSize: 12 }}>
                    Adicionar
                  </Button>
                </form>
              </Can>
            </div>
          </Card>

          {/* F23-T004/P4c — evidências anexadas (§11): disco local
              (ADR-033); upload ≤10 MB com allowlist no domínio. */}
          <Card title={`Evidências (${evidences.length})`}>
            <Can level="operate">
              <div
                style={{
                  display: "flex",
                  gap: 8,
                  alignItems: "center",
                  flexWrap: "wrap",
                  marginBottom: evidences.length > 0 ? 10 : 0,
                }}
              >
                <input
                  ref={evidenceInputRef}
                  type="file"
                  accept={EVIDENCE_ACCEPT}
                  onChange={(event) => setEvidenceFile(event.target.files?.[0] ?? null)}
                  style={{ fontSize: 12, maxWidth: 260 }}
                />
                <input
                  value={evidenceNote}
                  onChange={(event) => setEvidenceNote(event.target.value)}
                  maxLength={300}
                  placeholder="Nota (opcional, máx. 300)"
                  style={{
                    padding: "5px 8px",
                    fontSize: 12,
                    border: "1px solid #d1d5db",
                    borderRadius: 6,
                    flex: "1 1 160px",
                    minWidth: 140,
                  }}
                />
                <Button
                  variant="ghost"
                  disabled={saving || !evidenceFile}
                  onClick={() => void uploadEvidence()}
                >
                  <i className="pi pi-upload" style={{ fontSize: 11 }} /> Anexar
                </Button>
              </div>
            </Can>

            {evidences.length === 0 ? (
              <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>
                Sem evidências anexadas (PNG, JPG, PDF, TXT, LOG, CSV, JSON, XML ou ZIP —
                até 10 MB).
              </p>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {evidences.map((evidence) => (
                  <div
                    key={evidence.id}
                    style={{ display: "flex", alignItems: "center", gap: 8 }}
                  >
                    <button
                      type="button"
                      onClick={() => void downloadTaskEvidence(evidence)}
                      title="Baixar evidência"
                      style={{
                        background: "none",
                        border: "none",
                        padding: 0,
                        cursor: "pointer",
                        fontSize: 13,
                        color: "#2563eb",
                        textDecoration: "underline",
                        display: "flex",
                        alignItems: "center",
                        gap: 6,
                        textAlign: "left",
                      }}
                    >
                      <i className="pi pi-file" style={{ fontSize: 12 }} />
                      {evidence.fileName}
                    </button>
                    <span style={{ fontSize: 12, color: "#6b7280", flex: 1 }}>
                      {formatFileSize(evidence.fileSize)}
                      {evidence.note ? ` · ${evidence.note}` : ""}
                    </span>
                    <Can level="operate">
                      <Button
                        variant="ghost"
                        onClick={() => setPendingEvidence(evidence)}
                        title="Excluir evidência"
                        style={{ padding: "2px 8px", fontSize: 11 }}
                      >
                        <i className="pi pi-trash" style={{ fontSize: 10 }} />
                      </Button>
                    </Can>
                  </div>
                ))}
              </div>
            )}
          </Card>

          <Card title={`Comentários (${comments.length})`}>
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {comments.length === 0 && (
                <p style={{ fontSize: 12, color: "#9ca3af", margin: 0 }}>
                  Nenhum comentário ainda.
                </p>
              )}
              {comments.map((comment) => (
                <div
                  key={comment.id}
                  style={{
                    borderTop: comments[0]?.id === comment.id ? "none" : "1px solid #f3f4f6",
                    paddingTop: comments[0]?.id === comment.id ? 0 : 8,
                  }}
                >
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      gap: 8,
                    }}
                  >
                    <span style={{ fontSize: 11, fontWeight: 600 }}>
                      {comment.authorName ?? shortId(comment.authorId)}
                      <span style={{ color: "#9ca3af", fontWeight: 400 }}>
                        {" · "}
                        {formatDateTime(new Date(comment.createdAt))}
                      </span>
                    </span>
                    <Can level="operate">
                      <Button
                        variant="ghost"
                        onClick={() => setPendingComment(comment)}
                        title="Excluir comentário"
                        style={{ padding: "2px 6px", fontSize: 11 }}
                      >
                        <i className="pi pi-trash" style={{ fontSize: 10 }} />
                      </Button>
                    </Can>
                  </div>
                  <p style={{ fontSize: 13, margin: "4px 0 0", whiteSpace: "pre-wrap" }}>
                    {comment.content}
                  </p>
                </div>
              ))}
            </div>

            <Can level="operate">
              <form onSubmit={submitComment} style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 8 }}>
                <FormField label="Novo comentário (máx. 500 caracteres)">
                  <textarea
                    value={commentText}
                    onChange={(event) => setCommentText(event.target.value)}
                    maxLength={500}
                    rows={3}
                    placeholder="Escreva um comentário…"
                    style={{
                      padding: "7px 10px",
                      fontSize: 13,
                      border: "1px solid #d1d5db",
                      borderRadius: 6,
                      resize: "vertical",
                      fontFamily: "inherit",
                      width: "100%",
                    }}
                  />
                </FormField>
                <div style={{ display: "flex", justifyContent: "flex-end" }}>
                  <Button type="submit" variant="primary" disabled={saving} style={{ padding: "6px 14px" }}>
                    {saving ? "Enviando…" : "Comentar"}
                  </Button>
                </div>
              </form>
            </Can>
          </Card>

          {/* F23-T004/P4d — timeline unificada tipada (§8): histórico +
              comentários em leitura (impedimentos/evidências/SLA/fases
              entram pelo histórico), chips de tipo só com o que existe. */}
          <Card title="Linha do tempo">
            {timeline.length === 0 ? (
              <p style={{ fontSize: 12, color: "#9ca3af", margin: 0 }}>
                Sem registros.
              </p>
            ) : (
              <>
                {presentTypes.length > 1 && (
                  <div
                    style={{
                      display: "flex",
                      flexWrap: "wrap",
                      gap: 6,
                      marginBottom: 8,
                    }}
                  >
                    <button
                      type="button"
                      onClick={() => setTimelineType("")}
                      style={{
                        border: "1px solid",
                        borderColor: timelineType === "" ? "#3b82f6" : "#e5e7eb",
                        background: timelineType === "" ? "#eff6ff" : "white",
                        color: timelineType === "" ? "#1d4ed8" : "#475569",
                        borderRadius: 999,
                        padding: "2px 10px",
                        fontSize: 11,
                        cursor: "pointer",
                      }}
                    >
                      Todas
                    </button>
                    {presentTypes.map((type) => {
                      const visual = timelineTypeVisual(type);
                      const active = timelineType === type;
                      return (
                        <button
                          key={type}
                          type="button"
                          onClick={() => setTimelineType(active ? "" : type)}
                          style={{
                            border: "1px solid",
                            borderColor: active ? visual.color : "#e5e7eb",
                            background: active ? visual.background : "white",
                            color: visual.color,
                            borderRadius: 999,
                            padding: "2px 10px",
                            fontSize: 11,
                            cursor: "pointer",
                          }}
                        >
                          {visual.label}
                        </button>
                      );
                    })}
                  </div>
                )}

                {visibleTimeline.length === 0 ? (
                  <p style={{ fontSize: 12, color: "#9ca3af", margin: 0 }}>
                    Nenhuma entrada deste tipo.
                  </p>
                ) : (
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    {visibleTimeline.map((entry) => {
                      const visual = timelineTypeVisual(entry.type);
                      return (
                        <div
                          key={entry.id}
                          style={{
                            display: "flex",
                            gap: 8,
                            padding: "6px 0",
                            borderTop:
                              visibleTimeline[0]?.id === entry.id
                                ? "none"
                                : "1px solid #f3f4f6",
                          }}
                        >
                          <StatusChip
                            label={visual.label}
                            background={visual.background}
                            color={visual.color}
                            fontWeight={600}
                            fontSize={10}
                            style={{
                              whiteSpace: "nowrap",
                              padding: "2px 6px",
                              alignSelf: "flex-start",
                            }}
                          />
                          <div>
                            <div style={{ fontSize: 12 }}>
                              {entry.comment ? (
                                <span
                                  style={{
                                    color: "#374151",
                                    whiteSpace: "pre-wrap",
                                  }}
                                >
                                  {entry.comment}
                                </span>
                              ) : (
                                <>
                                  <strong>
                                    {entry.action
                                      ? historyActionLabel(entry.action)
                                      : visual.label}
                                  </strong>
                                  {entry.detail && (
                                    <span style={{ color: "#6b7280" }}>
                                      {" "}
                                      — {entry.detail}
                                    </span>
                                  )}
                                </>
                              )}
                            </div>
                            <div style={{ fontSize: 10, color: "#9ca3af" }}>
                              {formatDateTime(new Date(entry.at))}
                              {entry.actorName ? ` · ${entry.actorName}` : ""}
                            </div>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </>
            )}
          </Card>
        </div>

        {/* ── Coluna lateral ───────────────────────────────────────── */}
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <Card title="Estado">
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              <div>
                <StatusChip
                  label={stateInfo.label}
                  background={stateInfo.background}
                  color={stateInfo.color}
                  fontWeight={600}
                  fontSize={12}
                />
              </div>
              <Can level="operate">
                <SelectField
                  value={task.state}
                  disabled={saving}
                  aria-label={`Mover "${task.title}" para outro estado`}
                  options={stateColumns.map((column) => ({ value: column.id, label: column.label }))}
                  onChange={(target) => void move(target)}
                  style={{ width: "100%" }}
                />
              </Can>
            </div>
          </Card>

          <Card
            title="Identificação"
            actions={
              <Can level="operate">
                <Button variant="ghost" onClick={openIdentDialog} style={{ padding: "4px 10px", fontSize: 12 }}>
                  <i className="pi pi-pencil" style={{ fontSize: 10 }} /> Editar
                </Button>
              </Can>
            }
          >
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 12 }}>
                <span style={{ color: "#6b7280", minWidth: 82 }}>Prioridade</span>
                <StatusChip
                  label={priorityInfo.label}
                  background={priorityInfo.background}
                  color={priorityInfo.color}
                  fontWeight={600}
                  fontSize={11}
                />
              </div>
              <DataRow
                label="Prazo"
                value={
                  task.dueDate ? (
                    <span style={{ color: overdue ? "#dc2626" : undefined, fontWeight: overdue ? 600 : 400 }}>
                      {formatDateOnly(task.dueDate)}
                      {overdue ? " · vencido" : ""}
                    </span>
                  ) : (
                    <span style={{ color: "#9ca3af" }}>sem prazo</span>
                  )
                }
              />
              <DataRow
                label="Responsável"
                value={
                  task.responsibleId
                    ? task.responsibleName ?? `Usuário ${shortId(task.responsibleId)}`
                    : <span style={{ color: "#9ca3af" }}>sem responsável</span>
                }
              />
            </div>
          </Card>

          {/* F23-T003 — planejamento (§5): datas previstas + ciclo de
              replanejamento (§13) com motivo obrigatório. */}
          <Card
            title="Planejamento"
            actions={
              <Can level="operate">
                <span style={{ display: "flex", gap: 6 }}>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setPlannedStart(task.plannedStart ?? "");
                      setPlannedEnd(task.plannedEnd ?? "");
                      setPlanOpen(true);
                    }}
                    style={{ padding: "4px 10px", fontSize: 12 }}
                  >
                    <i className="pi pi-pencil" style={{ fontSize: 10 }} /> Editar
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setReplanStart(task.plannedStart ?? "");
                      setReplanEnd(task.plannedEnd ?? "");
                      setReplanDue(task.dueDate ?? "");
                      setReplanReason("");
                      setReplanOpen(true);
                    }}
                    style={{ padding: "4px 10px", fontSize: 12 }}
                  >
                    <i className="pi pi-refresh" style={{ fontSize: 10 }} /> Replanejar
                  </Button>
                </span>
              </Can>
            }
          >
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              <DataRow
                label="Início"
                value={
                  task.plannedStart ? (
                    formatDateOnly(task.plannedStart)
                  ) : (
                    <span style={{ color: "#9ca3af" }}>sem início planejado</span>
                  )
                }
              />
              <DataRow
                label="Fim"
                value={
                  task.plannedEnd ? (
                    formatDateOnly(task.plannedEnd)
                  ) : (
                    <span style={{ color: "#9ca3af" }}>sem fim planejado</span>
                  )
                }
              />
            </div>
          </Card>

          <Card title={`Participantes (${task.assigneeIds.length})`}>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {task.assigneeIds.length === 0 && (
                <p style={{ fontSize: 12, color: "#9ca3af", margin: 0 }}>
                  Nenhum participante vinculado.
                </p>
              )}
              {task.assigneeIds.map((userId) => (
                <div
                  key={userId}
                  style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}
                >
                  <span style={{ fontSize: 12 }}>
                    <i className="pi pi-user" style={{ fontSize: 10, marginRight: 5, color: "#6b7280" }} />
                    {userName(userId)}
                  </span>
                  <Can level="operate">
                    <Button
                      variant="ghost"
                      disabled={saving}
                      title="Remover participante"
                      onClick={() =>
                        void run(
                          () => unassignTaskUser(task.id, userId),
                          "Participante removido.",
                        )
                      }
                      style={{ padding: "2px 6px", fontSize: 11 }}
                    >
                      <i className="pi pi-times" style={{ fontSize: 10 }} />
                    </Button>
                  </Can>
                </div>
              ))}
              <Can level="operate">
                <div style={{ display: "flex", gap: 8 }}>
                  <SelectField
                    value=""
                    disabled={saving || assignableUsers.length === 0}
                    placeholder={
                      assignableUsers.length === 0 ? "Todos os usuários vinculados" : "Selecionar usuário"
                    }
                    options={assignableUsers.map((user) => ({
                      value: user.userId,
                      label: user.displayName || user.email,
                    }))}
                    onChange={(userId) => {
                      if (!userId) return;
                      void run(() => assignTaskUser(task.id, userId), "Participante vinculado.");
                    }}
                    style={{ width: "100%" }}
                    aria-label="Vincular participante"
                  />
                </div>
              </Can>
            </div>
          </Card>

          <Card title="Vínculos">
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              <div>
                <div style={{ fontSize: 11, color: "#6b7280", marginBottom: 4 }}>
                  Dispositivos ({task.deviceIds.length})
                </div>
                {task.deviceIds.length === 0 && (
                  <p style={{ fontSize: 12, color: "#9ca3af", margin: "0 0 6px" }}>Nenhum dispositivo.</p>
                )}
                {task.deviceIds.map((deviceId) => (
                  <div
                    key={deviceId}
                    style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}
                  >
                    <span style={{ fontSize: 12 }}>
                      <i className="pi pi-server" style={{ fontSize: 10, marginRight: 5, color: "#6b7280" }} />
                      {deviceName(deviceId)}
                    </span>
                    <Can level="operate">
                      <Button
                        variant="ghost"
                        disabled={saving}
                        title="Desvincular dispositivo"
                        onClick={() =>
                          void run(
                            () => unlinkTaskDevice(task.id, deviceId),
                            "Dispositivo desvinculado.",
                          )
                        }
                        style={{ padding: "2px 6px", fontSize: 11 }}
                      >
                        <i className="pi pi-times" style={{ fontSize: 10 }} />
                      </Button>
                    </Can>
                  </div>
                ))}
                <Can level="operate">
                  <SelectField
                    value=""
                    disabled={saving || linkableDevices.length === 0}
                    placeholder={
                      linkableDevices.length === 0 ? "Todos vinculados" : "Vincular dispositivo"
                    }
                    options={linkableDevices.map((device) => ({
                      value: device.id,
                      label: device.name,
                    }))}
                    onChange={(deviceId) => {
                      if (!deviceId) return;
                      void run(() => linkTaskDevice(task.id, deviceId), "Dispositivo vinculado.");
                    }}
                    style={{ width: "100%", marginTop: 6 }}
                    aria-label="Vincular dispositivo"
                  />
                </Can>
              </div>

              <div>
                <div style={{ fontSize: 11, color: "#6b7280", marginBottom: 4 }}>
                  Incidentes ({task.incidentIds.length})
                </div>
                {task.incidentIds.length === 0 && (
                  <p style={{ fontSize: 12, color: "#9ca3af", margin: "0 0 6px" }}>Nenhum incidente.</p>
                )}
                {task.incidentIds.map((incidentId) => (
                  <div
                    key={incidentId}
                    style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}
                  >
                    <span style={{ fontSize: 12 }}>
                      <i className="pi pi-exclamation-circle" style={{ fontSize: 10, marginRight: 5, color: "#6b7280" }} />
                      {incidentTitle(incidentId)}
                    </span>
                    <Can level="operate">
                      <Button
                        variant="ghost"
                        disabled={saving}
                        title="Desvincular incidente"
                        onClick={() =>
                          void run(
                            () => unlinkTaskIncident(task.id, incidentId),
                            "Incidente desvinculado.",
                          )
                        }
                        style={{ padding: "2px 6px", fontSize: 11 }}
                      >
                        <i className="pi pi-times" style={{ fontSize: 10 }} />
                      </Button>
                    </Can>
                  </div>
                ))}
                <Can level="operate">
                  <SelectField
                    value=""
                    disabled={saving || linkableIncidents.length === 0}
                    placeholder={
                      linkableIncidents.length === 0 ? "Todos vinculados" : "Vincular incidente"
                    }
                    options={linkableIncidents.map((incident) => ({
                      value: incident.id,
                      label: incident.title,
                    }))}
                    onChange={(incidentId) => {
                      if (!incidentId) return;
                      void run(() => linkTaskIncident(task.id, incidentId), "Incidente vinculado.");
                    }}
                    style={{ width: "100%", marginTop: 6 }}
                    aria-label="Vincular incidente"
                  />
                </Can>
              </div>
            </div>
          </Card>

          <Card title="SLA">
            {!sla ? (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <p style={{ fontSize: 12, color: "#9ca3af", margin: 0 }}>
                  Nenhum SLA aplicado a esta tarefa.
                </p>
                <Can level="operate">
                  <div style={{ display: "flex", gap: 8 }}>
                    <SelectField
                      value={slaPolicyChoice}
                      disabled={saving || policies.length === 0}
                      placeholder={policies.length === 0 ? "Sem políticas disponíveis" : "Selecionar política"}
                      options={policies.map((policy) => ({
                        value: policy.id,
                        label: `${policy.name} (${policy.responseMinutes}/${policy.resolutionMinutes} min)`,
                      }))}
                      onChange={setSlaPolicyChoice}
                      style={{ width: "100%" }}
                      aria-label="Política de SLA"
                    />
                    <Button
                      variant="primary"
                      disabled={saving || !slaPolicyChoice}
                      onClick={() =>
                        void run(() => applyTaskSla(task.id, slaPolicyChoice), "SLA aplicado.")
                      }
                      style={{ padding: "6px 14px", whiteSpace: "nowrap" }}
                    >
                      Aplicar
                    </Button>
                  </div>
                </Can>
              </div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                <DataRow label="Política" value={policyName(sla.slaPolicyId)} />
                <DataRow
                  label="Resposta até"
                  value={formatDateTime(new Date(sla.responseDueAt))}
                />
                <DataRow
                  label="Resolução até"
                  value={formatDateTime(new Date(sla.resolutionDueAt))}
                />
                <DataRow
                  label="Relógio"
                  value={
                    activePause ? (
                      <span style={{ color: "#d97706", fontWeight: 600 }}>
                        pausado desde {formatDateTime(new Date(activePause.startAt))}
                      </span>
                    ) : (
                      <span style={{ color: "#16a34a" }}>em curso</span>
                    )
                  }
                />
                {pauses.length > 0 && (
                  <div style={{ fontSize: 11, color: "#6b7280" }}>
                    {pauses.length} pausa(s) registrada(s)
                  </div>
                )}
                <Can level="operate">
                  <div style={{ marginTop: 6 }}>
                    <Button
                      variant={activePause ? "primary" : "warning"}
                      disabled={saving}
                      onClick={() =>
                        void run(
                          () => (activePause ? resumeTaskSla(task.id) : pauseTaskSla(task.id)),
                          activePause ? "SLA retomado." : "SLA pausado.",
                        )
                      }
                      style={{ padding: "6px 14px" }}
                    >
                      <i
                        className={activePause ? "pi pi-play" : "pi pi-pause"}
                        style={{ fontSize: 11 }}
                      />{" "}
                      {activePause ? "Retomar" : "Pausar"}
                    </Button>
                  </div>
                </Can>
              </div>
            )}
          </Card>
        </div>
      </div>

      {/* ── Diálogos ─────────────────────────────────────────────────── */}

      <Dialog
        visible={dataOpen}
        onHide={() => setDataOpen(false)}
        header="Editar dados da tarefa"
        width={480}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setDataOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void saveData()}>
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <FormField label="Título">
            <input
              value={dataTitle}
              onChange={(event) => setDataTitle(event.target.value)}
              maxLength={120}
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
          <FormField label="Descrição (opcional — máx. 500)">
            <textarea
              value={dataDescription}
              onChange={(event) => setDataDescription(event.target.value)}
              maxLength={500}
              rows={4}
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                resize: "vertical",
                fontFamily: "inherit",
                width: "100%",
              }}
            />
          </FormField>
        </div>
      </Dialog>

      <Dialog
        visible={identOpen}
        onHide={() => setIdentOpen(false)}
        header="Editar identificação"
        width={480}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setIdentOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void saveIdentification()}>
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <FormField label="Prioridade">
            <SelectField
              value={priority}
              onChange={setPriority}
              options={priorityOptions.map((option) => ({ value: option.value, label: option.label }))}
              style={{ width: "100%" }}
            />
          </FormField>
          <FormField label="Prazo (opcional)">
            <input
              type="date"
              value={dueDate}
              onChange={(event) => setDueDate(event.target.value)}
              style={{
                padding: "6px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
          <FormField label="Responsável (opcional)">
            <SelectField
              value={responsibleId}
              onChange={setResponsibleId}
              options={responsibleOptions}
              style={{ width: "100%" }}
            />
          </FormField>
        </div>
      </Dialog>

      {/* F23-T003 — planejamento (§5): substituição das 2 datas. */}
      <Dialog
        visible={planOpen}
        onHide={() => setPlanOpen(false)}
        header="Editar planejamento"
        width={480}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setPlanOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void savePlanning()}>
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <FormField label="Início planejado (opcional)">
            <input
              type="date"
              value={plannedStart}
              onChange={(event) => setPlannedStart(event.target.value)}
              style={{
                padding: "6px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
          <FormField label="Fim planejado (opcional)">
            <input
              type="date"
              value={plannedEnd}
              onChange={(event) => setPlannedEnd(event.target.value)}
              style={{
                padding: "6px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
        </div>
      </Dialog>

      {/* F23-T003 — replanejamento (§13): datas + motivo obrigatório. */}
      <Dialog
        visible={replanOpen}
        onHide={() => setReplanOpen(false)}
        header="Replanejar tarefa"
        width={520}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setReplanOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void saveReplan()}>
              {saving ? "Replanejando…" : "Replanejar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <p style={{ fontSize: 12, color: "#6b7280", margin: 0 }}>
            Ajuste as datas do plano e informe o motivo — o ciclo fica no
            histórico da tarefa.
          </p>
          <FormField label="Início planejado">
            <input
              type="date"
              value={replanStart}
              onChange={(event) => setReplanStart(event.target.value)}
              style={{
                padding: "6px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
          <FormField label="Fim planejado">
            <input
              type="date"
              value={replanEnd}
              onChange={(event) => setReplanEnd(event.target.value)}
              style={{
                padding: "6px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
          <FormField label="Prazo">
            <input
              type="date"
              value={replanDue}
              onChange={(event) => setReplanDue(event.target.value)}
              style={{
                padding: "6px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
          <FormField label="Motivo do replanejamento (obrigatório)">
            <textarea
              value={replanReason}
              onChange={(event) => setReplanReason(event.target.value)}
              rows={2}
              maxLength={80}
              placeholder="Ex.: impedimento de infraestrutura atrasou o início."
              style={{
                padding: "6px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
                resize: "vertical",
              }}
            />
          </FormField>
        </div>
      </Dialog>

      {/* F23-T003 — resultado da execução (§5). */}
      <Dialog
        visible={resultOpen}
        onHide={() => setResultOpen(false)}
        header="Editar resultado"
        width={560}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setResultOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void saveResult()}>
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <FormField label="Resultado (máx. 2000 caracteres; vazio limpa)">
          <textarea
            value={resultText}
            onChange={(event) => setResultText(event.target.value)}
            rows={6}
            maxLength={2000}
            placeholder="O que a execução desta tarefa entregou…"
            style={{
              padding: "6px 10px",
              fontSize: 13,
              border: "1px solid #d1d5db",
              borderRadius: 6,
              width: "100%",
              resize: "vertical",
            }}
          />
        </FormField>
      </Dialog>

      <ConfirmDialog
        visible={pendingComment != null}
        onHide={() => setPendingComment(null)}
        onConfirm={() => {
          if (!pendingComment) return;
          void run(() => deleteTaskComment(pendingComment.id), "Comentário excluído.");
        }}
        header="Excluir comentário"
        message="Excluir este comentário? Esta ação não pode ser desfeita."
        confirmLabel="Excluir"
      />

      <ConfirmDialog
        visible={pendingCriterion != null}
        onHide={() => setPendingCriterion(null)}
        onConfirm={() => void removeCriterion()}
        header="Excluir critério"
        message={
          pendingCriterion
            ? `Excluir o critério "${pendingCriterion.description}"?`
            : "Excluir o critério?"
        }
        confirmLabel="Excluir"
      />

      {/* F23-T004/P4a — criação de subtarefa (herda o tipo do pai). */}
      <Dialog
        visible={subtaskOpen}
        onHide={() => setSubtaskOpen(false)}
        header="Nova subtarefa"
        width={480}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setSubtaskOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void addSubtask()}>
              {saving ? "Salvando…" : "Adicionar"}
            </Button>
          </div>
        }
      >
        <form
          onSubmit={(event) => void addSubtask(event)}
          style={{ display: "flex", flexDirection: "column", gap: 10 }}
        >
          <FormField label="Título">
            <input
              value={subtaskTitle}
              onChange={(event) => setSubtaskTitle(event.target.value)}
              maxLength={120}
              autoFocus
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                width: "100%",
              }}
            />
          </FormField>
          <FormField label="Descrição (opcional — máx. 500)">
            <textarea
              value={subtaskDescription}
              onChange={(event) => setSubtaskDescription(event.target.value)}
              maxLength={500}
              rows={4}
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                resize: "vertical",
                fontFamily: "inherit",
                width: "100%",
              }}
            />
          </FormField>
        </form>
      </Dialog>

      <ConfirmDialog
        visible={pendingSubtask != null}
        onHide={() => setPendingSubtask(null)}
        onConfirm={() => void unlinkSubtask()}
        header="Desvincular subtarefa"
        message={
          pendingSubtask
            ? `Desvincular "${pendingSubtask.title}" do pai? A task passa a aparecer no quadro.`
            : "Desvincular a subtarefa do pai?"
        }
        confirmLabel="Desvincular"
      />

      {/* F23-T004/P4b — registro de impedimento (≤300; não muda estado). */}
      <Dialog
        visible={impedOpen}
        onHide={() => setImpedOpen(false)}
        header="Registrar impedimento"
        width={480}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setImpedOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void addImpediment()}>
              {saving ? "Salvando…" : "Registrar"}
            </Button>
          </div>
        }
      >
        <form
          onSubmit={(event) => void addImpediment(event)}
          style={{ display: "flex", flexDirection: "column", gap: 10 }}
        >
          <FormField label="Descrição (máx. 300)">
            <textarea
              value={impedText}
              onChange={(event) => setImpedText(event.target.value)}
              maxLength={300}
              rows={4}
              autoFocus
              placeholder="Ex.: aguardando aprovação de orçamento do fornecedor"
              style={{
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                resize: "vertical",
                fontFamily: "inherit",
                width: "100%",
              }}
            />
          </FormField>
        </form>
      </Dialog>

      <ConfirmDialog
        visible={pendingImpediment != null}
        onHide={() => setPendingImpediment(null)}
        onConfirm={() => void removeImpediment()}
        header="Excluir impedimento"
        message={
          pendingImpediment
            ? `Excluir o impedimento "${pendingImpediment.description}"?`
            : "Excluir o impedimento?"
        }
        confirmLabel="Excluir"
      />

      <ConfirmDialog
        visible={pendingEvidence != null}
        onHide={() => setPendingEvidence(null)}
        onConfirm={() => void removeEvidence()}
        header="Excluir evidência"
        message={
          pendingEvidence
            ? `Excluir "${pendingEvidence.fileName}"? O arquivo também sai do disco.`
            : "Excluir a evidência?"
        }
        confirmLabel="Excluir"
      />

      {/* F23-T004/P4d — exclusão de fase (§7). */}
      <ConfirmDialog
        visible={pendingPhase != null}
        onHide={() => setPendingPhase(null)}
        onConfirm={() => void removePhase()}
        header="Excluir fase"
        message={
          pendingPhase
            ? `Excluir a fase "${pendingPhase.name}"? O histórico permanece.`
            : "Excluir a fase?"
        }
        confirmLabel="Excluir"
      />

      <ConfirmDialog
        visible={confirmDeleteTask}
        onHide={() => setConfirmDeleteTask(false)}
        onConfirm={() => {
          void (async () => {
            setSaving(true);
            setError(null);
            try {
              await deleteTask(task.id);
              navigate("/tasks");
            } catch (caught) {
              setError(messageFor(caught));
              setSaving(false);
            }
          })();
        }}
        header="Excluir tarefa"
        message={`Excluir "${task.title}"? Comentários, histórico e vínculos serão removidos.`}
        confirmLabel="Excluir"
      />
    </div>
  );
}
