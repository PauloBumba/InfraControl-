using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Fase (etapa) dentro de uma Task (F23-T004/P4d — Plano de Execução §7):
/// sequência nomeada criada por task — nenhum conjunto fixo é imposto
/// pelo domínio (o texto da spec não está no repo; design escolhido pelo
/// usuário no checkpoint). "Fase atual" é um ponteiro (no máximo um por
/// task — o handler limpa os irmãos) e a conclusão é carimbada.
/// Fases são ortagonais ao workflow (§6): estado = situação, fase =
/// etapa de execução.
/// </summary>
public sealed class TaskPhase : AggregateRoot
{
    public const int NameMaxLength = 120;

    public Guid TaskId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Ordem dentro da task (0-based). Exclusões podem deixar gaps — a
    /// leitura ordena por posição; novas fases entram em max+1.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>Ponteiro de "fase atual" — no máximo um por task.</summary>
    public bool IsCurrent { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public bool IsCompleted => CompletedAt != null;

    private TaskPhase() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskPhaseDeletedDomainEvent(Id, TaskId));

    public static TaskPhase Create(
        Guid taskId, string name, int position, bool isCurrent = false)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException(
                "O id da task é obrigatório.", nameof(taskId));
        if (position < 0)
            throw new ArgumentException(
                "A posição da fase é inválida.", nameof(position));

        var phase = new TaskPhase
        {
            TaskId = taskId,
            Name = ValidateName(name),
            Position = position,
            IsCurrent = isCurrent,
            CompletedAt = null
        };
        phase.RaiseDomainEvent(new TaskPhaseCreatedDomainEvent(
            phase.Id, phase.TaskId));
        return phase;
    }

    /// <summary>ADR-030 §1 — renomear (mesmos guards do Create).</summary>
    public void Rename(string name)
    {
        Name = ValidateName(name);
        RaiseDomainEvent(new TaskPhaseUpdatedDomainEvent(Id, TaskId));
    }

    /// <summary>
    /// Move o ponteiro de fase atual (o handler limpa os irmãos para
    /// manter no máximo um por task).
    /// </summary>
    public void SetCurrent(bool isCurrent)
    {
        IsCurrent = isCurrent;
        RaiseDomainEvent(new TaskPhaseUpdatedDomainEvent(Id, TaskId));
    }

    public void Complete()
    {
        CompletedAt = DateTime.UtcNow;
        RaiseDomainEvent(new TaskPhaseUpdatedDomainEvent(Id, TaskId));
    }

    public void Reopen()
    {
        CompletedAt = null;
        RaiseDomainEvent(new TaskPhaseUpdatedDomainEvent(Id, TaskId));
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome da fase é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da fase pode ter no máximo {NameMaxLength} caracteres.",
                nameof(name));

        return name;
    }
}
