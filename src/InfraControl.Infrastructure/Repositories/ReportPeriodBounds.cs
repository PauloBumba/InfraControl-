namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Limites de período UTC dos relatórios (ADR-049): converte <c>DateOnly</c>
/// em <c>DateTime</c> com <c>Kind=Utc</c>. Sem essa conversão, o Postgres
/// rejeita o parâmetro (<c>timestamptz</c> só aceita UTC) e <b>todo</b>
/// filtro <c>from/to</c> quebra só em produção — o SQLite dos testes
/// aceita qualquer <c>Kind</c> e esconde o erro (bug ao vivo 2026-10-27).
/// </summary>
public static class ReportPeriodBounds
{
    /// <summary>Retorna (início do dia <paramref name="from"/>, fim do dia <paramref name="to"/>) em UTC.</summary>
    public static (DateTime? Lower, DateTime? Upper) Between(DateOnly? from, DateOnly? to)
        => (AsUtc(from, TimeOnly.MinValue), AsUtc(to, TimeOnly.MaxValue));

    /// <summary>Início do dia em UTC (comparações de "hoje" contra <c>timestamptz</c>).</summary>
    public static DateTime StartOfDayUtc(DateOnly day)
        => DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DateTime? AsUtc(DateOnly? day, TimeOnly time)
        => day is null
            ? null
            : DateTime.SpecifyKind(day.Value.ToDateTime(time), DateTimeKind.Utc);
}
