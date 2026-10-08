using InfraControl.Domain.Enums;

namespace InfraControl.Application.Tasks;

/// <summary>
/// Catálogo dos 13 tipos da timeline unificada da Task
/// (F23-T004/P4d — Plano de Execução §8). A timeline é DERIVADA na
/// leitura (sem tabela nova): as 42 ações do `TaskHistoryAction`
/// mapeiam para tipos grosseiros, comentários entram pela fonte rica
/// (tabela de comentários) e a ação `Commented` do histórico é
/// excluída para não duplicar. Valores em pt-BR minúsculo (query
/// string amigável e comparação ordinal).
/// </summary>
public static class TaskTimelineTypes
{
    public const string Creation = "criacao";
    public const string Edit = "edicao";
    public const string State = "estado";
    public const string Assignment = "atribuicao";
    public const string Link = "vinculo";
    public const string Planning = "planejamento";
    public const string Criterion = "criterio";
    public const string Subtask = "subtarefa";
    public const string Blocker = "bloqueio";
    public const string Attachment = "anexo";
    public const string Sla = "sla";
    public const string Phase = "fase";
    public const string Comment = "comentario";

    public static readonly IReadOnlyCollection<string> All =
    [
        Creation, Edit, State, Assignment, Link, Planning, Criterion,
        Subtask, Blocker, Attachment, Sla, Phase, Comment,
    ];

    /// <summary>Mapeia a ação do histórico para o tipo da timeline.</summary>
    public static string ForAction(TaskHistoryAction action) => action switch
    {
        TaskHistoryAction.Created => Creation,
        TaskHistoryAction.StateChanged => State,
        TaskHistoryAction.Assigned or TaskHistoryAction.Unassigned => Assignment,
        TaskHistoryAction.DeviceLinked or TaskHistoryAction.DeviceUnlinked
            or TaskHistoryAction.IncidentLinked or TaskHistoryAction.IncidentUnlinked
            or TaskHistoryAction.KnowledgeLinked or TaskHistoryAction.KnowledgeUnlinked
            or TaskHistoryAction.PdiLinked or TaskHistoryAction.PdiUnlinked => Link,
        TaskHistoryAction.PriorityChanged or TaskHistoryAction.DueDateChanged
            or TaskHistoryAction.ResponsibleChanged or TaskHistoryAction.PlanningChanged
            or TaskHistoryAction.ResultChanged or TaskHistoryAction.Replanned => Planning,
        TaskHistoryAction.CriterionAdded or TaskHistoryAction.CriterionUpdated
            or TaskHistoryAction.CriterionRemoved => Criterion,
        TaskHistoryAction.ParentChanged or TaskHistoryAction.SubtaskAdded
            or TaskHistoryAction.SubtaskRemoved => Subtask,
        TaskHistoryAction.ImpedimentAdded or TaskHistoryAction.ImpedimentUpdated
            or TaskHistoryAction.ImpedimentResolved or TaskHistoryAction.ImpedimentReopened
            or TaskHistoryAction.ImpedimentRemoved => Blocker,
        TaskHistoryAction.EvidenceAdded or TaskHistoryAction.EvidenceRemoved => Attachment,
        TaskHistoryAction.SlaApplied or TaskHistoryAction.SlaPaused
            or TaskHistoryAction.SlaResumed => Sla,
        TaskHistoryAction.PhaseAdded or TaskHistoryAction.PhaseUpdated
            or TaskHistoryAction.PhaseCurrentChanged or TaskHistoryAction.PhaseCompleted
            or TaskHistoryAction.PhaseReopened or TaskHistoryAction.PhaseRemoved => Phase,
        TaskHistoryAction.Commented => Comment,
        // Updated e ações futuras: edição genérica.
        _ => Edit,
    };
}
