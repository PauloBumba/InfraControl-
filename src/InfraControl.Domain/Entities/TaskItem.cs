using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Unidade genérica de trabalho (docs/04 — F7-T001). Nome `TaskItem`
/// (não `Task`) evita a colisão com `System.Threading.Tasks.Task` em
/// todo o codebase async. Nasce em <see cref="TaskState.Open"/>;
/// relacionamentos (assignees/devices/incidents), comentários, histórico
/// e workflow de transição vêm em F7-T002→T007.
/// </summary>
public sealed class TaskItem : AggregateRoot
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    // F23-T003 — resultado da execução (Plano de Execução §5): texto mais
    // longo que a descrição (fecha/resultado do trabalho executado).
    public const int ResultMaxLength = 2000;

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskKind Kind { get; private set; }
    public TaskState State { get; private set; }

    // F23-T001 — identificação básica (Plano de Execução §5): prioridade
    // nasce Medium; prazo e responsável são opcionais. Sem FK para o
    // usuário (precedente manager_id do PDI, ADR-029) — o usuário pode
    // ser excluído e a task permanece com o registro histórico.
    public TaskPriority Priority { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public Guid? ResponsibleId { get; private set; }

    // F32-T001 — quem criou a task (espelho CreatedById do Project):
    // distingue o autor do pedido de quem executa (ResponsibleId). Sem
    // FK (mesmo critério do ResponsibleId / ADR-029) — usuário excluído
    // não apaga o registro histórico. Nulo só em tasks legadas pré-migração.
    public Guid? CreatedById { get; private set; }

    // F23-T003 — planejamento e resultado (Plano de Execução §5): datas
    // previstas de início/fim (o prazo de compromisso continua no
    // DueDate) e o resultado obtido na execução.
    public DateOnly? PlannedStart { get; private set; }
    public DateOnly? PlannedEnd { get; private set; }
    public string? Result { get; private set; }

    // F23-T004/P4a — subtarefas (spec subtarefas/progresso): auto-relação
    // na própria tabela `tasks` (subtarefa É task). Pai excluído → filho
    // sobrevive (FK SetNull na configuração). Regra de profundidade
    // (subtarefa não tem subtarefa) é do handler, que precisa consultar
    // o pai; aqui só o guard de auto-relação.
    public Guid? ParentTaskId { get; private set; }

    private TaskItem() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskItemDeletedDomainEvent(Id, Title));

    public static TaskItem Create(
        string title,
        TaskKind kind,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        DateOnly? dueDate = null,
        Guid? responsibleId = null,
        Guid? createdById = null)
    {
        var task = new TaskItem();
        task.Update(title, description);
        task.Kind = kind;
        task.State = TaskState.Open;
        task.Priority = priority;
        task.DueDate = dueDate;
        task.SetResponsible(responsibleId);

        if (createdById == Guid.Empty)
            throw new ArgumentException(
                "O id do criador não pode ser vazio.", nameof(createdById));
        task.CreatedById = createdById;

        // ADR-030 §5 — descarta o Updated transitório do Update interno.
        task.ClearDomainEvents();
        task.RaiseDomainEvent(new TaskItemCreatedDomainEvent(task.Id, task.Title, task.Kind));
        return task;
    }

    public void Update(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da task é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título da task pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição da task pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        RaiseDomainEvent(new TaskItemUpdatedDomainEvent(Id, Title, State));
    }

    /// <summary>
    /// F23-T001 — identificação básica. Os setters são silenciosos: o
    /// registro por campo alterado (PriorityChanged/DueDateChanged/
    /// ResponsibleChanged) é responsabilidade do handler, que captura o
    /// antes/depois. Prazo no passado é permitido (task atrasada ≠ erro).
    /// </summary>
    public void SetPriority(TaskPriority priority) =>
        Priority = priority;

    public void SetDueDate(DateOnly? dueDate) =>
        DueDate = dueDate;

    public void SetResponsible(Guid? responsibleId)
    {
        if (responsibleId == Guid.Empty)
            throw new ArgumentException(
                "O id do usuário é obrigatório.", nameof(responsibleId));

        ResponsibleId = responsibleId;
    }

    /// <summary>
    /// F23-T003 — planejamento (§5). Fim antes do início é erro de domínio
    /// → ArgumentException (400 pt-BR); nulls limpam a data. Setter
    /// silencioso: o histórico (PlanningChanged) é do handler, que captura
    /// antes/depois (mesma arquitetura da identificação, F23-T001).
    /// </summary>
    public void SetPlanning(DateOnly? plannedStart, DateOnly? plannedEnd)
    {
        if (plannedStart is { } start && plannedEnd is { } end && end < start)
            throw new ArgumentException(
                "O fim planejado não pode ser anterior ao início planejado.",
                nameof(plannedEnd));

        PlannedStart = plannedStart;
        PlannedEnd = plannedEnd;
    }

    /// <summary>
    /// F23-T003 — resultado da execução (§5); vazio/null limpa o valor.
    /// Silencioso — o histórico (ResultChanged) é do handler.
    /// </summary>
    public void SetResult(string? result)
    {
        if (result?.Length > ResultMaxLength)
            throw new ArgumentException(
                $"O resultado pode ter no máximo {ResultMaxLength} caracteres.",
                nameof(result));

        Result = string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    /// <summary>
    /// F23-T004/P4a — vincula/desvincula o pai da subtarefa (null =
    /// vira task raiz). Auto-relação → ArgumentException (400 pt-BR);
    /// a validação de existência e de profundidade fica no handler.
    /// </summary>
    public void SetParent(Guid? parentTaskId)
    {
        if (parentTaskId == Id)
            throw new ArgumentException(
                "Uma task não pode ser própria subtarefa.",
                nameof(parentTaskId));

        ParentTaskId = parentTaskId;
    }

    /// <summary>
    /// F7-T007 (Workflow): no-op se o estado já é o alvo; fora do mapa de
    /// <see cref="TaskStateWorkflow"/> lança InvalidOperationException
    /// (GlobalExceptionHandler → 409 pt-BR).
    /// </summary>
    /// <summary>
    /// F48-T001: <paramref name="changedByUserId"/> identifica o executor da
    /// transição — em conclusões (Resolved/Closed) vira notificação de volta
    /// para ele e para os interessados (solicitante/responsável).
    /// </summary>
    public void ChangeState(TaskState state, Guid? changedByUserId = null)
    {
        if (State == state)
            return;

        if (!TaskStateWorkflow.CanTransition(State, state))
            throw new InvalidOperationException(
                $"Transição inválida: '{State}' → '{state}'.");

        State = state;
        RaiseDomainEvent(new TaskItemUpdatedDomainEvent(Id, Title, State));

        if (state is TaskState.Resolved or TaskState.Closed)
            RaiseDomainEvent(new TaskResolvedDomainEvent(Id, Title, state, changedByUserId));
    }
}
