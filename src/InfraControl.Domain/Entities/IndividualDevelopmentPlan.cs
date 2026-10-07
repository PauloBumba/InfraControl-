using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Plano de Desenvolvimento Individual (PDI) — docs/29 §8 e docs/30.
/// Conceito de pessoas: direção de crescimento de um usuário (Operador/
/// Admin), com estratégia, prazo, status do ciclo de vida (9 estados,
/// ADR-029), gestor responsável e período do ciclo. 1 registro vigente
/// por usuário (índice único em <see cref="UserId"/>); ciclo novo
/// sobrescreve o anterior via upsert — <b>não</b> guarda snapshots de
/// ciclos anteriores (decisão de 2026-10-03), mas mantém o
/// <b>timeline de eventos</b> do plano vigente em
/// <see cref="PdiHistoryEntry"/> (docs/30 §17, ADR-029). CRUD apenas:
/// não há remoção — encerrar é <see cref="PdiStatus.Cancelled"/> (só a
/// partir de Active). Create/Update levantam os domain events
/// distribuídos (ADR-005).
/// </summary>
public sealed class IndividualDevelopmentPlan : AggregateRoot
{
    public const int StrategyMaxLength = 4000;

    public Guid UserId { get; private set; }
    public string Strategy { get; private set; } = string.Empty;
    public DateOnly TargetDate { get; private set; }
    public PdiStatus Status { get; private set; }

    /// <summary>Gestor/liderança responsável (definido só pelo Admin, ADR-029 §3).</summary>
    public Guid? ManagerId { get; private set; }

    /// <summary>Início do período/ciclo (opcional).</summary>
    public DateOnly? PeriodStart { get; private set; }

    /// <summary>Fim do período/ciclo (opcional; ≥ <see cref="PeriodStart"/>).</summary>
    public DateOnly? PeriodEnd { get; private set; }

    private readonly List<PdiHistoryEntry> _pendingHistory = [];

    /// <summary>Entradas de histórico geradas nesta sessão (antes do drenar).</summary>
    public IReadOnlyCollection<PdiHistoryEntry> PendingHistory =>
        _pendingHistory.AsReadOnly();

    private IndividualDevelopmentPlan() { }

    public static IndividualDevelopmentPlan Create(
        Guid userId,
        string strategy,
        DateOnly targetDate,
        PdiStatus status,
        Guid? managerId,
        DateOnly? periodStart,
        DateOnly? periodEnd,
        PdiChangeActor actor)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "O usuário dono do PDI é obrigatório.", nameof(userId));

        ArgumentNullException.ThrowIfNull(actor);

        var plan = new IndividualDevelopmentPlan { UserId = userId };
        plan.Apply(new PdiChanges(
            strategy, targetDate, status, managerId, periodStart, periodEnd,
            Reason: null));
        plan._pendingHistory.Add(
            PdiHistoryEntry.ForCreated(userId, plan.Status, actor.Id));
        plan.RaiseDomainEvent(new PdiCreatedDomainEvent(
            plan.UserId, plan.TargetDate, plan.Status,
            actor.Id, actor.Email, actor.OwnerEmail));
        return plan;
    }

    public void Update(PdiChanges changes, PdiChangeActor actor)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(actor);

        // Transição validada pela tabela do docs/30 §3 (ADR-029) antes
        // de qualquer mutação — inválida = 400, estado intacto.
        PdiTransitions.EnsureCanMove(Status, changes.Status);

        var before = (
            Strategy,
            TargetDate,
            Status,
            ManagerId,
            PeriodStart,
            PeriodEnd);

        // Validações puras (mesmas mensagens do Create) antes de cobrar
        // o motivo — precedence de erro preservada, estado intacto.
        var normalizedStrategy = RequireStrategy(changes.Strategy);
        EnsureValid(changes);

        // §14 (Fase 5, ADR-035): mudar campos exige motivo; transição
        // de status sozinha mantém o motivo opcional (§15 aceite).
        var fieldChanges = DescribeFieldChanges(
            before.Strategy, before.TargetDate, before.ManagerId,
            before.PeriodStart, before.PeriodEnd,
            normalizedStrategy, changes);

        if (fieldChanges.Count > 0 && string.IsNullOrWhiteSpace(changes.Reason))
            throw new ArgumentException(
                "O motivo da alteração é obrigatório.", nameof(changes));

        Strategy = normalizedStrategy;
        TargetDate = changes.TargetDate;
        Status = changes.Status;
        ManagerId = changes.ManagerId;
        PeriodStart = changes.PeriodStart;
        PeriodEnd = changes.PeriodEnd;
        Touch();

        if (changes.Status != before.Status)
            _pendingHistory.Add(PdiHistoryEntry.ForStatusChanged(
                UserId, before.Status, changes.Status, actor.Id,
                changes.Reason));

        if (fieldChanges.Count > 0)
            _pendingHistory.Add(PdiHistoryEntry.ForUpdated(
                UserId, actor.Id,
                detail: string.Join("; ", fieldChanges),
                reason: changes.Reason));

        RaiseDomainEvent(new PdiUpdatedDomainEvent(
            UserId, TargetDate, Status,
            actor.Id, actor.Email, actor.OwnerEmail));
    }

    /// <summary>
    /// Drena as entradas pendentes do histórico para o repositório
    /// persistir na mesma transação do save.
    /// </summary>
    public IReadOnlyCollection<PdiHistoryEntry> DrainPendingHistory()
    {
        if (_pendingHistory.Count == 0)
            return [];

        var drained = _pendingHistory.ToArray();
        _pendingHistory.Clear();
        return drained;
    }

    /// <summary>Evidência anexada a uma fase (docs/30 §12, §17 — Fase 4).</summary>
    public void RecordEvidenceAdded(
        Guid actorId, PdiEvidenceType type, string? description)
    {
        var detail = description is null
            ? $"evidência anexada: {type.ToDisplayLabel()}"
            : $"evidência anexada: {type.ToDisplayLabel()} — {description}";

        _pendingHistory.Add(PdiHistoryEntry.ForEvidenceAdded(
            UserId, actorId, detail));
    }

    /// <summary>Evidência revisada pelo gestor (§12 — validada/rejeitada).</summary>
    public void RecordEvidenceReviewed(
        Guid actorId, bool approved, string? rejectionReason)
    {
        _pendingHistory.Add(PdiHistoryEntry.ForEvidenceReviewed(
            UserId, actorId,
            approved ? "validada" : "rejeitada",
            approved ? null : rejectionReason));
    }

    /// <summary>Fase iniciada (auto-start da próxima ao validar, §15/§17 — Fase 5).</summary>
    public void RecordPhaseStarted(Guid actorId, string phaseTitle)
        => _pendingHistory.Add(PdiHistoryEntry.ForPhaseEvent(
            UserId, actorId, PdiHistoryType.PhaseStarted,
            $"fase iniciada: {phaseTitle}"));

    /// <summary>Fase concluída e enviada para validação pelo dono (§15 — Fase 5).</summary>
    public void RecordPhaseSubmitted(Guid actorId, string phaseTitle)
        => _pendingHistory.Add(PdiHistoryEntry.ForPhaseEvent(
            UserId, actorId, PdiHistoryType.PhaseSubmitted,
            $"fase concluída e enviada para validação: {phaseTitle}"));

    /// <summary>Fase validada pelo gestor (§15 — Fase 5).</summary>
    public void RecordPhaseValidated(Guid actorId, string phaseTitle)
        => _pendingHistory.Add(PdiHistoryEntry.ForPhaseEvent(
            UserId, actorId, PdiHistoryType.PhaseValidated,
            $"fase validada: {phaseTitle}"));

    /// <summary>Fase rejeitada pelo gestor — motivo obrigatório (§15 — Fase 5).</summary>
    public void RecordPhaseRejected(
        Guid actorId, string phaseTitle, string reason)
        => _pendingHistory.Add(PdiHistoryEntry.ForPhaseEvent(
            UserId, actorId, PdiHistoryType.PhaseRejected,
            $"fase rejeitada: {phaseTitle}", reason));

    /// <summary>Check-in registrado na fase (§17 — Fase 5).</summary>
    public void RecordCheckInCreated(
        Guid actorId, DateOnly checkInDate, string whatDone)
        => _pendingHistory.Add(PdiHistoryEntry.ForCheckInCreated(
            UserId, actorId,
            $"check-in de {checkInDate:yyyy-MM-dd}: {whatDone}"));

    /// <summary>Impedimento registrado (§17 — Fase 5).</summary>
    public void RecordImpedimentCreated(
        Guid actorId, string type, string description)
        => _pendingHistory.Add(PdiHistoryEntry.ForImpediment(
            UserId, actorId, PdiHistoryType.ImpedimentCreated,
            $"impedimento registrado ({type}): {description}"));

    /// <summary>Impedimento resolvido (§17 — Fase 5).</summary>
    public void RecordImpedimentResolved(Guid actorId, string description)
        => _pendingHistory.Add(PdiHistoryEntry.ForImpediment(
            UserId, actorId, PdiHistoryType.ImpedimentResolved,
            $"impedimento resolvido: {description}"));

    private void Apply(PdiChanges changes)
    {
        Strategy = RequireStrategy(changes.Strategy);
        EnsureValid(changes);

        TargetDate = changes.TargetDate;
        Status = changes.Status;
        ManagerId = changes.ManagerId;
        PeriodStart = changes.PeriodStart;
        PeriodEnd = changes.PeriodEnd;
        Touch();
    }

    private static void EnsureValid(PdiChanges changes)
    {
        if (changes.TargetDate == default)
            throw new ArgumentException(
                "O prazo do PDI é obrigatório.", nameof(changes));

        if (!Enum.IsDefined(changes.Status))
            throw new ArgumentException(
                "Status do PDI inválido.", nameof(changes));

        if (changes.PeriodStart is { } start && changes.PeriodEnd is { } end
            && end < start)
            throw new ArgumentException(
                "O fim do período do PDI não pode ser anterior ao início.",
                nameof(changes.PeriodEnd));
    }

    /// <summary>
    /// Campos alterados com valor anterior → novo, em pt-BR (§14,
    /// Fase 5/ADR-035) — base do <c>Detail</c> do histórico
    /// <c>Updated</c> e da exigência de motivo.
    /// </summary>
    private static List<string> DescribeFieldChanges(
        string beforeStrategy,
        DateOnly beforeTargetDate,
        Guid? beforeManagerId,
        DateOnly? beforePeriodStart,
        DateOnly? beforePeriodEnd,
        string normalizedStrategy,
        PdiChanges changes)
    {
        List<string>? changed = null;

        if (!string.Equals(beforeStrategy, normalizedStrategy, StringComparison.Ordinal))
            (changed ??= []).Add(
                $"estratégia: \"{TruncateValue(beforeStrategy)}\" → \"{TruncateValue(normalizedStrategy)}\"");

        if (beforeTargetDate != changes.TargetDate)
            (changed ??= []).Add(
                $"prazo: {beforeTargetDate:yyyy-MM-dd} → {changes.TargetDate:yyyy-MM-dd}");

        if (beforeManagerId != changes.ManagerId)
            (changed ??= []).Add(
                $"gestor: {FormatId(beforeManagerId)} → {FormatId(changes.ManagerId)}");

        if (beforePeriodStart != changes.PeriodStart
            || beforePeriodEnd != changes.PeriodEnd)
            (changed ??= []).Add(
                $"período: {FormatPeriod(beforePeriodStart, beforePeriodEnd)} → {FormatPeriod(changes.PeriodStart, changes.PeriodEnd)}");

        return changed ?? [];
    }

    private static string TruncateValue(string value)
        => value.Length <= 80 ? value : value[..79] + "…";

    private static string FormatId(Guid? id)
        => id?.ToString() ?? "sem gestor";

    private static string FormatPeriod(DateOnly? start, DateOnly? end)
        => start is null && end is null
            ? "sem período"
            : $"{start?.ToString("yyyy-MM-dd") ?? "—"}..{end?.ToString("yyyy-MM-dd") ?? "—"}";

    private static string RequireStrategy(string strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy))
            throw new ArgumentException(
                "A estratégia do PDI é obrigatória.", nameof(strategy));

        strategy = strategy.Trim();
        if (strategy.Length > StrategyMaxLength)
            throw new ArgumentException(
                $"A estratégia do PDI pode ter no máximo {StrategyMaxLength} caracteres.",
                nameof(strategy));

        return strategy;
    }
}

/// <summary>
/// Mudanças aplicadas num PDI (domínio, ADR-029). <c>Reason</c> é o
/// motivo opcional da transição de status (timeline).
/// </summary>
public sealed record PdiChanges(
    string Strategy,
    DateOnly TargetDate,
    PdiStatus Status,
    Guid? ManagerId,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    string? Reason);
