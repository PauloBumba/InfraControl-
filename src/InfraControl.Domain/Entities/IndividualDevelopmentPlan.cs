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

        Apply(changes);

        if (changes.Status != before.Status)
            _pendingHistory.Add(PdiHistoryEntry.ForStatusChanged(
                UserId, before.Status, changes.Status, actor.Id,
                changes.Reason));

        List<string>? changed = null;
        if (Strategy != before.Strategy)
            (changed ??= []).Add("estratégia");
        if (TargetDate != before.TargetDate)
            (changed ??= []).Add("prazo");
        if (ManagerId != before.ManagerId)
            (changed ??= []).Add("gestor");
        if (PeriodStart != before.PeriodStart || PeriodEnd != before.PeriodEnd)
            (changed ??= []).Add("período");

        if (changed is not null)
            _pendingHistory.Add(PdiHistoryEntry.ForUpdated(
                UserId, actor.Id, string.Join(", ", changed)));

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

    private void Apply(PdiChanges changes)
    {
        Strategy = RequireStrategy(changes.Strategy);

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

        TargetDate = changes.TargetDate;
        Status = changes.Status;
        ManagerId = changes.ManagerId;
        PeriodStart = changes.PeriodStart;
        PeriodEnd = changes.PeriodEnd;
        Touch();
    }

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
