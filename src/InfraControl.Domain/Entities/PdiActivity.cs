using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Atividade dentro de uma fase (docs/30 §8 — "ações menores",
/// checklist ☐; distinção: fase = marco, atividade = ação necessária).
/// </summary>
public sealed class PdiActivity : Entity
{
    public const int TitleMaxLength = 200;

    public Guid PhaseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly? DueDate { get; private set; }
    public bool Completed { get; private set; }
    public int Order { get; private set; }

    private PdiActivity() { }

    public static PdiActivity Create(
        Guid phaseId, string title, DateOnly? dueDate, int order)
    {
        if (phaseId == Guid.Empty)
            throw new ArgumentException(
                "A fase da atividade é obrigatória.", nameof(phaseId));

        var activity = new PdiActivity { PhaseId = phaseId, Order = order };
        activity.Apply(title, dueDate, completed: false);
        return activity;
    }

    public void Update(string title, DateOnly? dueDate, bool completed)
        => Apply(title, dueDate, completed);

    private void Apply(string title, DateOnly? dueDate, bool completed)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "O título da atividade é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título da atividade pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        Title = title;
        DueDate = dueDate;
        Completed = completed;
        Touch();
    }
}
