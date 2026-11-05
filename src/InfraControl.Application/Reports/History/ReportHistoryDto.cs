namespace InfraControl.Application.Reports.History;

/// <summary>
/// F25-T005 §13 — série histórica mensal dos indicadores para comparação e
/// tendência (ex.: Tasks/Concluídas/Knowledge/PDIs JAN→ABR). Calculada
/// on-the-fly por mês de <c>CreateDateTime</c> (sem tabela de snapshot).
/// "Concluídas" = tasks criadas no mês que estão concluídas hoje (domínio
/// sem carimbo de conclusão).
/// </summary>
public sealed record ReportHistoryDto(
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<HistoryMonthDto> Months);

public sealed record HistoryMonthDto(
    string Month,
    int Tasks,
    int CompletedTasks,
    int Knowledge,
    int Pdis);
