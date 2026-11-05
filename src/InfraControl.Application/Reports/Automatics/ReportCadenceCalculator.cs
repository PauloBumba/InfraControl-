namespace InfraControl.Application.Reports.Automatics;

/// <summary>
/// F25-T005/§15 — cadência dos relatórios automáticos (docs/33 §15).
/// </summary>
public enum ReportCadence
{
    Mensal,
    Trimestral,
    Semestral,
    Anual,
}

/// <summary>
/// Janela do período corrente para cada cadência (§15). O agendamento em
/// background (cron + distribuição) é decisão arquitetural separada, pendente
/// de ADR — aqui está só a GERAÇÃO sob demanda pela cadência.
/// </summary>
public static class ReportCadenceCalculator
{
    /// <summary>Texto inválido = 400 (ArgumentException no GlobalExceptionHandler).</summary>
    public static ReportCadence Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ReportCadence.Mensal;

        return value.Trim().ToLowerInvariant() switch
        {
            "mensal" => ReportCadence.Mensal,
            "trimestral" => ReportCadence.Trimestral,
            "semestral" => ReportCadence.Semestral,
            "anual" => ReportCadence.Anual,
            _ => throw new ArgumentException(
                $"'{value}' não é uma cadência válida — use mensal, trimestral, semestral ou anual.")
        };
    }

    /// <summary>Início do período corrente até hoje, conforme a cadência.</summary>
    public static (DateOnly From, DateOnly To) Window(
        ReportCadence cadence, DateOnly today)
    {
        var from = cadence switch
        {
            ReportCadence.Trimestral => FirstOfQuarter(today),
            ReportCadence.Semestral => FirstOfSemester(today),
            ReportCadence.Anual => new DateOnly(today.Year, 1, 1),
            _ => new DateOnly(today.Year, today.Month, 1),
        };
        return (from, today);
    }

    private static DateOnly FirstOfQuarter(DateOnly day)
    {
        var startMonth = ((day.Month - 1) / 3) * 3 + 1;
        return new DateOnly(day.Year, startMonth, 1);
    }

    private static DateOnly FirstOfSemester(DateOnly day)
        => new(day.Year, day.Month <= 6 ? 1 : 7, 1);
}
