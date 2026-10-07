using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Fase do desenvolvimento (docs/30 §7 — "parte principal do modelo",
/// marco de desenvolvimento entre objetivo e atividade). Campos de
/// acompanhamento das children (evidências, dificuldades, feedbacks)
/// vêm nas Fases 3/4 — ADR-032.
/// </summary>
public sealed class PdiPhase : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int CriteriaMaxLength = 1000;

    public Guid StrategyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Responsável pela fase (§7) — usuário (dono, gestor etc.).</summary>
    public Guid? ResponsibleId { get; private set; }

    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? CompletionCriteria { get; private set; }
    public PdiPhaseStatus Status { get; private set; }
    public int Percent { get; private set; }
    public int Order { get; private set; }

    private PdiPhase() { }

    public static PdiPhase Create(
        Guid strategyId,
        string title,
        string? description,
        Guid? responsibleId,
        DateOnly? startDate,
        DateOnly? endDate,
        string? completionCriteria,
        PdiPhaseStatus status,
        int percent,
        int order)
    {
        if (strategyId == Guid.Empty)
            throw new ArgumentException(
                "A estratégia da fase é obrigatória.", nameof(strategyId));

        var phase = new PdiPhase { StrategyId = strategyId, Order = order };
        phase.Apply(
            title, description, responsibleId, startDate, endDate,
            completionCriteria, status, percent);
        return phase;
    }

    public void Update(
        string title,
        string? description,
        Guid? responsibleId,
        DateOnly? startDate,
        DateOnly? endDate,
        string? completionCriteria,
        PdiPhaseStatus status,
        int percent)
        => Apply(
            title, description, responsibleId, startDate, endDate,
            completionCriteria, status, percent);

    /// <summary>
    /// Auto-start da próxima fase ao validar a anterior (§15, ADR-035).
    /// Estado inválido = 409.
    /// </summary>
    public void Start()
    {
        if (Status != PdiPhaseStatus.NotStarted)
            throw new InvalidOperationException(
                "A fase precisa estar não iniciada para ser iniciada.");

        Status = PdiPhaseStatus.InProgress;
        Touch();
    }

    /// <summary>
    /// Dono conclui a fase → aguarda validação do gestor (§15, ADR-035).
    /// Estado inválido = 409 (mensagem cita "em execução").
    /// </summary>
    public void Submit()
    {
        if (Status != PdiPhaseStatus.InProgress)
            throw new InvalidOperationException(
                "A fase precisa estar em execução para ser concluída.");

        Status = PdiPhaseStatus.PendingValidation;
        Percent = 100;
        Touch();
    }

    /// <summary>
    /// Gestor valida a fase concluída → <see cref="PdiPhaseStatus.Completed"/>
    /// (§15, ADR-035). Estado inválido = 409.
    /// </summary>
    public void Validate()
    {
        EnsureAwaitingValidation();
        Status = PdiPhaseStatus.Completed;
        Touch();
    }

    /// <summary>
    /// Gestor rejeita a fase concluída → volta a
    /// <see cref="PdiPhaseStatus.InProgress"/> (§15, ADR-035). O motivo é
    /// obrigatório e cobrado pelo handler. Estado inválido = 409.
    /// </summary>
    public void Reject()
    {
        EnsureAwaitingValidation();
        Status = PdiPhaseStatus.InProgress;
        Touch();
    }

    private void EnsureAwaitingValidation()
    {
        if (Status != PdiPhaseStatus.PendingValidation)
            throw new InvalidOperationException(
                "A fase precisa estar aguardando validação.");
    }

    private void Apply(
        string title,
        string? description,
        Guid? responsibleId,
        DateOnly? startDate,
        DateOnly? endDate,
        string? completionCriteria,
        PdiPhaseStatus status,
        int percent)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "O título da fase é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título da fase pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        if (startDate is { } start && endDate is { } end && end < start)
            throw new ArgumentException(
                "O fim da fase não pode ser anterior ao início.", nameof(endDate));

        if (!Enum.IsDefined(status))
            throw new ArgumentException(
                "Status da fase inválido (use NotStarted, InProgress, PendingValidation ou Completed).",
                nameof(status));

        if (percent is < 0 or > 100)
            throw new ArgumentException(
                "O percentual da fase deve estar entre 0 e 100.", nameof(percent));

        Title = title;
        Description = PdiCompetency.Normalize(description, DescriptionMaxLength, nameof(description));
        ResponsibleId = responsibleId;
        StartDate = startDate;
        EndDate = endDate;
        CompletionCriteria = PdiCompetency.Normalize(completionCriteria, CriteriaMaxLength, nameof(completionCriteria));
        Status = status;
        Percent = percent;
        Touch();
    }
}
