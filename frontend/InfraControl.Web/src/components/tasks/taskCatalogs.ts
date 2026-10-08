/**
 * F23-T002 — catálogo e utilitários compartilhados entre o kanban
 * (`TasksPage`) e a tela de detalhe (`TaskDetailPage`): estados, tipos,
 * prioridade, prazo e rótulos do histórico. Extraído da TasksPage para
 * as duas telas consumirem a MESMA fonte (enums reais de docs/04) sem
 * importar página em página.
 */

/** Colunas do kanban — os 7 estados de `TaskState` (rótulos no plural). */
export const stateColumns = [
  { id: "Open", label: "Abertas", color: "#6b7280" },
  { id: "InProgress", label: "Em progresso", color: "#3b82f6" },
  { id: "Waiting", label: "Aguardando", color: "#f59e0b" },
  { id: "Blocked", label: "Bloqueadas", color: "#dc2626" },
  { id: "Resolved", label: "Resolvidas", color: "#22c55e" },
  { id: "Cancelled", label: "Canceladas", color: "#94a3b8" },
  { id: "Closed", label: "Fechadas", color: "#374151" },
];

export const stateLabel = (state: string): string =>
  stateColumns.find((column) => column.id === state)?.label ?? state;

/** Rótulo no singular + cores para o chip de estado no detalhe. */
export const stateVisual = (state: string): { label: string; color: string; background: string } =>
  ({
    Open: { label: "Aberta", color: "#475569", background: "#f1f5f9" },
    InProgress: { label: "Em progresso", color: "#3b82f6", background: "#eff6ff" },
    Waiting: { label: "Aguardando", color: "#d97706", background: "#fffbeb" },
    Blocked: { label: "Bloqueada", color: "#dc2626", background: "#fef2f2" },
    Resolved: { label: "Resolvida", color: "#16a34a", background: "#f0fdf4" },
    Cancelled: { label: "Cancelada", color: "#94a3b8", background: "#f1f5f9" },
    Closed: { label: "Fechada", color: "#374151", background: "#f3f4f6" },
  })[state] ?? { label: state, color: "#475569", background: "#f1f5f9" };

/** Rótulos pt-BR dos valores enviados no body (`CreateTaskCommand`). */
export const kindOptions = [
  { value: "Manual", label: "Manual" },
  { value: "Incident", label: "Incidente" },
  { value: "Alert", label: "Alerta" },
  { value: "Monitoring", label: "Monitoramento" },
  { value: "Discovery", label: "Descoberta" },
  { value: "Maintenance", label: "Manutenção" },
  { value: "Change", label: "Mudança" },
  { value: "Preventive", label: "Preventiva" },
  { value: "Corrective", label: "Corretiva" },
  { value: "Project", label: "Projeto" },
  { value: "System", label: "Sistema" },
];

export const kindLabel = (kind: string): string =>
  kindOptions.find((option) => option.value === kind)?.label ?? kind;

/** Rótulos e cores da prioridade (`TaskPriority`, F23-T001). */
export const priorityOptions = [
  { value: "Low", label: "Baixa" },
  { value: "Medium", label: "Média" },
  { value: "High", label: "Alta" },
  { value: "Critical", label: "Crítica" },
];

export const priorityVisual = (priority: string): { label: string; color: string; background: string } =>
  ({
    Low: { label: "Baixa", color: "#64748b", background: "#f1f5f9" },
    Medium: { label: "Média", color: "#3b82f6", background: "#eff6ff" },
    High: { label: "Alta", color: "#d97706", background: "#fffbeb" },
    Critical: { label: "Crítica", color: "#dc2626", background: "#fef2f2" },
  })[priority] ?? { label: priority, color: "#475569", background: "#f1f5f9" };

/**
 * Rótulos pt-BR das ações de `TaskHistoryAction` (F7-T006 + F23-T001) —
 * o histórico da tela de detalhe; desconhecida → PascalCase humanizado.
 */
export const historyActionLabel = (action: string): string =>
  ({
    Created: "Criada",
    Updated: "Atualizada",
    Assigned: "Participante vinculado",
    Unassigned: "Participante desvinculado",
    PriorityChanged: "Prioridade alterada",
    DueDateChanged: "Prazo alterado",
    ResponsibleChanged: "Responsável alterado",
    DeviceLinked: "Dispositivo vinculado",
    DeviceUnlinked: "Dispositivo desvinculado",
    IncidentLinked: "Incidente vinculado",
    IncidentUnlinked: "Incidente desvinculado",
    Commented: "Comentário adicionado",
    StateChanged: "Estado alterado",
    SlaApplied: "SLA aplicado",
    SlaPaused: "SLA pausado",
    SlaResumed: "SLA retomado",
    PlanningChanged: "Planejamento alterado",
    ResultChanged: "Resultado alterado",
    Replanned: "Replanejado",
    CriterionAdded: "Critério adicionado",
    CriterionUpdated: "Critério atualizado",
    CriterionRemoved: "Critério removido",
    ParentChanged: "Vínculo de subtarefa alterado",
    SubtaskAdded: "Subtarefa adicionada",
    SubtaskRemoved: "Subtarefa removida",
    ImpedimentAdded: "Impedimento registrado",
    ImpedimentUpdated: "Impedimento atualizado",
    ImpedimentResolved: "Impedimento resolvido",
    ImpedimentReopened: "Impedimento reaberto",
    ImpedimentRemoved: "Impedimento excluído",
    EvidenceAdded: "Evidência anexada",
    EvidenceRemoved: "Evidência excluída",
    PhaseAdded: "Fase adicionada",
    PhaseUpdated: "Fase renomeada",
    PhaseCurrentChanged: "Fase atual alterada",
    PhaseCompleted: "Fase concluída",
    PhaseReopened: "Fase reaberta",
    PhaseRemoved: "Fase removida",
    KnowledgeLinked: "Knowledge vinculada",
    KnowledgeUnlinked: "Knowledge desvinculada",
    PdiLinked: "PDI vinculado",
    PdiUnlinked: "PDI desvinculado",
  })[action] ??
  action.replace(/([a-z])([A-Z])/g, "$1 $2");

/**
 * 13 tipos da timeline unificada (F23-T004/P4d — §8) — espelho de
 * `TaskTimelineTypes` (Application): os chips de filtro do card
 * "Linha do tempo" só exibem os tipos presentes na resposta.
 */
export const timelineTypes = [
  { id: "criacao", label: "Criação" },
  { id: "edicao", label: "Edição" },
  { id: "estado", label: "Estado" },
  { id: "atribuicao", label: "Atribuição" },
  { id: "vinculo", label: "Vínculo" },
  { id: "planejamento", label: "Planejamento" },
  { id: "criterio", label: "Critério" },
  { id: "subtarefa", label: "Subtarefa" },
  { id: "bloqueio", label: "Bloqueio" },
  { id: "anexo", label: "Anexo" },
  { id: "sla", label: "SLA" },
  { id: "fase", label: "Fase" },
  { id: "comentario", label: "Comentário" },
];

export const timelineTypeLabel = (type: string): string =>
  timelineTypes.find((entry) => entry.id === type)?.label ?? type;

/** Rótulo + cores do chip de tipo na linha do tempo. */
export const timelineTypeVisual = (
  type: string,
): { label: string; color: string; background: string } =>
  ({
    criacao: { label: "Criação", color: "#16a34a", background: "#f0fdf4" },
    edicao: { label: "Edição", color: "#475569", background: "#f1f5f9" },
    estado: { label: "Estado", color: "#3b82f6", background: "#eff6ff" },
    atribuicao: { label: "Atribuição", color: "#6366f1", background: "#eef2ff" },
    vinculo: { label: "Vínculo", color: "#0891b2", background: "#ecfeff" },
    planejamento: { label: "Planejamento", color: "#7c3aed", background: "#f5f3ff" },
    criterio: { label: "Critério", color: "#0d9488", background: "#f0fdfa" },
    subtarefa: { label: "Subtarefa", color: "#d97706", background: "#fffbeb" },
    bloqueio: { label: "Bloqueio", color: "#dc2626", background: "#fef2f2" },
    anexo: { label: "Anexo", color: "#ea580c", background: "#fff7ed" },
    sla: { label: "SLA", color: "#e11d48", background: "#fff1f2" },
    fase: { label: "Fase", color: "#9333ea", background: "#faf5ff" },
    comentario: { label: "Comentário", color: "#0284c7", background: "#f0f9ff" },
  })[type] ?? { label: type, color: "#475569", background: "#f1f5f9" };

/** `yyyy-MM-dd` (DateOnly) → `dd/MM/yyyy` sem passar por Date (anti-fuso). */
export const formatDateOnly = (value: string): string => {
  const [year, month, day] = value.split("-");
  return `${day}/${month}/${year}`;
};

/** Vencido = prazo anterior ao hoje local (comparação lexicográfica ISO). */
export const isOverdue = (value: string): boolean =>
  value < new Date().toISOString().slice(0, 10);

/** Tamanho de arquivo legível (evidências — ADR-033; 10 MB no limite). */
export const formatFileSize = (bytes: number): string =>
  bytes < 1024
    ? `${bytes} B`
    : bytes < 1024 * 1024
      ? `${Math.round(bytes / 1024)} kB`
      : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

/** Extensões aceitas no upload de evidência — espelho da allowlist do domínio. */
export const EVIDENCE_ACCEPT =
  ".png,.jpg,.jpeg,.gif,.webp,.pdf,.txt,.log,.csv,.json,.xml,.zip";

/** Mensagem pt-BR do envelope (`ApiError`) ou genérica de transporte. */
export function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}
