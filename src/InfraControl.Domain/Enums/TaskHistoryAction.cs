namespace InfraControl.Domain.Enums;

/// <summary>
/// Ações registradas no histórico da Task (F7-T006). Persistido como
/// string(40); F7-T007 (Workflow) pode adicionar transições de estado.
/// </summary>
public enum TaskHistoryAction
{
    Created,
    Updated,
    Assigned,
    Unassigned,
    PriorityChanged,
    DueDateChanged,
    ResponsibleChanged,
    DeviceLinked,
    DeviceUnlinked,
    IncidentLinked,
    IncidentUnlinked,
    Commented,
    StateChanged,
    SlaApplied,
    SlaPaused,
    SlaResumed,
    // F23-T003 — planejamento/resultado (§5), replanejo (§13) e
    // critérios de aceite (§12).
    PlanningChanged,
    ResultChanged,
    Replanned,
    CriterionAdded,
    CriterionUpdated,
    CriterionRemoved,
    // F23-T004/P4a — subtarefas: filho ganha/perde pai (ParentChanged) e
    // os pais recebem SubtaskAdded/SubtaskRemoved com o título do filho.
    ParentChanged,
    SubtaskAdded,
    SubtaskRemoved,
    // F23-T004/P4b — impedimentos estruturados (§10): registro, edição
    // de descrição, resolução/reabertura e exclusão.
    ImpedimentAdded,
    ImpedimentUpdated,
    ImpedimentResolved,
    ImpedimentReopened,
    ImpedimentRemoved,
    // F23-T004/P4c — evidências anexadas (§11): upload em disco local
    // (ADR-033) e exclusão (arquivo + registro).
    EvidenceAdded,
    EvidenceRemoved,
    // F23-T004/P4d — fases dentro da task (§7): etapa adicionada,
    // renomeada, apontada como atual, concluída/reaberta e removida.
    PhaseAdded,
    PhaseUpdated,
    PhaseCurrentChanged,
    PhaseCompleted,
    PhaseReopened,
    PhaseRemoved,
    // F23-T005/P5a — Knowledge vinculada à task (docs/32 §10, spec Tasks
    // §16): vínculo e desvinculação N:N com artigo da base.
    KnowledgeLinked,
    KnowledgeUnlinked,
    // F23-T005/P5b — PDI vinculado à task (spec §15; alvo = plano do
    // usuário, 1 por usuário): junção por user_id (TaskPdi).
    PdiLinked,
    PdiUnlinked
}
