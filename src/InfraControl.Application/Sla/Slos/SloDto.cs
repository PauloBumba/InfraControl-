namespace InfraControl.Application.Sla.Slos;

/// <summary>
/// Payload de Slo devolvido pela API (sucesso → data do envelope).
/// Os campos de medição/orçamento são projeções de leitura (null quando
/// o SLO ainda não tem dados) — a UI deriva o status sem N+1.
/// </summary>
public sealed record SloDto(
    Guid Id,
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId,
    /// <summary>Valor da medição mais recente dentro da janela (0–100).</summary>
    decimal? LatestValuePercent = null,
    /// <summary>Quando a última medição foi registrada (UTC).</summary>
    DateTime? LatestMeasuredAtUtc = null,
    /// <summary>Percentual do error budget já consumido (0–100).</summary>
    decimal? BudgetConsumedPercent = null,
    /// <summary>Fim do período do orçamento vigente (null = aberto).</summary>
    DateTime? BudgetPeriodEndUtc = null,
    /// <summary>Checks de monitoramento medidos por este SLO (ADR-068; vazio = só manual).</summary>
    IReadOnlyList<Guid>? MonitoringCheckIds = null);
